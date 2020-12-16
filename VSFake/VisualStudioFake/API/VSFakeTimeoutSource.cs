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

namespace Google.VisualStudioFake.API
{
    public enum VSFakeTimeout
    {
        // Collection of timeouts that map directly to VSFake methods.
        LaunchAndAttach,
        RunUntilIdle,
        RunUntilBreak,
        RunUntil,

        // Collection of helpful timeouts that can be used for explicit control flow.
        Short,
        Medium,
        Long
    }

    /// <summary>
    /// Source of timeout durations used by the IVSFake.
    /// </summary>
    /// <remarks>
    /// The class abstraction makes it possible to easily switch to a developer mode where timeouts
    /// are set to TimeSpan.MaxValue to enable developers to debug a test without causing timeout
    /// errors.
    /// </remarks>
    public class VSFakeTimeoutSource : TimeoutSource<VSFakeTimeout>
    {
        // This value is used by a CancellationTokenSource which only supports Int.MaxValue
        //milliseconds which is about 24 days.
        static readonly TimeSpan DEBUG_TIMEOUT = TimeSpan.FromDays(7);

        static readonly TimeSpan DEFAULT_TIMEOUT = TimeSpan.FromSeconds(60);

        public VSFakeTimeoutSource() : base(
            System.Diagnostics.Debugger.IsAttached ? DEBUG_TIMEOUT : DEFAULT_TIMEOUT)
        {
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                this[VSFakeTimeout.Short] = TimeSpan.FromSeconds(5);
                this[VSFakeTimeout.Medium] = TimeSpan.FromSeconds(60);
                this[VSFakeTimeout.Long] = TimeSpan.FromSeconds(120);
            }
        }

    }
}
