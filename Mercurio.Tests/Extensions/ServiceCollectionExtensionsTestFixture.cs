// -------------------------------------------------------------------------------------------------
//  <copyright file="ServiceCollectionExtensionsTestFixture.cs" company="Starion Group S.A.">
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

namespace Mercurio.Tests.Extensions
{
    using Mercurio.Configuration.IConfiguration;
    using Mercurio.Configuration.SerializationConfiguration;
    using Mercurio.Extensions;
    using Mercurio.Messaging;
    using Mercurio.Provider;
    using Mercurio.Serializer;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    using RabbitMQ.Client;
    using RabbitMQ.Client.Exceptions;

    [TestFixture]
    public class ServiceCollectionExtensionsTestFixture
    {
        private IServiceCollection serviceCollection;

        [SetUp]
        public void Setup()
        {
            this.serviceCollection = new ServiceCollection();
            this.serviceCollection.AddLogging();
        }

        [Test]
        public void VerifyConfigurationValidation()
        {
            this.serviceCollection.AddOptions<RetryPolicyConfiguration>()
                .Configure(configuration =>
                {
                    configuration.MaxConnectionRetryAttempts = -100;
                    configuration.TimeSpanBetweenAttempts = 1;
                })
                .ValidateDataAnnotations()
                .ValidateOnStart();

            Assert.That(() => this.serviceCollection.BuildServiceProvider().GetRequiredService<IOptions<RetryPolicyConfiguration>>().Value, Throws.Exception.TypeOf<OptionsValidationException>());

            this.serviceCollection.AddOptions<RetryPolicyConfiguration>()
                .Configure(configuration =>
                {
                    configuration.MaxConnectionRetryAttempts = 4;
                    configuration.TimeSpanBetweenAttempts = 0;
                })
                .ValidateDataAnnotations()
                .ValidateOnStart();

            Assert.That(() => this.serviceCollection.BuildServiceProvider().GetRequiredService<IOptions<RetryPolicyConfiguration>>().Value, Throws.Exception.TypeOf<OptionsValidationException>());

            this.serviceCollection.AddOptions<RetryPolicyConfiguration>()
                .Configure(configuration =>
                {
                    configuration.MaxConnectionRetryAttempts = 4;
                    configuration.TimeSpanBetweenAttempts = 1;
                })
                .ValidateDataAnnotations()
                .ValidateOnStart();

            Assert.That(this.serviceCollection.BuildServiceProvider().GetRequiredService<IOptions<RetryPolicyConfiguration>>().Value, Is.Not.Null);
        }

        [Test]
        public async Task VerifyProviderRegistration()
        {
            this.serviceCollection.AddRabbitMqConnectionProvider()
                .WithRabbitMqConnectionFactoryAsync("Primary", _ =>
                {
                    var connectionFactory = new ConnectionFactory
                    {
                        HostName = "localhost",
                        Port = 5432
                    };

                    return Task.FromResult(connectionFactory);
                })
                .WithRabbitMqConnectionFactory("Secondary", _ =>
                {
                    var connectionFactory = new ConnectionFactory
                    {
                        HostName = "localhost",
                        Port = 5433
                    };

                    return connectionFactory;
                })
                .WithSerialization();

            var serviceProvider = this.serviceCollection.BuildServiceProvider();
            var connectionProvider = serviceProvider.GetRequiredService<IRabbitMqConnectionProvider>();

            using (Assert.EnterMultipleScope())
            {
                await Assert.ThatAsync(() => connectionProvider.GetConnectionAsync("Primary"), Throws.Exception.TypeOf<BrokerUnreachableException>());
                await Assert.ThatAsync(() => connectionProvider.GetConnectionAsync("Secondary"), Throws.Exception.TypeOf<BrokerUnreachableException>());
                await Assert.ThatAsync(() => connectionProvider.GetConnectionAsync("NonRegister"), Throws.ArgumentException);
                Assert.That(() => ((IDisposable)connectionProvider).Dispose(), Throws.Nothing);
            };
        }

        [Test]
        public void VerifyProviderRegistrationWithOtherSerializers()
        {
            this.serviceCollection.AddRabbitMqConnectionProvider()
                .WithRabbitMqConnectionFactoryAsync("Primary", _ =>
                {
                    var connectionFactory = new ConnectionFactory
                    {
                        HostName = "localhost",
                        Port = 5432
                    };

                    return Task.FromResult(connectionFactory);
                })
                .WithRabbitMqConnectionFactory("Secondary", _ =>
                {
                    var connectionFactory = new ConnectionFactory
                    {
                        HostName = "localhost",
                        Port = 5433
                    };

                    return connectionFactory;
                })
                .WithSerialization(x => x.UseJson<NewtonSoftSerializer>().UseMessagePack<MessagePackSerializer>());

            this.serviceCollection.AddTransient<IMessageClientService, MessageClientService>();

            var serviceProvider = this.serviceCollection.BuildServiceProvider();
            var deserializers = serviceProvider.GetService<IDictionary<string, IMessageDeserializerService>>();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(serviceProvider.GetKeyedService<IMessageSerializerService>(SupportedSerializationFormat.Unspecified), Is.InstanceOf<MessagePackSerializer>());
                Assert.That(deserializers, Has.Count.EqualTo(3));
                Assert.That(() => serviceProvider.GetKeyedService<MessagePackSerializer>(SupportedSerializationFormat.MessagePackFormat), Is.Not.Null, "MessagePackSerializer should be registered and resolved successfully.");
                Assert.That(() => serviceProvider.GetKeyedService<NewtonSoftSerializer>(SupportedSerializationFormat.JsonFormat), Is.Not.Null, "NewtonSoftSerializer should be registered and resolved successfully.");
                Assert.That(() => serviceProvider.GetRequiredService<ISerializationProviderService>(), Is.Not.Null, "ISerializationProviderService should be registered and resolved successfully.");
                Assert.That(() => serviceProvider.GetRequiredService<ISerializationProviderService>().ResolveDeserializer(SupportedSerializationFormat.JsonFormat), Is.InstanceOf<NewtonSoftSerializer>());
                Assert.That(() => serviceProvider.GetRequiredService<ISerializationProviderService>().ResolveDeserializer("application/json"), Is.InstanceOf<NewtonSoftSerializer>());
                Assert.That(() => serviceProvider.GetRequiredService<ISerializationProviderService>().ResolveSerializer(), Is.InstanceOf<MessagePackSerializer>());
                Assert.That(() => serviceProvider.GetRequiredService<ISerializationProviderService>().ResolveDeserializer(), Is.InstanceOf<MessagePackSerializer>());
            };
        }

        private class MessagePackSerializer : IMessageSerializerService, IMessageDeserializerService
        {
            public Task<TMessage> DeserializeAsync<TMessage>(Stream content, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException("Attempt to serialize message using message pack");
            }

            public Task<Stream> SerializeAsync(object obj, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException("Attempt to serialize message using message pack");
            }
        }

        private class NewtonSoftSerializer : IMessageSerializerService, IMessageDeserializerService
        {
            public Task<TMessage> DeserializeAsync<TMessage>(Stream content, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException("Attempt to serialize message using NewtonSoft");
            }

            public Task<Stream> SerializeAsync(object obj, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException("Attempt to serialize message using NewtonSoft");
            }
        }
    }
}
