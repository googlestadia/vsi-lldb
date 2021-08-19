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
using YetiCommon.PerformanceTracing;

namespace YetiCommon.Tests.PerformanceTracing.TestSupport
{
    /// <summary>
    /// Simple implementation of ITimeSource, where GetTimestampTicks returns a monotonically
    /// increasing value that each represents one microsecond.
    /// </summary>
    public class MonotonicTimeSource : ITimeSource
    {
        public const long TicksPerUs = 10;

        public long TimestampTicks;

        public long ConvertTicksToUs(long ticks) => ticks / TicksPerUs;

        public long GetDurationUs(long timestampTicks1, long timestampTicks2) =>
            ConvertTicksToUs(Math.Abs(timestampTicks2 - timestampTicks1));

        public double GetDurationMs(long timestampTicks1, long timestampTicks2) =>
            GetDurationUs(timestampTicks2, timestampTicks1) / 1000.0;

        public long GetTimestampTicks() => TimestampTicks += TicksPerUs;

        public long GetTimestampUs() => ConvertTicksToUs(GetTimestampTicks());
    }
}
