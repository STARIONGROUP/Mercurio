// -------------------------------------------------------------------------------------------------
//  <copyright file="MessageClientBaseServiceTestFixture.cs" company="Starion Group S.A.">
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
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;

    using Mercurio.Messaging;
    using Mercurio.Model;
    using Mercurio.Provider;

    using Microsoft.Extensions.Logging;

    using Moq;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    [TestFixture]
    public class MessageClientBaseServiceTestFixture
    {
        private TestMessageClientBaseService service;
        private Mock<IRabbitMqConnectionProvider> connectionProvider;

        [SetUp]
        public void Setup()
        {
            this.connectionProvider = new Mock<IRabbitMqConnectionProvider>();
            var logger = new Mock<ILogger<TestMessageClientBaseService>>();
            
            this.service = new TestMessageClientBaseService(this.connectionProvider.Object, logger.Object);
        }

        [TearDown]
        public void Teardown()
        {
            this.service.Dispose();
        }

        [Test]
        public async Task VerifyGetChannel()
        {
            const string connectionName = "Connection";
            this.connectionProvider.Setup(x => x.LeaseChannelAsync(connectionName, It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("Non registered connection"));

            await Assert.ThatAsync(() => this.service.TestGetChannelAsync(connectionName, CancellationToken.None), Throws.ArgumentException);

            var channel = new Mock<IChannel>();

            var lease = new ChannelLease(connectionName, channel.Object, this.connectionProvider.Object);
            this.connectionProvider.Setup(x => x.LeaseChannelAsync(connectionName, It.IsAny<CancellationToken>())).ReturnsAsync(lease);

            await Assert.MultipleAsync(async () =>
            {
                await Assert.ThatAsync(async () => await this.service.TestGetChannelAsync(connectionName, CancellationToken.None), Is.SameAs(channel.Object));
                this.connectionProvider.Verify(x => x.LeaseChannelAsync(connectionName, It.IsAny<CancellationToken>()), Times.Exactly(2));
            });
        }
    }
    
    [ExcludeFromCodeCoverage]
    public class TestMessageClientBaseService: MessageClientBaseService
    {
        /// <summary>
        /// Establishes a connection to the RabbitMQ server and returns an asynchronous <see cref="IChannel" /> Channel.
        /// </summary>
        /// <param name="connectionName">The name of the registered connection that should be used to establish the connection</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> for task cancellation.</param>
        /// <returns>An asynchronous task returning a <see cref="IChannel" /> Channel.</returns>
        public async Task<IChannel> TestGetChannelAsync(string connectionName, CancellationToken cancellationToken = default)
        {
            return (await this.LeaseChannelAsync(connectionName, cancellationToken)).Channel;
        }
        
        /// <summary>
        /// Initializes a new instance of <see cref="MessageClientBaseService" />
        /// </summary>
        /// <param name="connectionProvider">
        /// The injected <see cref="IRabbitMqConnectionProvider" /> <see cref="RabbitMQ.Client.IConnection" />
        /// access based on registered <see cref="RabbitMQ.Client.ConnectionFactory" />
        /// </param>
        /// <param name="logger">The injected <see cref="Microsoft.Extensions.Logging.ILogger{TCategoryName}" /></param>
        public TestMessageClientBaseService(IRabbitMqConnectionProvider connectionProvider, ILogger<MessageClientBaseService> logger) : base(connectionProvider, logger)
        {
        }

        /// <summary>
        /// Listens for messages of type <typeparamref name="TMessage" /> on the specified queue.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="activityName">
        /// Defines the name of an <see cref="Activity" /> that should be initialized when a message has been received, for traceability. In case of null or empty, no
        /// <see cref="Activity" /> is started
        /// </param>
        /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
        /// <returns>An observable sequence of messages.</returns>
        public override Task<IObservable<TMessage>> ListenAsync<TMessage>(string connectionName, IExchangeConfiguration exchangeConfiguration, string activityName = "", CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Adds a listener to the specified queue
        /// </summary>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="onReceiveAsync">The <see cref="AsyncEventHandler{TEvent}" /></param>
        /// <param name="activityName">
        /// Defines the name of an <see cref="Activity" /> that should be initialized when a message has been received, for traceability. In case of null or empty, no
        /// <see cref="Activity" /> is started
        /// </param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <return>A <see cref="Task" /> of <see cref="IDisposable" /></return>
        public override Task<IDisposable> AddListenerAsync(string connectionName, IExchangeConfiguration exchangeConfiguration, AsyncEventHandler<BasicDeliverEventArgs> onReceiveAsync, string activityName = "", CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Pushes the specified <paramref name="messages" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="messages">The collection of <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">An optional <see cref="System.Threading.CancellationToken" /></param>
        /// <returns>An awaitable <see cref="System.Threading.Tasks.Task" /></returns>
        /// <remarks>
        /// By default, the <see cref="RabbitMQ.Client.BasicProperties" /> is configured to use the <see cref="RabbitMQ.Client.DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="RabbitMQ.Client.BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        public override Task PushAsync<TMessage>(string connectionName, IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Pushes the specified <paramref name="message" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">A possible <see cref="System.Threading.CancellationToken" /></param>
        /// <returns>An awaitable <see cref="System.Threading.Tasks.Task" /></returns>
        /// <exception cref="System.ArgumentNullException">When the provided <typeparamref name="TMessage" /> is null</exception>
        /// <remarks>
        /// By default, the <see cref="RabbitMQ.Client.BasicProperties" /> is configured to use the <see cref="RabbitMQ.Client.DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="RabbitMQ.Client.BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        public override Task PushAsync<TMessage>(string connectionName, TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
