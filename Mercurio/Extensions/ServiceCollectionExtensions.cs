// -------------------------------------------------------------------------------------------------
//  <copyright file="ServiceCollectionExtensions.cs" company="Starion Group S.A.">
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

namespace Mercurio.Extensions
{
    using System.Diagnostics;

    using Mercurio.Configuration.IConfiguration;
    using Mercurio.Provider;

    using Microsoft.Extensions.DependencyInjection;

    using RabbitMQ.Client;

    /// <summary>
    /// Provides <see cref="IServiceCollection" /> extension methods
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the <see cref="IRabbitMqConnectionProvider" /> implementation into an <see cref="IServiceCollection" />
        /// </summary>
        /// <param name="serviceCollection">
        /// The <see cref="IServiceCollection" /> that will be used to build the
        /// <see cref="IServiceProvider" />
        /// </param>
        /// <returns>The <see cref="IServiceCollection" /></returns>
        public static IServiceCollection AddRabbitMqConnectionProvider(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
            return serviceCollection;
        }

        /// <summary>
        /// Registers a new <see cref="ConnectionFactory" /> that have to be initialize asynchronously
        /// </summary>
        /// <param name="serviceCollection">
        /// The <see cref="IServiceCollection" /> that will be used to build the
        /// <see cref="IServiceProvider" />
        /// </param>
        /// <param name="name">The name of the <see cref="ConnectionFactory" /> that will be used to create connection later</param>
        /// <param name="factory">The <see cref="Func{T,TResult}" /> that provide <see cref="ConnectionFactory" /> asynchronously initialization behavior</param>
        /// <param name="activitySource">Provides an optional <see cref="ActivitySource" /> to be used for traceability. In case of null, no traceability will be sent</param>
        /// <returns>The <see cref="IServiceCollection" /></returns>
        public static IServiceCollection WithRabbitMqConnectionFactoryAsync(this IServiceCollection serviceCollection, string name, Func<IServiceProvider, Task<ConnectionFactory>> factory, ActivitySource activitySource = null)
        {
            serviceCollection.AddSingleton<IConnectionFactoryConfiguration>(new ConnectionFactoryConfiguration(name, factory, activitySource));
            return serviceCollection;
        }

        /// <summary>
        /// Registers a new <see cref="ConnectionFactory" /> that have to be initialize
        /// </summary>
        /// <param name="serviceCollection">
        /// The <see cref="IServiceCollection" /> that will be used to build the
        /// <see cref="IServiceProvider" />
        /// </param>
        /// <param name="name">The name of the <see cref="ConnectionFactory" /> that will be used to create connection later</param>
        /// <param name="factory">The <see cref="Func{T,TResult}" /> that provide <see cref="ConnectionFactory" /> initialization behavior</param>
        /// <param name="activitySource">Provides an optional <see cref="ActivitySource" /> to be used for traceability. In case of null, no traceability will be sent</param>
        /// ///
        /// <returns>The <see cref="IServiceCollection" /></returns>
        public static IServiceCollection WithRabbitMqConnectionFactory(this IServiceCollection serviceCollection, string name, Func<IServiceProvider, ConnectionFactory> factory, ActivitySource activitySource = null)
        {
            serviceCollection.AddSingleton<IConnectionFactoryConfiguration>(new ConnectionFactoryConfiguration(name, sp => Task.FromResult(factory(sp)), activitySource));
            return serviceCollection;
        }
    }
}
