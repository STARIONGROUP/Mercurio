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

namespace Mercurio.Tests.Messaging
{
    using Mercurio.Extensions;
    using Mercurio.Messaging;
    using Mercurio.Model;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using RabbitMQ.Client;

    using System.Collections.Concurrent;

    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public class MessageClientServiceTestFixture
    {
        private IMessageClientService firstService;
        private IMessageClientService secondService;
        private const string FirstConnectionName = "RabbitMQConnection1";
        private const string SecondConnectionName = "RabbitMQConnection2";
        private const string FirstSentMessage = "Hello World!";
        private const string SecondSentMessage = "Hello World!";
        private const int TimeOut = 100;
        
        [SetUp]
        public void Setup()
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
                        Password = "guest",
                    };

                    return connectionFactory;
                })
                .WithSerialization()
                .AddLogging(x => x.AddConsole());

            serviceCollection.AddTransient<IMessageClientService, MessageClientService>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            this.firstService = serviceProvider.GetRequiredService<IMessageClientService>();
            this.secondService = serviceProvider.GetRequiredService<IMessageClientService>();
        }

        [TearDown]
        public async Task TeardownAsync()
        {
            this.firstService.Dispose();
            this.secondService.Dispose();
            await Task.Delay(TimeOut);
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
                Assert.That(completedTask.Result, Is.EqualTo(expectedMessage.Dequeue()));
                tasks.Remove(completedTask);
            }
        }

        [Test]
        [TestCase(20, 5, 10)]
        public async Task Should_ReUse_Channel_Under_Stress(int listenerCount = 20, int producerCount = 5, int pushRepetitions = 10)
        {
            const string exchangeName = "PoolReuseExchange";
            const string message = "stress-message";

            var receivedMessages = new ConcurrentBag<string>();
            var completionSources = new List<TaskCompletionSource<string>>();

            var disposables = new List<IDisposable>();

            for (var linstenerIndex = 0; linstenerIndex < listenerCount; linstenerIndex++)
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

            foreach (var taskCompletionSource in completionSources)
            {
                var result = await Task.WhenAny(taskCompletionSource.Task, Task.Delay(2000));
                
                Assert.Multiple(() =>
                {
                    Assert.That(result, Is.EqualTo(taskCompletionSource.Task), "Expected listener did not receive message in time.");
                    Assert.That(taskCompletionSource.Task.Result, Is.EqualTo(message));
                });
            }

            Assert.That(receivedMessages, Has.Count.GreaterThanOrEqualTo(listenerCount), "Not all listeners received at least one message.");

            disposables.ForEach(d => d.Dispose());
        }

        [Test]
        [TestCase("DirectChannel", "", "")]
        [TestCase("DirectChannelWithExchange", "CustomNewExchange", "")]
        [TestCase("DirectChannelWithExchangeAndRouting", "CustomNewExchangeWithRouting", "RoutingKey")]
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
                Assert.That(completedTask.Result, Is.EqualTo(expectedMessage.Dequeue()));
                tasks.Remove(completedTask);
            }
        }
        
       [Test]
       [TestCase("FanoutExchange", "")]
       [TestCase("FanoutExchange", "WithRoutingKey")]
       [TestCase("CustomNewExchangeForFanout", "")]
       [TestCase( "CustomNewExchangeWithRoutingForFanout", "RoutingKeyForFanout")]
       public async Task VerifyMessageExchangeWithFanoutExchangeAsync(string exchangeName, string routingKey)
       {
           var firstExchangeConfiguration = new FanoutExchangeConfiguration(exchangeName, routingKey);
           var secondExchangeConfiguration = new FanoutExchangeConfiguration(exchangeName, routingKey);
           
           var listenObservable = await this.firstService.ListenAsync<string>(FirstConnectionName, firstExchangeConfiguration);
           var secondObservable = await this.secondService.ListenAsync<string>(SecondConnectionName, secondExchangeConfiguration);
           var firstTaskCompletion = new TaskCompletionSource<string>();
           var secondTaskCompletion = new TaskCompletionSource<string>();
           listenObservable.Subscribe(message => firstTaskCompletion.TrySetResult(message));
           secondObservable.Subscribe(message => secondTaskCompletion.TrySetResult(message));

           await Task.Delay(TimeOut);

           await this.firstService.PushAsync(FirstConnectionName, FirstSentMessage, firstExchangeConfiguration);

           var tasks = new List<Task<string>> { firstTaskCompletion.Task, secondTaskCompletion.Task };

           await Task.WhenAll(tasks);

           Assert.Multiple(() =>
           {
               foreach (var completedTask in tasks)
               {
                   Assert.That(completedTask.Result, Is.EqualTo(FirstSentMessage));
               }
           });
       }

       [Test]
       public async Task VerifyAddListenerBehaviorAsync()
       {
           var exchangeConfiguration = new DefaultExchangeConfiguration("DefaultChannelForAddListener");
           var messageReceived = false;
           
           var disposable = await this.firstService.AddListenerAsync(FirstConnectionName, exchangeConfiguration, (_,_) =>
           {
               return Task.Run(() => messageReceived = true);
           });
           
           await this.firstService.PushAsync(FirstConnectionName, FirstSentMessage, exchangeConfiguration);
           
           await Task.Delay(TimeOut);
           Assert.That(messageReceived, Is.True);
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
           
           var fanoutObservable = await this.firstService.ListenAsync<string>(FirstConnectionName, fanoutLikeExchangeConfiguration);
           var listenWithWildCardObservable = await this.secondService.ListenAsync<string>(SecondConnectionName, listenWithWildCardConfiguration);
           var listenWithHashObservable = await this.secondService.ListenAsync<string>(SecondConnectionName, listenWithHashConfiguration);
           var equalRoutingObservable = await this.secondService.ListenAsync<string>(SecondConnectionName, pushExchangeConfiguration);
           var invalidListenObservable = await this.secondService.ListenAsync<string>(SecondConnectionName, invalidListenConfiguration);

           var fanoutTask = new TaskCompletionSource<string>();
           var listenWithWildCardTask = new TaskCompletionSource<string>();
           var listenWithHashTask = new TaskCompletionSource<string>();
           var equalRoutingTask = new TaskCompletionSource<string>();
           var invalidListenTask = new TaskCompletionSource<string>();

           fanoutObservable.Subscribe(message => fanoutTask.TrySetResult(message));
           listenWithWildCardObservable.Subscribe(message => listenWithWildCardTask.TrySetResult(message));
           listenWithHashObservable.Subscribe(message => listenWithHashTask.TrySetResult(message));
           equalRoutingObservable.Subscribe(message => equalRoutingTask.TrySetResult(message));
           invalidListenObservable.Subscribe(message => invalidListenTask.TrySetResult(message));
           
           await Task.Delay(TimeOut);
           await this.firstService.PushAsync(FirstConnectionName, FirstSentMessage, pushExchangeConfiguration);

           var validTasks = new List<Task<string>> { fanoutTask.Task, listenWithWildCardTask.Task , listenWithHashTask.Task, equalRoutingTask.Task};
           await Task.WhenAll(validTasks);

           Assert.Multiple(() =>
           {
               foreach (var validTask in validTasks)
               {
                   Assert.That(validTask.Result, Is.EqualTo(FirstSentMessage));
               }
           });
           
           await this.firstService.PushAsync(FirstConnectionName, SecondSentMessage, pushExchangeConfiguration);

           var taskWithTimeout = await Task.WhenAny(invalidListenTask.Task, Task.Delay(TimeOut));

           Assert.Multiple(() =>
           {
               Assert.That(taskWithTimeout, Is.Not.EqualTo(invalidListenTask.Task));
               Assert.That(invalidListenTask.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation));
           });
       }
    }
}
