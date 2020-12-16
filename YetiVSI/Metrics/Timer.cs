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

﻿using System.Diagnostics;

namespace YetiVSI.Metrics
{
    // Simple interface to record duration of actions.
    public interface ITimer
    {
        // Start (or resume) recording duration.
        void Start();

        // Stop (pause) recording duration.
        void Stop();

        // Report the duration recorded so far.
        long ElapsedMilliseconds { get; }

        // Restart (or start) recording duration from zero.
        void Restart();

        // Stops time interval measurement and resets the elapsed time to zero.
        void Reset();
    }

    // Implementation of timer interface using Stopwatch.
    public class Timer : Stopwatch, ITimer
    {
        public class Factory
        {
            // Create a new timer in the stopped state.
            public virtual ITimer Create() { return new Timer(); }

            // Helper to create a timer in the started state.
            public ITimer CreateStarted()
            {
                var timer = Create();
                timer.Start();
                return timer;
            }
        }
    }
}
