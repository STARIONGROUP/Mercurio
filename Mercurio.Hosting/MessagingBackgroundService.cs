// -------------------------------------------------------------------------------------------------
//  <copyright file="MessagingBackgroundService.cs" company="Starion Group S.A.">
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
    using System.Collections.Concurrent;
    using System.Diagnostics;

    using Mercurio.Extensions;
    using Mercurio.Messaging;
    using Mercurio.Model;
    using Mercurio.Provider;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    using RabbitMQ.Client;

    /// <summary>
    /// The <see cref="MessagingBackgroundService" /> provides <see cref="IMessageClientService" /> interaction through a
    /// <see cref="BackgroundService" />
    /// </summary>
    public abstract class MessagingBackgroundService : BackgroundService, IMessagingBackgroundService, IHealthCheck
    {
        /// <summary>
        /// Gets the injected <see cref="IConfiguration" /> to provides configuration information for service initialization
        /// </summary>
        protected readonly IConfiguration Configuration;

        /// <summary>
        /// Gets the resolved <see cref="IRabbitMqConnectionProvider" /> that provides registered <see cref="ActivitySource" />
        /// access
        /// </summary>
        private readonly IRabbitMqConnectionProvider connectionProvider;

        /// <summary>
        /// Gets the injected <see cref="ILogger{TCategory}" /> to allow logging
        /// </summary>
        protected readonly ILogger<MessagingBackgroundService> Logger;

        /// <summary>
        /// Gets the resolved <see cref="IMessageClientService" /> that allow RabbitMQ communisation
        /// </summary>
        protected readonly IMessageClientService MessageClientService;

        /// <summary>
        /// A <see cref="ConcurrentQueue{T}" /> of <see cref="Func{TResult}" /> that records all messages that have to be sent
        /// </summary>
        private readonly ConcurrentQueue<(Func<Task> MessageToPush, ActivityContext Context, string ActivityName)> messagesToPush = [];

        /// <summary>
        /// Gets the injected <see cref="IServiceProvider" /> to allow registered type resolving on inherited classes
        /// </summary>
        protected readonly IServiceProvider ServiceProvider;

        /// <summary>
        /// Gets the <see cref="Dictionary{TKey, TValue}" /> of <see cref="IDisposable" /> that represents
        /// <see cref="IMessageClientService" />
        /// listen subscriptions
        /// </summary>
        private readonly Dictionary<Guid, IDisposable> subscriptions = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagingBackgroundService" />
        /// </summary>
        /// <param name="serviceProvider">
        /// The injected <see cref="IServiceProvider" /> that allow to resolve
        /// <see cref="IMessageClientService" /> instance, even if not registered as scope
        /// </param>
        /// <param name="logger">The injected <see cref="ILogger{TCategory}" /> to allow logging</param>
        /// <param name="configuration">The injected <see cref="IConfiguration" /> to provides configuration information for service initialization</param>
        protected MessagingBackgroundService(IServiceProvider serviceProvider, ILogger<MessagingBackgroundService> logger, IConfiguration configuration)
        {
            this.ServiceProvider = serviceProvider;
            using var scope = this.ServiceProvider.CreateScope();
            this.MessageClientService = scope.ServiceProvider.GetService<IMessageClientService>();
            this.connectionProvider = scope.ServiceProvider.GetService<IRabbitMqConnectionProvider>();
            this.Logger = logger;
            this.Configuration = configuration;
        }

        /// <summary>
        /// Gets or sets the asserts that the current <see cref="MessagingBackgroundService" /> is healthy or not
        /// </summary>
        protected bool IsHealthy { get; private set; }

        /// <summary>
        /// Gets or sets the name of the registered connection that has to be used to communicate with RabbitMQ
        /// </summary>
        public string ConnectionName { get; protected set; }

        /// <summary>
        /// Runs the health check, returning the status of the component being checked.
        /// </summary>
        /// <param name="context">A context object associated with the current execution.</param>
        /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> that can be used to cancel the health check.</param>
        /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> that completes when the health check has finished, yielding the status of the component being checked.</returns>
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.IsHealthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
        }

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
        public void PushMessage<TMessage>(TMessage message, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
        {
            var activityContext = Activity.Current?.Context ?? default;
            var activityName = $"Background Push {typeof(TMessage).Name} [{exchangeConfiguration}]";
            this.messagesToPush.Enqueue((() => this.MessageClientService.PushAsync(this.ConnectionName, message, exchangeConfiguration, configureProperties, cancellationToken), activityContext, activityName));
        }

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
        public void PushMessages<TMessage>(IEnumerable<TMessage> messages, IExchangeConfiguration exchangeConfiguration, Action<BasicProperties> configureProperties = null, CancellationToken cancellationToken = default)
        {
            var activityContext = Activity.Current?.Context ?? default;
            var activityName = $"Background Push Multiple {typeof(TMessage).Name} [{exchangeConfiguration}]";
            this.messagesToPush.Enqueue((() => this.MessageClientService.PushAsync(this.ConnectionName, messages, exchangeConfiguration, configureProperties, cancellationToken), activityContext, activityName));
        }

        /// <summary>
        /// Triggered when the application host is ready to start the service.
        /// </summary>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous Start operation.</returns>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await this.InitializeAsync();

            if (string.IsNullOrEmpty(this.ConnectionName))
            {
                this.Logger.LogError("No configuration process to set the ConnectionName, can not go further");
                throw new InvalidOperationException("The ConnectionName property must be set during the initialization.");
            }

            await base.StartAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            base.Dispose();

            foreach (var subscription in this.subscriptions)
            {
                subscription.Value.Dispose();
            }

            this.subscriptions.Clear();
        }

        /// <summary>
        /// This method is called when the <see cref="T:Microsoft.Extensions.Hosting.IHostedService" /> starts. The implementation should return a tas  hat represents
        /// the lifetime of the long running operation(s) being performed.
        /// </summary>
        /// <param name="stoppingToken">
        /// Triggered when
        /// <see cref="M:Microsoft.Extensions.Hosting.IHostedService.StopAsync(System.Threading.CancellationToken)" /> is called.
        /// </param>
        /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> that represents the long running operations.</returns>
        /// <remarks>See <see href="https://learn.microsoft.com/dotnet/core/extensions/workers">Worker Services in .NET</see> for implementation guidelines.</remarks>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.Logger.LogInformation("Background Service ready to start");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (this.messagesToPush.TryDequeue(out var messageToPush))
                    {
                        var activitySource = this.connectionProvider.GetRegisteredActivitySource(this.ConnectionName);

                        _ = Task.Run(async () =>
                        {
                            using var activity = activitySource?.StartActivity(messageToPush.ActivityName, ActivityKind.Producer, messageToPush.Context);
                            await messageToPush.MessageToPush();
                        }, stoppingToken);
                    }
                }
                catch (OperationCanceledException exception)
                {
                    this.Logger.LogError(exception, "Processing message got canceled.");
                }
                catch (Exception exception)
                {
                    this.Logger.LogError(exception, "Error occured while processing a message");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10), stoppingToken);
            }

            this.IsHealthy = false;
            this.Logger.LogInformation("Background Service stopped");
        }

        /// <summary>
        /// Initializes this service (e.g. to set the <see cref="ConnectionName" /> and register subscriptions
        /// </summary>
        /// <returns>An awaitable <see cref="Task" /></returns>
        protected abstract Task InitializeAsync();

        /// <summary>
        /// Registers an <see cref="IObservable{T}" /> by creating a subscription based on provided <see cref="Action" />s and apply auto-recovery on error behavior
        /// </summary>
        /// <param name="observableFunction">The <see cref="IObservable{T}" /> function that should produice the subscription subject</param>
        /// <param name="onReceive">
        /// The <see cref="Action{T}" /> that should be executed when the <paramref name="observableFunction" />
        /// value changes
        /// </param>
        /// <param name="onError">An optional <see cref="Action{T}" /> that should be executed in case of error</param>
        /// <param name="onCompleted">An optional <see cref="Action" /> that should be executed in case of completion</param>
        /// <typeparam name="TMessage">Any type of message that can be listen</typeparam>
        protected async Task RegisterListener<TMessage>(Func<Task<IObservable<TMessage>>> observableFunction, Action<TMessage> onReceive, Action<Exception> onError = null, Action onCompleted = null)
        {
            onCompleted ??= () => { };
            var key = Guid.NewGuid();

            var observable = await observableFunction();
            var subscription = observable.Subscribe(onReceive, x => _ = this.HandleErrorAndRecover(x, observableFunction, onReceive, onError, onCompleted, key), onCompleted);
            this.subscriptions[key] = subscription;
        }

        /// <summary>
        /// Registers an <see cref="IObservable{T}" /> by creating a subscription for an async process, based on provided
        /// <see cref="Action" />s and apply auto-recovery on error behavior
        /// </summary>
        /// <param name="observableFunction">The <see cref="IObservable{T}" /> function that should produice the subscription subject</param>
        /// <param name="onReceive">
        /// The <see cref="Func{T1,T2}" /> that should be executed when the <paramref name="observableFunction" />
        /// value changes
        /// </param>
        /// <param name="onError">An optional <see cref="Action{T}" /> that should be executed in case of error</param>
        /// <param name="onCompleted">An optional <see cref="Action" /> that should be executed in case of completion</param>
        /// <typeparam name="TMessage">Any type of message that can be listen</typeparam>
        protected async Task RegisterAsyncListener<TMessage>(Func<Task<IObservable<TMessage>>> observableFunction, Func<TMessage, Task> onReceive, Action<Exception> onError = null, Action onCompleted = null)
        {
            onCompleted ??= () => { };

            var key = Guid.NewGuid();

            var observable = await observableFunction();
            var subscription = observable.SubscribeAsync(onReceive, x => _ = this.HandleErrorAndRecover(x, observableFunction, onReceive, onError, onCompleted, key), onCompleted);
            this.subscriptions[key] = subscription;
        }

        /// <summary>
        /// Handles the <see cref="IObservable{T}" />'s error and recover from it
        /// </summary>
        /// <param name="exception">The thrown <see cref="Exception" /></param>
        /// <param name="observableFunction">The <see cref="IObservable{T}" /> that had an error</param>
        /// <param name="onReceive">
        /// The <see cref="Action{T}" /> that should be executed when the <paramref name="observableFunction" />
        /// value changes
        /// </param>
        /// <param name="onError">The <see cref="Action{T}" /> that should be executed in case of error</param>
        /// <param name="onCompleted">The <see cref="Action" /> that should be executed in case of completion</param>
        /// <param name="key">The <see cref="Guid" /> that was used </param>
        /// <typeparam name="TMessage">Any type of message that can be listen</typeparam>
        private async Task HandleErrorAndRecover<TMessage>(Exception exception, Func<Task<IObservable<TMessage>>> observableFunction, Action<TMessage> onReceive, Action<Exception> onError, Action onCompleted, Guid key)
        {
            this.TryDisposeSubscription(key);
            onError?.Invoke(exception);
            await this.RegisterListener(observableFunction, onReceive, onError, onCompleted);
        }

        /// <summary>
        /// Handles the <see cref="IObservable{T}" />'s error and recover from it
        /// </summary>
        /// <param name="exception">The thrown <see cref="Exception" /></param>
        /// <param name="observableFunction">The <see cref="IObservable{T}" /> that had an error</param>
        /// <param name="onReceive">
        /// The <see cref="Func{T1,T2}" /> that should be executed when the <paramref name="observableFunction" />
        /// value changes
        /// </param>
        /// <param name="onError">The <see cref="Action{T}" /> that should be executed in case of error</param>
        /// <param name="onCompleted">The <see cref="Action" /> that should be executed in case of completion</param>
        /// <param name="key">The <see cref="Guid" /> that was used</param>
        /// <typeparam name="TMessage">Any type of message that can be listen</typeparam>
        private async Task HandleErrorAndRecover<TMessage>(Exception exception, Func<Task<IObservable<TMessage>>> observableFunction, Func<TMessage, Task> onReceive, Action<Exception> onError, Action onCompleted, Guid key)
        {
            this.TryDisposeSubscription(key);
            onError?.Invoke(exception);
            await this.RegisterAsyncListener(observableFunction, onReceive, onError, onCompleted);
        }

        /// <summary>
        /// Tries to dispose a listening subsctiption, store into the <see cref="subscriptions" /> dictionary
        /// </summary>
        /// <param name="key">The <see cref="IDisposable" /> identifier</param>
        private void TryDisposeSubscription(Guid key)
        {
            if (!this.subscriptions.TryGetValue(key, out var oldSubscription))
            {
                return;
            }

            oldSubscription.Dispose();
            this.subscriptions.Remove(key);
        }
    }
}
