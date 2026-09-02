// -------------------------------------------------------------------------------------------------
//  <copyright file="MessagingErrors.cs" company="Starion Group S.A.">
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
    using ErrorOr;

    using RabbitMQ.Client.Exceptions;

    /// <summary>
    /// Provides all <see cref="Error" /> that can be reported by the <see cref="IMessageClientService" /> when publishing a message
    /// with publisher confirmations enabled
    /// </summary>
    /// <remarks>
    /// The <see cref="Error" /> does not carry an <see cref="Exception" />, so the original <see cref="Exception" /> is provided
    /// through the <see cref="Error.Metadata" />, under the <see cref="ExceptionMetadataKey" /> key
    /// </remarks>
    public static class MessagingErrors
    {
        /// <summary>
        /// The <see cref="Error.Metadata" /> key that provides the <see cref="Exception" /> at the origin of the <see cref="Error" />
        /// </summary>
        public const string ExceptionMetadataKey = "exception";

        /// <summary>
        /// The <see cref="Error.Metadata" /> key that provides the amount of messages that have been acknowledged before a batch failed
        /// </summary>
        public const string PublishedCountMetadataKey = "publishedCount";

        /// <summary>
        /// The <see cref="Error.Metadata" /> key that provides the index of the message that failed within a batch
        /// </summary>
        public const string FailedIndexMetadataKey = "failedIndex";

        /// <summary>
        /// The <see cref="Error.Code" /> reported when the RabbitMQ server did not acknowledge the published message
        /// </summary>
        public const string NotAcknowledgedCode = "Mercurio.Publish.NotAcknowledged";

        /// <summary>
        /// The <see cref="Error.Code" /> reported when the message could not be published at all
        /// </summary>
        public const string PublishFailedCode = "Mercurio.Publish.Failed";

        /// <summary>
        /// The <see cref="Error.Code" /> reported when the connection that should be used is not registered
        /// </summary>
        public const string ConnectionNotRegisteredCode = "Mercurio.Publish.ConnectionNotRegistered";

        /// <summary>
        /// Provides the <see cref="Error" /> that reports that the RabbitMQ server did not acknowledge the published message
        /// </summary>
        /// <param name="exception">The <see cref="PublishException" /> that reports the negative acknowledgment</param>
        /// <returns>The <see cref="Error" /></returns>
        public static Error NotAcknowledged(PublishException exception)
        {
            return Error.Failure(NotAcknowledgedCode, $"The RabbitMQ server did not acknowledge the message: {exception?.Message}",
                new Dictionary<string, object>
                {
                    { ExceptionMetadataKey, exception },
                    { "isReturn", exception?.IsReturn },
                    { "publishSequenceNumber", exception?.PublishSequenceNumber }
                });
        }

        /// <summary>
        /// Provides the <see cref="Error" /> that reports that the message could not be published
        /// </summary>
        /// <param name="exception">The <see cref="Exception" /> that occured while publishing</param>
        /// <returns>The <see cref="Error" /></returns>
        public static Error PublishFailed(Exception exception)
        {
            return Error.Unexpected(PublishFailedCode, $"The message could not be published: {exception?.Message}",
                new Dictionary<string, object>
                {
                    { ExceptionMetadataKey, exception }
                });
        }

        /// <summary>
        /// Provides the <see cref="Error" /> that reports that the connection that should be used is not registered
        /// </summary>
        /// <param name="connectionName">The name of the connection that is not registered</param>
        /// <param name="exception">The <see cref="Exception" /> that reports the missing registration</param>
        /// <returns>The <see cref="Error" /></returns>
        public static Error ConnectionNotRegistered(string connectionName, Exception exception)
        {
            return Error.Failure(ConnectionNotRegisteredCode, $"No connection has been registered under the name {connectionName}",
                new Dictionary<string, object>
                {
                    { ExceptionMetadataKey, exception }
                });
        }

        /// <summary>
        /// Provides the <see cref="Error" /> that reports that a message of a batch could not be published, keeping the
        /// <see cref="Error.Type" />, <see cref="Error.Code" /> and <see cref="Error.Description" /> of the reported
        /// <paramref name="error" />
        /// </summary>
        /// <param name="error">The <see cref="Error" /> that has been reported for the failing message</param>
        /// <param name="publishedCount">The amount of messages that have been acknowledged before the failure</param>
        /// <param name="failedIndex">The index, within the batch, of the message that failed</param>
        /// <returns>The <see cref="Error" /></returns>
        /// <remarks>
        /// Since publishing a batch is not transactional, the messages that have been acknowledged before the failure are already held
        /// by the RabbitMQ server
        /// </remarks>
        public static Error BatchFailed(Error error, int publishedCount, int failedIndex)
        {
            var metadata = error.Metadata == null ? new Dictionary<string, object>() : new Dictionary<string, object>(error.Metadata);
            metadata[PublishedCountMetadataKey] = publishedCount;
            metadata[FailedIndexMetadataKey] = failedIndex;

            return Error.Custom(error.NumericType, error.Code, error.Description, metadata);
        }
    }
}
