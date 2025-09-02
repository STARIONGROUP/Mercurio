// -------------------------------------------------------------------------------------------------
//  <copyright file="StreamExtensionsTestFixture.cs" company="Starion Group S.A.">
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

namespace Mercurio.Tests.Extensions
{
    using Mercurio.Extensions;

    using System.IO;

    [TestFixture]
    public class StreamExtensionsTestFixture
    {
        [Test]
        public void VerifyNullStreamThrows()
        {
            Stream stream = null;
            Assert.That(() => stream.ToReadOnlyMemory(), Throws.ArgumentNullException);
        }

        [Test]
        public void VerifyMemoryStreamConversion()
        {
            var data = new byte[] { 1, 2, 3 };
            using var stream = new MemoryStream(data);
            var memory = stream.ToReadOnlyMemory();

            Assert.That(memory.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public void VerifyNonMemoryStreamConversion()
        {
            var data = new byte[] { 4, 5, 6 };
            using var memoryStream = new MemoryStream(data);
            using var stream = new BufferedStream(memoryStream);
            var memory = stream.ToReadOnlyMemory();

            Assert.That(memory.ToArray(), Is.EqualTo(data));
        }
    }
}
