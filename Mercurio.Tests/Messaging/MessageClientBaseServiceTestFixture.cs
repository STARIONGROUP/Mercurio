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
    using System.Diagnostics.CodeAnalysis;

    using Mercurio.Configuration;
    using Mercurio.Messaging;
    using Mercurio.Provider;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    using ExchangeType = Mercurio.Enumeration.ExchangeType;

    [TestFixture]
    public class MessageClientBaseServiceTestFixture
    {
        private TestMessageClientBaseService service;
        private Mock<IOptions<RetryPolicyConfiguration>> retryPolicyConfiguration;
        private Mock<IRabbitMqConnectionProvider> connectionProvider;

        [SetUp]
        public void Setup()
        {
            this.retryPolicyConfiguration = new Mock<IOptions<RetryPolicyConfiguration>>();
            this.retryPolicyConfiguration.Setup(x => x.Value).Returns(new RetryPolicyConfiguration(){MaxConnectionRetryAttempts = 1});
            this.connectionProvider = new Mock<IRabbitMqConnectionProvider>();
            var logger = new Mock<ILogger<TestMessageClientBaseService>>();
            
            this.service = new TestMessageClientBaseService(this.connectionProvider.Object, logger.Object, this.retryPolicyConfiguration.Object);
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
            this.connectionProvider.Setup(x => x.GetConnectionAsync(connectionName)).ThrowsAsync(new ArgumentException("Non registered connection"));
            
            await Assert.ThatAsync(() => this.service.TestGetChannelAsync(connectionName, CancellationToken.None), Throws.ArgumentException);
            
            var connection = new Mock<IConnection>();
            this.connectionProvider.Setup(x => x.GetConnectionAsync(connectionName)).ReturnsAsync(connection.Object);
            connection.Setup(x => x.CreateChannelAsync(null, CancellationToken.None)).ThrowsAsync(new InvalidOperationException("Failed to open channel"));
            
            await Assert.ThatAsync(() => this.service.TestGetChannelAsync(connectionName, CancellationToken.None), Throws.InvalidOperationException);
            var channel = new Mock<IChannel>();

            channel.Setup(x => x.IsOpen).Returns(false);
            connection.Setup(x => x.CreateChannelAsync(null, CancellationToken.None)).ReturnsAsync(channel.Object);

            await Assert.ThatAsync(() => this.service.TestGetChannelAsync(connectionName, CancellationToken.None), Throws.TypeOf<TimeoutException>());
            channel.Setup(x => x.IsOpen).Returns(true);

            await Assert.MultipleAsync(async () =>
            {
                await Assert.ThatAsync(async () => await this.service.TestGetChannelAsync(connectionName, CancellationToken.None), Is.SameAs(channel.Object));
                this.connectionProvider.Verify(x => x.GetConnectionAsync(connectionName), Times.Exactly(6));
            });
            
            connection.Setup(x => x.IsOpen).Returns(true);

           await Assert.MultipleAsync(async () =>
            {
                await Assert.ThatAsync(async () => await this.service.TestGetChannelAsync(connectionName, CancellationToken.None), Is.SameAs(channel.Object));
                this.connectionProvider.Verify(x => x.GetConnectionAsync(connectionName), Times.Exactly(6));
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
        public Task<IChannel> TestGetChannelAsync(string connectionName, CancellationToken cancellationToken = default)
        {
            return this.GetChannelAsync(connectionName, cancellationToken);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="MessageClientBaseService" />
        /// </summary>
        /// <param name="connectionProvider">
        /// The injected <see cref="IRabbitMqConnectionProvider" /> <see cref="RabbitMQ.Client.IConnection" />
        /// access based on registered <see cref="RabbitMQ.Client.ConnectionFactory" />
        /// </param>
        /// <param name="logger">The injected <see cref="Microsoft.Extensions.Logging.ILogger{TCategoryName}" /></param>
        /// <param name="policyConfiguration">
        /// The injected and configured <see cref="Microsoft.Extensions.Options.IOptions{TOptions}" /> of
        /// <see cref="RetryPolicyConfiguration" />
        /// </param>
        public TestMessageClientBaseService(IRabbitMqConnectionProvider connectionProvider, ILogger<TestMessageClientBaseService> logger, IOptions<RetryPolicyConfiguration> policyConfiguration) : base(connectionProvider, logger, policyConfiguration)
        {
        }

        /// <summary>
        /// Listens for messages of type <typeparamref name="TMessage" /> on the specified queue.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="queueName">The name of the queue to listen on.</param>
        /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
        /// <param name="exchangeType">The exchange type. Default is <see cref="Enumeration.ExchangeType.Default" />.</param>
        /// <param name="exchangeName">The exchange name. Default is null.</param>
        /// <param name="routingKey">The routing key name. Default is null.</param>
        /// <returns>An observable sequence of messages.</returns>
        public override Task<IObservable<TMessage>> ListenAsync<TMessage>(string queueName, ExchangeType exchangeType = ExchangeType.Default, CancellationToken cancellationToken = default, string exchangeName = null, string routingKey = null)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Listens for messages of type <typeparamref name="TMessage" /> on the specified queue.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="queue">The queue to listen on.</param>
        /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
        /// <param name="exchangeType">The exchange type. Default is <see cref="ExchangeType.Default" />.</param>
        /// <param name="exchangeName">The exchange name. Default is null.</param>
        /// <param name="routingKey">The routing key name. Default is null.</param>
        /// <returns>An observable sequence of messages.</returns>
        public override Task<IObservable<TMessage>> ListenAsync<TMessage>(Enum queue, ExchangeType exchangeType = ExchangeType.Default, CancellationToken cancellationToken = default, string exchangeName = null, string routingKey = null)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Adds a listener to the specified queue
        /// </summary>
        /// <param name="queueName">The <see cref="string" /> queue name</param>
        /// <param name="onReceiveAsync">The <see cref="RabbitMQ.Client.Events.AsyncEventHandler{TEvent}" /></param>
        /// <param name="exchangeType">
        /// The string exchange type It can be any value from <see cref="ExchangeType" />, default value is
        /// <see cref="ExchangeType.Default" />
        /// </param>
        /// <param name="cancellationToken">An optional <see cref="System.Threading.CancellationToken" /></param>
        /// <return>A <see cref="System.Threading.Tasks.Task" /> of <see cref="System.IDisposable" /></return>
        public override Task<IDisposable> AddListenerAsync(string queueName, AsyncEventHandler<BasicDeliverEventArgs> onReceiveAsync, ExchangeType exchangeType = ExchangeType.Default, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Pushes the specified <paramref name="messages" /> to the specified <paramref name="messageQueue" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="messageQueue">
        /// The <see cref="string" /> queue name on which to send to the <paramref name="messages" />
        /// </param>
        /// <param name="messages">The collection of <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeType">
        /// The string exchange type It can be any value from <see cref="ExchangeType" />, default value is
        /// <see cref="ExchangeType.Default" />
        /// </param>
        /// <param name="cancellationToken">An optional <see cref="System.Threading.CancellationToken" /></param>
        /// <returns>An awaitable <see cref="System.Threading.Tasks.Task" /></returns>
        public override Task PushAsync<TMessage>(string messageQueue, IEnumerable<TMessage> messages, ExchangeType exchangeType = ExchangeType.Default, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Pushes the specified <paramref name="message" /> to the specified <paramref name="messageQueue" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="messageQueue">
        /// The <see cref="string" /> queue name on which to send to the <paramref name="message" />
        /// </param>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeType">
        /// The string exchange type It can be any value from <see cref="ExchangeType" />, default value is
        /// <see cref="ExchangeType.Default" />
        /// </param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">A possible <see cref="System.Threading.CancellationToken" /></param>
        /// <returns>An awaitable <see cref="System.Threading.Tasks.Task" /></returns>
        /// <exception cref="System.ArgumentNullException">When the provided <typeparamref name="TMessage" /> is null</exception>
        public override Task PushAsync<TMessage>(string messageQueue, TMessage message, ExchangeType exchangeType = ExchangeType.Default, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Pushes the specified <paramref name="message" /> to the specified <paramref name="messageQueue" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="messageQueue">The <see cref="System.Enum" /> queue on which to send to the <paramref name="message" /></param>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeType">
        /// The string exchange type It can be any value from <see cref="ExchangeType" />, default value is
        /// <see cref="ExchangeType.Default" />
        /// </param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">A possible <see cref="System.Threading.CancellationToken" /></param>
        /// <returns>An awaitable <see cref="System.Threading.Tasks.Task" /></returns>
        /// <exception cref="System.ArgumentNullException">When the provided <typeparamref name="TMessage" /> is null</exception>
        public override Task PushAsync<TMessage>(Enum messageQueue, TMessage message, ExchangeType exchangeType = ExchangeType.Default, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
