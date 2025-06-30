// -------------------------------------------------------------------------------------------------
//  <copyright file="IRabbitMqConnectionProvider.cs" company="Starion Group S.A.">
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
    using Mercurio.Messaging;

    using RabbitMQ.Client;

    /// <summary>
    /// The <see cref="IRabbitMqConnectionProvider" /> provides <see cref="IConnection" /> instance based on registered
    /// <see cref="Func{TResult}" /> for <see cref="ConnectionFactory" />
    /// </summary>
    public interface IRabbitMqConnectionProvider : ICanReleaseChannel
    {
        /// <summary>
        /// Gets an <see cref="IConnection" /> from a name-based registration of <see cref="ConnectionFactory" />
        /// </summary>
        /// <param name="connectionName">The name of the registered connection</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/></param>
        /// <returns>An awaitable <see cref="Task{TResult}" /> that provides access to an <see cref="IConnection" /></returns>
        Task<IConnection> GetConnectionAsync(string connectionName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously leases a channel from the pool or creates one if necessary.
        /// </summary>
        /// <param name="connectionName">The name of the registered connection that should be used to establish the connection</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/></param>
        /// <returns>A <see cref="ValueTask{TResult}"/> of <see cref="ChannelLease"/></returns>
        ValueTask<ChannelLease> LeaseChannelAsync(string connectionName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Register a new <see cref="ConnectionFactory" /> that could be resolved to provide an <see cref="IConnection" />
        /// </summary>
        /// <param name="name">The registration name that should be used</param>
        /// <param name="factory">A <see cref="Func{T,TResult}" /> that defines how the <see cref="ConnectionFactory" /> can be initialized</param>
        void RegisterFactory(string name, Func<IServiceProvider, Task<ConnectionFactory>> factory);
    }
}
