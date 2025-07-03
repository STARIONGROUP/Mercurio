// -------------------------------------------------------------------------------------------------
//  <copyright file="RabbitMqConnectionProvider.cs" company="Starion Group S.A.">
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

namespace Mercurio.Provider
{
    using System.Collections.Concurrent;

    using Mercurio.Configuration.IConfiguration;
    using Mercurio.Messaging;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Polly;

    using RabbitMQ.Client;

    /// <summary>
    /// The <see cref="RabbitMqConnectionProvider" /> provides <see cref="IConnection" /> instance based on registered
    /// <see cref="Func{TResult}" /> for <see cref="ConnectionFactory" />
    /// </summary>
    internal sealed class RabbitMqConnectionProvider : IRabbitMqConnectionProvider, IDisposable
    {
        /// <summary>
        /// The <see cref="ThreadLocal{IChannel}" /> that holds the current channel for the thread.
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentQueue<IChannel>> channelPool = new();

        /// <summary>
        /// Gets the <see cref="ConcurrentDictionary{TKey,TValue}" /> that caches living <see cref="IConnection" /> created based on registered
        /// <see cref="ConnectionFactory" />
        /// </summary>
        private readonly ConcurrentDictionary<string, IConnection> connections = new();

        /// <summary>
        /// Gets the injected <see cref="ILogger{TCategoryName}" /> that should be used to log events
        /// </summary>
        private readonly ILogger<RabbitMqConnectionProvider> logger;

        /// <summary>
        /// The <see cref="SemaphoreSlim" /> used to control access to the channel pool.
        /// </summary>
        private readonly ConcurrentDictionary<string, SemaphoreSlim> poolGates = new();

        /// <summary>
        /// Gets the <see cref="ConcurrentDictionary{TKey,TValue}" /> that caches registered <see cref="ConnectionFactory" />
        /// </summary>
        private readonly ConcurrentDictionary<string, Func<IServiceProvider, Task<ConnectionFactory>>> registeredFactories = new();

        /// <summary>
        /// The <see cref="ConcurrentDictionary{TKey,TValue}" /> that caches the pool size for each registered connection factory.
        /// </summary>
        private readonly ConcurrentDictionary<string, int> registeredFactoriesPoolSize = new();

        /// <summary>
        /// Gets the injected <see cref="RetryPolicyConfiguration" />
        /// </summary>
        private readonly RetryPolicyConfiguration retryPolicyConfiguration;

        /// <summary>
        /// Gets the injected <see cref="IServiceProvider" /> that should be used to provide <see cref="ConnectionFactory" />
        /// action
        /// </summary>
        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitMqConnectionProvider"></see> class.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger{TCategoryName}" /></param>
        /// <param name="serviceProvider">
        /// The injected <see cref="IServiceProvider" /> that should be used to provide
        /// <see cref="ConnectionFactory" /> action
        /// </param>
        /// <param name="configurations">A collection of injected <see cref="IConnectionFactoryConfiguration" /></param>
        /// <param name="policyConfiguration">
        /// The optional injected and configured <see cref="IOptions{TOptions}" /> of
        /// <see cref="RetryPolicyConfiguration" />. In case of null, using default value of
        /// <see cref="retryPolicyConfiguration" />
        /// </param>
        public RabbitMqConnectionProvider(ILogger<RabbitMqConnectionProvider> logger, IServiceProvider serviceProvider, IEnumerable<IConnectionFactoryConfiguration> configurations, IOptions<RetryPolicyConfiguration> policyConfiguration = null)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;

            this.retryPolicyConfiguration = policyConfiguration?.Value ?? new RetryPolicyConfiguration();

            foreach (var configuration in configurations)
            {
                this.registeredFactoriesPoolSize[configuration.ConnectionName] = configuration.PoolSize;
                this.RegisterFactory(configuration.ConnectionName, configuration.ConnectionFactory);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            foreach (var existingConnection in this.connections)
            {
                try
                {
                    if (existingConnection.Value.IsOpen)
                    {
                        existingConnection.Value.CloseAsync(200, "Service shutting down");
                    }
                }
                finally
                {
                    existingConnection.Value.Dispose();
                }

                if (this.channelPool.TryGetValue(existingConnection.Key, out var disposablePool))
                {
                    foreach (var channel in disposablePool)
                    {
                        channel.Dispose();
                    }    
                }

                if (this.poolGates.TryGetValue(existingConnection.Key, out var poolGate))
                {
                    poolGate.Dispose();
                }
            }

            this.connections.Clear();
        }

        /// <summary>
        /// Gets an <see cref="IConnection" /> from a name-based registration of <see cref="ConnectionFactory" />
        /// </summary>
        /// <param name="connectionName">The name of the registered connection</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task{TResult}" /> that provides access to an <see cref="IConnection" /></returns>
        public async Task<IConnection> GetConnectionAsync(string connectionName, CancellationToken cancellationToken = default)
        {
            if (!this.registeredFactories.TryGetValue(connectionName, out var registeredFactory))
            {
                throw new ArgumentException($"None ConnectionFactory has been registered for the name {connectionName}", nameof(connectionName));
            }

            if (this.connections.TryGetValue(connectionName, out var existingConnection) && existingConnection.IsOpen)
            {
                return existingConnection;
            }

            var connectionFactory = await registeredFactory(this.serviceProvider);
            var connection = await connectionFactory.CreateConnectionAsync(connectionName, cancellationToken);

            this.connections[connectionName] = connection;
            return connection;
        }

        /// <summary>
        /// Register a new <see cref="ConnectionFactory" /> that could be resolved to provide an <see cref="IConnection" />
        /// </summary>
        /// <param name="name">The registration name that should be used</param>
        /// <param name="factory">A <see cref="Func{T,TResult}" /> that defines how the <see cref="ConnectionFactory" /> can be initialized</param>
        public void RegisterFactory(string name, Func<IServiceProvider, Task<ConnectionFactory>> factory)
        {
            this.registeredFactories[name] = factory;
        }

        /// <summary>
        /// Asynchronously leases a channel from the pool or creates one if necessary.
        /// </summary>
        /// <param name="connectionName">The name of the registered connection that should be used to establish the connection</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>A <see cref="ValueTask{TResult}" /> of <see cref="ChannelLease" /></returns>
        public async ValueTask<ChannelLease> LeaseChannelAsync(string connectionName, CancellationToken cancellationToken = default)
        {
            return new ChannelLease(connectionName, await this.GetChannelAsync(connectionName, cancellationToken), this);
        }

        /// <summary>
        /// Returns the current channel to the pool if it is still open, or disposes it if it is closed or null.
        /// </summary>
        /// <param name="connectionName"> The name of the connection associated with the channel, used for logging or identification purposes.</param>
        /// <param name="channel">The <see cref="IChannel" /> to release back to the pool</param>
        /// <remarks>
        /// This method should always be called after a channel is obtained using <c>GetChannelAsync</c>, typically in a
        /// <c>finally</c> block,
        /// to ensure proper resource management and to maintain pool capacity. Or even better using <see cref="ChannelLease" />
        /// </remarks>
        /// <returns>The <see cref="Task" /> that represents the completion of the return operation. </returns>
        async Task ICanReleaseChannel.ReleaseChannelAsync(string connectionName, IChannel channel)
        {
            this.ReleaseChannel(connectionName, channel);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Returns the current channel to the pool if it is still open, or disposes it if it is closed or null.
        /// </summary>
        /// <param name="connectionName"> The name of the connection associated with the channel, used for logging or identification purposes.</param>
        /// <param name="channel">The <see cref="IChannel" /> to release back to the pool</param>
        /// <remarks>
        /// This method should always be called after a channel is obtained using <c>GetChannelAsync</c>, typically in a
        /// <c>finally</c> block,
        /// to ensure proper resource management and to maintain pool capacity. Or even better using <see cref="ChannelLease" />
        /// </remarks>
        void ICanReleaseChannel.ReleaseChannel(string connectionName, IChannel channel)
        {
            this.ReleaseChannel(connectionName, channel);
        }

        /// <summary>
        /// Asynchronously acquires a channel from the pool or creates one if necessary.
        /// </summary>
        /// <param name="connectionName">The name of the registered connection that should be used to establish the connection</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken" /> used to observe cancellation requests.
        /// </param>
        /// <returns>
        /// A task that completes with an open <see cref="IChannel" /> bound to the current async flow.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown if the operation is cancelled before a channel is acquired.
        /// </exception>
        private async Task<IChannel> GetChannelAsync(string connectionName, CancellationToken cancellationToken = default)
        {
            await this.poolGates.GetOrAdd(connectionName, new SemaphoreSlim(this.registeredFactoriesPoolSize[connectionName])).WaitAsync(cancellationToken).ConfigureAwait(false);

            if (!this.channelPool.TryGetValue(connectionName, out var queue) || !queue.TryDequeue(out var channel) || channel is { IsOpen: false })
            {
                channel = await this.CreateChannelWithRetryAsync(connectionName, cancellationToken).ConfigureAwait(false);
            }

            return channel;
        }

        /// <summary>
        /// Attempts to create a new RabbitMQ channel with retry logic using exponential backoff strategy.
        /// </summary>
        /// <param name="connectionName">The name of the registered connection that should be used to establish the connection</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
        /// <returns>
        /// A task representing the asynchronous operation. The result is an open <see cref="IChannel" /> for the current thread.
        /// </returns>
        /// <remarks>
        /// The method retries the connection a configured number of times using an exponential backoff policy.
        /// It marks the channel creation state per thread to coordinate parallel connection attempts.
        /// </remarks>
        /// <exception cref="TimeoutException">
        /// Thrown when the maximum number of retry attempts is reached and the connection to RabbitMQ could not be established.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if the operation is cancelled via
        /// <paramref name="cancellationToken" />.
        /// </exception>
        private async Task<IChannel> CreateChannelWithRetryAsync(string connectionName, CancellationToken cancellationToken)
        {
            var result = await Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(this.retryPolicyConfiguration.MaxConnectionRetryAttempts, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    (exception, delay, attemptNumber, _) => this.logger.LogWarning(exception, "ChannelLease creation attempt {Attempt} failed. Retrying in {Delay}", attemptNumber, delay))
                .ExecuteAndCaptureAsync(() => this.CreateChannelAsync(connectionName, cancellationToken));

            if (result.Outcome != OutcomeType.Successful)
            {
                throw result.FinalException ?? new TimeoutException(
                    $"Unable to connect to {connectionName} after {this.retryPolicyConfiguration.MaxConnectionRetryAttempts} attempts");
            }

            return result.Result;
        }

        /// <summary>
        /// Returns the current channel to the pool if it is still open, or disposes it if it is closed or null.
        /// </summary>
        /// <param name="connectionName"> The name of the connection associated with the channel, used for logging or identification purposes.</param>
        /// <param name="channel">The <see cref="IChannel" /> to release back to the pool</param>
        /// <remarks>
        /// This method should always be called after a channel is obtained using <c>GetChannelAsync</c>, typically in a
        /// <c>finally</c> block,
        /// to ensure proper resource management and to maintain pool capacity. Or even better using <see cref="ChannelLease" />
        /// </remarks>
        private void ReleaseChannel(string connectionName, IChannel channel)
        {
            switch (channel)
            {
                case null:
                    return;
                case { IsOpen: true }:
                    this.channelPool.GetOrAdd(connectionName, new ConcurrentQueue<IChannel>()).Enqueue(channel);
                    break;
                default:
                    channel.Dispose();
                    break;
            }

            this.poolGates.GetOrAdd(connectionName, new SemaphoreSlim(this.registeredFactoriesPoolSize.GetOrAdd(connectionName, 32))).Release();
        }

        /// <summary>
        /// Creates a new channel for an open connection asynchronously. If the connection is not open or does not exist,
        /// a new connection is established using the provided connection factory before creating the channel.
        /// </summary>
        /// <param name="connectionName">The name of the registered connection that should be used to establish the connection</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation, containing the created <see cref="IChannel" />.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the connection factory is not properly initialized.</exception>
        /// <exception cref="Exception">Thrown if connection or channel creation fails due to network or server issues.</exception>
        private async Task<IChannel> CreateChannelAsync(string connectionName, CancellationToken cancellationToken)
        {
            var connection = await this.GetConnectionAsync(connectionName, cancellationToken).ConfigureAwait(false);

            var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            return channel;
        }
    }
}
