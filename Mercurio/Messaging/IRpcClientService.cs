// -------------------------------------------------------------------------------------------------
//  <copyright file="IRpcClientService.cs" company="Starion Group S.A.">
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
    /// The <see cref="IRpcClientService{TResponse}" /> provides client implementation of the RPC protocol of RabbitMQ
    /// </summary>
    /// <typeparam name="TResponse">Any type that correspond to the kind of response that the server should reply</typeparam>
    public interface IRpcClientService<TResponse> : IDisposable
    {
        /// <summary>
        /// Sends a request message to a RPC server and awaits for the reply from the server
        /// </summary>
        /// <param name="connectionName">The name of the connection to use</param>
        /// <param name="rpcServerQueueName">The name of the queue that is used by the server to listen after request</param>
        /// <param name="request">The <typeparamref name="TRequest" /> that should be sent to the server</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="activityName">
        /// Defines the name of an <see cref="Activity" /> that should be initialized when the server response has been received, for traceability. In case of null or empty, no
        /// <see cref="Activity" /> is started
        /// </param>
        /// <param name="cancellationToken">A possible <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task{T}" /> with the observable that will track the server response</returns>
        /// <typeparam name="TRequest">Any type that correspond to the kind of request to be sent to the server</typeparam>
        /// <exception cref="ArgumentNullException">
        /// If any of <paramref name="connectionName" />,
        /// <paramref name="rpcServerQueueName" /> or <paramref name="request" /> is not set
        /// </exception>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to set the <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        Task<IObservable<TResponse>> SendRequestAsync<TRequest>(string connectionName, string rpcServerQueueName, TRequest request, Action<BasicProperties> configureProperties = null, string activityName = "", CancellationToken cancellationToken = default);
    }
}
