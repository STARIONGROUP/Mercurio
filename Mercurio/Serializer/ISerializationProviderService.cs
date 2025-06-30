// -------------------------------------------------------------------------------------------------
//  <copyright file="ISerializationProviderService.cs" company="Starion Group S.A.">
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
    /// The <see cref="ISerializationProviderService"/> provides access to serialization and deserialization services.
    /// </summary>
    public interface ISerializationProviderService
    {
        /// <summary>
        /// Gets the dictionary of serializers registered for different <see cref="SupportedSerializationFormat"/>s.
        /// </summary>
        SupportedSerializationFormat DefaultFormat { get; }

        /// <summary>
        /// Resolves a serializer for the specified <paramref name="format"/>.
        /// </summary>
        /// <param name="format">The <see cref="SupportedSerializationFormat"/> for which to retrieve the serializer. Defaults to <see cref="SupportedSerializationFormat.Unspecified"/>.</param>
        /// <returns>The <see cref="IMessageSerializerService"/> instance registered for the given format.</returns>
        IMessageSerializerService ResolveSerializer(SupportedSerializationFormat format = SupportedSerializationFormat.Unspecified);

        /// <summary>
        /// Resolves a deserializer for the specified <paramref name="format"/>.
        /// </summary>
        /// <param name="format">The <see cref="SupportedSerializationFormat"/> for which to retrieve the deserializer. Defaults to <see cref="SupportedSerializationFormat.Unspecified"/>.</param>
        /// <returns>The <see cref="IMessageDeserializerService"/> instance registered for the given format.</returns>
        IMessageDeserializerService ResolveDeserializer(SupportedSerializationFormat format = SupportedSerializationFormat.Unspecified);
    }
}
