// -------------------------------------------------------------------------------------------------
//  <copyright file="SupportedSerializationFormatExtensions.cs" company="Starion Group S.A.">
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

    /// <summary>
    /// Provides extension methods for converting between <see cref="SupportedSerializationFormat"/> and MIME content types.
    /// </summary>
    public static class SupportedSerializationFormatExtensions
    {
        /// <summary>
        /// A mapping between supported serialization formats and their corresponding MIME content types.
        /// </summary>
        private static readonly Dictionary<SupportedSerializationFormat, string> SupportedSerializationFormatContentTypeMapping = new()
        {
            { SupportedSerializationFormat.Json, "application/json" },
            { SupportedSerializationFormat.MessagePack, "application/x-msgpack" }
        };

        /// <summary>
        /// Converts a <see cref="SupportedSerializationFormat"/> to its corresponding MIME content type.
        /// </summary>
        /// <param name="format">The <see cref="SupportedSerializationFormat"/> value to convert.</param>
        /// <returns>The corresponding MIME content type as a <see cref="string"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the format is not supported.</exception>
        public static string ToContentType(this SupportedSerializationFormat format) =>
            SupportedSerializationFormatContentTypeMapping.TryGetValue(format, out var contentType)
                ? contentType
                : throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported serialization format");

        /// <summary>
        /// Converts a MIME content type string to its corresponding <see cref="SupportedSerializationFormat"/>.
        /// </summary>
        /// <param name="contentType">The MIME content type to convert.</param>
        /// <returns>The corresponding <see cref="SupportedSerializationFormat"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentType"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the content type is not supported.</exception>
        public static SupportedSerializationFormat ToSupportedSerializationFormat(this string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                throw new ArgumentNullException(nameof(contentType), "Content type cannot be null or empty");
            }

            var format = SupportedSerializationFormatContentTypeMapping
                .FirstOrDefault(kvp => kvp.Value.Equals(contentType, StringComparison.OrdinalIgnoreCase))
                .Key;

            return format != SupportedSerializationFormat.Unspecified
                ? format
                : throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unsupported content type for serialization format");
        }
    }
}
