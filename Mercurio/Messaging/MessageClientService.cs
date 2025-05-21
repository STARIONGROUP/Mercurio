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

    using Mercurio.Configuration;
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
        /// Gets the injected <see cref="IMessageDeserializerService" /> that will provide message deserialization capabilities
        /// </summary>
        protected readonly IMessageDeserializerService DeserializerService;

        /// <summary>
        /// Gets the injected <see cref="IMessageSerializerService" /> that will provide message serialization capabilities
        /// </summary>
        protected readonly IMessageSerializerService SerializerService;

        /// <summary>
        /// Initializes a new instance of <see cref="MessageClientService" />
        /// </summary>
        /// <param name="connectionProvider">
        /// The injected <see cref="IRabbitMqConnectionProvider" /> <see cref="IConnection" />
        /// access based on registered <see cref="ConnectionFactory" />
        /// </param>
        /// <param name="serializerService">The injected <see cref="IMessageSerializerService" /> that will provide message serialization capabilities</param>
        /// <param name="deserializerService">The injected <see cref="IMessageDeserializerService" /> that will provide message deserialization capabalities</param>
        /// <param name="logger">The injected <see cref="ILogger{TCategoryName}" /></param>
        /// <param name="policyConfiguration">
        /// The optional injected and configured <see cref="IOptions{TOptions}" /> of
        /// <see cref="RetryPolicyConfiguration" />. In case of null, using default value of
        /// <see cref="MessageClientBaseService.retryPolicyConfiguration" />
        /// </param>
        public MessageClientService(IRabbitMqConnectionProvider connectionProvider, IMessageSerializerService serializerService, IMessageDeserializerService deserializerService,
            ILogger<MessageClientService> logger, IOptions<RetryPolicyConfiguration> policyConfiguration = null) : base(connectionProvider, logger, policyConfiguration)
        {
            this.SerializerService = serializerService;
            this.DeserializerService = deserializerService;
        }

        /// <summary>
        /// Listens for messages of type <typeparamref name="TMessage" /> on the specified queue.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
        /// <returns>An observable sequence of messages.</returns>
        public override async Task<IObservable<TMessage>> ListenAsync<TMessage>(string connectionName, IExchangeConfiguration exchangeConfiguration, CancellationToken cancellationToken = default)
        {
            var channel = await this.GetChannelAsync(connectionName, cancellationToken);

            return Observable.Create<TMessage>(async observer =>
            {
                var disposables = await this.InitializeListenerAsync(observer, channel, exchangeConfiguration, cancellationToken);
                return Disposable.Create(() => disposables.Dispose());
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
        public override async Task<IDisposable> AddListenerAsync(string connectionName, IExchangeConfiguration exchangeConfiguration, AsyncEventHandler<BasicDeliverEventArgs> onReceiveAsync, CancellationToken cancellationToken = default)
        {
            IChannel channel = null;
            AsyncEventingBasicConsumer consumer = null;

            try
            {
                channel = await this.GetChannelAsync(connectionName, cancellationToken);

                await exchangeConfiguration.EnsureQueueAndExchangeAreDeclared(channel, false);

                consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += onReceiveAsync;
                consumer.ReceivedAsync += HandleActivityUpdateAsync;

                await channel.BasicConsumeAsync(exchangeConfiguration.QueueName, true, consumer, cancellationToken);
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception exception)
            {
                this.Logger.LogError(exception, "Error while adding a listener to the {QueueName}", exchangeConfiguration.QueueName);
            }
            finally
            {
                if (consumer != null)
                {
                    consumer.ReceivedAsync -= onReceiveAsync;
                    consumer.ReceivedAsync -= HandleActivityUpdateAsync;
                }
            }

            return channel;

            Task HandleActivityUpdateAsync(object _, BasicDeliverEventArgs m)
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
        public override async Task PushAsync<TMessage>(string connectionName, IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
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
        public override async Task PushAsync<TMessage>(string connectionName, TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
        {
            if (Equals(message, default(TMessage)))
            {
                throw new ArgumentNullException(nameof(message), "The message to be sent can not be null");
            }

            try
            {
                var channel = await this.GetChannelAsync(connectionName, cancellationToken);
                await exchangeConfiguration.EnsureQueueAndExchangeAreDeclared(channel, true);

                var properties = new BasicProperties
                {
                    Type = typeof(TMessage).Name,
                    DeliveryMode = DeliveryModes.Persistent,
                    ContentType = "application/json"
                };

                configureProperties?.Invoke(properties);
                this.OnPrePush(message, properties, exchangeConfiguration);

                var body = await this.SerializerService.SerializeAsync(message, cancellationToken);

                var routingKey = !string.IsNullOrEmpty(exchangeConfiguration.RoutingKey) || !string.IsNullOrEmpty(exchangeConfiguration.ExchangeName) 
                    ? exchangeConfiguration.RoutingKey 
                    : exchangeConfiguration.QueueName;

                await channel.BasicPublishAsync(exchangeConfiguration.PushExchangeName,
                    routingKey, false, properties,
                    body, cancellationToken);

                this.Logger.LogDebug("Message Body {Body}", Encoding.UTF8.GetString(body));
                this.Logger.LogInformation("Message {MessageName} sent to {MessageQueue}", typeof(TMessage).Name, exchangeConfiguration.QueueName);
            }
            catch (Exception exception)
            {
                this.Logger.LogError(exception, "The message {MessageName} could not be queued to {MessageQueue} reason : {Exception}", typeof(TMessage).Name, exchangeConfiguration.QueueName, exception.Message);
            }
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
                await exchangeConfiguration.EnsureQueueAndExchangeAreDeclared(channel, false);
                
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
                var content = await this.DeserializerService.DeserializeAsync<TMessage>(message.Body, cancellationToken);
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
