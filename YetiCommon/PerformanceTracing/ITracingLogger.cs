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

﻿using System;

namespace YetiCommon.PerformanceTracing
{
    public enum EventType
    {
        /// <summary>
        /// Synchronous event.
        /// </summary>
        Sync,
        /// <summary>
        /// Asynchronous event.
        /// </summary>
        Async
    }

    /// <summary>
    /// Receives and records events and their data.
    /// </summary>
    public interface ITracingLogger
    {
        /// <summary>
        /// Captures a single complete event with its duration and start time. The caller explicitly
        /// specifies the type of the event (sync / async).
        /// </summary>
        /// <param name="name">Name of the event.</param>
        /// <param name="eventType">Type of the event (sync / async).</param>
        /// <param name="callerType">Type that the event was called on.</param>
        /// <param name="durationUs">Elapsed time for the event in microseconds.</param>
        /// <param name="timestampUs">Relative start time in microseconds.</param>
        /// <param name="tid">Thread ID.</param>
        void TraceEvent(string name, EventType eventType, Type callerType, long durationUs,
                        long timestampUs, int tid);
    }
}