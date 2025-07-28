// -------------------------------------------------------------------------------------------------
//  <copyright file="SerializationConfigurationBuilder.cs" company="Starion Group S.A.">
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
    using Mercurio.Configuration.SerializationConfiguration;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provides extension methods for configuring serialization services in the <see cref="IServiceCollection"/>.
    /// </summary>
    public static class SerializationConfigurationBuilderExtensions
    {
        /// <summary>
        /// Adds and configures the serialization to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the serialization to.</param>
        /// <param name="configure">An optional configuration delegate using <see cref="ISerializationConfigurationBuilder"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
        public static IServiceCollection WithSerialization(this IServiceCollection services, Action<ISerializationConfigurationBuilder> configure = null)
        {
            var builder = new SerializationConfigurationBuilder();
            configure?.Invoke(builder);
            return builder.Build(services);
        }
    }
}
