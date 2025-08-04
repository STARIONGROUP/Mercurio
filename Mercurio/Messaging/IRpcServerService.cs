// -------------------------------------------------------------------------------------------------
//  <copyright file="IRpcServerService.cs" company="Starion Group S.A.">
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

namespace Mercurio.Messaging
{
    using System.Diagnostics;

    using RabbitMQ.Client;

    /// <summary>
    /// The <see cref="IRpcServerService" /> provides Server implementation of the RPC protocol of RabbitMQ
    /// </summary>
    public interface IRpcServerService
    {
        /// <summary>
        /// Listens for request, execute the <paramref name="onReceiveAsync" /> action to process the request, and send the response back to the
        /// </summary>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="queueName">The name of the listening queue</param>
        /// <param name="onReceiveAsync">The action that should be executed when a request is received</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="activityName">
        /// Defines the name of an <see cref="Activity" /> that should be initialized when a message has been received, for traceability. In case of null or empty, no
        /// <see cref="Activity" /> is started
        /// </param>
        /// <param name="cancellationToken">A possible <see cref="CancellationToken" /></param>
        /// <typeparam name="TRequest">Any type that correspond to the kind of request to be processed</typeparam>
        /// <typeparam name="TResponse">Any type that correspond to the kind of response that has to be send back</typeparam>
        /// <returns>An awaitable <see cref="Task" /> with an <see cref="IDisposable" /></returns>
        /// <exception cref="ArgumentNullException">
        /// If the <paramref name="queueName" /> is not set or if the
        /// <paramref name="onReceiveAsync" /> is null
        /// </exception>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to set the <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        Task<IDisposable> ListenForRequestAsync<TRequest, TResponse>(string connectionName, string queueName, Func<TRequest, Task<TResponse>> onReceiveAsync, Action<BasicProperties> configureProperties = null, string activityName = "", CancellationToken cancellationToken = default);
    }
}
