// -------------------------------------------------------------------------------------------------
//  <copyright file="ObservableExtensionsTestFixture.cs" company="Starion Group S.A.">
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
    using System;
    using System.Collections.Generic;
    using System.Reactive.Subjects;
    using System.Threading.Tasks;

    using Mercurio.Extensions;

    [TestFixture]
    public class ObservableExtensionsTestFixture
    {
        [Test]
        public void VerifyNullArgumentsThrow()
        {
            using (Assert.EnterMultipleScope())
            {
                IObservable<int> source = null;
                Assert.That(() => source.SubscribeAsync(_ => Task.CompletedTask), Throws.ArgumentNullException);

                var observable = new Subject<int>();
                Assert.That(() => observable.SubscribeAsync(null), Throws.ArgumentNullException);
            }
        }

        [Test]
        public async Task VerifySubscription()
        {
            var results = new List<int>();
            var completed = false;
            var subject = new Subject<int>();

            using var subscription = subject.SubscribeAsync(x =>
            {
                results.Add(x);
                return Task.CompletedTask;
            }, onCompleted: () => completed = true);

            subject.OnNext(1);
            subject.OnNext(2);
            subject.OnCompleted();

            await Task.Delay(10);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(results, Is.EqualTo(new[] { 1, 2 }));
                Assert.That(completed, Is.True);
            };
        }

        [Test]
        public async Task VerifyErrorIsHandled()
        {
            var subject = new Subject<int>();
            Exception captured = null;

            using var subscription = subject.SubscribeAsync(_ => Task.FromException(new InvalidOperationException("boom")), ex => captured = ex);

            subject.OnNext(1);

            await Task.Delay(10);

            Assert.That(captured, Is.TypeOf<InvalidOperationException>());
        }
    }
}
