// -------------------------------------------------------------------------------------------------
//  <copyright file="IMessageDeserializerService.cs" company="Starion Group S.A.">
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
    /// <summary>
    /// The <see cref="IMessageSerializerService" /> provides object deserialization features
    /// </summary>
    public interface IMessageDeserializerService
    {
        /// <summary>
        /// Deserializes the content of a RabbitMQ into a <typeparamref name="TMessage" />
        /// </summary>
        /// <param name="content">The <see cref="Stream"/> that contains serialized message sent via RabbitMQ</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <typeparam name="TMessage">Any object</typeparam>
        /// <returns>An awaitable <see cref="Task{TResult}" /> with the deserialized <typeparamref name="TMessage" /></returns>
        Task<TMessage> DeserializeAsync<TMessage>(Stream content, CancellationToken cancellationToken = default);
    }
}
