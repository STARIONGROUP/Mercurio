// -------------------------------------------------------------------------------------------------
//  <copyright file="AutoRecoveryMessageBackgroundServiceTestFixture.cs" company="Starion Group S.A.">
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
    using System.Reactive.Linq;
    using System.Reactive.Subjects;

    using Mercurio.Extensions;
    using Mercurio.Messaging;
    using Mercurio.Model;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using Moq;

    using RabbitMQ.Client;

    [TestFixture]
    public class AutoRecoveryMessageBackgroundServiceTestFixture
    {
        private Mock<IMessageClientService> messageClientService;
        private const string ConfiguredConnectionName = "RabbitMQ";
        private Mock<IConfiguration> configurationMock;
        private MessagingBackgroundServiceTestFixture.TestMessagingBackgroundService backgroundService;
        private ServiceProvider serviceProvider;
        private Subject<string> messageSubject;
        private Subject<string> postErrorSubject; 
        
        [SetUp]
        public void Setup()
        {
            this.messageClientService = new Mock<IMessageClientService>();
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

            serviceCollection.AddSingleton(this.messageClientService.Object);
            serviceCollection.AddLogging();
            this.serviceProvider = serviceCollection.BuildServiceProvider();
            
            this.backgroundService = new MessagingBackgroundServiceTestFixture.TestMessagingBackgroundService(this.serviceProvider, this.serviceProvider.GetRequiredService<ILogger<MessagingBackgroundServiceTestFixture.TestMessagingBackgroundService>>(), this.configurationMock.Object);
            
            this.messageSubject = new Subject<string>();
            this.postErrorSubject = new Subject<string>();

            this.messageClientService.SetupSequence(x => x.ListenAsync<string>(ConfiguredConnectionName, It.IsAny<IExchangeConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(this.messageSubject.AsObservable())
                .ReturnsAsync(this.postErrorSubject.AsObservable());
            
            _ = this.backgroundService.StartAsync(CancellationToken.None);
        }

        [TearDown]
        public void Teardown()
        {
            this.backgroundService.Dispose();
            this.serviceProvider.Dispose();
            this.messageSubject.Dispose();
            this.postErrorSubject.Dispose();
        }

        [Test]
        public void VerifyObservableAutoRecovered()
        {
            this.messageSubject.OnNext("\"Hello\"");
            Assert.That(this.backgroundService.ReceivedMessages, Has.Count.EqualTo(1));

            this.messageSubject.OnError(new Exception());
            Assert.That(this.backgroundService.ReceivedMessages, Is.Empty);
            
            this.messageSubject.OnNext("\"Hello again\"");
            Assert.That(this.backgroundService.ReceivedMessages, Is.Empty);
            
            this.postErrorSubject.OnNext("\"Hello again\"");
            Assert.That(this.backgroundService.ReceivedMessages, Has.Count.EqualTo(1));
        }
    }
}
