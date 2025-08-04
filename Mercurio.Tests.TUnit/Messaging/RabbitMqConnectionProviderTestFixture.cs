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

using ThrowsExtensions = TUnit.Assertions.AssertConditions.Throws.ThrowsExtensions;

namespace Mercurio.Tests.TUnit.Messaging
{
    using System.Diagnostics;

    using Mercurio.Configuration.IConfiguration;
    using Mercurio.Provider;

    using Microsoft.Extensions.Options;

    using Moq;

    using RabbitMQ.Client;

    public class RabbitMqConnectionProviderTestFixture
    {
        private const string ConnectionName = "TestConnection";
        private Mock<IChannel> channel;
        private Mock<IConnection> connection;
        private RabbitMqConnectionProvider service;

        [Before(Test)]
        public async Task Setup()
        {
            var mockServiceProvider = new Mock<IServiceProvider>();

            this.channel = new Mock<IChannel>();
            this.channel.SetupGet(c => c.IsOpen).Returns(true);

            this.connection = new Mock<IConnection>();
            this.connection.Setup(c => c.IsOpen).Returns(true);
            this.connection.SetupGet(c => c.ClientProvidedName).Returns(ConnectionName);
            this.connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(this.channel.Object);

            var mockConfig = new Mock<IConnectionFactoryConfiguration>();
            mockConfig.SetupGet(c => c.ConnectionName).Returns(ConnectionName);
            mockConfig.Setup(c => c.ActivitySource).Returns((ActivitySource)null);
            mockConfig.SetupGet(c => c.PoolSize).Returns(10);

            mockConfig.SetupGet(c => c.ConnectionFactory).Returns(_ => Task.FromResult(new ConnectionFactory()));

            this.service = new RabbitMqConnectionProvider(null, mockServiceProvider.Object, [mockConfig.Object],
                Options.Create(new RetryPolicyConfiguration { MaxConnectionRetryAttempts = 1 }));

            await Task.CompletedTask;
        }

        [After(Test)]
        public async Task Teardown()
        {
            this.service.Dispose();
            await Task.CompletedTask;
        }

        [Test]
        public async Task Should_Create_And_Cache_Connection_And_Channel()
        {
            IChannel oldLeaseChannel;

            using var _ = Assert.Multiple();

            await using (var lease = await this.service.LeaseChannelAsync(ConnectionName))
            {
                oldLeaseChannel = lease.Channel;

                await Assert.That(lease).IsNotDefault();
                await Assert.That(lease.Channel).IsNotDefault();
                await Assert.That(lease.Channel.IsOpen).IsTrue();
            }

            await using var newLease = await this.service.LeaseChannelAsync(ConnectionName);
            await Assert.That(() => oldLeaseChannel).IsSameReferenceAs(newLease.Channel);
            await Assert.That(newLease.Channel.IsOpen).IsTrue();
        }

        [Test]
        public async Task Should_Throw_When_Connection_Not_Registered()
        {
            await ThrowsExtensions.Throws<ArgumentException>(Assert.That(() => this.service.GetConnectionAsync("Unregistered")));
        }
    }
}
