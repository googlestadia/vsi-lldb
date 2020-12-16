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

﻿using NSubstitute;
using NUnit.Framework;
using System.Linq;
using System.Reflection;
using YetiVSI.Metrics;
using YetiVSI.Test.Metrics.TestSupport;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class DebugEventAggregatorTests
    {
        const int Timeout = 1024;

        EventSchedulerFake eventScheduler;
        TimerFake timer;
        long currentTimestamp;

        // Object under test
        DebugEventAggregator debugEventAggregator;

        [SetUp]
        public void SetUp()
        {
            eventScheduler = new EventSchedulerFake();
            var eventSchedulerFactory = Substitute.For<IEventSchedulerFactory>();
            eventSchedulerFactory.Create(
                Arg.Do<System.Action>(a => eventScheduler.Callback = a)).Returns(eventScheduler);
            timer = new TimerFake();
            currentTimestamp = 0;
            debugEventAggregator = new DebugEventAggregator(
                new DebugEventBatch.Factory(), Timeout, eventSchedulerFactory, timer);
        }

        [Test]
        public void TestAddingEvents()
        {
            DebugEventBatchSummary batchSummary = null;
            debugEventAggregator.BatchSummaryReady += (_, newSummary) => batchSummary = newSummary;

            AddEvent(TestClass.MethodInfo1);
            timer.Increment(Timeout / 2);
            eventScheduler.Increment(Timeout);
            Assert.IsNull(batchSummary);

            AddEvent(TestClass.MethodInfo2);
            timer.Increment(Timeout / 2);
            eventScheduler.Increment(Timeout);
            Assert.IsNull(batchSummary);

            AddEvent(TestClass.MethodInfo3);
            timer.Increment(Timeout);
            eventScheduler.Increment(Timeout);
            Assert.AreEqual(currentTimestamp, batchSummary.LatencyInMicroseconds);
            CollectionAssert.AreEquivalent(
                new[] { TestClass.MethodInfo1.GetProto(), TestClass.MethodInfo2.GetProto(),
                    TestClass.MethodInfo3.GetProto() },
                batchSummary.Proto.DebugEvents.Select(a => a.MethodInfo));
        }

        void AddEvent(MethodInfo methodInfo) =>
            debugEventAggregator.Add(methodInfo, currentTimestamp, currentTimestamp += 1);
    }
}
