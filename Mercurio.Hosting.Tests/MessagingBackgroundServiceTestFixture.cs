// -------------------------------------------------------------------------------------------------
//  <copyright file="MessagingBackgroundServiceTestFixture.cs" company="Starion Group S.A.">
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

namespace Mercurio.Tests
{
    using Mercurio.Extensions;
    using Mercurio.Hosting;
    using Mercurio.Messaging;
    using Mercurio.Model;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using Moq;

    using RabbitMQ.Client;

    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public class MessagingBackgroundServiceTestFixture
    {
        private const string ConfiguredConnectionName = "RabbitMQ";
        private const string RpcServerQueueName = "RPC";
        private Mock<IConfiguration> configurationMock;
        private TestMessagingBackgroundService backgroundService;
        private ServiceProvider serviceProvider;
        
        [SetUp]
        public void Setup()
        {
            this.configurationMock = new Mock<IConfiguration>();
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddRabbitMqConnectionProvider()
                .WithRabbitMqConnectionFactory(ConfiguredConnectionName, _ =>
                {
                    var connectionFactory = new ConnectionFactory()
                    {
                        HostName = RabbitMqContainerSetupFixture.RabbitMqContainer.Hostname,
                        Port = RabbitMqContainerSetupFixture.RabbitMqContainer.GetMappedPublicPort()
                    };
                    
                    return connectionFactory;
                })
                .WithSerialization();

            serviceCollection.AddScoped<IMessageClientService, MessageClientService>();
            serviceCollection.AddScoped<IRpcServerService, RpcServerService>();
            serviceCollection.AddScoped<IRpcClientService<bool>, RpcClientService<bool>>();
            serviceCollection.AddLogging();
            this.serviceProvider = serviceCollection.BuildServiceProvider();
            
            this.backgroundService = new TestMessagingBackgroundService(this.serviceProvider, this.serviceProvider.GetRequiredService<ILogger<TestMessagingBackgroundService>>(), this.configurationMock.Object);
        }

        [TearDown]
        public void Teardown()
        {
            this.backgroundService.Dispose();
            this.serviceProvider.Dispose();
        }

        [Test]
        public async Task VerifyBackgroundServiceBehaviour()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            _ = this.backgroundService.StartAsync(cancellationTokenSource.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);

            string[] messages = ["ABC", "DEF", "GHI"];

            foreach (var message in messages)
            {
                this.backgroundService.PushMessage(message,new FanoutExchangeConfiguration("BackgroundTest"), cancellationToken: cancellationTokenSource.Token);
                await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationTokenSource.Token);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(900), CancellationToken.None);

            await cancellationTokenSource.CancelAsync();
            
            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.backgroundService.ReceivedMessages, Has.Count.EqualTo(3));
                Assert.That(this.backgroundService.ReceivedMessages, Is.EquivalentTo(messages));
            }
        }
        
        [Test]
        public async Task VerifyBackgroundServiceBehaviourPushMultiple()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            _ = this.backgroundService.StartAsync(cancellationTokenSource.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);

            string[] messages = ["ABC", "DEF", "GHI"];

            this.backgroundService.PushMessages(messages,new FanoutExchangeConfiguration("BackgroundTest"), cancellationToken: cancellationTokenSource.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(1200), CancellationToken.None);

            await cancellationTokenSource.CancelAsync();
            
            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.backgroundService.ReceivedMessages, Has.Count.EqualTo(3));
                Assert.That(this.backgroundService.ReceivedMessages, Is.EquivalentTo(messages));
            }
        }
        
        [Test]
        public async Task VerifyBackgroundServiceBehaviourTaskCanceled()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            _ = this.backgroundService.StartAsync(cancellationTokenSource.Token);
            
            string[] messages = ["ABC", "DEF", "GHI"];

            this.backgroundService.PushMessages(messages,new FanoutExchangeConfiguration("BackgroundTest"), cancellationToken: cancellationTokenSource.Token);
            await cancellationTokenSource.CancelAsync();
            
            await Task.Delay(TimeSpan.FromMilliseconds(1000), CancellationToken.None);

            Assert.That(this.backgroundService.ReceivedMessages, Is.Empty);
        }

        [Test]
        public async Task VerifyInvalidInitialization()
        {
            var invalidService = new InvalidInitializationBackgroundService(this.serviceProvider, this.serviceProvider.GetRequiredService<ILogger<InvalidInitializationBackgroundService>>(), this.configurationMock.Object);
            await Assert.ThatAsync(() => invalidService.StartAsync(CancellationToken.None), Throws.InvalidOperationException);
        }

        [Test]
        public async Task VerifyRpcServer()
        {
            using var cancellationTokenSource = new CancellationTokenSource();

            var rpcBackground = new TestRpcServerBackgrounService(this.serviceProvider, this.serviceProvider.GetRequiredService<ILogger<TestRpcServerBackgrounService>>(), this.configurationMock.Object);
            _ = rpcBackground.StartAsync(cancellationTokenSource.Token);

            var client = this.serviceProvider.GetRequiredService<IRpcClientService<bool>>();
            
            var rpcObservable = await client.SendRequestAsync(ConfiguredConnectionName, RpcServerQueueName, 45, cancellationToken: cancellationTokenSource.Token);
            var taskCompletion = new TaskCompletionSource<bool>();
            rpcObservable.Subscribe(result => taskCompletion.SetResult(result));
            await Task.Delay(100, cancellationTokenSource.Token);
            
            await taskCompletion.Task;
            Assert.That(taskCompletion.Task.Result, Is.False);
            
            rpcObservable = await client.SendRequestAsync(ConfiguredConnectionName, RpcServerQueueName, 44, cancellationToken: cancellationTokenSource.Token);
            var newTaskCompletion = new TaskCompletionSource<bool>();
            rpcObservable.Subscribe(result => newTaskCompletion.SetResult(result));
            await Task.Delay(100, cancellationTokenSource.Token);
            
            await taskCompletion.Task;
            Assert.That(newTaskCompletion.Task.Result, Is.True);
            await cancellationTokenSource.CancelAsync();
            rpcBackground.Dispose();
            client.Dispose();
        }

        [Test]
        public async Task VerifyAutoRecoverySystem()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var autoRecoveryBackground = new AutoRecoveryTestMessagingBackgroundService(this.serviceProvider, this.serviceProvider.GetRequiredService<ILogger<AutoRecoveryTestMessagingBackgroundService>>(), this.configurationMock.Object);
            _ = autoRecoveryBackground.StartAsync(cancellationTokenSource.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
            
            var configuration = new FanoutExchangeConfiguration("AutoRecoveryBackgroundTest");
            autoRecoveryBackground.PushMessage("abc", configuration, cancellationToken: cancellationTokenSource.Token);
            await Task.Delay(100, cancellationTokenSource.Token);

            Assert.That(autoRecoveryBackground.ReceivedMessages, Has.Count.EqualTo(1));
            
            autoRecoveryBackground.PushMessage("123", configuration, cancellationToken: cancellationTokenSource.Token);
            await Task.Delay(100, cancellationTokenSource.Token);

            autoRecoveryBackground.PushMessage("abc", configuration, cancellationToken: cancellationTokenSource.Token);
            autoRecoveryBackground.PushMessage("abc", configuration, cancellationToken: cancellationTokenSource.Token);
            await Task.Delay(100, cancellationTokenSource.Token);

            Assert.That(autoRecoveryBackground.ReceivedMessages, Has.Count.EqualTo(3));
            autoRecoveryBackground.Dispose();
        }
        
        [Test]
        public async Task VerifyAsyncAutoRecoverySystem()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var autoRecoveryBackground = new AutoRecoveryTestMessagingBackgroundService(this.serviceProvider, this.serviceProvider.GetRequiredService<ILogger<AutoRecoveryTestMessagingBackgroundService>>(), this.configurationMock.Object);
            _ = autoRecoveryBackground.StartAsync(cancellationTokenSource.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(200), CancellationToken.None);
            
            var configuration = new FanoutExchangeConfiguration("AutoRecoveryBackgroundTestAsync");
            autoRecoveryBackground.PushMessage("abc", configuration, cancellationToken: cancellationTokenSource.Token);
            await Task.Delay(100, cancellationTokenSource.Token);

            Assert.That(autoRecoveryBackground.ReceivedMessages, Has.Count.EqualTo(1));
            
            autoRecoveryBackground.PushMessage("123", configuration, cancellationToken: cancellationTokenSource.Token);
            await Task.Delay(50, cancellationTokenSource.Token);

            autoRecoveryBackground.PushMessage("abc", configuration, cancellationToken: cancellationTokenSource.Token);
            autoRecoveryBackground.PushMessage("abc", configuration, cancellationToken: cancellationTokenSource.Token);
            await Task.Delay(50, cancellationTokenSource.Token);

            Assert.That(autoRecoveryBackground.ReceivedMessages, Has.Count.EqualTo(3));
            autoRecoveryBackground.Dispose();
        }

        private class InvalidInitializationBackgroundService : MessagingBackgroundService
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MessagingBackgroundService" />
            /// </summary>
            /// <param name="serviceProvider">
            /// The injected <see cref="IServiceProvider" /> that allow to resolve
            /// <see cref="IMessageClientService" /> instance, even if not registered as scope
            /// </param>
            /// <param name="logger">The injected <see cref="ILogger{TCategory}" /> to allow logging</param>
            /// <param name="configuration">The injected <see cref="IConfiguration" /> to provides configuration information for service initialization</param>
            public InvalidInitializationBackgroundService(IServiceProvider serviceProvider, ILogger<InvalidInitializationBackgroundService> logger, IConfiguration configuration) : base(serviceProvider, logger, configuration)
            {
            }

            /// <summary>
            /// Initializes this service (e.g. to set the <see cref="MessagingBackgroundService.ConnectionName" /> and register subscriptions
            /// collection
            /// </summary>
            /// <returns>An awaitable <see cref="Task" /></returns>
            protected override Task InitializeAsync()
            {
                return Task.CompletedTask;
            }
        }
        
        public class AutoRecoveryTestMessagingBackgroundService: MessagingBackgroundService
        {
            /// <summary>
            /// Stores all received message
            /// </summary>
            public readonly List<string> ReceivedMessages = [];
            
            /// <summary>
            /// Initializes a new instance of the <see cref="MessagingBackgroundService" />
            /// </summary>
            /// <param name="serviceProvider">
            /// The injected <see cref="IServiceProvider" /> that allow to resolve
            /// <see cref="IMessageClientService" /> instance, even if not registered as scope
            /// </param>
            /// <param name="logger"></param>
            /// <param name="configuration"></param>
            public AutoRecoveryTestMessagingBackgroundService(IServiceProvider serviceProvider, ILogger<AutoRecoveryTestMessagingBackgroundService> logger, IConfiguration configuration) : base(serviceProvider, logger, configuration)
            {
            }

            /// <summary>
            /// Initializes this service (e.g. to set the <see cref="MessagingBackgroundService.ConnectionName" /> and register subscriptions
            /// collection
            /// </summary>
            /// <returns>An awaitable <see cref="Task" /></returns>
            protected override async Task InitializeAsync()
            {
                this.ConnectionName = ConfiguredConnectionName;
                await this.RegisterListener(() => this.MessageClientService.ListenAsync<string>(this.ConnectionName, new FanoutExchangeConfiguration("AutoRecoveryBackgroundTest")), this.HandleMessage);
                await this.RegisterAsyncListener(() => this.MessageClientService.ListenAsync<string>(this.ConnectionName, new FanoutExchangeConfiguration("AutoRecoveryBackgroundTestAsync")), this.HandleMessageAsync);
            }

            private Task HandleMessageAsync(string arg)
            {
                this.HandleMessage(arg);
                return Task.CompletedTask;
            }

            private void HandleMessage(string obj)
            {
                if (int.TryParse(obj, out _))
                {
                    throw new InvalidOperationException("I want to throw something");
                }
                else
                {
                    this.ReceivedMessages.Add(obj);
                }
            }
        }

        public class TestMessagingBackgroundService: MessagingBackgroundService
        {
            /// <summary>
            /// Stores all received message
            /// </summary>
            public readonly List<string> ReceivedMessages = [];
            
            /// <summary>
            /// Initializes a new instance of the <see cref="MessagingBackgroundService" />
            /// </summary>
            /// <param name="serviceProvider">
            /// The injected <see cref="IServiceProvider" /> that allow to resolve
            /// <see cref="IMessageClientService" /> instance, even if not registered as scope
            /// </param>
            /// <param name="logger"></param>
            /// <param name="configuration"></param>
            public TestMessagingBackgroundService(IServiceProvider serviceProvider, ILogger<TestMessagingBackgroundService> logger, IConfiguration configuration) : base(serviceProvider, logger, configuration)
            {
            }

            /// <summary>
            /// Initializes this service (e.g. to set the <see cref="MessagingBackgroundService.ConnectionName" /> and register subscriptions
            /// collection
            /// </summary>
            /// <returns>An awaitable <see cref="Task" /></returns>
            protected override async Task InitializeAsync()
            {
                this.ConnectionName = ConfiguredConnectionName;
                await this.RegisterListener(() => this.MessageClientService.ListenAsync<string>(this.ConnectionName, new FanoutExchangeConfiguration("BackgroundTest")), this.ReceivedMessages.Add, onError: _ => this.ReceivedMessages.Clear());
                
                await this.RegisterAsyncListener(() => this.MessageClientService.ListenAsync<int>(this.ConnectionName, new DirectExchangeConfiguration("BackgroundTestInt")), (x) => 
                {
                    this.ReceivedMessages.Add(x.ToString());
                    return Task.CompletedTask;
                }, onError: _ => this.ReceivedMessages.Clear());
            }
        }

        public class TestRpcServerBackgrounService : RpcServerBackgroundService
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="RpcServerBackgroundService" />
            /// </summary>
            /// <param name="serviceProvider">
            /// The injected <see cref="IServiceProvider" /> that allow to resolve
            /// <see cref="IMessageClientService" /> instance, even if not registered as scope
            /// </param>
            /// <param name="logger">The injected <see cref="ILogger{TCategory}" /> to allow logging</param>
            /// <param name="configuration">The injected <see cref="IConfiguration" /> to provides configuration information for service initialization</param>
            public TestRpcServerBackgrounService(IServiceProvider serviceProvider, ILogger<TestRpcServerBackgrounService> logger, IConfiguration configuration) : base(serviceProvider, logger, configuration)
            {
            }

            /// <summary>
            /// Initializes this service (e.g. to set the <see cref="MessagingBackgroundService.ConnectionName" /> and register subscriptions
            /// </summary>
            /// <returns>An awaitable <see cref="Task" /></returns>
            protected override async Task InitializeAsync()
            {
                this.ConnectionName = ConfiguredConnectionName;
                
                this.RpcSubscriptions.Add(await this.RpcServerService.ListenForRequestAsync<int, bool>(this.ConnectionName, RpcServerQueueName, OnReceivedMessage));
            }

            private static Task<bool> OnReceivedMessage(int arg)
            {
                return Task.FromResult(arg%2 ==0);
            }
        }
    }
}
