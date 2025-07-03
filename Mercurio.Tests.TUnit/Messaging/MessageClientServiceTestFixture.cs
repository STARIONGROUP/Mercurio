// -------------------------------------------------------------------------------------------------
//  <copyright file="MessageClientServiceTestFixture.cs" company="Starion Group S.A.">
// 
//    Copyright 2025 Starion Group S.A.
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
// 
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mercurio.Tests.TUnit.Messaging
{
    using Mercurio.Extensions;
    using Mercurio.Messaging;
    using Mercurio.Model;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using RabbitMQ.Client;
    using System.Collections.Concurrent;

    [Category("Integration")]
    [NotInParallel]
    public class MessageClientServiceTestFixture
    {
        private IMessageClientService firstService;
        private IMessageClientService secondService;
        private ServiceProvider serviceProvider;
        private const string FirstConnectionName = "RabbitMQConnection1";
        private const string SecondConnectionName = "RabbitMQConnection2";
        private const string FirstSentMessage = "Hello World!";
        private const string SecondSentMessage = "Hello World!";
        private const int TimeOut = 100;
        
        [Before(HookType.Test)]
        public async Task Setup()
        {
            var serviceCollection = new ServiceCollection();
            
            serviceCollection.AddRabbitMqConnectionProvider()
                .WithRabbitMqConnectionFactory(FirstConnectionName,_ =>
                {
                    var connectionFactory = new ConnectionFactory
                    {
                        HostName = "localhost",
                        Port = 5672,
                        UserName = "guest",
                        Password = "guest"
                    };
                    
                    return connectionFactory;
                })
                .WithRabbitMqConnectionFactory(SecondConnectionName,_ =>
                {
                    var connectionFactory = new ConnectionFactory
                    {
                        HostName = "localhost",
                        Port = 5672,
                        UserName = "guest",
                        Password = "guest"
                    };
                    
                    return connectionFactory;
                })
                .WithSerialization()
                .AddLogging(x => x.AddConsole());

            serviceCollection.AddTransient<IMessageClientService, MessageClientService>();
            this.serviceProvider = serviceCollection.BuildServiceProvider();
            this.firstService = this.serviceProvider.GetRequiredService<IMessageClientService>();
            this.secondService = this.serviceProvider.GetRequiredService<IMessageClientService>();
            
            await Task.CompletedTask;
        }

        [After(HookType.Test)]
        public async Task TeardownAsync()
        {
            this.firstService.Dispose();
            this.secondService.Dispose();
            await this.serviceProvider.DisposeAsync();
            await Task.CompletedTask;
        }

        [Test]
        public async Task VerifyMessageExchangeWithDefaultExchangeAsync()
        {
            var exchangeConfiguration = new DefaultExchangeConfiguration("DefaultChannel");
            var listenObservable = await this.firstService.ListenAsync<string>(FirstConnectionName, exchangeConfiguration);
            var secondObservable = await this.secondService.ListenAsync<string>(SecondConnectionName, exchangeConfiguration);
            var firstTaskCompletion = new TaskCompletionSource<string>();
            var secondTaskCompletion = new TaskCompletionSource<string>();
            listenObservable.Subscribe(message => firstTaskCompletion.TrySetResult(message));
            secondObservable.Subscribe(message => secondTaskCompletion.TrySetResult(message));
            var tasks = new List<Task> { firstTaskCompletion.Task, secondTaskCompletion.Task };
            var expectedMessage = new Queue<string>();
            expectedMessage.Enqueue(FirstSentMessage);
            expectedMessage.Enqueue(SecondSentMessage);
            await Task.Delay(TimeOut);
            
            await this.firstService.PushAsync(FirstConnectionName, FirstSentMessage, exchangeConfiguration);
            await this.firstService.PushAsync(FirstConnectionName, SecondSentMessage, exchangeConfiguration);
            
            while (tasks.Count > 0)
            {
                var completedTask =(Task<string>) await Task.WhenAny(tasks);
                await Assert.That(completedTask.Result).IsEqualTo(expectedMessage.Dequeue());
                tasks.Remove(completedTask);
            }
        }
  
        [Test]
        [Arguments("DirectChannel", "", "")]
        [Arguments("DirectChannelWithExchange", "CustomNewExchange", "")]
        [Arguments("DirectChannelWithExchangeAndRouting", "CustomNewExchangeWithRouting", "RoutingKey")]
        [DisplayName("Test with: $queueName $exchangeName $routingKey!")]
        public async Task VerifyMessageExchangeWithDirectExchangeAsync(string queueName, string exchangeName, string routingKey)
        {
            var exchangeConfiguration = string.IsNullOrEmpty(exchangeName) 
                ? new DirectExchangeConfiguration(queueName, routingKey: routingKey) 
                : new DirectExchangeConfiguration(queueName, exchangeName, routingKey);

            var listenObservable = await this.firstService.ListenAsync<string>(FirstConnectionName, exchangeConfiguration);
            var secondObservable = await this.secondService.ListenAsync<string>(SecondConnectionName, exchangeConfiguration);
            var firstTaskCompletion = new TaskCompletionSource<string>();
            var secondTaskCompletion = new TaskCompletionSource<string>();
            listenObservable.Subscribe(message => firstTaskCompletion.TrySetResult(message));
            secondObservable.Subscribe(message => secondTaskCompletion.TrySetResult(message));
            await Task.Delay(TimeOut);
            
            await this.firstService.PushAsync(FirstConnectionName, FirstSentMessage, exchangeConfiguration);
            await this.firstService.PushAsync(FirstConnectionName, SecondSentMessage, exchangeConfiguration);

            var tasks = new List<Task<string>> { firstTaskCompletion.Task, secondTaskCompletion.Task };
            var expectedMessage = new Queue<string>();
            expectedMessage.Enqueue(FirstSentMessage);
            expectedMessage.Enqueue(SecondSentMessage);
            
            while (tasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(tasks);
                await Assert.That(completedTask.Result).IsEqualTo(expectedMessage.Dequeue());
                tasks.Remove(completedTask);
            }
        }
        
        [Test]
        [Arguments("FanoutExchange", "")]
        [Arguments("FanoutExchange", "WithRoutingKey")]
        [Arguments("CustomNewExchangeForFanout", "")]
        [Arguments( "CustomNewExchangeWithRoutingForFanout", "RoutingKeyForFanout")]
        [DisplayName("Test with: $exchangeName $routingKey!")]
        public async Task VerifyMessageExchangeWithFanoutExchangeAsync(string exchangeName, string routingKey)
        {
            var firstExchangeConfiguration = new FanoutExchangeConfiguration(exchangeName, routingKey);
            var secondExchangeConfiguration = new FanoutExchangeConfiguration(exchangeName, routingKey);
           
            var listenObservable = await this.firstService.ListenAsync<string>(MessageClientServiceTestFixture.FirstConnectionName, firstExchangeConfiguration);
            var secondObservable = await this.secondService.ListenAsync<string>(MessageClientServiceTestFixture.SecondConnectionName, secondExchangeConfiguration);
            var firstTaskCompletion = new TaskCompletionSource<string>();
            var secondTaskCompletion = new TaskCompletionSource<string>();
            listenObservable.Subscribe(message => firstTaskCompletion.TrySetResult(message));
            secondObservable.Subscribe(message => secondTaskCompletion.TrySetResult(message));

            await Task.Delay(MessageClientServiceTestFixture.TimeOut);

            await this.firstService.PushAsync(MessageClientServiceTestFixture.FirstConnectionName, MessageClientServiceTestFixture.FirstSentMessage, firstExchangeConfiguration);

            var tasks = new List<Task<string>> { firstTaskCompletion.Task, secondTaskCompletion.Task };

            await Task.WhenAll(tasks);

            using var _ = Assert.Multiple();
            
            foreach (var completedTask in tasks)
            {
                await Assert.That(completedTask.Result).IsEqualTo(MessageClientServiceTestFixture.FirstSentMessage);
            }
        }

        [Test]
        [Arguments(20, 5, 10)]
        [Arguments(20, 5, 100)]
        [DisplayName("Stress test with $listenerCount! listeners and $producerCount! producers pushing $pushRepetitions! times")]
        public async Task Should_ReUse_Channel_Under_Stress(int listenerCount = 20, int producerCount = 5, int pushRepetitions = 10)
        {
            const string exchangeName = "PoolReuseExchange";
            const string message = "stress-message";

            var receivedMessages = new ConcurrentBag<string>();
            var completionSources = new List<TaskCompletionSource<string>>();

            var disposables = new List<IDisposable>();

            for (var listenerIndex = 0; listenerIndex < listenerCount; listenerIndex++)
            {
                var taskCompletionSource = new TaskCompletionSource<string>();
                completionSources.Add(taskCompletionSource);

                var observable = await this.firstService.ListenAsync<string>(FirstConnectionName, new FanoutExchangeConfiguration(exchangeName));

                disposables.Add(observable.Subscribe(m =>
                {
                    receivedMessages.Add(m);
                    taskCompletionSource.TrySetResult(m);
                }));
            }

            await Task.Delay(100);

            var producers = Enumerable.Range(0, producerCount).Select(async _ =>
            {
                for (var pushIndex = 0; pushIndex < pushRepetitions; pushIndex++)
                {
                    await this.firstService.PushAsync(FirstConnectionName, message, new FanoutExchangeConfiguration(exchangeName));
                }
            });

            await Task.WhenAll(producers);

            using var _ = Assert.Multiple();

            foreach (var taskCompletionSource in completionSources)
            {
                var result = await Task.WhenAny(taskCompletionSource.Task, Task.Delay(2000));
                await Assert.That(result == taskCompletionSource.Task).IsTrue();
                await Assert.That(taskCompletionSource.Task.Result).IsEqualTo(message);
            }

            await Assert.That(receivedMessages.Count).IsGreaterThanOrEqualTo(listenerCount);

            disposables.ForEach(d => d.Dispose());
        }

        [Test]
        public async Task VerifyAddListenerBehaviorAsync()
        {
            var exchangeConfiguration = new DefaultExchangeConfiguration("DefaultChannelForAddListener");
            var messageReceived = false;
           
            var disposable = await this.firstService.AddListenerAsync(MessageClientServiceTestFixture.FirstConnectionName, exchangeConfiguration, (_,_) =>
            {
                return Task.Run(() => messageReceived = true);
            });
           
            await this.firstService.PushAsync(MessageClientServiceTestFixture.FirstConnectionName, MessageClientServiceTestFixture.FirstSentMessage, exchangeConfiguration);
           
            await Task.Delay(MessageClientServiceTestFixture.TimeOut);
            await Assert.That(messageReceived).IsTrue();
            disposable.Dispose();
        }

        [Test]
        public async Task VerifyMessageExchangeWithTopicExchangeAsync()
        {
            const string exchangeName = "TopicExchange";
            var pushExchangeConfiguration = new TopicExchangeConfiguration(exchangeName, "mercurio.topic.info");
            var fanoutLikeExchangeConfiguration = new TopicExchangeConfiguration(exchangeName);
            var listenWithWildCardConfiguration = new TopicExchangeConfiguration(exchangeName, "mercurio.*.*");
            var listenWithHashConfiguration = new TopicExchangeConfiguration(exchangeName, "mercurio.#");
            var invalidListenConfiguration = new TopicExchangeConfiguration(exchangeName, "mercurio.topic.inf");
           
            var fanoutObservable = await this.firstService.ListenAsync<string>(MessageClientServiceTestFixture.FirstConnectionName, fanoutLikeExchangeConfiguration);
            var listenWithWildCardObservable = await this.secondService.ListenAsync<string>(MessageClientServiceTestFixture.SecondConnectionName, listenWithWildCardConfiguration);
            var listenWithHashObservable = await this.secondService.ListenAsync<string>(MessageClientServiceTestFixture.SecondConnectionName, listenWithHashConfiguration);
            var equalRoutingObservable = await this.secondService.ListenAsync<string>(MessageClientServiceTestFixture.SecondConnectionName, pushExchangeConfiguration);
            var invalidListenObservable = await this.secondService.ListenAsync<string>(MessageClientServiceTestFixture.SecondConnectionName, invalidListenConfiguration);

            var fanoutTask = new TaskCompletionSource<string>();
            var listenWithWildCardTask = new TaskCompletionSource<string>();
            var listenWithHashTask = new TaskCompletionSource<string>();
            var equalRoutingTask = new TaskCompletionSource<string>();
            var invalidListenTask = new TaskCompletionSource<string>();

            using var subscription0 = fanoutObservable.Subscribe(message => fanoutTask.TrySetResult(message));
            using var subscription1 = listenWithWildCardObservable.Subscribe(message => listenWithWildCardTask.TrySetResult(message));
            using var subscription2 = listenWithHashObservable.Subscribe(message => listenWithHashTask.TrySetResult(message));
            using var subscription3 = equalRoutingObservable.Subscribe(message => equalRoutingTask.TrySetResult(message));
            using var subscription4 = invalidListenObservable.Subscribe(message => invalidListenTask.TrySetResult(message));
           
            await Task.Delay(MessageClientServiceTestFixture.TimeOut);
            await this.firstService.PushAsync(MessageClientServiceTestFixture.FirstConnectionName, MessageClientServiceTestFixture.FirstSentMessage, pushExchangeConfiguration);

            var validTasks = new List<Task<string>> { fanoutTask.Task, listenWithWildCardTask.Task , listenWithHashTask.Task, equalRoutingTask.Task};
            await Task.WhenAll(validTasks);

            using var _ = Assert.Multiple();
            
            foreach (var validTask in validTasks)
            {
                await Assert.That(validTask.Result).IsEqualTo(MessageClientServiceTestFixture.FirstSentMessage);
            }
           
            await this.firstService.PushAsync(MessageClientServiceTestFixture.FirstConnectionName, MessageClientServiceTestFixture.SecondSentMessage, pushExchangeConfiguration);

            var taskWithTimeout = await Task.WhenAny(invalidListenTask.Task, Task.Delay(MessageClientServiceTestFixture.TimeOut));

            await Assert.That((object)taskWithTimeout).IsNotSameReferenceAs(invalidListenTask.Task);
            await Assert.That(invalidListenTask.Task.Status).IsEqualTo(TaskStatus.WaitingForActivation);
        }
    }
}
