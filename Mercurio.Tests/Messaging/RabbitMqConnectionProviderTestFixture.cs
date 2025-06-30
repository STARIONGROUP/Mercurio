// -------------------------------------------------------------------------------------------------
//  <copyright file="RabbitMqConnectionProviderTestFixture.cs" company="Starion Group S.A.">
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
    using Mercurio.Configuration.IConfiguration;
    using Mercurio.Provider;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;

    using RabbitMQ.Client;

    public class RabbitMqConnectionProviderTestFixture
    {
        private RabbitMqConnectionProvider service;
        private Mock<IConnection> connection;
        private Mock<IChannel> channel;
        private const string ConnectionName = "TestConnection";

        [SetUp]
        public async Task Setup()
        {
            var mockServiceProvider = new Mock<IServiceProvider>();

            this.channel = new Mock<IChannel>();
            this.channel.SetupGet(c => c.IsOpen).Returns(true);

            this.connection = new Mock<IConnection>();
            this.connection.Setup(c => c.IsOpen).Returns(true);
            this.connection.SetupGet(c => c.ClientProvidedName).Returns(ConnectionName);
            this.connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(channel.Object);

            var mockConfig = new Mock<IConnectionFactoryConfiguration>();
            mockConfig.Setup(c => c.ConnectionName).Returns(ConnectionName);
            mockConfig.Setup(c => c.PoolSize).Returns(10);
            mockConfig.Setup(c => c.ConnectionFactory)
                      .Returns(_ => Task.FromResult(new ConnectionFactory()));

            this.service = new RabbitMqConnectionProvider(null, mockServiceProvider.Object, new[] { mockConfig.Object },
                Options.Create(new RetryPolicyConfiguration { MaxConnectionRetryAttempts = 1 }));

            await Task.CompletedTask;
        }

        [TearDown]
        public async Task Teardown()
        {
            this.service.Dispose();
            await Task.CompletedTask;
        }

        [Test]
        public async Task Should_Create_And_Cache_Connection_And_Channel()
        {
            var oldLeaseChannel = default(IChannel);

            using (var lease = await this.service.LeaseChannelAsync(ConnectionName))
            {
                oldLeaseChannel = lease.Channel;

                Assert.Multiple(() =>
                {
                    Assert.That(lease, Is.Not.Default);
                    Assert.That(lease.Channel, Is.Not.Null);
                    Assert.That(lease.Channel.IsOpen, Is.True);
                });
            }

            await using var newLease = await this.service.LeaseChannelAsync(ConnectionName);
            Assert.That(() => oldLeaseChannel, Is.SameAs(newLease.Channel));
            Assert.That(newLease.Channel.IsOpen, Is.True);
        }

        [Test]
        public void Should_Throw_When_Connection_Not_Registered()
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await this.service.GetConnectionAsync("Unregistered");
            });
        }
    }
}
