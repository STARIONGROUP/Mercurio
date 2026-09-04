// -------------------------------------------------------------------------------------------------
//  <copyright file="IMessagingBackgroundService.cs" company="Starion Group S.A.">
// 
//    Copyright 2025 - 2026 Starion Group S.A.
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
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to use the <see cref="DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        void PushMessage<TMessage>(TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes the specified <paramref name="messages" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="messages">The collection of <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to use the <see cref="DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        void PushMessages<TMessage>(IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously pushes the specified <paramref name="message" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />, bypassing the internal background queue so the caller can
        /// await completion and observe any publishing errors.
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /> that completes when the message has been published</returns>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to use the <see cref="DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        Task PushMessageAsync<TMessage>(TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously pushes the specified <paramref name="messages" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />, bypassing the internal background queue so the caller can
        /// await completion and observe any publishing errors.
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="messages">The collection of <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /> that completes when the messages have been published</returns>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to use the <see cref="DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        Task PushMessagesAsync<TMessage>(IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously pushes the specified <paramref name="message" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" /> and waits for the RabbitMQ server to acknowledge that it has taken
        /// responsibility for the message
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>
        /// An awaitable <see cref="Task{TResult}" /> that provides true once the RabbitMQ server has acknowledged the message, false
        /// if it has not
        /// </returns>
        /// <exception cref="ArgumentNullException">When the provided <typeparamref name="TMessage" /> or <paramref name="exchangeConfiguration" /> is null</exception>
        /// <remarks>
        /// This bypasses the internal background queue, so that the caller can await the acknowledgment. Invalid arguments still
        /// throw, any operational failure is logged and returns false. The acknowledgment only asserts that the server took
        /// responsibility for the message, not that any consumer received it.
        /// </remarks>
        Task<bool> PushMessageWithConfirmationAsync<TMessage>(TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously pushes the specified <paramref name="messages" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" /> and waits for the RabbitMQ server to acknowledge that it has taken
        /// responsibility for each message
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="messages">The collection of <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>
        /// An awaitable <see cref="Task{TResult}" /> that provides true once the RabbitMQ server has acknowledged all the messages,
        /// false as soon as one of them is not acknowledged
        /// </returns>
        /// <exception cref="ArgumentException">When the provided <paramref name="messages" /> collection is null</exception>
        /// <exception cref="ArgumentNullException">When the provided <paramref name="exchangeConfiguration" /> is null</exception>
        /// <remarks>
        /// This bypasses the internal background queue, so that the caller can await the acknowledgment. The messages are published
        /// one by one and the process stops at the first message that is not acknowledged. Since there is no transaction involved,
        /// the messages that have been acknowledged before the failure are already held by the server.
        /// </remarks>
        Task<bool> PushMessagesWithConfirmationAsync<TMessage>(IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);
    }
}
