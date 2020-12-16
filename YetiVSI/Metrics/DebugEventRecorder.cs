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

﻿using System.Reflection;
using YetiCommon.MethodRecorder;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Records information about debug events and sends that data, aggregated into batches, to
    /// a debug session metrics.
    /// </summary>
    public class DebugEventRecorder : IMethodInvocationRecorder
    {
        private readonly IDebugEventAggregator debugEventAggregator;
        private readonly IMetrics metrics;

        public DebugEventRecorder(
            IDebugEventAggregator debugEventAggregator, IMetrics metrics)
        {
            this.debugEventAggregator = debugEventAggregator;
            this.metrics = metrics;

            debugEventAggregator.BatchSummaryReady +=
                (_, batchSummary) => OnBatchSummaryReady(batchSummary);
        }

        public void Record(MethodInfo methodInfo, long startTimestampUs, long endTimestampUs) =>
            debugEventAggregator.Add(methodInfo, startTimestampUs, endTimestampUs);

        private void OnBatchSummaryReady(DebugEventBatchSummary batchSummary)
        {
            var logEvent = new DeveloperLogEvent
            {
                DebugEventBatch = batchSummary.Proto,
                StatusCode = DeveloperEventStatus.Types.Code.Success,
                LatencyMilliseconds = MillisFromMicros(batchSummary.LatencyInMicroseconds),
                LatencyType = DeveloperLogEvent.Types.LatencyType.LatencyTool
            };
            metrics.RecordEvent(DeveloperEventType.Types.Type.VsiDebugEventBatch, logEvent);
        }

        private long MillisFromMicros(long valueInMicroseconds) => valueInMicroseconds / 1000;
    }
}
