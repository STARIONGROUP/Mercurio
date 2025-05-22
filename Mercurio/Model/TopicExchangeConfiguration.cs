// -------------------------------------------------------------------------------------------------
//  <copyright file="TopicExchangeConfiguration.cs" company="Starion Group S.A.">
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

namespace Mercurio.Model
{
    using RabbitMQ.Client;

    /// <summary>
    /// The <see cref="TopicExchangeConfiguration" /> is an <see cref="BaseExchangeConfiguration" /> that support the
    /// <see cref="RabbitMQ.Client.ExchangeType.Topic" /> exchange
    /// </summary>
    public class TopicExchangeConfiguration : BaseExchangeConfiguration
    {
        /// <summary>
        /// Gets the name of the pre-defined exchange name
        /// </summary>
        public const string PreDefinedTopicExchangeName = "amq.topic";

        /// <summary>
        /// Gets the value of the routing key that should be use to act like a Fanout exchange
        /// </summary>
        public const string FanoutLikeRoutingKey = "#";

        /// <summary>Initializes a new instance of the <see cref="BaseExchangeConfiguration"></see> class.</summary>
        /// <param name="exchangeName">
        /// A specific name for the exchange to use. The default value is
        /// <see cref="PreDefinedTopicExchangeName" />.
        /// </param>
        /// <param name="routingKey">
        /// Defines a specific routing key to be used. The default value is
        /// <see cref="FanoutLikeRoutingKey" />
        /// </param>
        /// <param name="isTemporary">
        /// Asserts that the queue should be a temporary one or not. In case of a temporary queue, the queue is automatically deleted when the consumer
        /// of the queue disconnects.
        /// </param>
        /// <exception cref="ArgumentNullException">If any of the of string arguments is null</exception>
        /// <exception cref="ArgumentException">
        /// If the provided <paramref name="exchangeName" /> or <paramref name="routingKey" /> is empty. Moreover, the provided
        /// <paramref name="routingKey" /> should be a valid routing key.
        /// </exception>
        /// <remarks>
        /// A valid routing key is either equal to the <see cref="FanoutLikeRoutingKey" /> or should contains at least one '.'. If it contains '.', it can contains
        /// <see cref="FanoutLikeRoutingKey" />.
        /// Check the following <a href="https://www.rabbitmq.com/tutorials/tutorial-five-dotnet">tutorial</a> for more information
        /// </remarks>
        public TopicExchangeConfiguration(string exchangeName = PreDefinedTopicExchangeName, string routingKey = FanoutLikeRoutingKey, bool isTemporary = false) : base("", exchangeName, routingKey, isTemporary)
        {
            if (string.IsNullOrWhiteSpace(this.ExchangeName))
            {
                throw new ArgumentException("The exchange name cannot be empty", nameof(exchangeName));
            }

            if (string.IsNullOrWhiteSpace(this.RoutingKey))
            {
                throw new ArgumentException("The routing key cannot be empty", nameof(routingKey));
            }

            if (routingKey != FanoutLikeRoutingKey && !routingKey.Contains('.'))
            {
                throw new ArgumentException($"The routing key should either be {FanoutLikeRoutingKey} or contains at least one '.' to be valid", nameof(routingKey));
            }
        }

        /// <summary>
        /// Declares the message queue if not declared yet
        /// </summary>
        /// <param name="channel">The <see cref="IChannel" /> that will handle the queue</param>
        /// <param name="isDeclareForPush">Asserts that the declaration is used for a push action</param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        /// <exception cref="ArgumentNullException">If the provided <paramref name="channel" /> is null</exception>
        /// <exception cref="InvalidOperationException">
        /// If it is used to declare for push and that the routing key is either <see cref="FanoutLikeRoutingKey" />
        /// or contains any *
        /// </exception>
        public override Task EnsureQueueAndExchangeAreDeclaredAsync(IChannel channel, bool isDeclareForPush)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel), "The channel cannot be null");
            }

            if (isDeclareForPush && (this.RoutingKey == FanoutLikeRoutingKey || this.RoutingKey.Contains('*')))
            {
                throw new InvalidOperationException($"The provided routing key is not supported for a push. A valid push routing key should not be equal to {FanoutLikeRoutingKey} or contains any *");
            }

            return this.EnsureQueueAndExchangeAreDeclaredInternalAsync(channel, isDeclareForPush);
        }

        /// <summary>
        /// Declares the message queue if not declared yet
        /// </summary>
        /// <param name="channel">The <see cref="IChannel" /> that will handle the queue</param>
        /// <param name="isDeclareForPush">Asserts that the declaration is used for a push action</param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        private async Task EnsureQueueAndExchangeAreDeclaredInternalAsync(IChannel channel, bool isDeclareForPush)
        {
            await channel.ExchangeDeclareAsync(this.ExchangeName, ExchangeType.Topic, true);

            if (isDeclareForPush)
            {
                return;
            }

            var queueDeclared = await channel.QueueDeclareAsync();
            this.QueueName = queueDeclared.QueueName;

            await channel.QueueBindAsync(this.QueueName, this.ExchangeName, this.RoutingKey);
        }
    }
}
