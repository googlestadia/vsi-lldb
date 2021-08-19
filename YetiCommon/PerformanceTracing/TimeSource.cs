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
using System.Diagnostics;

namespace YetiCommon.PerformanceTracing
{
    /// <summary>
    /// Simple interface to get the current timestamp, used only for comparisions relative
    /// to other timestamps from the same source.
    /// The timestamps cannot make guarantees about accuracy to absolute time.
    /// </summary>
    public interface ITimeSource
    {
        /// <summary>
        /// Gets a timestamp in ticks, stored as a long.
        /// </summary>
        long GetTimestampTicks();

        /// <summary>
        /// Gets a timestamp stored as a long, in the microsecond scale.
        /// </summary>
        long GetTimestampUs();

        /// <summary>
        /// Converts timestamp to microsecond scale.
        /// </summary>
        long ConvertTicksToUs(long ticks);

        /// <summary>
        /// Returns the duration in microseconds of two timestamps, which are in ticks.
        /// </summary>
        long GetDurationUs(long timestampTicks1, long timestampTicks2);

        /// <summary>
        /// Returns the duration in milliseconds of two timestamps, which are in ticks.
        /// </summary>
        double GetDurationMs(long timestampTicks1, long timestampTicks2);
    }

    /// <summary>
    /// ITimeSource implementation using Stopwatch. Stopwatch is most suitable for
    /// high resolution time measurements. For performance tracing we only need relative
    /// timestamps so we do not need to convert it to a DateTime.
    /// </summary>
    public class StopwatchTimeSource : ITimeSource
    {
        const long _microsecondsPerSecond = 1000000;

        public long GetTimestampTicks() => Stopwatch.GetTimestamp();

        public long ConvertTicksToUs(long ticks) =>
            (long) (ticks * (double) _microsecondsPerSecond / Stopwatch.Frequency);

        public long GetTimestampUs() =>
            ConvertTicksToUs(GetTimestampTicks());

        public long GetDurationUs(long timestampTicks1, long timestampTicks2) =>
            ConvertTicksToUs(Math.Abs(timestampTicks1 - timestampTicks2));

        public double GetDurationMs(long timestampTicks1, long timestampTicks2) =>
            GetDurationUs(timestampTicks1, timestampTicks2) / 1000.0;
    }
}