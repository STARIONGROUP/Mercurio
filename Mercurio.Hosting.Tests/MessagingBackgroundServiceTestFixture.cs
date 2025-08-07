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

namespace Mercurio.Hosting.Tests
{
    using Mercurio.Extensions;
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
                        HostName = "localhost",
                        Port = 5672
                    };
                    
                    return connectionFactory;
                })
                .WithSerialization();

            serviceCollection.AddScoped<IMessageClientService, MessageClientService>();
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
            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);

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
            /// Initializes this service (e.g. to set the <see cref="ConnectionName" /> and fill <see cref="Subscriptions" />
            /// collection
            /// </summary>
            /// <returns>An awaitable <see cref="Task" /></returns>
            protected override Task InitializeAsync()
            {
                return Task.CompletedTask;
            }
        }

        private class TestMessagingBackgroundService: MessagingBackgroundService
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
            /// Initializes this service (e.g. to set the <see cref="ConnectionName" /> and fill <see cref="Subscriptions" />
            /// collection
            /// </summary>
            /// <returns>An awaitable <see cref="Task" /></returns>
            protected override async Task InitializeAsync()
            {
                this.ConnectionName = ConfiguredConnectionName;
                var listenerObservable = await this.MessageClientService.ListenAsync<string>(this.ConnectionName, new FanoutExchangeConfiguration("BackgroundTest"));
                this.Subscriptions.Add(listenerObservable.Subscribe(x => this.ReceivedMessages.Add(x)));
            }
        }
    }
}
