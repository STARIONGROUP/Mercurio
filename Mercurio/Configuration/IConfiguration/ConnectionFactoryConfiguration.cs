// -------------------------------------------------------------------------------------------------
//  <copyright file="ConnectionFactoryConfiguration.cs" company="Starion Group S.A.">
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
    using Mercurio.Provider;

    using RabbitMQ.Client;

    /// <summary>
    /// The <see cref="ConnectionFactoryConfiguration" /> provides all required configuration material to be able to register <see cref="RabbitMQ.Client.ConnectionFactory"/>
    /// into the <see cref="IRabbitMqConnectionProvider"/>
    /// </summary>
    internal class ConnectionFactoryConfiguration : IConnectionFactoryConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionFactoryConfiguration"></see> class.
        /// </summary>
        /// <param name="connectionName">The name of the <see cref="RabbitMQ.Client.ConnectionFactory" /> to register</param>
        /// <param name="connectionFactoryAsync">The <see cref="Func{T, TResult}"/> that should be invoked to create a <see cref="RabbitMQ.Client.ConnectionFactory"/> asynchronously</param>
        public ConnectionFactoryConfiguration(string connectionName, Func<IServiceProvider, Task<ConnectionFactory>> connectionFactoryAsync)
        {
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentNullException(nameof(connectionName), "A connection name must be provided.");
            }

            this.ConnectionName = connectionName;
            this.ConnectionFactory = connectionFactoryAsync ?? throw new ArgumentNullException(nameof(connectionFactoryAsync), "A connection factory must be provided.");
        }

        /// <summary>
        /// Gets the name of the <see cref="RabbitMQ.Client.ConnectionFactory" /> 
        /// </summary>
        public string ConnectionName { get; }

        /// <summary>
        /// Gets the <see cref="Func{T, TResult}"/> that should be invoked to create a <see cref="RabbitMQ.Client.ConnectionFactory"/> asynchronously
        /// </summary>
        public Func<IServiceProvider, Task<ConnectionFactory>> ConnectionFactory { get; }

        /// <summary>
        /// Gets or sets the default pool size of channel for the configured connection
        /// </summary>
        /// <remarks>
        /// Defaults to 32
        /// </remarks>
        public int PoolSize { get; set; } = 32;
    }
}
