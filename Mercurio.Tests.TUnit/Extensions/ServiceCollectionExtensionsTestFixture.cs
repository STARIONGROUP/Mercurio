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

namespace Mercurio.Tests.TUnit.Extensions
{
    using global::TUnit.Assertions.AssertConditions.Throws;
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

    public class ServiceCollectionExtensionsTest
    {
        private IServiceCollection serviceCollection;

        [Before(HookType.Test)]
        public async Task Setup()
        {
            this.serviceCollection = new ServiceCollection();
            this.serviceCollection.AddLogging();
            await Task.CompletedTask;
        }

        [Test]
        public async Task VerifyProviderRegistrationWithOtherSerializers()
        {
            this.serviceCollection.AddRabbitMqConnectionProvider()
                .WithRabbitMqConnectionFactoryAsync("Primary", _ =>
                {
                    var connectionFactory = new ConnectionFactory()
                    {
                        HostName = "localhost",
                        Port = 5432
                    };
                    return Task.FromResult(connectionFactory);
                })
                .WithRabbitMqConnectionFactory("Secondary", _ =>
                {
                    var connectionFactory = new ConnectionFactory()
                    {
                        HostName = "localhost",
                        Port = 5433
                    };
                    return connectionFactory;
                })
                .WithSerialization(x => x.UseJson<NewtonSoftSerializer>()
                                         .UseMessagePack<MessagePackSerializer>(true));

            this.serviceCollection.AddTransient<IMessageClientService, MessageClientService>();

            var serviceProvider = this.serviceCollection.BuildServiceProvider();

            using var _ = Assert.Multiple();
            var deserializers = serviceProvider.GetService<IDictionary<SupportedSerializationFormat, IMessageDeserializerService>>();
            
            await Assert.That(serviceProvider.GetKeyedService<IMessageSerializerService>(SupportedSerializationFormat.Unspecified)).IsTypeOf<MessagePackSerializer>();
            await Assert.That(deserializers).HasCount(3);
            await Assert.That(serviceProvider.GetKeyedService<MessagePackSerializer>(SupportedSerializationFormat.MessagePack)).IsNotNull();
            await Assert.That(serviceProvider.GetKeyedService<NewtonSoftSerializer>(SupportedSerializationFormat.Json)).IsNotNull();
            await Assert.That(serviceProvider.GetRequiredService<ISerializationProviderService>()).IsNotNull();
            await Assert.That(serviceProvider.GetRequiredService<ISerializationProviderService>().ResolveDeserializer(SupportedSerializationFormat.Json)).IsTypeOf<NewtonSoftSerializer>();
            await Assert.That(serviceProvider.GetRequiredService<ISerializationProviderService>().ResolveDeserializer("application/json".ToSupportedSerializationFormat())).IsTypeOf<NewtonSoftSerializer>();
            await Assert.That(serviceProvider.GetRequiredService<ISerializationProviderService>().ResolveSerializer()).IsTypeOf<MessagePackSerializer>();
            await Assert.That(serviceProvider.GetRequiredService<ISerializationProviderService>().ResolveDeserializer()).IsTypeOf<MessagePackSerializer>();
        }

        [Test]
        public async Task VerifyProviderRegistration()
        {
            this.serviceCollection.AddRabbitMqConnectionProvider()
                .WithRabbitMqConnectionFactoryAsync("Primary", _ =>
                {
                    var connectionFactory = new ConnectionFactory()
                    {
                        HostName = "localhost",
                        Port = 5432
                    };

                    return Task.FromResult(connectionFactory);
                })
                .WithRabbitMqConnectionFactory("Secondary", _ =>
                {
                    var connectionFactory = new ConnectionFactory()
                    {
                        HostName = "localhost",
                        Port = 5433
                    };

                    return connectionFactory;
                })
                .WithSerialization();

            var serviceProvider = this.serviceCollection.BuildServiceProvider();
            var connectionProvider = serviceProvider.GetRequiredService<IRabbitMqConnectionProvider>();

            using var _ = Assert.Multiple();

            await Assert.That(() => connectionProvider.GetConnectionAsync("Primary"))
                .Throws<BrokerUnreachableException>();

            await Assert.That(() => connectionProvider.GetConnectionAsync("Secondary"))
                .Throws<BrokerUnreachableException>();

            await Assert.That(() => connectionProvider.GetConnectionAsync("NonRegister"))
                .Throws<ArgumentException>();

            await Assert.That(() => ((IDisposable)connectionProvider).Dispose()).ThrowsNothing();
        }

        [Test]
        public async Task VerifyConfigurationValidation()
        {
            this.serviceCollection.AddOptions<RetryPolicyConfiguration>()
                .Configure(configuration =>
                {
                    configuration.MaxConnectionRetryAttempts = -100;
                    configuration.TimeSpanBetweenAttempts = 1;
                })
                .ValidateDataAnnotations()
                .ValidateOnStart();

            await Assert.That(() =>
                this.serviceCollection.BuildServiceProvider().GetRequiredService<IOptions<RetryPolicyConfiguration>>().Value
            ).Throws<OptionsValidationException>();


            this.serviceCollection.AddOptions<RetryPolicyConfiguration>()
                .Configure(configuration =>
                {
                    configuration.MaxConnectionRetryAttempts = 4;
                    configuration.TimeSpanBetweenAttempts = 0;
                })
                .ValidateDataAnnotations()
                .ValidateOnStart();

            await Assert.That(() =>
                this.serviceCollection.BuildServiceProvider().GetRequiredService<IOptions<RetryPolicyConfiguration>>().Value
            ).Throws<OptionsValidationException>();


            this.serviceCollection.AddOptions<RetryPolicyConfiguration>()
                .Configure(configuration =>
                {
                    configuration.MaxConnectionRetryAttempts = 4;
                    configuration.TimeSpanBetweenAttempts = 1;
                })
                .ValidateDataAnnotations()
                .ValidateOnStart();

            await Assert
                .That(this.serviceCollection.BuildServiceProvider().GetRequiredService<IOptions<RetryPolicyConfiguration>>().Value)
                .IsNotNull();
        }

        private class MessagePackSerializer() : IMessageSerializerService, IMessageDeserializerService
        {
            public Task<TMessage> DeserializeAsync<TMessage>(Stream content, CancellationToken cancellationToken = default) =>
                throw new NotImplementedException("Attempt to serialize message using message pack");

            public Task<Stream> SerializeAsync(object obj, CancellationToken cancellationToken = default) =>
                throw new NotImplementedException("Attempt to serialize message using message pack");
        }

        private class NewtonSoftSerializer() : IMessageSerializerService, IMessageDeserializerService
        {
            public Task<TMessage> DeserializeAsync<TMessage>(Stream content, CancellationToken cancellationToken = default) =>
                throw new NotImplementedException("Attempt to serialize message using NewtonSoft");

            public Task<Stream> SerializeAsync(object obj, CancellationToken cancellationToken = default) =>
                throw new NotImplementedException("Attempt to serialize message using NewtonSoft");
        }
    }
}
