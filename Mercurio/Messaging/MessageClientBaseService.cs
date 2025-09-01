// -------------------------------------------------------------------------------------------------
//  <copyright file="MessageClientBaseService.cs" company="Starion Group S.A.">
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

    using Mercurio.Extensions;
    using Mercurio.Model;
    using Mercurio.Provider;

    using Microsoft.Extensions.Logging;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    /// <summary>
    /// The <see cref="MessageClientBaseService" /> is the base abstract class for any RabbitMQ client
    /// </summary>
    public abstract class MessageClientBaseService : IMessageClientService
    {
        /// <summary>
        /// The header key used for the traceparent value in the message headers.
        /// </summary>
        protected const string TraceParentHeaderKey = "traceparent";

        /// <summary>
        /// The header key used for the tracestate value in the message headers.
        /// </summary>
        protected const string TraceStateHeaderKey = "tracestate";

        /// <summary>
        /// Initializes a new instance of <see cref="MessageClientBaseService" />
        /// </summary>
        /// <param name="connectionProvider">
        /// The injected <see cref="IRabbitMqConnectionProvider" /> <see cref="IConnection" />
        /// access based on registered <see cref="ConnectionFactory" />
        /// </param>
        /// <param name="logger">The injected <see cref="ILogger{TCategoryName}" /></param>
        protected MessageClientBaseService(IRabbitMqConnectionProvider connectionProvider, ILogger<MessageClientBaseService> logger)
        {
            this.ConnectionProvider = connectionProvider;
            this.Logger = logger;
        }

        /// <summary>
        /// Gets the injected <see cref="ILogger{TCategoryName}" />
        /// </summary>
        protected ILogger<MessageClientBaseService> Logger { get; }

        /// <summary>
        /// Gets the injected <see cref="IRabbitMqConnectionProvider" /> <see cref="IConnection" /> access based on registered
        /// <see cref="ConnectionFactory" />
        /// </summary>
        protected IRabbitMqConnectionProvider ConnectionProvider { get; }

        /// <summary>
        /// Listens for messages of type <typeparamref name="TMessage" /> on the specified queue.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to listen for.</typeparam>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
        /// <returns>An observable sequence of messages.</returns>
        public abstract Task<IObservable<TMessage>> ListenAsync<TMessage>(string connectionName, IExchangeConfiguration exchangeConfiguration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a listener to the specified queue
        /// </summary>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="exchangeConfiguration">The <see cref="IExchangeConfiguration" /> that should be used to configure the queue and exchange to use</param>
        /// <param name="onReceiveAsync">The <see cref="AsyncEventHandler{TEvent}" /></param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <return>A <see cref="Task" /> of <see cref="IDisposable" /></return>
        public abstract Task<IDisposable> AddListenerAsync(string connectionName, IExchangeConfiguration exchangeConfiguration, AsyncEventHandler<BasicDeliverEventArgs> onReceiveAsync, CancellationToken cancellationToken = default);

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
        public abstract Task PushAsync<TMessage>(string connectionName, IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

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
        public abstract Task PushAsync<TMessage>(string connectionName, TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously leases a channel from the pool or creates one if necessary.
        /// </summary>
        /// <param name="connectionName">The name of the registered connection that should be used to establish the connection</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken" /></param>
        /// <returns>A <see cref="ValueTask{TResult}" /> of <see cref="ChannelLease" /></returns>
        public async ValueTask<ChannelLease> LeaseChannelAsync(string connectionName, CancellationToken cancellationToken = default)
        {
            return await this.ConnectionProvider.LeaseChannelAsync(connectionName, cancellationToken);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            //Nothing to dispose so far
        }

        /// <summary>
        /// Starts a new <see cref="Activity" /> from the provided <paramref name="activitySource" /> defined by the
        /// <paramref name="activityName" />
        /// </summary>
        /// <param name="message">The received <see cref="BasicDeliverEventArgs" /></param>
        /// <param name="activitySource">The <see cref="ActivitySource" /> that will starts the <see cref="Activity" />. If null, the process is ignored</param>
        /// <param name="activityName">The name of the <see cref="Activity" /> to start. If null or empty, the process is ignored.</param>
        /// <param name="activityKind">
        /// The <see cref="ActivityKind" /> that should be set on the started <see cref="Activity" />. By default,
        /// <see cref="ActivityKind.Consumer" />
        /// </param>
        /// <returns>The started <see cref="Activity" /></returns>
        protected Activity StartActivity(BasicDeliverEventArgs message, ActivitySource activitySource, string activityName, ActivityKind activityKind = ActivityKind.Consumer)
        {
            if (string.IsNullOrEmpty(activityName) || activitySource == null)
            {
                return null;
            }

            var traceState = message.BasicProperties.TryReadHeader(TraceStateHeaderKey, out string state) ? state : null;

            var context = message.BasicProperties.TryReadHeader(TraceParentHeaderKey, out string parent)
                ? ActivityContext.Parse(parent, traceState)
                : default;

            return this.StartActivityInternal(activitySource, context, activityName, activityKind);
        }

        /// <summary>
        /// Starts a new <see cref="Activity" /> from the provided <paramref name="activitySource" /> defined by the
        /// <paramref name="activityName" />
        /// </summary>
        /// <param name="activitySource">The <see cref="ActivitySource" /> that will starts the <see cref="Activity" />. If null, the process is ignored</param>
        /// <param name="context">The <see cref="ActivityContext" /> that should be specified </param>
        /// <param name="activityName">The name of the <see cref="Activity" /> to start. If null or empty, the process is ignored.</param>
        /// <param name="activityKind">
        /// The <see cref="ActivityKind" /> that should be set on the started <see cref="Activity" />. By default,
        /// <see cref="ActivityKind.Consumer" />
        /// </param>
        /// <returns>The started <see cref="Activity" /></returns>
        protected Activity StartActivity(ActivitySource activitySource, ActivityContext context, string activityName, ActivityKind activityKind = ActivityKind.Consumer)
        {
            if (string.IsNullOrEmpty(activityName) || activitySource == null)
            {
                return null;
            }

            return this.StartActivityInternal(activitySource, context, activityName, activityKind);
        }

        /// <summary>
        /// Adds current <see cref="Activity" />'s information into the <paramref name="properties" /> headers
        /// </summary>
        /// <param name="properties">The <see cref="BasicProperties" /> that will hold <see cref="Activity" />'s information</param>
        /// <param name="currentActivity">The <see cref="Activity" /> that should be used to send information</param>
        protected static void IntegrateActivityInformation(BasicProperties properties, Activity currentActivity)
        {
            if (currentActivity == null || currentActivity.IsStopped)
            {
                return;
            }

            if (!properties.IsHeadersPresent())
            {
                properties.Headers = new Dictionary<string, object>();
            }

            if (!string.IsNullOrEmpty(currentActivity.Id))
            {
                properties.Headers[TraceParentHeaderKey] = currentActivity.Id;
            }

            if (!string.IsNullOrEmpty(currentActivity.TraceStateString))
            {
                properties.Headers[TraceStateHeaderKey] = currentActivity.TraceStateString;
            }
        }

        /// <summary>
        /// Starts a new <see cref="Activity" /> from the provided <paramref name="activitySource" /> defined by the
        /// <paramref name="activityName" />
        /// </summary>
        /// <param name="activitySource">The <see cref="ActivitySource" /> that will starts the <see cref="Activity" />.</param>
        /// <param name="context">The <see cref="ActivityContext" /> that should be specified </param>
        /// <param name="activityName">The name of the <see cref="Activity" /> to start. </param>
        /// <param name="activityKind">
        /// The <see cref="ActivityKind" /> that should be set on the started <see cref="Activity" />
        /// </param>
        /// <returns>The started <see cref="Activity" /></returns>
        private Activity StartActivityInternal(ActivitySource activitySource, ActivityContext context, string activityName, ActivityKind activityKind)
        {
            var activity = activitySource.StartActivity(activityName, activityKind, context);

            if (activity is null)
            {
                return null;
            }

            this.Logger.LogTrace("{ActivityName} started!", activityName);

            return activity;
        }
    }
}
