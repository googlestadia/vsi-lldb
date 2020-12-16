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

﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using YetiVSI.Util;
using YetiVSI.Shared.Metrics;
using static YetiVSI.Shared.Metrics.VSIDebugEventBatch.Types;

namespace YetiVSI.Metrics
{
    /// <summary>
    /// Stores debug events and generates a <see cref="DebugEventBatchSummary"/> based on them.
    /// </summary>
    public interface IDebugEventBatch
    {
        /// <summary>
        /// Adds a new event. Not thread-safe.
        /// </summary>
        /// <param name="methodInfo">Information about the callee method.</param>
        /// <param name="startTimestampUs">Timestamp just before the method was called,
        /// in microseconds.</param>
        /// <param name="endTimestampUs">Timestamp immediately after the method finishes
        /// executing, in microseconds.</param>
        /// <remarks>
        /// Implementations should prioritize performance. CPU-intensive work should be postponed to
        /// when GetLogEventAndType gets called.
        /// </remarks>
        void Add(MethodInfo methodInfo, long startTimestampUs, long endTimestampUs);

        /// <summary>
        /// Gets a debug event batch proto alongside its latency based on the events added so far.
        /// It is not safe to call "Add" and this method concurrently.
        /// </summary>
        DebugEventBatchSummary GetSummary();
    }

    /// <summary>
    /// Non-thread safe implementation of <see cref="IDebugEventBatch"/>. All CPU-intensive tasks
    /// are delegated to GetProtoDetails so Add can run quickly.
    /// </summary>
    public class DebugEventBatch : IDebugEventBatch
    {
        public class Factory
        {
            public virtual IDebugEventBatch Create() => new DebugEventBatch();
        }

        readonly Dictionary<MethodInfo, List<DebugEventInterval>> _debugEvents =
            new Dictionary<MethodInfo, List<DebugEventInterval>>();

        long _firstInitialTimestampInMicro;
        long _lastFinalTimestampInMicro;

        public void Add(
            MethodInfo methodInfo, long startTimestampUs, long endTimestampUs)
        {
            _lastFinalTimestampInMicro = endTimestampUs;
            if (_debugEvents.Count == 0)
            {
                _firstInitialTimestampInMicro = startTimestampUs;
            }

            _debugEvents.GetOrAddValue(methodInfo)
                .Add(new DebugEventInterval(startTimestampUs, endTimestampUs));
        }

        public DebugEventBatchSummary GetSummary() =>
            new DebugEventBatchSummary(GetProto(_debugEvents),
                                       _lastFinalTimestampInMicro -
                                       _firstInitialTimestampInMicro);

        private VSIDebugEventBatch GetProto(
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

        private static VSIDebugEvent CreateDebugEvent(MethodInfo eventMethodInfo,
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

        private struct DebugEventInterval
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
}
