// -------------------------------------------------------------------------------------------------
//  <copyright file="IExchangeConfiguration.cs" company="Starion Group S.A.">
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
    /// Base internal that defines configuration and declare queue and exchange, for a specific exchange type
    /// </summary>
    public interface IExchangeConfiguration
    {
        /// <summary>
        /// Gets the routing key that should be used
        /// </summary>
        string RoutingKey { get; }

        /// <summary>
        /// Gets the name of the used exchange
        /// </summary>
        string ExchangeName { get; }

        /// <summary>
        /// Asserts that the used queue should be a temporary one
        /// </summary>
        bool IsTemporary { get; }

        /// <summary>
        /// The name of the queue
        /// </summary>
        string QueueName { get; }
        
        /// <summary>
        /// Gets the name of the exchange that should be used to push message
        /// </summary>
        string PushExchangeName { get; }

        /// <summary>
        /// Declares the message queue if not declared yet
        /// </summary>
        /// <param name="channel">The <see cref="IChannel" /> that will handle the queue</param>
        /// <param name="isDeclareForPush">Asserts that the declaration is used for a push action</param>
        /// <returns>An awaitable <see cref="Task" /></returns>
        Task EnsureQueueAndExchangeAreDeclaredAsync(IChannel channel, bool isDeclareForPush);
    }
}
