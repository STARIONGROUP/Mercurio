// -------------------------------------------------------------------------------------------------
//  <copyright file="RpcCommunicationTestFixture.cs" company="Starion Group S.A.">
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

namespace Mercurio.Tests.TUnit.Messaging
{
    using System.Globalization;

    using Mercurio.Extensions;
    using Mercurio.Messaging;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using RabbitMQ.Client;

    [Category("Integration")]
    [NotInParallel]
    public class RpcCommunicationTestFixture
    {
        private const string FirstConnectionName = "RabbitMQConnection1";
        private const string SecondConnectionName = "RabbitMQConnection2";
        private IRpcClientService<string> rpcClientService;
        private IRpcServerService rpcServerService;
        private ServiceProvider serviceProvider;

        [Before(Test)]
        public async Task Setup()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddRabbitMqConnectionProvider()
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
                })
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
                })
                .WithSerialization()
                .AddLogging(x => x.AddConsole());

            serviceCollection.AddTransient<IRpcServerService, RpcServerService>();
            serviceCollection.AddTransient<IRpcClientService<string>, RpcClientService<string>>();
            this.serviceProvider = serviceCollection.BuildServiceProvider();
            this.rpcClientService = this.serviceProvider.GetRequiredService<IRpcClientService<string>>();
            this.rpcServerService = this.serviceProvider.GetRequiredService<IRpcServerService>();
            await Task.CompletedTask;
        }

        [After(Test)]
        public void Teardown()
        {
            this.rpcClientService.Dispose();
            this.serviceProvider.Dispose();
        }

        [Test]
        public async Task VerifyRpcProtocolAsync()
        {
            const string listeningQueue = "rpc_request";

            var disposable = await this.rpcServerService.ListenForRequestAsync<int, string>(FirstConnectionName, listeningQueue, OnReceiveAsync);
            var clientRequestObservable = await this.rpcClientService.SendRequestAsync(SecondConnectionName, listeningQueue, 41);
            var taskComplettion = new TaskCompletionSource<string>();
            clientRequestObservable.Subscribe(result => taskComplettion.SetResult(result));

            await Task.Delay(50);

            await taskComplettion.Task;
            await Assert.That(taskComplettion.Task.Result).IsEqualTo("41");
            disposable.Dispose();
        }

        private static Task<string> OnReceiveAsync(int arg)
        {
            return Task.FromResult(arg.ToString(CultureInfo.InvariantCulture));
        }
    }
}
