// -------------------------------------------------------------------------------------------------
//  <copyright file="ExchangeType.cs" company="Starion Group S.A.">
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

namespace Mercurio.Enumeration
{
    /// <summary>
    /// The <see cref="ExchangeType" /> defines differents exchange type possibilites that could be use for the RabbitMQ Channel scope
    /// </summary>
    public enum ExchangeType
    {
        /// <summary>
        /// Default RabbitMQ exchange type. More information about this exchange type can be found <a href="https://www.rabbitmq.com/docs/exchanges#default-exchange">here</a>.
        /// </summary>
        Default,
        
        /// <summary>
        /// Exchange type used for AMQP direct exchanges. More information about this exchange type can be found <a href="https://www.rabbitmq.com/docs/exchanges#direct">here</a>.
        /// </summary>
        Direct,
        
        /// <summary>
        /// Exchange type used for AMQP direct exchanges. This exchange type provide message broadcasting capabilities.
        /// More information about this exchange type can be found <a href="https://www.rabbitmq.com/docs/exchanges#direct">here</a>.
        /// </summary>
        Fanout,
        
        /// <summary>
        /// Exchange type used for AMQP headers exchanges.
        /// </summary>
        Headers,
        
        /// <summary>
        /// Exchange type used for AMQP direct exchanges. More information about this exchange type can be found <a href="https://www.rabbitmq.com/docs/exchanges#topic">here</a>.
        /// </summary>
        Topic,
        
        /// <summary>
        /// Exchange type use for AMQP RPC exchanges. More information about this exchange type can be found  <a href="https://www.rabbitmq.com/docs/local-random-exchange">here</a>.
        /// <remarks>This kind of exchange requires to have at least RabbitMQ 4.0 </remarks>
        /// </summary>
        Rpc
    }
}
