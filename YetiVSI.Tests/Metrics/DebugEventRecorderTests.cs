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
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSI.Test.Metrics.TestSupport;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class DebugEventRecorderTests
    {
        const string DebugSessionId = "1234";
        const int Timeout = 1024;

        DebugEventAggregator debugEventAggregator;
        EventSchedulerFake eventScheduler;
        IMetrics metrics;
        TimerFake timer;

        // Object under test
        DebugEventRecorder debugEventRecorder;

        [SetUp]
        public void SetUp()
        {
            eventScheduler = new EventSchedulerFake();
            var eventSchedulerFactory = Substitute.For<IEventSchedulerFactory>();
            eventSchedulerFactory.Create(
                Arg.Do<System.Action>(a => eventScheduler.Callback = a)).Returns(eventScheduler);
            timer = new TimerFake();
            debugEventAggregator = new DebugEventAggregator(
                new DebugEventBatch.Factory(), Timeout, eventSchedulerFactory, timer);
            metrics = Substitute.For<IMetrics>();
            debugEventRecorder = new DebugEventRecorder(debugEventAggregator, metrics);
        }

        [Test]
        public void TestRecordMultipleEvents()
        {
            debugEventAggregator.Add(TestClass.MethodInfo1, 0, 123);
            debugEventAggregator.Add(TestClass.MethodInfo2, 456, 789);
            debugEventAggregator.Add(TestClass.MethodInfo3, 1234, 56789);

            // Get a copy of the batch summary sent to debugEventAggregator so we can verify
            // that it matches the one being sent to metrics.
            DebugEventBatchSummary batchSummary = null;
            debugEventAggregator.BatchSummaryReady += (_, newSummary) => batchSummary = newSummary;

            timer.Increment(Timeout);
            eventScheduler.Increment(Timeout);
            CollectionAssert.AreEquivalent(
                new[] { TestClass.MethodInfo1.GetProto(), TestClass.MethodInfo2.GetProto(),
                    TestClass.MethodInfo3.GetProto() },
                batchSummary.Proto.DebugEvents.Select(a => a.MethodInfo));

            metrics.Received(1).RecordEvent(
                DeveloperEventType.Types.Type.VsiDebugEventBatch,
                new DeveloperLogEvent
                {
                    DebugEventBatch = batchSummary.Proto,
                    StatusCode = DeveloperEventStatus.Types.Code.Success,
                    LatencyMilliseconds = 56,
                    LatencyType = DeveloperLogEvent.Types.LatencyType.LatencyTool
                });
        }
    }
}
