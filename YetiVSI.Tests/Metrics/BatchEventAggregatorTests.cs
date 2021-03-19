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
using System.Reflection;
using YetiVSI.Metrics;
using YetiVSI.Test.Metrics.TestSupport;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class BatchEventAggregatorTests
    {
        const int _timeout = 1024;

        EventSchedulerFake _eventScheduler;
        TimerFake _timer;
        long _currentTimestamp;

        // Object under test
        BatchEventAggregator<DebugEventBatch, DebugEventBatchParams, DebugEventBatchSummary>
            _batchEventAggregator;

        [SetUp]
        public void SetUp()
        {
            _eventScheduler = new EventSchedulerFake();
            var eventSchedulerFactory = Substitute.For<IEventSchedulerFactory>();
            eventSchedulerFactory.Create(
                Arg.Do<System.Action>(a => _eventScheduler.Callback = a)).Returns(_eventScheduler);
            _timer = new TimerFake();
            _currentTimestamp = 0;
            _batchEventAggregator =
                new BatchEventAggregator<DebugEventBatch, DebugEventBatchParams,
                    DebugEventBatchSummary>(_timeout, eventSchedulerFactory, _timer);
        }

        [Test]
        public void TestAddingDebugEvents()
        {
            DebugEventBatchSummary batchSummary = null;
            _batchEventAggregator.BatchSummaryReady += (_, newSummary) => batchSummary = newSummary;

            AddEvent(TestClass.MethodInfo1);
            _timer.Increment(_timeout / 2);
            _eventScheduler.Increment(_timeout);
            Assert.IsNull(batchSummary);

            AddEvent(TestClass.MethodInfo2);
            _timer.Increment(_timeout / 2);
            _eventScheduler.Increment(_timeout);
            Assert.IsNull(batchSummary);

            AddEvent(TestClass.MethodInfo3);
            _timer.Increment(_timeout);
            _eventScheduler.Increment(_timeout);
            Assert.NotNull(batchSummary);
            Assert.AreEqual(_currentTimestamp, batchSummary.LatencyInMicroseconds);
            CollectionAssert.AreEquivalent(
                new[]
                    {
                        TestClass.MethodInfo1.GetProto(), TestClass.MethodInfo2.GetProto(),
                        TestClass.MethodInfo3.GetProto()
                    }, batchSummary.Proto.DebugEvents.Select(a => a.MethodInfo));
        }

        void AddEvent(MethodInfo methodInfo) =>
            _batchEventAggregator.Add(
                new DebugEventBatchParams(methodInfo, _currentTimestamp, _currentTimestamp += 1));
    }
}
