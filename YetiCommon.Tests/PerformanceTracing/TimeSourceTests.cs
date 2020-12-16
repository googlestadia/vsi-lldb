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

﻿using NUnit.Framework;
using YetiCommon.PerformanceTracing;

namespace YetiCommon.Tests.PerformanceTracing
{
    [TestFixture]
    public class TimeSourceTests
    {
        [Test]
        public void ShouldHaveConsistentTimestampRepresentations()
        {
            var classUnderTest = new StopwatchTimeSource();
            long timestampTicks1 = 100;
            long timestampTicks2 = 1000;

            long durationTestUs = classUnderTest.GetDurationUs(timestampTicks1, timestampTicks2);
            long durationTestReverseUs =
                classUnderTest.GetDurationUs(timestampTicks2, timestampTicks1);

            var microseconds1 = classUnderTest.ConvertTicksToUs(timestampTicks1);
            var microseconds2 = classUnderTest.ConvertTicksToUs(timestampTicks2);
            var durationUs = microseconds2 - microseconds1;

            Assert.AreEqual(durationUs, durationTestUs, 1);
            Assert.AreEqual(durationTestUs, durationTestReverseUs);
        }
    }
}
