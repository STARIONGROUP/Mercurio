// -------------------------------------------------------------------------------------------------
//  <copyright file="RpcClientService.cs" company="Starion Group S.A.">
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
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;

    using CommunityToolkit.HighPerformance;

    using Mercurio.Extensions;
    using Mercurio.Provider;
    using Mercurio.Serializer;

    using Microsoft.Extensions.Logging;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    /// <summary>
    /// The <see cref="RpcClientService{TResponse} " /> is a <see cref="MessageClientService" /> that supports the RPC protocol implementation of RabbitMQ on the client side
    /// </summary>
    /// <typeparam name="TResponse">Any type that correspond to the kind of response that the server should reply</typeparam>
    public class RpcClientService<TResponse> : MessageClientService, IRpcClientService<TResponse>
    {
        /// <summary>
        /// Gets the <see cref="ConcurrentDictionary{TKey,TValue}" /> that stores, per request,
        /// <see cref="TaskCompletionSource{TResult}" /> handler
        /// </summary>
        private readonly ConcurrentDictionary<string, TaskCompletionSource<TResponse>> callbacks = new();

        /// <summary>
        /// Gets the <see cref="ConcurrentDictionary{TKey,TValue}" /> that stores, per connection name, the initialized
        /// <see cref="RpcQueueDeclared" />
        /// </summary>
        private readonly ConcurrentDictionary<string, RpcQueueDeclared> rpcQueueDeclareds = [];

        /// <summary>
        /// Initializes a new instance of <see cref="RpcClientService{TResponse}" />
        /// </summary>
        /// <param name="connectionProvider">
        /// The injected <see cref="IRabbitMqConnectionProvider" /> <see cref="IConnection" />
        /// access based on registered <see cref="ConnectionFactory" />
        /// </param>
        /// <param name="serializationService">The injected <see cref="ISerializationProviderService" /> that will provide message serialization and deserialization capabilities</param>
        /// <param name="logger">The injected <see cref="ILogger{TCategoryName}" /></param>
        public RpcClientService(IRabbitMqConnectionProvider connectionProvider, ISerializationProviderService serializationService, ILogger<RpcClientService<TResponse>> logger)
            : base(connectionProvider, serializationService, logger)
        {
        }

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
        public Task<IObservable<TResponse>> SendRequestAsync<TRequest>(string connectionName, string rpcServerQueueName, TRequest request, Action<BasicProperties> configureProperties = null, string activityName = "", CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentNullException(nameof(connectionName), "The connection name have to be provided");
            }

            if (string.IsNullOrWhiteSpace(rpcServerQueueName))
            {
                throw new ArgumentNullException(nameof(rpcServerQueueName), "The rpc server queue name have to be provided");
            }

            if (EqualityComparer<TRequest>.Default.Equals(request, default))
            {
                throw new ArgumentNullException(nameof(request), "The request message must not be null");
            }

            return this.SendRequestInternalAsync(connectionName, rpcServerQueueName, request, configureProperties, activityName, cancellationToken);
        }

        /// <summary>
        /// Disposes of the even handlers
        /// </summary>
        /// <param name="disposing">A value indicating whether this client is disposing</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            foreach (var rpcQueueDeclared in this.rpcQueueDeclareds.Values)
            {
                rpcQueueDeclared.ConsumerRegistrationDisposable.Dispose();
            }

            foreach (var taskCompletionSource in this.callbacks.Values)
            {
                taskCompletionSource.SetCanceled();
            }

            this.callbacks.Clear();
            this.rpcQueueDeclareds.Clear();
        }

        /// <summary>
        /// Ensures that the RPC listening queue is declared and initialized correctly to listen on response
        /// </summary>
        /// <param name="activityName">
        /// Defines the name of an <see cref="Activity" /> that should be initialized when the server response has been received, for traceability. In case of null or empty, no
        /// <see cref="Activity" /> is started
        /// </param>
        /// <param name="connectionName">The name of the connection to use</param>
        /// <returns>An awaitable <see cref="Task{T}" /> that have the <see cref="IChannel" /> to use as response</returns>
        private async Task<RpcQueueDeclared> EnsureRpcClientIsInitializedAsync(string connectionName, string activityName)
        {
            if (this.rpcQueueDeclareds.TryGetValue(connectionName, out var rpcQueue))
            {
                return rpcQueue;
            }

            var channelLease = await this.ConnectionProvider.LeaseChannelAsync(connectionName);
            var queueDeclare = await channelLease.Channel.QueueDeclareAsync();
            var activitySource = this.ConnectionProvider.GetRegisteredActivitySource(connectionName);

            var consumer = new AsyncEventingBasicConsumer(channelLease.Channel);

            consumer.ReceivedAsync += OnRpcResponseReceivedAsync;

            var disposable = Disposable.Create(() => { consumer.ReceivedAsync -= OnRpcResponseReceivedAsync; });

            var rpcQueueDeclared = new RpcQueueDeclared(channelLease, queueDeclare.QueueName, disposable);
            this.rpcQueueDeclareds[connectionName] = rpcQueueDeclared;
            await channelLease.Channel.BasicConsumeAsync(queueDeclare.QueueName, true, consumer);

            return rpcQueueDeclared;

            async Task OnRpcResponseReceivedAsync(object sender, BasicDeliverEventArgs args)
            {
                var correlationId = args.BasicProperties.CorrelationId;

                if (!string.IsNullOrEmpty(correlationId) && this.callbacks.TryRemove(correlationId, out var taskCompletionSource))
                { 
                    var activity = this.StartActivity(args, activitySource, activityName, ActivityKind.Client);

                    try
                    {
                        var response = await this.SerializationProviderService.ResolveDeserializer(args.BasicProperties.ContentType)
                            .DeserializeAsync<TResponse>(args.Body.AsStream());

                        taskCompletionSource.TrySetResult(response);
                        activity?.SetStatus(ActivityStatusCode.Ok);
                    }
                    catch (Exception ex)
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    }
                    finally
                    {
                        activity?.Dispose();
                    }
                }
            }
        }

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
        /// <typeparam name="TRequest">Any type that correspond to the kind of request to be sent to the server</typeparam>
        /// <returns>An awaitable <see cref="Task{T}" /> with the observable that will track the server response</returns>
        /// <remarks>
        /// By default, the <see cref="BasicProperties" /> is configured to set the <see cref="BasicProperties.ContentType" /> as 'application/json
        /// </remarks>
        private async Task<IObservable<TResponse>> SendRequestInternalAsync<TRequest>(string connectionName, string rpcServerQueueName, TRequest request, Action<BasicProperties> configureProperties, string activityName, CancellationToken cancellationToken)
        {
            var rpcQueueDeclared = await this.EnsureRpcClientIsInitializedAsync(connectionName, activityName);
            var correlationId = Guid.NewGuid().ToString();

            var properties = new BasicProperties
            {
                Type = typeof(TResponse).Name,
                ContentType = this.SerializationProviderService.DefaultFormat
            };

            configureProperties?.Invoke(properties);
            properties.CorrelationId = correlationId;
            properties.ReplyTo = rpcQueueDeclared.QueueName;

            return Observable.Create<TResponse>(async observer =>
            {
                Activity activity = null;

                try
                {
                    var taskCompletionSource = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                    this.callbacks.TryAdd(correlationId, taskCompletionSource);
                    rpcQueueDeclared.ChannelLease.Channel.CallbackExceptionAsync += ChannelOnCallbackException;
                    var activitySource = this.ConnectionProvider.GetRegisteredActivitySource(connectionName);
                    var context = Activity.Current == null ? default : Activity.Current.Context;
                    activity = this.StartActivity(activitySource, context, $"{activityName} - Request", ActivityKind.Client);
                    IntegrateActivityInformation(properties, activity);

                    var serializedBody = await this.SerializationProviderService.ResolveSerializer(properties.ContentType).SerializeAsync(request, cancellationToken);
                    await rpcQueueDeclared.ChannelLease.Channel.BasicPublishAsync(string.Empty, rpcServerQueueName, true, properties, serializedBody.ToReadOnlyMemory(), cancellationToken);

                    using var cancellationTokenRegistration =
                        cancellationToken.Register(() =>
                        {
                            this.callbacks.TryRemove(correlationId, out _);
                            taskCompletionSource.SetCanceled();
                            observer.OnError(new TimeoutException("Operation canceled"));
                        });

                    var response = await taskCompletionSource.Task;

                    if (taskCompletionSource.Task.Status != TaskStatus.RanToCompletion)
                    {
                        if (taskCompletionSource.Task.Exception != null)
                        {
                            observer.OnError(taskCompletionSource.Task.Exception);
                            activity?.SetStatus(ActivityStatusCode.Error, taskCompletionSource.Task.Exception.Message);
                        }
                        else
                        {
                            observer.OnError(new InvalidOperationException("Error occured during the process"));
                            activity?.SetStatus(ActivityStatusCode.Error, "Error occured during the process");
                        }
                    }
                    else
                    {
                        observer.OnNext(response);
                        activity?.SetStatus(ActivityStatusCode.Ok);
                    }
                }
                catch (Exception exception)
                {
                    observer.OnError(exception);
                    activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                }
                finally
                {
                    activity?.Dispose();
                }

                var disposable = Disposable.Create(() =>
                {
                    rpcQueueDeclared.ChannelLease.Channel.CallbackExceptionAsync -= ChannelOnCallbackException;
                    rpcQueueDeclared.ChannelLease.Dispose();
                });

                return Disposable.Create(() => disposable.Dispose());

                Task ChannelOnCallbackException(object sender, CallbackExceptionEventArgs args)
                {
                    observer.OnError(args.Exception);
                    return Task.CompletedTask;
                }
            });
        }

        /// <summary>
        /// The <see cref="RpcQueueDeclared" /> store key information about created queue to listen for RPC response
        /// </summary>
        private sealed class RpcQueueDeclared
        {
            /// <summary>Initializes a new instance of the <see cref="RpcQueueDeclared"></see> class.</summary>
            /// <param name="channelLease">The <see cref="ChannelLease" /> that provides the <see cref="IChannel" /> to be used for communication</param>
            /// <param name="queueName">The name of the created queue</param>
            /// <param name="consumerRegistrationDisposable">The specific <see cref="IDisposable" /> tied to the created consumer</param>
            public RpcQueueDeclared(ChannelLease channelLease, string queueName, IDisposable consumerRegistrationDisposable)
            {
                this.ChannelLease = channelLease;
                this.QueueName = queueName;
                this.ConsumerRegistrationDisposable = consumerRegistrationDisposable;
            }

            /// <summary>
            /// Gets dedicated <see cref="IChannel" />
            /// </summary>
            public ChannelLease ChannelLease { get; }

            /// <summary>
            /// Gets the name of the created queue
            /// </summary>
            public string QueueName { get; }

            /// <summary>
            /// Gets the specific <see cref="IDisposable" /> tied to the created consumer
            /// </summary>
            public IDisposable ConsumerRegistrationDisposable { get; }
        }
    }
}
