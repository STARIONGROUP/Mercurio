// -------------------------------------------------------------------------------------------------
//  <copyright file="RuntimeTracingTestFixture.cs" company="Starion Group S.A.">
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

namespace Mercurio.Tests.Messaging
{
    using System.Diagnostics;
    using System.Globalization;

    using Mercurio.Extensions;
    using Mercurio.Messaging;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    using OpenTelemetry.Exporter;
    using OpenTelemetry.Logs;
    using OpenTelemetry.Trace;

    using RabbitMQ.Client;

    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public class RuntimeTracingTestFixture
    {
        private IServiceProvider serviceProvider;
        private IHost host;
        private IRpcClientService<string> rpcClientService;
        private IRpcServerService rpcServerService;
        private ILogger<RuntimeTracingTestFixture> logger;
        private const string FirstConnectionName = "RabbitMQConnection1";
        private const string SecondConnectionName = "RabbitMQConnection2";

        [OneTimeSetUp]
        public void Setup()
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            
            var otlpOptions = (OtlpExporterOptions x) =>
            {
                x.Endpoint = new Uri("http://localhost:4317");
                x.Protocol = OtlpExportProtocol.Grpc;
            };
            
            builder.Logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;
                options.AddOtlpExporter(otlpOptions);
            });

            builder.Services.AddOpenTelemetry()
                .WithTracing(traceBuilder =>
                {
                    traceBuilder.AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddSource("Local", FirstConnectionName, SecondConnectionName)
                        .AddOtlpExporter(otlpOptions);
                })
                .WithLogging(x => x.AddOtlpExporter(otlpOptions));

            builder.Services.AddRabbitMqConnectionProvider()
                .WithRabbitMqConnectionFactory(FirstConnectionName, _ =>
                {
                    var connectionFactory = new ConnectionFactory
                    {
                        HostName = "localhost",
                        Port = 5672,
                        UserName = "guest",
                        Password = "guest"
                    };

                    return connectionFactory;
                }, FirstConnectionName)
                .WithRabbitMqConnectionFactory(SecondConnectionName, _ =>
                {
                    var connectionFactory = new ConnectionFactory
                    {
                        HostName = "localhost",
                        Port = 5672,
                        UserName = "guest",
                        Password = "guest"
                    };

                    return connectionFactory;
                }, SecondConnectionName)
                .WithSerialization();
            
            builder.Services.AddTransient<IRpcServerService,RpcServerService>();
            builder.Services.AddTransient<IRpcClientService<string>, RpcClientService<string>>();
            
            this.host = builder.Build();
            this.serviceProvider = this.host.Services;
            this.rpcClientService = this.serviceProvider.GetRequiredService<IRpcClientService<string>>();
            this.rpcServerService = this.serviceProvider.GetRequiredService<IRpcServerService>();
            this.logger = this.serviceProvider.GetRequiredService<ILogger<RuntimeTracingTestFixture>>();
            this.host.Start();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            this.host.Dispose();
            this.rpcClientService.Dispose();
        }

        [Test]
        public async Task VerifyTracing()
        {
            const string listeningQueue = "rpc_request";
            Assert.That(Activity.Current, Is.Null);

            var activitySource = new ActivitySource("Local");

            using var newActivity = activitySource.StartActivity("Parent", ActivityKind.Consumer, null);
            var disposable = await this.rpcServerService.ListenForRequestAsync<int, string>(FirstConnectionName, listeningQueue, OnReceiveAsync, activityName:"Server");
            var clientRequestObservable = await this.rpcClientService.SendRequestAsync(SecondConnectionName, listeningQueue, 41, activityName:"Client" );
            var taskComplettion = new TaskCompletionSource<string>();
            clientRequestObservable.Subscribe(result => taskComplettion.SetResult(result));

            await Task.Delay(50);
            
            await taskComplettion.Task;
            disposable.Dispose();
            this.logger.LogInformation("Integration Test over");
        }
        
        private static Task<string> OnReceiveAsync(int arg)
        {
            return Task.FromResult(arg.ToString(CultureInfo.InvariantCulture));
        }
    }
}
