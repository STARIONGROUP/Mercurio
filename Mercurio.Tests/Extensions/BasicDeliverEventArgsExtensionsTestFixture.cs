// -------------------------------------------------------------------------------------------------
//  <copyright file="BasicDeliverEventArgsExtensionsTestFixture.cs" company="Starion Group S.A.">
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
    using System.Collections.Generic;
    using System.Text;

    using Mercurio.Extensions;

    using RabbitMQ.Client;

    [TestFixture]
    public class BasicDeliverEventArgsExtensionsTestFixture
    {
        [Test]
        public void VerifyNullArgumentsThrow()
        {
            using (Assert.EnterMultipleScope())
            {
                IReadOnlyBasicProperties properties = null;
                Assert.That(() => properties.TryReadHeader<string>("header", out _), Throws.ArgumentNullException);

                var basicProperties = new BasicProperties();
                Assert.That(() => basicProperties.TryReadHeader<string>(null, out _), Throws.ArgumentNullException);
            }
        }

        [Test]
        public void VerifyHeaderRead()
        {
            var properties = new BasicProperties
            {
                Headers = new Dictionary<string, object>
                {
                    { "string", "value" },
                    { "bytes", Encoding.UTF8.GetBytes("hello") }
                }
            };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(properties.TryReadHeader<string>("string", out var text), Is.True);
                Assert.That(text, Is.EqualTo("value"));

                Assert.That(properties.TryReadHeader<string>("bytes", out var fromBytes), Is.True);
                Assert.That(fromBytes, Is.EqualTo("hello"));

                Assert.That(properties.TryReadHeader<string>("missing", out _), Is.False);
            }
        }
    }
}
