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
        readonly IBatchEventAggregator<DebugEventBatchParams, DebugEventBatchSummary> _batchEventAggregator;
        readonly IMetrics _metrics;

        public DebugEventRecorder(
            IBatchEventAggregator<DebugEventBatchParams, DebugEventBatchSummary>
                batchEventAggregator, IMetrics metrics)
        {
            _batchEventAggregator = batchEventAggregator;
            _metrics = metrics;

            batchEventAggregator.BatchSummaryReady +=
                (_, batchSummary) => OnBatchSummaryReady(batchSummary);
        }

        public void Record(MethodInfo methodInfo, long startTimestampUs, long endTimestampUs) =>
            _batchEventAggregator.Add(
                new DebugEventBatchParams(methodInfo, startTimestampUs, endTimestampUs));

        void OnBatchSummaryReady(DebugEventBatchSummary batchSummary)
        {
            var debugBatchSummary = batchSummary;
            var logEvent = new DeveloperLogEvent
            {
                DebugEventBatch = debugBatchSummary.Proto,
                StatusCode = DeveloperEventStatus.Types.Code.Success,
                LatencyMilliseconds = MillisFromMicros(debugBatchSummary.LatencyInMicroseconds),
                LatencyType = DeveloperLogEvent.Types.LatencyType.LatencyTool
            };
            _metrics.RecordEvent(DeveloperEventType.Types.Type.VsiDebugEventBatch, logEvent);
        }

        long MillisFromMicros(long valueInMicroseconds) => valueInMicroseconds / 1000;
    }
}
