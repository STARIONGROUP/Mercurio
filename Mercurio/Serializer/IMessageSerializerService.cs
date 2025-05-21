// -------------------------------------------------------------------------------------------------
//  <copyright file="ISerializerService.cs" company="Starion Group S.A.">
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
    /// The <see cref="IMessageSerializerService" /> provides object serialization features
    /// </summary>
    public interface IMessageSerializerService
    {
        /// <summary>
        /// Serializes an <see cref="object"/> into a <see cref="byte"/> array asynchronously
        /// </summary>
        /// <param name="obj">The object to serialize</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task{TResult}"/> that results as the serialized object as <see cref="byte"/> array</returns>
        Task<byte[]> SerializeAsync(object obj, CancellationToken cancellationToken = default);
    }
}
