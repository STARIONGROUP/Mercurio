// -------------------------------------------------------------------------------------------------
//  <copyright file="MessageClientBaseService.cs" company="Starion Group S.A.">
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

namespace Mercurio.Messaging
{
    using Mercurio.Configuration;
    using Mercurio.Model;
    using Mercurio.Provider;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Polly;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    /// <summary>
    /// The <see cref="MessageClientBaseService" /> is the base abstract class for any RabbitMQ client
    /// </summary>
    public abstract class MessageClientBaseService : IMessageClientBaseService
    {
        /// <summary>
        /// Gets the injected <see cref="RetryPolicyConfiguration" />
        /// </summary>
        private readonly RetryPolicyConfiguration retryPolicyConfiguration;

        /// <summary>
        /// The per thread <see cref="IChannel" />
        /// </summary>
        private readonly ThreadLocal<IChannel> threadLocalChannel = new();

        /// <summary>
        /// Gets the current <see cref="IConnection" />
        /// </summary>
        private IConnection connection;

        /// <summary>
        /// Initializes a new instance of <see cref="MessageClientBaseService" />
        /// </summary>
        /// <param name="connectionProvider">
        /// The injected <see cref="IRabbitMqConnectionProvider" /> <see cref="IConnection" />
        /// access based on registered <see cref="ConnectionFactory" />
        /// </param>
        /// <param name="logger">The injected <see cref="ILogger{TCategoryName}" /></param>
        /// <param name="policyConfiguration">
        /// The optional injected and configured <see cref="IOptions{TOptions}" /> of
        /// <see cref="RetryPolicyConfiguration" />. In case of null, using default value of
        /// <see cref="retryPolicyConfiguration" />
        /// </param>
        protected MessageClientBaseService(IRabbitMqConnectionProvider connectionProvider, ILogger<MessageClientBaseService> logger, IOptions<RetryPolicyConfiguration> policyConfiguration = null)
        {
            this.ConnectionProvider = connectionProvider;
            this.Logger = logger;
            this.retryPolicyConfiguration = policyConfiguration?.Value ?? new RetryPolicyConfiguration();
        }

        /// <summary>
        /// Gets the injected <see cref="ILogger{TCategoryName}" />
        /// </summary>
        protected ILogger<MessageClientBaseService> Logger { get; }

        /// <summary>
        /// Gets the injected <see cref="IRabbitMqConnectionProvider" /> <see cref="IConnection" /> access based on registered
        /// <see cref="ConnectionFactory" />
        /// </summary>
        protected IRabbitMqConnectionProvider ConnectionProvider { get; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Listens for messages of type <typeparamref name="TMessage" /> on the specified queue.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
        /// <returns>An observable sequence of messages.</returns>
        public abstract Task<IObservable<TMessage>> ListenAsync<TMessage>(string connectionName, IExchangeConfiguration exchangeConfiguration, CancellationToken cancellationToken = default) where TMessage : class;

        /// <summary>
        /// Adds a listener to the specified queue
        /// </summary>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="onReceiveAsync">The <see cref="AsyncEventHandler{TEvent}" /></param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <return>A <see cref="Task" /> of <see cref="IDisposable" /></return>
        public abstract Task<IDisposable> AddListenerAsync(string connectionName, IExchangeConfiguration exchangeConfiguration, AsyncEventHandler<BasicDeliverEventArgs> onReceiveAsync, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes the specified <paramref name="messages" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="messages">The collection of <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to use the <see cref="DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        public abstract Task PushAsync<TMessage>(string connectionName, IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes the specified <paramref name="message" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">A possible <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <exception cref="ArgumentNullException">When the provided <typeparamref name="TMessage" /> is null</exception>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to use the <see cref="DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        public abstract Task PushAsync<TMessage>(string connectionName, TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Establishes a connection to the RabbitMQ server and returns an asynchronous <see cref="IChannel" /> Channel.
        /// </summary>
        /// <param name="connectionName">The name of the registered connection that should be used to establish the connection</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> for task cancellation.</param>
        /// <returns>An asynchronous task returning a <see cref="IChannel" /> Channel.</returns>
        protected async Task<IChannel> GetChannelAsync(string connectionName, CancellationToken cancellationToken = default)
        {
            var currentThreadChannel = this.threadLocalChannel.Value;

            if (currentThreadChannel is { IsOpen: true })
            {
                return currentThreadChannel;
            }

            var attemptNumber = 0;

            var policy = Policy.HandleResult<bool>(result =>
                {
                    if (result)
                    {
                        this.Logger.LogDebug("Established connection to the message broker. {Endpoint}", this.connection.Endpoint.HostName);
                        return false;
                    }

                    this.HandleConnectionFailed(connectionName, ref attemptNumber);
                    return true;
                })
                .Or<Exception>(x => this.HandleConnectionFailed(connectionName, ref attemptNumber, x))
                .WaitAndRetryAsync(this.retryPolicyConfiguration.MaxConnectionRetryAttempts, _ => TimeSpan.FromSeconds(this.retryPolicyConfiguration.TimeSpanBetweenAttempts));

            var result = await policy.ExecuteAndCaptureAsync(_ => this.GetChannel(connectionName), cancellationToken);

            if (result.Outcome is not OutcomeType.Successful)
            {
                throw result.FinalException ?? new TimeoutException(
                    $"Unable to connect to '{connectionName}' after {attemptNumber} attempts");
            }

            return this.threadLocalChannel.Value;
        }

        /// <summary>
        /// Logic to run after the <see cref="IChannel" /> has been created
        /// </summary>
        protected virtual void AfterChannelCreation()
        {
            //Does nothing
        }

        /// <summary>
        /// Model shutdown event handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The arguments.</param>
        protected virtual Task OnChannelModelShutdownAsync(object sender, ShutdownEventArgs e)
        {
            this.Logger.LogWarning("Message broker channel model has shut down. {Cause}", e.Cause);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Connection unblocked event handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The arguments.</param>
        protected virtual Task OnConnectionUnblockedAsync(object sender, AsyncEventArgs e)
        {
            this.Logger.LogInformation("Message broker connection unblocked.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Connection shutdown event handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The arguments.</param>
        protected virtual Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs e)
        {
            this.Logger.LogWarning("Message broker connection shutdown. {Cause}", e.Cause);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Connection blocked event handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The arguments.</param>
        protected virtual Task OnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs e)
        {
            this.Logger.LogWarning("Message broker connection blocked. {Reason}", e.Reason);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes of the even handlers
        /// </summary>
        /// <param name="disposing">A value indicating whether this client is disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            if (this.connection != null)
            {
                this.connection.ConnectionBlockedAsync -= this.OnConnectionBlockedAsync;
                this.connection.ConnectionShutdownAsync -= this.OnConnectionShutdownAsync;
                this.connection.ConnectionUnblockedAsync -= this.OnConnectionUnblockedAsync;
            }

            if (this.threadLocalChannel.IsValueCreated && this.threadLocalChannel.Value != null)
            {
                this.threadLocalChannel.Value.ChannelShutdownAsync -= this.OnChannelModelShutdownAsync;
                this.threadLocalChannel.Value.Dispose();
            }
        }

        /// <summary>
        /// Creates a RabbitMQ connection and a channel, establishing a connection to the server.
        /// </summary>
        /// <param name="connectionName">The name of the registered connection that should be used to establish the connection</param>
        /// <returns>An asynchronous task returning a value indicating whether the channel is open.</returns>
        private async Task<bool> GetChannel(string connectionName)
        {
            this.connection = await this.ConnectionProvider.GetConnectionAsync(connectionName);

            this.connection.ConnectionBlockedAsync += this.OnConnectionBlockedAsync;
            this.connection.ConnectionShutdownAsync += this.OnConnectionShutdownAsync;
            this.connection.ConnectionUnblockedAsync += this.OnConnectionUnblockedAsync;

            var newChannel = await this.connection.CreateChannelAsync();

            newChannel.ChannelShutdownAsync += this.OnChannelModelShutdownAsync;

            this.threadLocalChannel.Value = newChannel;

            this.AfterChannelCreation();

            return newChannel is { IsOpen: true };
        }

        /// <summary>
        /// Handles connection to the RabbitMQ server failures
        /// </summary>
        /// <param name="connectionName">The name of the registered connection that has been be used to try to establish the connection</param>
        /// <param name="attemptNumber">The current attempt number</param>
        /// <param name="exception">The exception which occured</param>
        private bool HandleConnectionFailed(string connectionName, ref int attemptNumber, Exception exception = null)
        {
            if (this.connection != null)
            {
                this.connection.ConnectionBlockedAsync -= this.OnConnectionBlockedAsync;
                this.connection.ConnectionShutdownAsync -= this.OnConnectionShutdownAsync;
                this.connection.ConnectionUnblockedAsync -= this.OnConnectionUnblockedAsync;
            }

            if (this.threadLocalChannel.Value != null)
            {
                this.threadLocalChannel.Value.ChannelShutdownAsync -= this.OnChannelModelShutdownAsync;
            }

            var message = $"The message client failed to connect to '{connectionName}'. {exception?.Message ?? ""}";

            if (attemptNumber > this.retryPolicyConfiguration.MaxConnectionRetryAttempts)
            {
                this.Logger.LogError("{Message} A time out occurred", message);
                return false;
            }

            attemptNumber++;
            this.Logger.LogError("{Message} Retrying in {SleepInterval} seconds...", message, this.retryPolicyConfiguration.TimeSpanBetweenAttempts);
            return true;
        }
    }
}
