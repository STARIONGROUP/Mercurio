// -------------------------------------------------------------------------------------------------
//  <copyright file="SupportedSerializationFormat.cs" company="Starion Group S.A.">
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

namespace Mercurio.Configuration.SerializationConfiguration
{
    /// <summary>
    /// Static values defining supported serialization formats for message serialization and deserialization provided by Mercurio.
    /// </summary>
    public static class SupportedSerializationFormat
    {
        /// <summary>
        /// When no format is specified
        /// </summary>
        public const string Unspecified = "";

        /// <summary>
        /// The JSON serialization format
        /// </summary>
        public const string JsonFormat = "application/json";
        
        /// <summary>
        /// The MessagePack serialization format
        /// </summary>
        public const string MessagePackFormat = "application/x-msgpack";
    }
}
