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

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Metrics.Shared;
using YetiVSI.Util;
using static Metrics.Shared.VSIDebugEventBatch.Types;

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Non-thread safe implementation of <see cref="IEventBatch{TParams, TSummary}"/>. All
    /// CPU-intensive tasks are delegated to GetProtoDetails so Add can run quickly.
    /// </summary>
    public class DebugEventBatch : IEventBatch<DebugEventBatchParams, DebugEventBatchSummary>
    {
        readonly Dictionary<MethodInfo, List<DebugEventInterval>> _debugEvents =
            new Dictionary<MethodInfo, List<DebugEventInterval>>();

        long _firstInitialTimestampInMicro;
        long _lastFinalTimestampInMicro;

        public void Add(DebugEventBatchParams batchParams)
        {
            _lastFinalTimestampInMicro = batchParams.EndTimestampUs;
            if (_debugEvents.Count == 0)
            {
                _firstInitialTimestampInMicro = batchParams.StartTimestampUs;
            }

            _debugEvents.GetOrAddValue(batchParams.MethodInfo)
                .Add(new DebugEventInterval(batchParams.StartTimestampUs,
                                            batchParams.EndTimestampUs));
        }

        public DebugEventBatchSummary GetSummary() =>
            new DebugEventBatchSummary(GetProto(_debugEvents),
                                       _lastFinalTimestampInMicro - _firstInitialTimestampInMicro);

        VSIDebugEventBatch GetProto(
            Dictionary<MethodInfo, List<DebugEventInterval>> eventData)
        {
            var eventBatch = new VSIDebugEventBatch();
            if (eventData.Count > 0)
            {
                eventBatch.BatchStartTimestampMicroseconds = _firstInitialTimestampInMicro;
                eventBatch.DebugEvents.AddRange(
                    eventData.Select(a => CreateDebugEvent(a.Key, a.Value)));
            }
            return eventBatch;
        }

        static VSIDebugEvent CreateDebugEvent(MethodInfo eventMethodInfo,
                                              List<DebugEventInterval> eventInterval)
        {
            VSIDebugEvent debugEvent = new VSIDebugEvent()
            {
                MethodInfo = eventMethodInfo.GetProto(),
                TotalCount = eventInterval.Count
            };

            foreach (DebugEventInterval interval in eventInterval)
            {
                debugEvent.StartOffsetMicroseconds.Add(interval.StartOffsetUs);
                debugEvent.DurationMicroseconds.Add(interval.DurationUs);
            }

            return debugEvent;
        }

        readonly struct DebugEventInterval
        {
            public long StartOffsetUs { get; }
            public long DurationUs { get; }

            public DebugEventInterval(long startTimestampUs, long endTimestampUs)
            {
                StartOffsetUs = startTimestampUs;
                DurationUs = endTimestampUs - startTimestampUs;
            }
        }
    }

    public class DebugEventBatchParams
    {
        public MethodInfo MethodInfo { get; }
        public long StartTimestampUs { get; }
        public long EndTimestampUs { get; }

        public DebugEventBatchParams(MethodInfo methodInfo, long startTimestampUs,
                                     long endTimestampUs)
        {
            MethodInfo = methodInfo;
            StartTimestampUs = startTimestampUs;
            EndTimestampUs = endTimestampUs;
        }
    }

    public class DebugEventBatchSummary
    {
        public VSIDebugEventBatch Proto { get; }
        public long LatencyInMicroseconds { get; }

        public DebugEventBatchSummary(VSIDebugEventBatch proto, long latencyInMicroseconds)
        {
            Proto = proto;
            LatencyInMicroseconds = latencyInMicroseconds;
        }
    }
}
