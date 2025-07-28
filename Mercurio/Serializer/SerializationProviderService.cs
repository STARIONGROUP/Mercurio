// -------------------------------------------------------------------------------------------------
//  <copyright file="SerializationProviderService.cs" company="Starion Group S.A.">
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

namespace Mercurio.Serializer
{
    using Mercurio.Configuration.SerializationConfiguration;

    /// <summary>
    /// The <see cref="SerializationProviderService"/> Provides access to <see cref="IMessageSerializerService"/> and <see cref="IMessageDeserializerService"/>
    /// instances based on <see cref="SupportedSerializationFormat"/>.
    /// </summary>
    internal sealed class SerializationProviderService : ISerializationProviderService
    {
        /// <summary>
        /// Gets the Dictionary of <see cref="string"/> to <see cref="IMessageSerializerService"/> instances
        /// </summary>
        private readonly IDictionary<string, IMessageSerializerService> serializers;
        
        /// <summary>
        /// Gets the Dictionary of <see cref="string"/> to <see cref="IMessageDeserializerService"/> instances
        /// </summary>
        private readonly IDictionary<string, IMessageDeserializerService> deserializers;

        /// <summary>
        /// The <see cref="SerializationProviderService"/> Provides access to <see cref="IMessageSerializerService"/> and <see cref="IMessageDeserializerService"/>
        /// instances based on <see cref="SupportedSerializationFormat"/>.
        /// </summary>
        /// <param name="serializers">Dictionary of <see cref="string"/> to <see cref="IMessageSerializerService"/> instances.</param>
        /// <param name="deserializers">Dictionary of <see cref="string"/> to <see cref="IMessageDeserializerService"/> instances.</param>
        /// <param name="defaultFormat">The default format</param>
        public SerializationProviderService(IDictionary<string, IMessageSerializerService> serializers, IDictionary<string, IMessageDeserializerService> deserializers, string defaultFormat = SupportedSerializationFormat.JsonFormat)
        {
            this.serializers = serializers;
            this.deserializers = deserializers;
            this.DefaultFormat = defaultFormat;
        }

        /// <summary>
        /// Gets the dictionary of serializers registered for different <see cref="SupportedSerializationFormat"/>s.
        /// </summary>
        public string DefaultFormat { get; }

        /// <summary>
        /// Resolves a serializer for the specified <paramref name="format"/>.
        /// </summary>
        /// <param name="format">The string value for which to retrieve the serializer. Defaults to <see cref="SupportedSerializationFormat.Unspecified"/>.</param>
        /// <returns>The <see cref="IMessageSerializerService"/> instance registered for the given format.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no serializer is registered for the specified format.</exception>
        public IMessageSerializerService ResolveSerializer(string format = SupportedSerializationFormat.Unspecified)
        {
            if (this.serializers.TryGetValue(format, out var serializer))
            {
                return serializer;
            }

            throw new InvalidOperationException($"Cannot resolve a serializer for the specified format {format}");
        }

        /// <summary>
        /// Resolves a deserializer for the specified <paramref name="format"/>.
        /// </summary>
        /// <param name="format">The string value for which to retrieve the serializer. Defaults to <see cref="SupportedSerializationFormat.Unspecified"/>.</param>
        /// <returns>The <see cref="IMessageDeserializerService"/> instance registered for the given format.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no deserializer is registered for the specified format.</exception>
        public IMessageDeserializerService ResolveDeserializer(string format = SupportedSerializationFormat.Unspecified)
        {
            if (this.deserializers.TryGetValue(format, out var deserializer))
            {
                return deserializer;
            }

            throw new InvalidOperationException($"Cannot resolve a deserializer for the specified format {format}");
        }
    }
}
