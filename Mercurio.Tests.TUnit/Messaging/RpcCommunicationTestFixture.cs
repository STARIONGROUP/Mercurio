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
    using Mercurio.Extensions;
    using Mercurio.Messaging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using RabbitMQ.Client;
    using System.Globalization;

    [Category("Integration")]
    [NotInParallel]
    public class RpcCommunicationTestFixture
    {
        private IRpcClientService<string> rpcClientService;
        private IRpcServerService rpcServerService;
        private const string FirstConnectionName = "RabbitMQConnection1";
        private const string SecondConnectionName = "RabbitMQConnection2";

        [Before(HookType.Test)]
        public async Task Setup()
        {
            var serviceCollection = new ServiceCollection();
            
            serviceCollection.AddRabbitMqConnectionProvider()
                .WithRabbitMqConnectionFactory(RpcCommunicationTestFixture.FirstConnectionName,_ =>
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
                .WithRabbitMqConnectionFactory(RpcCommunicationTestFixture.SecondConnectionName,_ =>
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

            serviceCollection.AddTransient<IRpcServerService,RpcServerService>();
            serviceCollection.AddTransient<IRpcClientService<string>, RpcClientService<string>>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            this.rpcClientService = serviceProvider.GetRequiredService<IRpcClientService<string>>();
            this.rpcServerService = serviceProvider.GetRequiredService<IRpcServerService>();
            await Task.CompletedTask;
        }

        [After(HookType.Test)]
        public void Teardown()
        {
            this.rpcClientService.Dispose();
        }

        [Test]
        public async Task VerifyRpcProtocolAsync()
        {
            const string listeningQueue = "rpc_request";

            var disposable = await this.rpcServerService.ListenForRequestAsync<int, string>(RpcCommunicationTestFixture.FirstConnectionName, listeningQueue, RpcCommunicationTestFixture.OnReceiveAsync);
            var clientRequestObservable = await this.rpcClientService.SendRequestAsync(RpcCommunicationTestFixture.SecondConnectionName, listeningQueue, 41);
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
