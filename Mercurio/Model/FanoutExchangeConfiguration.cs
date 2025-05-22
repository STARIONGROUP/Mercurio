// -------------------------------------------------------------------------------------------------
//  <copyright file="FanoutExchangeConfiguration.cs" company="Starion Group S.A.">
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
    /// The <see cref="FanoutExchangeConfiguration" /> is a <see cref="BaseExchangeConfiguration"/> for the Fanout exchange
    /// </summary>
    public class FanoutExchangeConfiguration: BaseExchangeConfiguration
    {
        /// <summary>
        /// Gets the name of the exchange that should be used to push message
        /// </summary>
        public override string PushExchangeName => this.ExchangeName;

        /// <summary>
        /// Gets the name of the pre-defined exchange name
        /// </summary>
        public const string PreDefinedFanoutExchangeName = "amq.fanout";
        
        /// <summary>Initializes a new instance of the <see cref="FanoutExchangeConfiguration"></see> class.</summary>
        /// <param name="exchangeName">A specific name for the exchange to use. Default value is <see cref="PreDefinedFanoutExchangeName"/>. In case of empty, an exchange based on the queue name will be created.</param>
        /// <param name="routingKey">Defines a specific routing key to be used. Default value is <i>string.empty</i>.</param>
        /// <param name="isTemporary">
        /// Asserts that the queue should be a temporary one or not. In case of a temporary queue, the queue is automatically deleted when the consumer
        /// of the queue disconnects.
        /// </param>
        /// <exception cref="ArgumentNullException">If any of the of string arguments is null</exception>
        public FanoutExchangeConfiguration(string exchangeName = PreDefinedFanoutExchangeName, string routingKey = "", bool isTemporary = false) : base("", exchangeName, routingKey, isTemporary)
        {
            if (string.IsNullOrEmpty(this.ExchangeName))
            {
                throw new ArgumentException("The exchange name cannot be empty", nameof(exchangeName));
            }
        }

        /// <summary>
        /// Declares the message queue if not declared yet
        /// </summary>
        /// <param name="channel">The <see cref="IChannel" /> that will handle the queue</param>
        /// <param name="isDeclareForPush">Asserts that the declaration is used for a push action</param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        public override async Task EnsureQueueAndExchangeAreDeclaredAsync(IChannel channel, bool isDeclareForPush)
        {
            await base.EnsureQueueAndExchangeAreDeclaredAsync(channel, isDeclareForPush);
            await channel.ExchangeDeclareAsync(this.ExchangeName, ExchangeType.Fanout, durable: true);

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
