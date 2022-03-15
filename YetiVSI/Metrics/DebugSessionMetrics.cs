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
using Metrics.Shared;

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Wrapper for IMetrics that can store a debug session ID and append it to events.
    /// </summary>
    public class DebugSessionMetrics : IMetrics
    {
        readonly IMetrics metricsService;

        /// <summary>
        /// Optional session ID to record with each event.
        /// If set, this value must come from IMetrics::NewDebugSessionId.
        /// </summary>
        public string DebugSessionId { get; set; }

        public DebugSessionMetrics(IMetrics metricsService)
        {
            this.metricsService = metricsService;
            // TODO: Create a new debug session ID inside this constructor.
        }

        [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
        protected DebugSessionMetrics() { }

        public void UseNewDebugSessionId() => DebugSessionId = NewDebugSessionId();

        public string NewDebugSessionId() => metricsService.NewDebugSessionId();

        public void RecordEvent(DeveloperEventType.Types.Type type, DeveloperLogEvent proto)
        {
            if (DebugSessionId != null)
            {
                proto = proto.Clone();
                proto.DebugSessionIdStr = DebugSessionId;
            }
            metricsService.RecordEvent(type, proto);
        }
    }
}
