// -------------------------------------------------------------------------------------------------
//  <copyright file="JsonMessageSerializerService.cs" company="Starion Group S.A.">
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
    using System.Text.Json;

    using Mercurio.Messaging;

    /// <summary>
    /// The <see cref="JsonMessageSerializerService" /> provides standard JSON (de)serialization to be used by the <see cref="MessageClientService" />
    /// </summary>
    public class JsonMessageSerializerService: IMessageSerializerService, IMessageDeserializerService
    {
        /// <summary>
        /// Gets the default <see cref="JsonSerializerOptions" /> used during serialization
        /// </summary>
        private static readonly JsonSerializerOptions JsonWriterOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Serializes an <see cref="object"/> into a <see cref="byte"/> array asynchronously
        /// </summary>
        /// <param name="obj">The object to serialize</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task{TResult}"/> that results as the serialized object as <see cref="byte"/> array</returns>
        public async Task<Stream> SerializeAsync(object obj, CancellationToken cancellationToken = default)
        {
            var memoryStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(memoryStream, obj, JsonWriterOptions, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }

        /// <summary>
        /// Deserializes the content of a RabbitMQ into a <typeparamref name="TMessage" />
        /// </summary>
        /// <param name="content">The <see cref="Stream"/> that contains serialized message sent via RabbitMQ</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <typeparam name="TMessage">Any object</typeparam>
        /// <returns>An awaitable <see cref="Task{TResult}" /> with the deserialized <typeparamref name="TMessage" /></returns>
        public async Task<TMessage> DeserializeAsync<TMessage>(Stream content, CancellationToken cancellationToken = default)
        {
            return await JsonSerializer.DeserializeAsync<TMessage>(content, JsonWriterOptions, cancellationToken);
        }
    }
}
