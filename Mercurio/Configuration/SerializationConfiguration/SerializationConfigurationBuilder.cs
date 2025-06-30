// -------------------------------------------------------------------------------------------------
//  <copyright file="SerializationConfigurationBuilder.cs" company="Starion Group S.A.">
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

namespace Mercurio.Configuration.SerializationConfiguration
{
    using Mercurio.Serializer;

    using Microsoft.Extensions.DependencyInjection;

    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides a fluent API to configure serialization and deserialization services, keyed by format.
    /// </summary>
    internal class SerializationConfigurationBuilder : ISerializationConfigurationBuilder
    {
        /// <summary>
        /// The default serialization format to use when no specific format is requested. Defaults to <see cref="SupportedSerializationFormat.Json"/>.
        /// </summary>
        private SupportedSerializationFormat defaultFormat = SupportedSerializationFormat.Json;

        /// <summary>
        /// Gets the collection of supported serialization formats that have been registered.
        /// </summary>
        private HashSet<SupportedSerializationFormat> supportedSerializationFormats { get; } = [];

        /// <summary>
        /// Gets the list of registration delegates to apply to the <see cref="IServiceCollection"/>.
        /// </summary>
        private List<Func<IServiceCollection, IServiceCollection>> Registrations { get; } = [];

        /// <summary>
        /// Registers the default JSON serialization service.
        /// </summary>
        public ISerializationConfigurationBuilder UseDefaultJson() => this.UseJson<JsonMessageSerializerService>();

        /// <summary>
        /// Registers a keyed JSON serialization/deserialization service.
        /// </summary>
        /// <typeparam name="TSerializationService">The service type implementing both serializer and deserializer interfaces.</typeparam>
        public ISerializationConfigurationBuilder UseJson<TSerializationService>()
            where TSerializationService : class, IMessageSerializerService, IMessageDeserializerService
        {
            this.supportedSerializationFormats.Add(SupportedSerializationFormat.Json);

            this.AddAsDefault<TSerializationService>();

            this.Registrations.Add(services => services
                .AddKeyedSingleton<TSerializationService, TSerializationService>(SupportedSerializationFormat.Json)
                .AddKeyedSingleton<IMessageSerializerService, TSerializationService>(SupportedSerializationFormat.Json)
                .AddKeyedSingleton<IMessageDeserializerService, TSerializationService>(SupportedSerializationFormat.Json));

            return this;
        }

        /// <summary>
        /// Registers a MessagePack serialization/deserialization service and optionally marks it as default.
        /// </summary>
        /// <typeparam name="TSerializationService">The service type implementing both serializer and deserializer interfaces.</typeparam>
        /// <param name="asDefault">Whether to use this format as the default.</param>
        public ISerializationConfigurationBuilder UseMessagePack<TSerializationService>(bool asDefault = true)
            where TSerializationService : class, IMessageSerializerService, IMessageDeserializerService
        {
            this.supportedSerializationFormats.Add(SupportedSerializationFormat.MessagePack);

            if (asDefault)
            {
                this.AddAsDefault<TSerializationService>(SupportedSerializationFormat.MessagePack);
            }

            this.Registrations.Add(services => services
                .AddKeyedSingleton<TSerializationService, TSerializationService>(SupportedSerializationFormat.MessagePack)
                .AddKeyedSingleton<IMessageSerializerService, TSerializationService>(SupportedSerializationFormat.MessagePack)
                .AddKeyedSingleton<IMessageDeserializerService, TSerializationService>(SupportedSerializationFormat.MessagePack));

            return this;
        }

        /// <summary>
        /// Registers the specified serialization service as the default implementation
        /// for both <see cref="IMessageSerializerService"/> and <see cref="IMessageDeserializerService"/>
        /// using the <see cref="SupportedSerializationFormat.Unspecified"/> key.
        /// </summary>
        /// <typeparam name="TSerializationService">
        /// The service type implementing both <see cref="IMessageSerializerService"/> and <see cref="IMessageDeserializerService"/>.
        /// </typeparam>
        /// <param name="defaultFormat">The default <see cref="SupportedSerializationFormat"/></param>
        private void AddAsDefault<TSerializationService>(SupportedSerializationFormat defaultFormat = SupportedSerializationFormat.Json) where TSerializationService : class, IMessageSerializerService, IMessageDeserializerService
        {
            if(defaultFormat != SupportedSerializationFormat.MessagePack && this.supportedSerializationFormats.Any(x => x == SupportedSerializationFormat.Unspecified))
            {
                return;
            }

            this.Registrations.Add(services => services
                .AddKeyedSingleton<IMessageSerializerService, TSerializationService>(SupportedSerializationFormat.Unspecified)
                .AddKeyedSingleton<IMessageDeserializerService, TSerializationService>(SupportedSerializationFormat.Unspecified));

            this.supportedSerializationFormats.Add(SupportedSerializationFormat.Unspecified);

            this.defaultFormat = defaultFormat;
        }

        /// <summary>
        /// Builds and applies the configured registrations to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The target service collection.</param>
        /// <returns>The modified <see cref="IServiceCollection"/> with the serialization services registered.</returns>
        internal IServiceCollection Build(IServiceCollection services)
        {
            if (this.Registrations.Count == 0)
            {
                this.UseDefaultJson();
            }

            foreach (var registration in this.Registrations)
            {
                registration(services);
            }

            services.AddSingleton<IDictionary<SupportedSerializationFormat, IMessageDeserializerService>>(provider => this.supportedSerializationFormats.ToDictionary(x => x, x => provider.GetRequiredKeyedService<IMessageDeserializerService>(x)));
            services.AddSingleton<IDictionary<SupportedSerializationFormat, IMessageSerializerService>>(provider => this.supportedSerializationFormats.ToDictionary(x => x, x => provider.GetRequiredKeyedService<IMessageSerializerService>(x)));

            services.AddSingleton<ISerializationProviderService>(provider =>
            {
                var serializers = provider.GetRequiredService<IDictionary<SupportedSerializationFormat, IMessageSerializerService>>();
                var deserializers = provider.GetRequiredService<IDictionary<SupportedSerializationFormat, IMessageDeserializerService>>();
                return new SerializationProviderService(serializers, deserializers, this.defaultFormat);
            });

            return services;
        }
    }
}
