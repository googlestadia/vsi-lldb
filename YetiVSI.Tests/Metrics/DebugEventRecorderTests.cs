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

using NSubstitute;
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
        const int _batchIntervalMs = 1024;

        BatchEventAggregator<DebugEventBatch, DebugEventBatchParams, DebugEventBatchSummary>
            _batchEventAggregator;

        EventSchedulerFake _eventScheduler;
        IMetrics _metrics;
        TimerFake _timer;

        // Object under test
        DebugEventRecorder _debugEventRecorder;

        [SetUp]
        public void SetUp()
        {
            _eventScheduler = new EventSchedulerFake();
            var eventSchedulerFactory = Substitute.For<IEventSchedulerFactory>();
            eventSchedulerFactory.Create(Arg.Do<System.Action>(a => _eventScheduler.Callback = a),
                                         _batchIntervalMs)
                .Returns(_eventScheduler);
            _timer = new TimerFake();
            _batchEventAggregator =
                new BatchEventAggregator<DebugEventBatch, DebugEventBatchParams,
                    DebugEventBatchSummary>(_batchIntervalMs, eventSchedulerFactory);
            _metrics = Substitute.For<IMetrics>();
            _debugEventRecorder = new DebugEventRecorder(_batchEventAggregator, _metrics);
        }

        [Test]
        public void TestRecordMultipleEvents()
        {
            _debugEventRecorder.Record(TestClass.MethodInfo1, 0, 123);
            _debugEventRecorder.Record(TestClass.MethodInfo2, 456, 789);
            _debugEventRecorder.Record(TestClass.MethodInfo3, 1234, 56789);

            // Get a copy of the batch summary sent to batchEventAggregator so we can verify
            // that it matches the one being sent to metrics.
            DebugEventBatchSummary batchSummary = null;
            _batchEventAggregator.BatchSummaryReady += (_, newSummary) => batchSummary = newSummary;

            _timer.Increment(_batchIntervalMs);
            _eventScheduler.Increment(_batchIntervalMs);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    TestClass.MethodInfo1.GetProto(), TestClass.MethodInfo2.GetProto(),
                    TestClass.MethodInfo3.GetProto()
                }, batchSummary.Proto.DebugEvents.Select(a => a.MethodInfo));

            _metrics.Received(1)
                .RecordEvent(DeveloperEventType.Types.Type.VsiDebugEventBatch,
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