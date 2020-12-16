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
using System.Linq;
using YetiVSI.Metrics;
using YetiVSI.Test.Metrics.TestSupport;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class DebugEventBatchTests
    {
        // Object under test
        DebugEventBatch _debugEventBatch;

        [SetUp]
        public void SetUp()
        {
            _debugEventBatch = new DebugEventBatch();
        }

        [Test]
        public void TestEmptyBatch()
        {
            var batchSummary = _debugEventBatch.GetSummary();

            Assert.False(batchSummary.Proto.BatchStartTimestampMicroseconds.HasValue);
            Assert.Zero(batchSummary.Proto.DebugEvents.Count);
        }

        [Test]
        public void TestSingleEventBatch()
        {
            long startTimestampUs = 200;
            long endTimestampUs = 1000;
            _debugEventBatch.Add(TestClass.MethodInfo1, startTimestampUs, endTimestampUs);

            DebugEventBatchSummary batchSummary = _debugEventBatch.GetSummary();

            Assert.AreEqual(startTimestampUs, batchSummary.Proto.BatchStartTimestampMicroseconds);
            Assert.AreEqual(endTimestampUs - startTimestampUs, batchSummary.LatencyInMicroseconds);

            Assert.AreEqual(1, batchSummary.Proto.DebugEvents.Count);
            var debugEvent = batchSummary.Proto.DebugEvents[0];
            Assert.Multiple(() =>
            {
                Assert.AreEqual(TestClass.MethodInfo1.GetProto(), debugEvent.MethodInfo);
                Assert.AreEqual(1, debugEvent.TotalCount);
                Assert.AreEqual(1, debugEvent.StartOffsetMicroseconds.Count);
                Assert.AreEqual(startTimestampUs, debugEvent.StartOffsetMicroseconds[0]);
                Assert.AreEqual(1, debugEvent.DurationMicroseconds.Count);
                Assert.AreEqual(endTimestampUs - startTimestampUs,
                                debugEvent.DurationMicroseconds[0]);
            });
        }

        [Test]
        public void TestMultipleEventsBatch()
        {
            _debugEventBatch.Add(TestClass.MethodInfo2, 0, 789);
            _debugEventBatch.Add(TestClass.MethodInfo1, 900, 12300);
            _debugEventBatch.Add(TestClass.MethodInfo1, 12400, 89789);
            _debugEventBatch.Add(TestClass.MethodInfo2, 91234, 91237);
            _debugEventBatch.Add(TestClass.MethodInfo1, 100000, 987654321);

            var batchSummary = _debugEventBatch.GetSummary();

            Assert.AreEqual(0, batchSummary.Proto.BatchStartTimestampMicroseconds);
            Assert.AreEqual(987654321, batchSummary.LatencyInMicroseconds);

            var debugEvents = batchSummary.Proto.DebugEvents;
            Assert.AreEqual(2, debugEvents.Count);

            var debugEvent1 = debugEvents.ElementAt(0);
            Assert.Multiple(() =>
            {
                Assert.AreEqual(TestClass.MethodInfo2.GetProto(), debugEvent1.MethodInfo);
                Assert.AreEqual(2, debugEvent1.TotalCount);
                Assert.AreEqual(2, debugEvent1.StartOffsetMicroseconds.Count);
                Assert.AreEqual(new[]{0, 91234}, debugEvent1.StartOffsetMicroseconds);
                Assert.AreEqual(2, debugEvent1.DurationMicroseconds.Count);
                Assert.AreEqual(new[]{789, 3}, debugEvent1.DurationMicroseconds);
            });

            var debugEvent2 = debugEvents.ElementAt(1);
            Assert.Multiple(() =>
            {
                Assert.AreEqual(TestClass.MethodInfo1.GetProto(), debugEvent2.MethodInfo);
                Assert.AreEqual(3, debugEvent2.TotalCount);
                Assert.AreEqual(3, debugEvent2.StartOffsetMicroseconds.Count);
                Assert.AreEqual(new[]{900, 12400, 100000}, debugEvent2.StartOffsetMicroseconds);
                Assert.AreEqual(3, debugEvent2.DurationMicroseconds.Count);
                Assert.AreEqual(new[]{11400, 77389, 987554321}, debugEvent2.DurationMicroseconds);
            });
        }
    }
}
