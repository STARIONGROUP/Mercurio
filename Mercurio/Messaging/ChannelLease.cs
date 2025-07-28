// -------------------------------------------------------------------------------------------------
//  <copyright file="ChannelLease.cs" company="Starion Group S.A.">
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
    /// Represents an asynchronous lease for a RabbitMQ <see cref="IChannel"/>.
    /// Callers obtain a lease via <c>LeaseChannelAsync</c> and
    /// use <c>await using</c> so the channel is automatically returned
    /// to the pool when <see cref="DisposeAsync"/> completes.
    /// </summary>
    public readonly record struct ChannelLease : IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// Gets the owner of the leased channel, which must implement <see cref="ICanReleaseChannel"/>.
        /// </summary>
        private readonly ICanReleaseChannel owner;

        /// <summary>
        /// The name of the connection associated with this lease, used for logging or identification purposes.
        /// </summary>
        private readonly string connectionName;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelLease"/> struct with the specified channel and owner.
        /// </summary>
        /// <param name="connectionName">The <see cref="string"/> connection name</param>
        /// <param name="channel">
        /// Gets the leased <see cref="IChannel"/> instance.
        /// </param>
        /// <param name="owner">Gets the lessor <see cref="ICanReleaseChannel"/>, owner of the leasing</param>
        public ChannelLease(string connectionName, IChannel channel, ICanReleaseChannel owner)
        {
            this.connectionName = connectionName ?? throw new ArgumentNullException(nameof(connectionName), "A connection name must be provided.");
            this.Channel = channel ?? throw new ArgumentNullException(nameof(channel), "A channel must be provided.");
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner), "An owner must be provided.");
        }

        /// <summary>
        /// Gets the leased <see cref="IChannel"/> instance.
        /// </summary>
        public IChannel Channel { get; }

        /// <summary>
        /// Disposes of the leased channel, returning it to the owning pool.
        /// </summary>
        public void Dispose()
        {
            this.owner.ReleaseChannel(this.connectionName, this.Channel);
        }

        /// <summary>
        /// Asynchronously releases the leased channel back to the owning pool.
        /// </summary>
        /// <returns>
        /// A <see cref="ValueTask"/> that completes when the channel has been
        /// returned to the pool or disposed.
        /// </returns>
        public async ValueTask DisposeAsync()
        {
            await this.owner.ReleaseChannelAsync(this.connectionName, this.Channel);
        }
    }
}
