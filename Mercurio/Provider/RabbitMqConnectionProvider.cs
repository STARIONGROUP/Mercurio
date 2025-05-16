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

    using Mercurio.Configuration;

    using RabbitMQ.Client;

    /// <summary>
    /// The <see cref="RabbitMqConnectionProvider" /> provides <see cref="IConnection" /> instance based on registered
    /// <see cref="Func{TResult}" /> for <see cref="ConnectionFactory" />
    /// </summary>
    internal sealed class RabbitMqConnectionProvider : IRabbitMqConnectionProvider, IDisposable
    {
        /// <summary>
        /// Gets the <see cref="ConcurrentDictionary{TKey,TValue}" /> that caches living <see cref="IConnection" /> created based on registered
        /// <see cref="ConnectionFactory" />
        /// </summary>
        private readonly ConcurrentDictionary<string, IConnection> connections = new();

        /// <summary>
        /// Gets the <see cref="ConcurrentDictionary{TKey,TValue}" /> that caches registered <see cref="ConnectionFactory" />
        /// </summary>
        private readonly ConcurrentDictionary<string, Func<IServiceProvider, Task<ConnectionFactory>>> registeredFactories = new();

        /// <summary>
        /// Gets the injected <see cref="IServiceProvider" /> that should be used to provide <see cref="ConnectionFactory" />
        /// action
        /// </summary>
        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitMqConnectionProvider"></see> class.
        /// </summary>
        /// <param name="serviceProvider">
        /// The injected <see cref="IServiceProvider" /> that should be used to provide
        /// <see cref="ConnectionFactory" /> action
        /// </param>
        /// <param name="configurations">A collection of injected <see cref="IConnectionFactoryConfiguration" /></param>
        public RabbitMqConnectionProvider(IServiceProvider serviceProvider, IEnumerable<IConnectionFactoryConfiguration> configurations)
        {
            this.serviceProvider = serviceProvider;

            foreach (var configuration in configurations)
            {
                this.RegisterFactory(configuration.ConnectionName, configuration.ConnectionFactory);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            foreach (var existingConnection in this.connections.Values)
            {
                existingConnection.Dispose();
            }

            this.connections.Clear();
        }

        /// <summary>
        /// Gets an <see cref="IConnection" /> from a name-based registration of <see cref="ConnectionFactory" />
        /// </summary>
        /// <param name="connectionName">The name of the registered connection</param>
        /// <returns>An awaitable <see cref="Task{TResult}" /> that provides access to an <see cref="IConnection" /></returns>
        public async Task<IConnection> GetConnectionAsync(string connectionName)
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
            var connection = await connectionFactory.CreateConnectionAsync();

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
    }
}
