// -------------------------------------------------------------------------------------------------
//  <copyright file="IMessagingBackgroundService.cs" company="Starion Group S.A.">
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

namespace Mercurio.Hosting
{
    using System.Diagnostics;

    using Mercurio.Messaging;
    using Mercurio.Model;

    using Microsoft.Extensions.Hosting;

    using RabbitMQ.Client;

    /// <summary>
    /// The <see cref="IMessagingBackgroundService"/> provides <see cref="IMessageClientService"/> interaction through a <see cref="BackgroundService" /> 
    /// </summary>
    public interface IMessagingBackgroundService
    {
        /// <summary>
        /// Gets or sets the name of the registered connection that has to be used to communicate with RabbitMQ
        /// </summary>
        string ConnectionName { get; }

        /// <summary>
        /// Pushes the specified <paramref name="message" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="activityName">
        /// Defines the name of an <see cref="Activity" /> that should be initialized before sending the message, for traceability.
        /// <see cref="Activity" /> information will be sent in the message header.
        /// In case of null or empty, no <see cref="Activity" /> is started
        /// </param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to use the <see cref="DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        void PushMessage<TMessage>(TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, string activityName = "", CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes the specified <paramref name="messages" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="messages">The collection of <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="activityName">
        /// Defines the name of an <see cref="Activity" /> that should be initialized before sending the message, for traceability.
        /// <see cref="Activity" /> information will be sent in the message header.
        /// In case of null or empty, no <see cref="Activity" /> is started
        /// </param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to use the <see cref="DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        void PushMessages<TMessage>(IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, string activityName = "", CancellationToken cancellationToken = default);
    }
}
