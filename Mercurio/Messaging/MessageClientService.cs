// -------------------------------------------------------------------------------------------------
//  <copyright file="MessageClientService.cs" company="Starion Group S.A.">
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
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Text;

    using CommunityToolkit.HighPerformance;

    using Mercurio.Configuration.IConfiguration;
    using Mercurio.Extensions;
    using Mercurio.Model;
    using Mercurio.Provider;
    using Mercurio.Serializer;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    /// <summary>
    /// The <see cref="MessageClientService" /> is the concrete implementation of the <see cref="MessageClientService" />
    /// <remarks>
    /// To make sure to have a smoothly working messaging client, it is recommend to use it within a
    /// <a href='https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services'>Background Service</a>
    /// </remarks>
    /// </summary>
    public class MessageClientService : MessageClientBaseService
    {
        /// <summary>
        /// Gets the injected <see cref="ISerializationProviderService" /> that will provide message serialization and deserialization capabilities
        /// </summary>
        protected readonly ISerializationProviderService serializationProviderService;

        /// <summary>
        /// Initializes a new instance of <see cref="MessageClientService" />
        /// </summary>
        /// <param name="connectionProvider">
        /// The injected <see cref="IRabbitMqConnectionProvider" /> <see cref="IConnection" />
        /// access based on registered <see cref="ConnectionFactory" />
        /// </param>
        /// <param name="serializationProviderService">The injected <see cref="ISerializationProviderService" /> that will provide message serialization and deserialization capabilities</param>
        /// <param name="logger">The injected <see cref="ILogger{TCategoryName}" /></param>
        public MessageClientService(IRabbitMqConnectionProvider connectionProvider, ISerializationProviderService serializationProviderService,
            ILogger<MessageClientService> logger) : base(connectionProvider, logger)
        {
            this.serializationProviderService = serializationProviderService;
        }

        /// <summary>
        /// Listens for messages of type <typeparamref name="TMessage" /> on the specified queue.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
        /// <returns>An observable sequence of messages.</returns>
        public override Task<IObservable<TMessage>> ListenAsync<TMessage>(string connectionName, IExchangeConfiguration exchangeConfiguration, CancellationToken cancellationToken = default)
        {
            if (exchangeConfiguration == null)
            {
                throw new ArgumentNullException(nameof(exchangeConfiguration), "The exchange configuration cannot be null");
            }

            return this.ListenInternalAsync<TMessage>(connectionName, exchangeConfiguration, cancellationToken);
        }

        /// <summary>
        /// Adds a listener to the specified queue
        /// </summary>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="onReceiveAsync">The <see cref="AsyncEventHandler{TEvent}" /></param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <return>A <see cref="Task" /> of <see cref="IDisposable" /></return>
        public override Task<IDisposable> AddListenerAsync(string connectionName, IExchangeConfiguration exchangeConfiguration, AsyncEventHandler<BasicDeliverEventArgs> onReceiveAsync, CancellationToken cancellationToken = default)
        {
            if (exchangeConfiguration == null)
            {
                throw new ArgumentNullException(nameof(exchangeConfiguration), "The exchange configuration cannot be null");
            }

            return this.AddListenerInternalAsync(connectionName, exchangeConfiguration, onReceiveAsync, cancellationToken);
        }

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
        public override Task PushAsync<TMessage>(string connectionName, IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
        {
            if (messages == null)
            {
                throw new ArgumentException("The messages collection cannot be null", nameof(messages));
            }

            return this.PushInternalAsync(connectionName, messages, exchangeConfiguration, configureProperties, cancellationToken);
        }

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
        public override Task PushAsync<TMessage>(string connectionName, TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
        {
            if (Equals(message, default(TMessage)))
            {
                throw new ArgumentNullException(nameof(message), "The message to be sent can not be null");
            }

            if (exchangeConfiguration == null)
            {
                throw new ArgumentNullException(nameof(exchangeConfiguration), "The exchange configuration cannot be null");
            }

            return this.PushInternalAsync(connectionName, message, exchangeConfiguration, configureProperties, cancellationToken);
        }

        /// <summary>
        /// Declares specific action that could be performed while a message has been received (e.g. to record new circuit Activity, for traceability).
        /// </summary>
        /// <param name="message">The <typeparamref name="TMessage" /></param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <typeparam name="TMessage">Any object</typeparam>
        protected virtual void OnMessageReceive<TMessage>(TMessage message, IExchangeConfiguration exchangeConfiguration)
        {
        }

        /// <summary>
        /// Declares specific action that could be performed before pushing a new message (e.g. to record new circuit Activity, for traceability).
        /// </summary>
        /// <param name="message">The <typeparamref name="TMessage" /></param>
        /// <param name="properties">
        /// The <see cref="BasicProperties" /> that will be used to sent the <paramref name="message" />
        /// </param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <typeparam name="TMessage">Any object</typeparam>
        protected virtual void OnPrePush<TMessage>(TMessage message, BasicProperties properties, IExchangeConfiguration exchangeConfiguration)
        {
        }

        /// <summary>
        /// Listens for messages of type <typeparamref name="TMessage" /> on the specified queue.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
        /// <returns>An observable sequence of messages.</returns>
        private async Task<IObservable<TMessage>> ListenInternalAsync<TMessage>(string connectionName, IExchangeConfiguration exchangeConfiguration, CancellationToken cancellationToken)
            where TMessage : class
        {
            var channelLease = await this.LeaseChannelAsync(connectionName, cancellationToken);

            return Observable.Create<TMessage>(async observer =>
            {
                var disposables = await this.InitializeListenerAsync(observer, channelLease.Channel, exchangeConfiguration, cancellationToken);

                return Disposable.Create(() =>
                {
                    disposables.Dispose();
                    channelLease.Dispose();
                });
            });
        }

        /// <summary>
        /// Adds a listener to the specified queue
        /// </summary>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="onReceiveAsync">The <see cref="AsyncEventHandler{TEvent}" /></param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <return>A <see cref="Task" /> of <see cref="IDisposable" /></return>
        private async Task<IDisposable> AddListenerInternalAsync(string connectionName, IExchangeConfiguration exchangeConfiguration, AsyncEventHandler<BasicDeliverEventArgs> onReceiveAsync, CancellationToken cancellationToken)
        {
            AsyncEventingBasicConsumer consumer = null;
            ChannelLease channelLease = default;

            try
            {
                channelLease = await this.LeaseChannelAsync(connectionName, cancellationToken);

                await exchangeConfiguration.EnsureQueueAndExchangeAreDeclaredAsync(channelLease.Channel, false);

                consumer = new AsyncEventingBasicConsumer(channelLease.Channel);
                consumer.ReceivedAsync += onReceiveAsync;
                consumer.ReceivedAsync += OnMessageReceiveAsync;

                await channelLease.Channel.BasicConsumeAsync(exchangeConfiguration.QueueName, true, consumer, cancellationToken);
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception exception)
            {
                this.Logger.LogError(exception, "Error while adding a listener to the {QueueName}", exchangeConfiguration.QueueName);
            }

            return Disposable.Create(() =>
            {
                if (consumer != null)
                {
                    consumer.ReceivedAsync -= onReceiveAsync;
                    consumer.ReceivedAsync -= OnMessageReceiveAsync;
                }

                channelLease.Dispose();
            });

            Task OnMessageReceiveAsync(object _, BasicDeliverEventArgs m)
            {
                return Task.Run(() => this.OnMessageReceive(m, exchangeConfiguration), cancellationToken);
            }
        }

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
        private async Task PushInternalAsync<TMessage>(string connectionName, IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties, CancellationToken cancellationToken)
        {
            foreach (var message in messages)
            {
                await this.PushAsync(connectionName, message, exchangeConfiguration, configureProperties, cancellationToken);
            }
        }

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
        private async Task PushInternalAsync<TMessage>(string connectionName, TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties, CancellationToken cancellationToken)
        {
            try
            {
                await using var channelLease = await this.LeaseChannelAsync(connectionName, cancellationToken);
                await exchangeConfiguration.EnsureQueueAndExchangeAreDeclaredAsync(channelLease.Channel, true);

                var properties = new BasicProperties
                {
                    Type = typeof(TMessage).Name,
                    DeliveryMode = DeliveryModes.Persistent,
                    ContentType = this.serializationProviderService.DefaultFormat.ToContentType(),
                };

                configureProperties?.Invoke(properties);
                this.OnPrePush(message, properties, exchangeConfiguration);

                var stream = await this.serializationProviderService.ResolveSerializer().SerializeAsync(message, cancellationToken);

                var routingKey = !string.IsNullOrEmpty(exchangeConfiguration.RoutingKey) || !string.IsNullOrEmpty(exchangeConfiguration.ExchangeName)
                    ? exchangeConfiguration.RoutingKey
                    : exchangeConfiguration.QueueName;

                var body = stream.ToReadOnlyMemory();
                
                await channelLease.Channel.BasicPublishAsync(exchangeConfiguration.PushExchangeName,
                    routingKey, false, properties,
                    body, cancellationToken);

                this.Logger.LogDebug("Message Body {Body}", Encoding.UTF8.GetString(body.ToArray()));
                this.Logger.LogInformation("Message {MessageName} sent to {MessageQueue}", typeof(TMessage).Name, exchangeConfiguration.QueueName);
            }
            catch (Exception exception)
            {
                this.Logger.LogError(exception, "The message {MessageName} could not be queued to {MessageQueue} reason : {Exception}", typeof(TMessage).Name, exchangeConfiguration.QueueName, exception.Message);
            }
        }

        /// <summary>
        /// Initializes the listener by setting up event handlers and consuming messages.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="observer">The observer to push messages to.</param>
        /// <param name="channel">The RabbitMQ channel.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>A disposable to clean up resources.</returns>
        private async Task<IDisposable> InitializeListenerAsync<TMessage>(IObserver<TMessage> observer, IChannel channel, IExchangeConfiguration exchangeConfiguration,
            CancellationToken cancellationToken = default) where TMessage : class
        {
            AsyncEventingBasicConsumer consumer = null;

            try
            {
                channel.CallbackExceptionAsync += ChannelOnCallbackException;

                consumer = new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += ConsumerOnReceivedAsync;
                consumer.ShutdownAsync += ConsumerOnShutdownAsync;
                await exchangeConfiguration.EnsureQueueAndExchangeAreDeclaredAsync(channel, false);

                await channel.BasicConsumeAsync(exchangeConfiguration.QueueName, true, consumer, cancellationToken);
            }
            catch (Exception exception)
            {
                observer.OnError(exception);
            }

            return Disposable.Create(() =>
            {
                channel.CallbackExceptionAsync -= ChannelOnCallbackException;

                if (consumer == null)
                {
                    return;
                }

                consumer.ReceivedAsync -= ConsumerOnReceivedAsync;
                consumer.ShutdownAsync -= ConsumerOnShutdownAsync;
            });

            async Task ConsumerOnReceivedAsync(object o, BasicDeliverEventArgs message)
            {
                this.OnMessageReceive(message, exchangeConfiguration);
                using var stream = message.Body.AsStream();
                var content = await this.serializationProviderService.ResolveDeserializer(message.BasicProperties.ContentType.ToSupportedSerializationFormat()).DeserializeAsync<TMessage>(stream, cancellationToken);
                observer.OnNext(content);
                await Task.CompletedTask;
            }

            Task ConsumerOnShutdownAsync(object _, ShutdownEventArgs arguments)
            {
                observer.OnError(new OperationCanceledException($"The channel has shutdown [Reply: {arguments.ReplyText}, AMQPcode: {arguments.ReplyCode}]"));
                return Task.CompletedTask;
            }

            Task ChannelOnCallbackException(object _, CallbackExceptionEventArgs arguments)
            {
                observer.OnError(arguments.Exception);
                return Task.CompletedTask;
            }
        }
    }
}
