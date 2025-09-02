// -------------------------------------------------------------------------------------------------
//  <copyright file="ObservableExtensions.cs" company="Starion Group S.A.">
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
    using System.Reactive.Linq;

    /// <summary>
    /// Extension class for the <see cref="IObservable{T}" />
    /// </summary>
    public static class ObservableExtensions
    {
        /// <summary>
        /// Subscribe to an <see cref="IObservable{T}" /> with async capabilities
        /// </summary>
        /// <typeparam name="T">An object</typeparam>
        /// <param name="source">The source <see cref="IObservable{T}" /></param>
        /// <param name="onNextAsync">The <see cref="Func{TResult,UResult}" /> to call</param>
        /// <param name="onError">An optional <see cref="Action{T}"/> to handle exception</param>
        /// <param name="onCompleted">An optional <see cref="Action"/> to handle completed action</param>
        /// <returns>The created <see cref="IDisposable" /></returns>
        public static IDisposable SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNextAsync, Action<Exception> onError = null,
            Action onCompleted = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (onNextAsync == null)
            {
                throw new ArgumentNullException(nameof(onNextAsync));
            }

            return source.Select(x => Observable.FromAsync(() => onNextAsync(x))).Concat()
                .Subscribe(_ => { }, onError ?? (_ => { }), onCompleted ?? (() => { }));
        }
    }
}
    