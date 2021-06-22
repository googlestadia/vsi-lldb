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

﻿using YetiVSI.Metrics;

namespace YetiVSI.Test.Metrics.TestSupport
{
    class TimerFake : ITimer
    {
        bool _running;
        long _ticks;

        public long ElapsedMilliseconds => _ticks;

        public void Reset()
        {
            _ticks = 0;
            _running = false;
        }

        public void Restart()
        {
            _ticks = 0;
            _running = true;
        }

        public void Start() => _running = true;

        public void Stop() => _running = false;

        public void Increment(long tick)
        {
            if (_running)
            {
                _ticks += tick;
            }
        }
    }
}
