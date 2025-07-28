// -------------------------------------------------------------------------------------------------
//  <copyright file="ICanReleaseChannel.cs" company="Starion Group S.A.">
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

namespace Mercurio.Messaging
{
    using RabbitMQ.Client;

    /// <summary>
    /// This interface defines a contract for releasing a channel.
    /// </summary>
    public interface ICanReleaseChannel
    {
        /// <summary>
        /// Returns the current channel to the pool if it is still open, or disposes it if it is closed or null.
        /// </summary>
        /// <param name="connectionName"> The name of the connection associated with the channel, used for logging or identification purposes.</param>
        /// <param name="channel">The <see cref="IChannel"/> to release back to the pool</param>
        /// <remarks>
        /// This method should always be called after a channel is obtained using <c>GetChannelAsync</c>, typically in a <c>finally</c> block,
        /// to ensure proper resource management and to maintain pool capacity. Or even better using <see cref="ChannelLease"/>
        /// </remarks>
        /// <returns>The <see cref="Task"/> that represents the completion of the return operation. </returns>
        Task ReleaseChannelAsync(string connectionName, IChannel channel);

        /// <summary>
        /// Returns the current channel to the pool if it is still open, or disposes it if it is closed or null.
        /// </summary>
        /// <param name="connectionName"> The name of the connection associated with the channel, used for logging or identification purposes.</param>
        /// <param name="channel">The <see cref="IChannel"/> to release back to the pool</param>
        /// <remarks>
        /// This method should always be called after a channel is obtained using <c>GetChannelAsync</c>, typically in a <c>finally</c> block,
        /// to ensure proper resource management and to maintain pool capacity. Or even better using <see cref="ChannelLease"/>
        /// </remarks>
        void ReleaseChannel(string connectionName, IChannel channel);
    }
}
