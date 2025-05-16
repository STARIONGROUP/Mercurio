// -------------------------------------------------------------------------------------------------
//  <copyright file="RetryPolicyConfiguration.cs" company="Starion Group S.A.">
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

namespace Mercurio.Configuration
{
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// The <see cref="RetryPolicyConfiguration" /> provides Polly configuration values that could be configured to be resilient while establishing a connection against
    /// RabbitMQ
    /// </summary>
    public class RetryPolicyConfiguration
    {
        /// <summary>
        /// Define the maximum connection retry attempts that have to be performed
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Please provide a value greater than 0")]
        public int MaxConnectionRetryAttempts { get; set; } = 4;
        
        /// <summary>
        /// Define the timespan to wait in case of failure before retrying, expressed in seconds
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Please provide a value strickly greater than 0")]
        public int TimeSpanBetweenAttempts { get; set; } = 1;
    }
}
