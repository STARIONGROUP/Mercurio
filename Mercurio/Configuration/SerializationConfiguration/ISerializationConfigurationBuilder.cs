// -------------------------------------------------------------------------------------------------
//  <copyright file="ISerializationConfigurationBuilder.cs" company="Starion Group S.A.">
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

namespace Mercurio.Configuration.SerializationConfiguration
{
    using Mercurio.Serializer;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Defines a fluent API for configuring serialization and deserialization services in the application.
    /// </summary>
    public interface ISerializationConfigurationBuilder
    {
        /// <summary>
        /// Registers the default JSON serialization service.
        /// </summary>
        ISerializationConfigurationBuilder UseDefaultJson();

        /// <summary>
        /// Registers a keyed JSON serialization/deserialization service.
        /// </summary>
        /// <typeparam name="TSerializationService">The service type implementing both serializer and deserializer interfaces.</typeparam>
        ISerializationConfigurationBuilder UseJson<TSerializationService>() where TSerializationService : class, IMessageSerializerService, IMessageDeserializerService;

        /// <summary>
        /// Registers a MessagePack serialization/deserialization service and optionally marks it as default.
        /// </summary>
        /// <typeparam name="TSerializationService">The service type implementing both serializer and deserializer interfaces.</typeparam>
        /// <param name="asDefault">Whether to use this format as the default.</param>
        ISerializationConfigurationBuilder UseMessagePack<TSerializationService>(bool asDefault = true) where TSerializationService : class, IMessageSerializerService, IMessageDeserializerService;
    }
}
