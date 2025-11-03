// -------------------------------------------------------------------------------------------------
//  <copyright file="RpcServerBackgroundService.cs" company="Starion Group S.A.">
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

namespace Mercurio.Hosting
{
    using Mercurio.Messaging;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The <see cref="RpcServerBackgroundService" /> is a specific <see cref="MessagingBackgroundService" /> that provides
    /// RPC server capabilities
    /// </summary>
    public abstract class RpcServerBackgroundService : MessagingBackgroundService, IRpcServerBackgroundService
    {
        /// <summary>
        /// Gets the collection of <see cref="IDisposable" /> that stores RPC action to perform that needs to be disposed
        /// </summary>
        protected readonly List<IDisposable> RpcSubscriptions = [];
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcServerBackgroundService" />
        /// </summary>
        /// <param name="serviceProvider">
        /// The injected <see cref="IServiceProvider" /> that allow to resolve
        /// <see cref="IMessageClientService" /> instance, even if not registered as scope
        /// </param>
        /// <param name="logger">The injected <see cref="ILogger{TCategory}" /> to allow logging</param>
        /// <param name="configuration">The injected <see cref="IConfiguration" /> to provides configuration information for service initialization</param>
        protected RpcServerBackgroundService(IServiceProvider serviceProvider, ILogger<RpcServerBackgroundService> logger, IConfiguration configuration)
            : base(serviceProvider, logger, configuration)
        {
            this.RpcServerService = this.ServiceProvider.GetRequiredService<IRpcServerService>();
        }

        /// <summary>
        /// Gets the resolved <see cref="IRpcServerService" /> that will provides RPC server features
        /// </summary>
        protected IRpcServerService RpcServerService { get; }

        /// <inheritdoc />
        public override void Dispose()
        {
            base.Dispose();

            foreach (var subscription in this.RpcSubscriptions)
            {
                subscription.Dispose();
            }
            
            this.RpcSubscriptions.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
