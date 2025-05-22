// -------------------------------------------------------------------------------------------------
//  <copyright file="StreamExtensions.cs" company="Starion Group S.A.">
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
    /// <summary>
    /// Extension methods for the <see cref="Stream" /> class
    /// </summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// Converts the content of the provided <see cref="Stream" /> into a <see cref="ReadOnlyMemory{T}" /> of byte
        /// </summary>
        /// <param name="stream">The <see cref="Stream" /></param>
        /// <returns>
        /// The <see cref="ReadOnlyMemory{T}" /> of byte that contains the content of the <paramref name="stream" />
        /// </returns>
        /// <exception cref="ArgumentNullException">If the provided <paramref name="stream" /> is null</exception>
        public static ReadOnlyMemory<byte> ToReadOnlyMemory(this Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "The stream cannot be null");
            }

            stream.Position = 0;

            if (stream is MemoryStream memoryStream)
            {
                return new ReadOnlyMemory<byte>(memoryStream.ToArray());
            }

            using var temporaryStream = new MemoryStream();
            stream.CopyTo(temporaryStream);
            temporaryStream.Position = 0;
            return temporaryStream.ToReadOnlyMemory();
        }
    }
}
