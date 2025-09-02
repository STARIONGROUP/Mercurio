// -------------------------------------------------------------------------------------------------
//  <copyright file="BasicDeliverEventArgsExtensions.cs" company="Starion Group S.A.">
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
    using System.Text;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    /// <summary>
    /// Extension method for the <see cref="BasicDeliverEventArgs" /> class
    /// </summary>
    public static class BasicDeliverEventArgsExtensions
    {
        /// <summary>
        /// Attempts to read a header from the RabbitMQ message properties and convert it to the specified type.
        /// </summary>
        /// <typeparam name="T">
        /// The expected type of the header value. Use <see cref="string" /> for text-based headers.
        /// </typeparam>
        /// <param name="properties">The message properties containing the headers.</param>
        /// <param name="header">The name of the header to read.</param>
        /// <param name="result">
        /// When this method returns, contains the value of the header converted to type <typeparamref name="T" /> if the conversion was successful; otherwise, the default value of
        /// <typeparamref name="T" />.
        /// </param>
        /// <returns>
        /// <c>true</c> if the header exists and can be converted to type <typeparamref name="T" />; otherwise, <c>false</c>.
        /// </returns>
        public static bool TryReadHeader<T>(this IReadOnlyBasicProperties properties, string header, out T result) where T : class
        {
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            result = null;

            if (properties.Headers?.TryGetValue(header, out var value) is true)
            {
                result = value switch
                {
                    T tValue => tValue,
                    byte[] byteArray when typeof(T) == typeof(string) => Encoding.UTF8.GetString(byteArray) as T,
                    _ => null
                };
            }

            return result != null;
        }
    }
}
