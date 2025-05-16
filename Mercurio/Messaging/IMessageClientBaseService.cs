// -------------------------------------------------------------------------------------------------
//  <copyright file="IMessageClientBaseService.cs" company="Starion Group S.A.">
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
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    using ExchangeType = Mercurio.Enumeration.ExchangeType;

    /// <summary>
    /// The <see cref="IMessageClientBaseService" /> is the base interface definition for any implemention of a RabbitMQ message client service.
    /// </summary>
    public interface IMessageClientBaseService : IDisposable
    {
        /// <summary>
        /// Listens for messages of type <typeparamref name="TMessage" /> on the specified queue.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="queueName">The name of the queue to listen on.</param>
        /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
        /// <param name="exchangeType">The exchange type. Default is <see cref="Enumeration.ExchangeType.Default" />.</param>
        /// <param name="exchangeName">The exchange name. Default is null.</param>
        /// <param name="routingKey">The routing key name. Default is null.</param>
        /// <returns>An observable sequence of messages.</returns>
        Task<IObservable<TMessage>> ListenAsync<TMessage>(string queueName, ExchangeType exchangeType = ExchangeType.Default, CancellationToken cancellationToken = default, string exchangeName = null, string routingKey = null) where TMessage : class;

        /// <summary>
        /// Listens for messages of type <typeparamref name="TMessage" /> on the specified queue.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="queue">The queue to listen on.</param>
        /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
        /// <param name="exchangeType">The exchange type. Default is <see cref="ExchangeType.Default" />.</param>
        /// <param name="exchangeName">The exchange name. Default is null.</param>
        /// <param name="routingKey">The routing key name. Default is null.</param>
        /// <returns>An observable sequence of messages.</returns>
        Task<IObservable<TMessage>> ListenAsync<TMessage>(Enum queue, ExchangeType exchangeType = ExchangeType.Default, CancellationToken cancellationToken = default, string exchangeName = null, string routingKey = null) where TMessage : class;

        /// <summary>
        /// Adds a listener to the specified queue
        /// </summary>
        /// <param name="queueName">The <see cref="string" /> queue name</param>
        /// <param name="onReceiveAsync">The <see cref="AsyncEventHandler{TEvent}" /></param>
        /// <param name="exchangeType">
        /// The string exchange type It can be any value from <see cref="ExchangeType" />, default value is
        /// <see cref="ExchangeType.Default" />
        /// </param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <return>A <see cref="Task" /> of <see cref="IDisposable" /></return>
        Task<IDisposable> AddListenerAsync(string queueName, AsyncEventHandler<BasicDeliverEventArgs> onReceiveAsync, ExchangeType exchangeType = ExchangeType.Default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes the specified <paramref name="messages" /> to the specified <paramref name="messageQueue" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="messageQueue">
        /// The <see cref="string" /> queue name on which to send to the <paramref name="messages" />
        /// </param>
        /// <param name="messages">The collection of <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeType">
        /// The string exchange type It can be any value from <see cref="ExchangeType" />, default value is
        /// <see cref="ExchangeType.Default" />
        /// </param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        Task PushAsync<TMessage>(string messageQueue, IEnumerable<TMessage> messages, ExchangeType exchangeType = ExchangeType.Default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes the specified <paramref name="message" /> to the specified <paramref name="messageQueue" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="messageQueue">
        /// The <see cref="string" /> queue name on which to send to the <paramref name="message" />
        /// </param>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeType">
        /// The string exchange type It can be any value from <see cref="ExchangeType" />, default value is
        /// <see cref="ExchangeType.Default" />
        /// </param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">A possible <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <exception cref="ArgumentNullException">When the provided <typeparamref name="TMessage" /> is null</exception>
        Task PushAsync<TMessage>(string messageQueue, TMessage message, ExchangeType exchangeType = ExchangeType.Default, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pushes the specified <paramref name="message" /> to the specified <paramref name="messageQueue" />
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="messageQueue">The <see cref="Enum" /> queue on which to send to the <paramref name="message" /></param>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeType">
        /// The string exchange type It can be any value from <see cref="ExchangeType" />, default value is
        /// <see cref="ExchangeType.Default" />
        /// </param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">A possible <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <exception cref="ArgumentNullException">When the provided <typeparamref name="TMessage" /> is null</exception>
        Task PushAsync<TMessage>(Enum messageQueue, TMessage message, ExchangeType exchangeType = ExchangeType.Default, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);
    }
}
