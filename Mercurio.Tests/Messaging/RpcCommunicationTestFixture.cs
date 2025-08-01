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

namespace Mercurio.Tests.Messaging
{
    using System.Diagnostics;
    using System.Globalization;

    using Mercurio.Extensions;
    using Mercurio.Messaging;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using RabbitMQ.Client;

    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public class RpcCommunicationTestFixture
    {
        private IRpcClientService<string> rpcClientService;
        private IRpcServerService rpcServerService;
        private ServiceProvider serviceProvider;
        private const string FirstConnectionName = "RabbitMQConnection1";
        private const string SecondConnectionName = "RabbitMQConnection2";

        [SetUp]
        public void Setup()
        {
            var activityListener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData        
            };
            
            ActivitySource.AddActivityListener(activityListener);
            
            var serviceCollection = new ServiceCollection();
            
            serviceCollection.AddRabbitMqConnectionProvider()
                .WithRabbitMqConnectionFactory(FirstConnectionName,_ =>
                {
                    var connectionFactory = new ConnectionFactory
                    {
                        HostName = "localhost",
                        Port = 5672,
                        UserName = "guest",
                        Password = "guest"
                    };
                    
                    return connectionFactory;
                }, new ActivitySource(FirstConnectionName))
                .WithRabbitMqConnectionFactory(SecondConnectionName,_ =>
                {
                    var connectionFactory = new ConnectionFactory
                    {
                        HostName = "localhost",
                        Port = 5672,
                        UserName = "guest",
                        Password = "guest"
                    };
                    
                    return connectionFactory;
                }, new ActivitySource(SecondConnectionName))
                .WithSerialization()
                .AddLogging(x => x.AddConsole());

            serviceCollection.AddTransient<IRpcServerService,RpcServerService>();
            serviceCollection.AddTransient<IRpcClientService<string>, RpcClientService<string>>();
            this.serviceProvider = serviceCollection.BuildServiceProvider();
            this.rpcClientService = this.serviceProvider.GetRequiredService<IRpcClientService<string>>();
            this.rpcServerService = this.serviceProvider.GetRequiredService<IRpcServerService>();
        }

        [TearDown]
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
            Assert.That(taskComplettion.Task.Result, Is.EqualTo("41"));
            disposable.Dispose();
        }

        [Test]
        public async Task VerifyActivites()
        {
            const string listeningQueue = "rpc_request";
            Assert.That(Activity.Current, Is.Null);

            var activities = new HashSet<Activity>();
            Activity.CurrentChanged += ActivityOnCurrentChanged;

            using var newActivity = new Activity("Parent").Start();
            activities.Add(newActivity);
            var disposable = await this.rpcServerService.ListenForRequestAsync<int, string>(FirstConnectionName, listeningQueue, OnReceiveAsync, activityName:"Server");
            var clientRequestObservable = await this.rpcClientService.SendRequestAsync(SecondConnectionName, listeningQueue, 41, activityName:"Client" );
            var taskComplettion = new TaskCompletionSource<string>();
            clientRequestObservable.Subscribe(result => taskComplettion.SetResult(result));

            await Task.Delay(50);
            
            await taskComplettion.Task;

            Assert.Multiple(() =>
            {
                Assert.That(activities, Has.Count.EqualTo(4));
                Assert.That(activities.ElementAt(0).OperationName, Is.EqualTo("Parent"));
                Assert.That(activities.ElementAt(1).OperationName, Is.EqualTo("Client - Request"));
                Assert.That(activities.ElementAt(2).OperationName, Is.EqualTo("Server"));
                Assert.That(activities.ElementAt(3).OperationName, Is.EqualTo("Client"));

                foreach (var activity in activities)
                {
                    Assert.That(activity.TraceId.ToString(), Is.EqualTo(newActivity.TraceId.ToString()));
                }
            });
            
            disposable.Dispose();
            Activity.CurrentChanged -= ActivityOnCurrentChanged;
            
            void ActivityOnCurrentChanged(object sender, ActivityChangedEventArgs e)
            {
                if (e.Current != null && e.Current.Source.Name is FirstConnectionName or SecondConnectionName)
                {
                    activities.Add(e.Current!);
                }
            }
        }

        private static Task<string> OnReceiveAsync(int arg)
        {
            return Task.FromResult(arg.ToString(CultureInfo.InvariantCulture));
        }
    }
}
