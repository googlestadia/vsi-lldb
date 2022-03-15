// Copyright 2020 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Security.Cryptography;
using Metrics.Shared;
using Microsoft.VisualStudio.Threading;
using YetiCommon;

namespace Metrics
{
    // Stub implementation of Metrics service.
    public sealed class MetricsService : SMetrics, IMetrics
    {
        readonly RandomNumberGenerator _random;

        public MetricsService(JoinableTaskContext taskContext, Versions versions)
            : this(taskContext,
                   new CredentialConfig.Factory(new JsonUtil()),
                   new SdkConfig.Factory(new JsonUtil()),
                   RandomNumberGenerator.Create(),
                   versions)
        {
        }

        // Create a metrics service with custom configs; used for large tests.
        public MetricsService(JoinableTaskContext taskContext,
                              CredentialConfig.Factory credentialConfigFactory,
                              SdkConfig.Factory sdkConfigFactory, RandomNumberGenerator random,
                              Versions versions)
        {
            _random = random;
        }

        public string NewDebugSessionId() => MakeSessionId();

        public void RecordEvent(DeveloperEventType.Types.Type type, DeveloperLogEvent partialProto)
        {
        }

        /// <summary>
        /// Create session IDs used to identify related actions in metrics.
        /// 
        /// The returned value must be a random number and not identifying this user or machine.
        /// </summary>
        /// <returns>Debug session ID.</returns>
        string MakeSessionId()
        {
            byte[] bytes = new byte[16];
            _random.GetBytes(bytes);

            // Use Guid as a convenient byte[] -> string converter.
            // The result is 32 hexadecimal characters.
            var guid = new Guid(bytes);
            return guid.ToString("N");
        }
    }
}