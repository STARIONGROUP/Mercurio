// -------------------------------------------------------------------------------------------------
//  <copyright file="IMessageClientService.cs" company="Starion Group S.A.">
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

namespace Mercurio.Messaging
{
    using ErrorOr;

    using Mercurio.Model;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    /// <summary>
    /// The <see cref="IMessageClientService" /> is the base interface definition for any implemention of a RabbitMQ message client service.
    /// </summary>
    public interface IMessageClientService : IDisposable
    {
        /// <summary>
        /// Listens for messages of type <typeparamref name="TMessage" /> on the specified queue.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
        /// <returns>An observable sequence of messages.</returns>
        Task<IObservable<TMessage>> ListenAsync<TMessage>(string connectionName, IExchangeConfiguration exchangeConfiguration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a listener to the specified queue
        /// </summary>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="onReceiveAsync">The <see cref="AsyncEventHandler{TEvent}" /></param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <return>A <see cref="Task" /> of <see cref="IDisposable" /></return>
        Task<IDisposable> AddListenerAsync(string connectionName, IExchangeConfiguration exchangeConfiguration, AsyncEventHandler<BasicDeliverEventArgs> onReceiveAsync, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes the specified <paramref name="messages" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="messages">The collection of <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to use the <see cref="DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        Task PushAsync<TMessage>(string connectionName, IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes the specified <paramref name="message" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">A possible <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <exception cref="ArgumentNullException">When the provided <typeparamref name="TMessage" /> is null</exception>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to use the <see cref="DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        Task PushAsync<TMessage>(string connectionName, TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes the specified <paramref name="message" /> to the specified queue via the <paramref name="exchangeConfiguration" />
        /// and waits for the RabbitMQ server to acknowledge that it has taken responsibility for the message
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">A possible <see cref="CancellationToken" /></param>
        /// <returns>
        /// An awaitable <see cref="Task{TResult}" /> of <see cref="ErrorOr{TValue}" /> that provides <see cref="Success" /> once the
        /// RabbitMQ server has acknowledged the message, or the <see cref="Error" /> that describes the failure
        /// </returns>
        /// <exception cref="ArgumentNullException">When the provided <typeparamref name="TMessage" /> or <paramref name="exchangeConfiguration" /> is null</exception>
        /// <remarks>
        /// Contrary to <see cref="PushAsync{TMessage}(string,TMessage,IExchangeConfiguration,Action{BasicProperties},CancellationToken)" />,
        /// any publication failure is reported to the caller instead of being logged only. Invalid arguments still throw, only the
        /// operational failures are reported as an <see cref="Error" />, see <see cref="MessagingErrors" /> for the reported ones.
        /// Publisher confirmations throttle the amount of outstanding publications, so this is slower than a regular push. The
        /// acknowledgment only asserts that the server took responsibility for the message, not that any consumer received it.
        /// </remarks>
        Task<ErrorOr<Success>> PushWithConfirmationAsync<TMessage>(string connectionName, TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes the specified <paramref name="messages" /> to the specified queue via the <paramref name="exchangeConfiguration" />
        /// and waits for the RabbitMQ server to acknowledge that it has taken responsibility for each message
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="messages">The collection of <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>
        /// An awaitable <see cref="Task{TResult}" /> of <see cref="ErrorOr{TValue}" /> that provides <see cref="Success" /> once the
        /// RabbitMQ server has acknowledged all the messages, or the <see cref="Error" /> that describes the failure
        /// </returns>
        /// <exception cref="ArgumentException">When the provided <paramref name="messages" /> collection is null</exception>
        /// <exception cref="ArgumentNullException">When the provided <paramref name="exchangeConfiguration" /> is null</exception>
        /// <remarks>
        /// Invalid arguments still throw, only the operational failures are reported as an <see cref="Error" />, see
        /// <see cref="MessagingErrors" /> for the reported ones. The messages are published one by one and the process stops at the
        /// first message that is not acknowledged. Since there is no transaction involved, the messages that have been acknowledged
        /// before the failure are already held by the server, the reported <see cref="Error.Metadata" /> provides how many of them
        /// under the <see cref="MessagingErrors.PublishedCountMetadataKey" /> key.
        /// </remarks>
        Task<ErrorOr<Success>> PushWithConfirmationAsync<TMessage>(string connectionName, IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously leases a channel from the pool or creates one if necessary.
        /// </summary>
        /// <param name="connectionName">The name of the registered connection that should be used to establish the connection</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>A <see cref="ValueTask{TResult}" /> of <see cref="ChannelLease" /></returns>
        ValueTask<ChannelLease> LeaseChannelAsync(string connectionName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously leases a channel from the pool or creates one if necessary.
        /// </summary>
        /// <param name="connectionName">The name of the registered connection that should be used to establish the connection</param>
        /// <param name="publisherConfirmationsEnabled">
        /// Asserts that the leased channel has to support publisher confirmations, so that the RabbitMQ server acknowledges any
        /// published message
        /// </param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>A <see cref="ValueTask{TResult}" /> of <see cref="ChannelLease" /></returns>
        ValueTask<ChannelLease> LeaseChannelAsync(string connectionName, bool publisherConfirmationsEnabled, CancellationToken cancellationToken = default);
    }
}
