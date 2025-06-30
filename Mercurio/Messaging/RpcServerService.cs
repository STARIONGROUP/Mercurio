// -------------------------------------------------------------------------------------------------
//  <copyright file="RpcServerService.cs" company="Starion Group S.A.">
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
    /// The <see cref="RpcServerService " /> is a <see cref="MessageClientService" /> that supports the RPC protocol implementation of RabbitMQ on the server side
    /// </summary>
    public class RpcServerService : MessageClientService, IRpcServerService
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RpcServerService" />
        /// </summary>
        /// <param name="connectionProvider">
        /// The injected <see cref="IRabbitMqConnectionProvider" /> <see cref="IConnection" />
        /// access based on registered <see cref="ConnectionFactory" />
        /// </param>
        /// <param name="serializationService">The injected <see cref="ISerializationProviderService" /> that will provide message serialization and deserialization capabilities</param>
        /// <param name="logger">The injected <see cref="ILogger{TCategoryName}" /></param>
        public RpcServerService(IRabbitMqConnectionProvider connectionProvider, ISerializationProviderService serializationService, ILogger<RpcServerService> logger) 
            : base(connectionProvider, serializationService, logger)
        {
        }

        /// <summary>
        /// Listens for request, execute the <paramref name="onReceiveAsync" /> action to process the request, and send the response back to the
        /// </summary>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="queueName">The name of the listening queue</param>
        /// <param name="onReceiveAsync">The action that should be executed when a request is received</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
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
        public Task<IDisposable> ListenForRequestAsync<TRequest, TResponse>(string connectionName, string queueName, Func<TRequest, Task<TResponse>> onReceiveAsync, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentNullException(nameof(queueName), "The queue name must be set.");
            }

            if (onReceiveAsync == null)
            {
                throw new ArgumentNullException(nameof(onReceiveAsync), "The OnReceiveAsync cannot be null.");
            }

            return this.ListenForRequestInternalAsync(connectionName, queueName, onReceiveAsync, configureProperties, cancellationToken);
        }

        /// <summary>
        /// Listens for request, execute the <paramref name="onReceiveAsync" /> action to process the request, and send the response back to the
        /// </summary>
        /// <param name="connectionName">The name of the registered connection to use.</param>
        /// <param name="queueName">The name of the listening queue</param>
        /// <param name="onReceiveAsync">The action that should be executed when a request is received</param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">A possible <see cref="CancellationToken" /></param>
        /// <typeparam name="TRequest">Any type that correspond to the kind of request to be processed</typeparam>
        /// <typeparam name="TResponse">Any type that correspond to the kind of response that has to be send back</typeparam>
        /// <returns>An awaitable <see cref="Task" /> with an <see cref="IDisposable" /></returns>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to set the <see cref="BasicProperties.ContentType" /> as 'application/json"
        /// </remarks>
        private async Task<IDisposable> ListenForRequestInternalAsync<TRequest, TResponse>(string connectionName, string queueName, Func<TRequest, Task<TResponse>> onReceiveAsync, Action<BasicProperties> configureProperties, CancellationToken cancellationToken)
        {
            AsyncEventingBasicConsumer consumer = null;
            ChannelLease channelLease = default;

            try
            {
                channelLease = await this.ConnectionProvider.LeaseChannelAsync(connectionName, cancellationToken);
                await channelLease.Channel.QueueDeclareAsync(queueName, false, false, false, cancellationToken: cancellationToken);
                await channelLease.Channel.BasicQosAsync(0, 1, false, cancellationToken);

                consumer = new AsyncEventingBasicConsumer(channelLease.Channel);
                consumer.ReceivedAsync += OnMessageReceiveAsync;

                await channelLease.Channel.BasicConsumeAsync(queueName, false, consumer, cancellationToken);
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception exception)
            {
                this.Logger.LogError(exception, "Error during the RPC server process: {ExceptionMessage}", exception.Message);
            }

            return Disposable.Create(() =>
            {
                if (consumer != null)
                {
                    consumer.ReceivedAsync -= OnMessageReceiveAsync;
                }

                channelLease.Dispose();
            });

            async Task OnMessageReceiveAsync(object sender, BasicDeliverEventArgs args)
            {
                await this.OnRequestReceivedAsync(sender, args, queueName, onReceiveAsync, configureProperties, cancellationToken);
            }
        }

        /// <summary>
        /// Handles the behabviour to handle the reception of a request
        /// </summary>
        /// <param name="sender">The original event sender</param>
        /// <param name="args">The <see cref="BasicDeliverEventArgs" /> of the request</param>
        /// <param name="queueName">The name of the used queue</param>
        /// <param name="onReceiveAsync">The action to perform to compute the <typeparamref name="TResponse" /></param>
        /// <param name="configureProperties">Possible action to configure additional properties</param>
        /// <param name="cancellationToken">A possible <see cref="CancellationToken" /></param>
        /// <typeparam name="TRequest">Any type that correspond to the kind of request to be processed</typeparam>
        /// <typeparam name="TResponse">Any type that correspond to the kind of response that has to be send back</typeparam>
        /// <returns>An awaitable <see cref="Task" /></returns>
        private async Task OnRequestReceivedAsync<TRequest, TResponse>(object sender, BasicDeliverEventArgs args, string queueName, Func<TRequest, Task<TResponse>> onReceiveAsync, Action<BasicProperties> configureProperties, CancellationToken cancellationToken)
        {
            this.OnMessageReceive(args, new DefaultExchangeConfiguration(queueName));

            var request = await this.serializationProviderService.ResolveDeserializer(args.BasicProperties.ContentType.ToSupportedSerializationFormat())
                .DeserializeAsync<TRequest>(args.Body.AsStream(), cancellationToken);

            var response = await onReceiveAsync(request);

            var consumer = (AsyncEventingBasicConsumer)sender;
            var exchangeChannel = consumer.Channel;

            var properties = new BasicProperties
            {
                Type = typeof(TResponse).Name,
                ContentType = this.serializationProviderService.DefaultFormat.ToContentType()
            };

            configureProperties?.Invoke(properties);
            var receivedProperties = args.BasicProperties;

            properties.CorrelationId = receivedProperties.CorrelationId;
            var serializedResponse = await this.serializationProviderService.ResolveSerializer().SerializeAsync(response, cancellationToken);

            await exchangeChannel.BasicPublishAsync(string.Empty, receivedProperties.ReplyTo!, true, properties, serializedResponse.ToReadOnlyMemory(), cancellationToken);

            await exchangeChannel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);
        }
    }
}
