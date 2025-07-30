// -------------------------------------------------------------------------------------------------
//  <copyright file="IConnectionFactoryConfiguration.cs" company="Starion Group S.A.">
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

namespace Mercurio.Configuration.IConfiguration
{
    using System.Diagnostics;

    using Mercurio.Provider;

    using RabbitMQ.Client;

    /// <summary>
    /// The <see cref="IConnectionFactoryConfiguration" /> provides all required configuration material to be able to register
    /// <see cref="RabbitMQ.Client.ConnectionFactory" />
    /// into the <see cref="IRabbitMqConnectionProvider" />
    /// </summary>
    public interface IConnectionFactoryConfiguration
    {
        /// <summary>
        /// Gets the name of the <see cref="RabbitMQ.Client.ConnectionFactory" />
        /// </summary>
        string ConnectionName { get; }

        /// <summary>
        /// Gets the <see cref="Func{T, TResult}" /> that should be invoked to create a
        /// <see cref="RabbitMQ.Client.ConnectionFactory" /> asynchronously
        /// </summary>
        Func<IServiceProvider, Task<ConnectionFactory>> ConnectionFactory { get; }

        /// <summary>
        /// Gets or sets the default pool size of channel for the configured connection
        /// </summary>
        /// <remarks>
        /// Defaults to 32
        /// </remarks>
        int PoolSize { get; set; }

        /// <summary>
        /// Gets the name of the <see cref="ActivitySource" /> that should be use for traceabitility
        /// </summary>
        string ActivitySourceName { get; }
    }
}
