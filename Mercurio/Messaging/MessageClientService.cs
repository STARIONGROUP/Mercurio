// -------------------------------------------------------------------------------------------------
//  <copyright file="MessageClientService.cs" company="Starion Group S.A.">
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
    using System.Diagnostics;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Text;

    using CommunityToolkit.HighPerformance;

    using ErrorOr;

    using Mercurio.Extensions;
    using Mercurio.Model;
    using Mercurio.Provider;
    using Mercurio.Serializer;

    using Microsoft.Extensions.Logging;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;
    using RabbitMQ.Client.Exceptions;

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
        protected readonly ISerializationProviderService SerializationProviderService;

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
            this.SerializationProviderService = serializationProviderService;
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
                throw new ArgumentNullException(nameof(exchangeConfiguration));
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
                throw new ArgumentNullException(nameof(exchangeConfiguration));
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

            return this.PushMultipleInternalAsync(connectionName, messages, exchangeConfiguration, configureProperties, cancellationToken);
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

            var context = Activity.Current == null ? default : Activity.Current.Context;
            var activityName = $"Push {typeof(TMessage).Name} [{exchangeConfiguration}]";

            return this.PushInternalAsync(connectionName, message, exchangeConfiguration, configureProperties, activityName, context, cancellationToken);
        }

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
        public override Task<ErrorOr<Success>> PushWithConfirmationAsync<TMessage>(string connectionName, TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
        {
            if (Equals(message, default(TMessage)))
            {
                throw new ArgumentNullException(nameof(message), "The message to be sent can not be null");
            }

            if (exchangeConfiguration == null)
            {
                throw new ArgumentNullException(nameof(exchangeConfiguration), "The exchange configuration cannot be null");
            }

            var context = Activity.Current == null ? default : Activity.Current.Context;
            var activityName = $"Push with confirmation {typeof(TMessage).Name} [{exchangeConfiguration}]";

            return this.TryPublishAsync(connectionName, message, exchangeConfiguration, configureProperties, activityName, context, cancellationToken);
        }

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
        public override Task<ErrorOr<Success>> PushWithConfirmationAsync<TMessage>(string connectionName, IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
        {
            if (messages == null)
            {
                throw new ArgumentException("The messages collection cannot be null", nameof(messages));
            }

            if (exchangeConfiguration == null)
            {
                throw new ArgumentNullException(nameof(exchangeConfiguration), "The exchange configuration cannot be null");
            }

            return this.PushMultipleWithConfirmationInternalAsync(connectionName, messages, exchangeConfiguration, configureProperties, cancellationToken);
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
        {
            var channelLease = await this.LeaseChannelAsync(connectionName, cancellationToken);
            var activitySource = this.ConnectionProvider.GetRegisteredActivitySource(connectionName);

            return Observable.Create<TMessage>(async observer =>
            {
                var disposables = await this.InitializeListenerAsync(observer, channelLease.Channel, exchangeConfiguration, activitySource, cancellationToken);

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
            ActivitySource activitySource = null;

            try
            {
                channelLease = await this.LeaseChannelAsync(connectionName, cancellationToken);
                activitySource = this.ConnectionProvider.GetRegisteredActivitySource(connectionName);

                await exchangeConfiguration.EnsureQueueAndExchangeAreDeclaredAsync(channelLease.Channel, false);

                consumer = new AsyncEventingBasicConsumer(channelLease.Channel);
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
                    consumer.ReceivedAsync -= OnMessageReceiveAsync;
                }

                channelLease.Dispose();
            });

            async Task OnMessageReceiveAsync(object sender, BasicDeliverEventArgs m)
            {
                var activityName = $"On Received [{exchangeConfiguration}]";
                var activity = this.StartActivity(m, activitySource, activityName);

                try
                {
                    await onReceiveAsync(sender, m);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception e)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                }
                finally
                {
                    activity?.Dispose();
                }
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
        private async Task PushMultipleInternalAsync<TMessage>(string connectionName, IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties, CancellationToken cancellationToken)
        {
            var activitySource = this.ConnectionProvider.GetRegisteredActivitySource(connectionName);

            var context = Activity.Current == null ? default : Activity.Current.Context;
            var activityName = $"Push {typeof(TMessage).Name} collection [{exchangeConfiguration}]";

            using var activity = this.StartActivity(activitySource, context, activityName, ActivityKind.Producer);

            var messagesList = messages.ToList();
            var messageIndex = 1;

            foreach (var message in messagesList)
            {
                var subActivityName = activity == null ? null : $"{activityName} [{messageIndex++}/{messagesList.Count}]";
                await this.PushInternalAsync(connectionName, message, exchangeConfiguration, configureProperties, subActivityName, context, cancellationToken: cancellationToken);
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
        /// <param name="activityName">
        /// Defines the name of an <see cref="Activity" /> that should be initialized before sending the message, for traceability.
        /// <see cref="Activity" /> information will be sent in the message header.
        /// In case of null or empty, no <see cref="Activity" /> is started
        /// </param>
        /// <param name="activityContext">An optional <see cref="ActivityContext"/>. If not set, current context will be based on <see cref="Activity.Current"/></param>
        /// <param name="cancellationToken">A possible <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <exception cref="ArgumentNullException">When the provided <typeparamref name="TMessage" /> is null</exception>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to use the <see cref="DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        private async Task PushInternalAsync<TMessage>(string connectionName, TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties, string activityName, ActivityContext activityContext, CancellationToken cancellationToken)
        {
            try
            {
                await this.PublishInternalAsync(connectionName, message, exchangeConfiguration, configureProperties, activityName, activityContext, false, cancellationToken);
            }
            catch (Exception exception)
            {
                this.Logger.LogError(exception, "The message {MessageName} could not be queued to {MessageQueue} reason : {Exception}", typeof(TMessage).Name, exchangeConfiguration.QueueName, exception.Message);
            }
        }

        /// <summary>
        /// Publishes the specified <paramref name="messages" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />, waiting for the RabbitMQ server to acknowledge each of them
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
        /// <remarks>
        /// The process stops at the first message that is not acknowledged, so that a broken connection does not have every remaining
        /// message pay the connection retry backoff
        /// </remarks>
        private async Task<ErrorOr<Success>> PushMultipleWithConfirmationInternalAsync<TMessage>(string connectionName, IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties, CancellationToken cancellationToken)
        {
            var activitySource = this.ConnectionProvider.GetRegisteredActivitySource(connectionName);

            var context = Activity.Current == null ? default : Activity.Current.Context;
            var activityName = $"Push with confirmation {typeof(TMessage).Name} collection [{exchangeConfiguration}]";

            using var activity = this.StartActivity(activitySource, context, activityName, ActivityKind.Producer);

            var messagesList = messages.ToList();

            for (var messageIndex = 0; messageIndex < messagesList.Count; messageIndex++)
            {
                var subActivityName = activity == null ? null : $"{activityName} [{messageIndex + 1}/{messagesList.Count}]";
                var result = await this.TryPublishAsync(connectionName, messagesList[messageIndex], exchangeConfiguration, configureProperties, subActivityName, context, cancellationToken);

                if (result.IsError)
                {
                    return MessagingErrors.BatchFailed(result.FirstError, messageIndex, messageIndex);
                }
            }

            return Result.Success;
        }

        /// <summary>
        /// Publishes the specified <paramref name="message" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />, waiting for the RabbitMQ server to acknowledge it and reporting any failure as
        /// an <see cref="Error" /> instead of throwing
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="activityName">
        /// Defines the name of an <see cref="Activity" /> that should be initialized before sending the message, for traceability.
        /// In case of null or empty, no <see cref="Activity" /> is started
        /// </param>
        /// <param name="activityContext">An optional <see cref="ActivityContext" />. If not set, current context will be based on <see cref="Activity.Current" /></param>
        /// <param name="cancellationToken">A possible <see cref="CancellationToken" /></param>
        /// <returns>
        /// An awaitable <see cref="Task{TResult}" /> of <see cref="ErrorOr{TValue}" /> that provides <see cref="Success" /> once the
        /// RabbitMQ server has acknowledged the message, or the <see cref="Error" /> that describes the failure
        /// </returns>
        private async Task<ErrorOr<Success>> TryPublishAsync<TMessage>(string connectionName, TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties, string activityName, ActivityContext activityContext, CancellationToken cancellationToken)
        {
            try
            {
                await this.PublishInternalAsync(connectionName, message, exchangeConfiguration, configureProperties, activityName, activityContext, true, cancellationToken);
                return Result.Success;
            }
            catch (PublishException publishException)
            {
                this.Logger.LogWarning(publishException, "The message {MessageName} has not been acknowledged by the RabbitMQ server for {MessageQueue}", typeof(TMessage).Name, exchangeConfiguration.QueueName);
                return MessagingErrors.NotAcknowledged(publishException);
            }
            catch (Exception exception)
            {
                this.Logger.LogWarning(exception, "The message {MessageName} could not be published to {MessageQueue}", typeof(TMessage).Name, exchangeConfiguration.QueueName);
                return MessagingErrors.PublishFailed(exception);
            }
        }

        /// <summary>
        /// Publishes the specified <paramref name="message" /> to the specified queue via the
        /// <paramref name="exchangeConfiguration" />, reporting any failure to the caller
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="message">The <typeparamref name="TMessage" /> to push</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="activityName">
        /// Defines the name of an <see cref="Activity" /> that should be initialized before sending the message, for traceability.
        /// <see cref="Activity" /> information will be sent in the message header.
        /// In case of null or empty, no <see cref="Activity" /> is started
        /// </param>
        /// <param name="activityContext">An optional <see cref="ActivityContext" />. If not set, current context will be based on <see cref="Activity.Current" /></param>
        /// <param name="publisherConfirmationsEnabled">
        /// Asserts that the message has to be published on a channel that supports publisher confirmations, making the
        /// <see cref="IChannel.BasicPublishAsync{TProperties}(string,string,bool,TProperties,ReadOnlyMemory{byte},CancellationToken)" />
        /// await the acknowledgment of the RabbitMQ server
        /// </param>
        /// <param name="cancellationToken">A possible <see cref="CancellationToken" /></param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to use the <see cref="DeliveryModes.Persistent" /> mode and sets the
        /// <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        private async Task PublishInternalAsync<TMessage>(string connectionName, TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties, string activityName, ActivityContext activityContext, bool publisherConfirmationsEnabled, CancellationToken cancellationToken)
        {
            Activity activity = null;

            try
            {
                await using var channelLease = await this.LeaseChannelAsync(connectionName, publisherConfirmationsEnabled, cancellationToken);
                await exchangeConfiguration.EnsureQueueAndExchangeAreDeclaredAsync(channelLease.Channel, true);

                var properties = new BasicProperties
                {
                    Type = typeof(TMessage).Name,
                    DeliveryMode = DeliveryModes.Persistent,
                    ContentType = this.SerializationProviderService.DefaultFormat
                };

                configureProperties?.Invoke(properties);

                var activitySource = this.ConnectionProvider.GetRegisteredActivitySource(connectionName);

                var context = activityContext;

                if (activityContext == default && Activity.Current != null)
                {
                    context = Activity.Current.Context;
                }

                activity = this.StartActivity(activitySource, context, activityName, ActivityKind.Producer);
                IntegrateActivityInformation(properties, activity);

                var stream = await this.SerializationProviderService.ResolveSerializer(properties.ContentType).SerializeAsync(message, cancellationToken);

                var routingKey = !string.IsNullOrEmpty(exchangeConfiguration.RoutingKey) || !string.IsNullOrEmpty(exchangeConfiguration.ExchangeName)
                    ? exchangeConfiguration.RoutingKey
                    : exchangeConfiguration.QueueName;

                var body = stream.ToReadOnlyMemory();

                await channelLease.Channel.BasicPublishAsync(exchangeConfiguration.PushExchangeName,
                    routingKey, false, properties,
                    body, cancellationToken);

                if (this.Logger.IsEnabled(LogLevel.Debug))
                {
                    this.Logger.LogDebug("Message Body {Body}", Encoding.UTF8.GetString(body.ToArray()));
                }

                this.Logger.LogInformation("Message {MessageName} sent to {MessageQueue}", typeof(TMessage).Name, exchangeConfiguration.QueueName);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        /// <summary>
        /// Initializes the listener by setting up event handlers and consuming messages.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="observer">The observer to push messages to.</param>
        /// <param name="channel">The RabbitMQ channel.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="activitySource">The <see cref="ActivitySource" /> that will be use to start an <see cref="Activity" />, if applicable</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>A disposable to clean up resources.</returns>
        private async Task<IDisposable> InitializeListenerAsync<TMessage>(IObserver<TMessage> observer, IChannel channel, IExchangeConfiguration exchangeConfiguration, ActivitySource activitySource,
            CancellationToken cancellationToken = default)
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
                var activityName = $"{typeof(TMessage).Name} Received [{exchangeConfiguration}]";
                var activity = this.StartActivity(message, activitySource, activityName);

                try
                {
                    TMessage content;

                    try
                    {
                        await using var stream = message.Body.AsStream();
                        content = await this.SerializationProviderService.ResolveDeserializer(message.BasicProperties.ContentType).DeserializeAsync<TMessage>(stream, cancellationToken);
                    }
                    catch (Exception deserializationException)
                    {
                        var invalidMessageException = new InvalidMessageException(typeof(TMessage), message, deserializationException);

                        this.Logger.LogError(deserializationException, "The message received on {QueueName} could not be deserialized into a {MessageName}", exchangeConfiguration.QueueName, typeof(TMessage).Name);
                        activity?.SetStatus(ActivityStatusCode.Error, invalidMessageException.Message);
                        observer.OnError(invalidMessageException);
                        return;
                    }

                    observer.OnNext(content);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    throw;
                }
                finally
                {
                    activity?.Dispose();
                }
            }

            Task ConsumerOnShutdownAsync(object _, ShutdownEventArgs arguments)
            {
                if (arguments.ReplyCode == 200)
                {
                    observer.OnCompleted();
                }
                else
                {
                    observer.OnError(new OperationCanceledException($"The channel has shutdown [Reply: {arguments.ReplyText}, AMQPcode: {arguments.ReplyCode}]"));
                }

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
