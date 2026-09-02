// -------------------------------------------------------------------------------------------------
//  <copyright file="InvalidMessageException.cs" company="Starion Group S.A.">
//
//    Copyright 2025 - 2026 Starion Group S.A.
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

namespace Mercurio.Messaging
{
    using System.Text;

    using RabbitMQ.Client.Events;

    /// <summary>
    /// The <see cref="InvalidMessageException" /> is the <see cref="Exception" /> that is reported when a received message could not
    /// be deserialized into the type that is being listened for
    /// </summary>
    /// <remarks>
    /// The content of the received message is copied, so that the <see cref="InvalidMessageException" /> can safely be kept alive
    /// after the reception of the message has been handled, which is not the case of the <see cref="BasicDeliverEventArgs" />
    /// </remarks>
    public class InvalidMessageException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidMessageException" />
        /// </summary>
        /// <param name="messageType">The <see cref="Type" /> into which the received message could not be deserialized</param>
        /// <param name="deliverEventArgs">The <see cref="BasicDeliverEventArgs" /> of the received message</param>
        /// <param name="innerException">The <see cref="Exception" /> that has been thrown during the deserialization</param>
        /// <exception cref="ArgumentNullException">If any of the provided arguments is null</exception>
        public InvalidMessageException(Type messageType, BasicDeliverEventArgs deliverEventArgs, Exception innerException)
            : base($"The received message could not be deserialized into a {messageType?.Name}", innerException)
        {
            if (messageType == null)
            {
                throw new ArgumentNullException(nameof(messageType));
            }

            if (deliverEventArgs == null)
            {
                throw new ArgumentNullException(nameof(deliverEventArgs));
            }

            if (innerException == null)
            {
                throw new ArgumentNullException(nameof(innerException));
            }

            var properties = deliverEventArgs.BasicProperties;

            this.MessageType = messageType;
            this.Body = deliverEventArgs.Body.ToArray();
            this.Exchange = deliverEventArgs.Exchange;
            this.RoutingKey = deliverEventArgs.RoutingKey;
            this.ContentType = properties?.ContentType;
            this.CorrelationId = properties?.CorrelationId;
        }

        /// <summary>
        /// Gets the <see cref="Type" /> into which the received message could not be deserialized
        /// </summary>
        public Type MessageType { get; }

        /// <summary>
        /// Gets a copy of the raw content of the received message
        /// </summary>
        public byte[] Body { get; }

        /// <summary>
        /// Gets the name of the exchange the received message has been published to
        /// </summary>
        public string Exchange { get; }

        /// <summary>
        /// Gets the routing key that has been used to publish the received message
        /// </summary>
        public string RoutingKey { get; }

        /// <summary>
        /// Gets the content type of the received message
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// Gets the correlation id of the received message, if any
        /// </summary>
        public string CorrelationId { get; }

        /// <summary>
        /// Gets the raw content of the received message as an UTF8 <see cref="string" />
        /// </summary>
        /// <returns>The content of the received message</returns>
        public string GetBodyAsString()
        {
            return Encoding.UTF8.GetString(this.Body);
        }
    }
}
