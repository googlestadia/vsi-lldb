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
        const int _batchIntervalMs = 1026;

        EventSchedulerFake _eventScheduler;
        long _currentTimestamp;

        // Object under test
        BatchEventAggregator<DebugEventBatch, DebugEventBatchParams, DebugEventBatchSummary>
            _batchEventAggregator;

        [SetUp]
        public void SetUp()
        {
            _eventScheduler = new EventSchedulerFake();
            var eventSchedulerFactory = Substitute.For<IEventSchedulerFactory>();
            eventSchedulerFactory.Create(Arg.Do<System.Action>(a => _eventScheduler.Callback = a),
                                         _eventScheduler.Interval = _batchIntervalMs)
                .Returns(_eventScheduler);
            _currentTimestamp = 0;
            _batchEventAggregator =
                new BatchEventAggregator<DebugEventBatch, DebugEventBatchParams,
                    DebugEventBatchSummary>(_batchIntervalMs, eventSchedulerFactory);
        }

        [Test]
        public void TestAddingDebugEvents()
        {
            DebugEventBatchSummary batchSummary = null;
            _batchEventAggregator.BatchSummaryReady += (_, newSummary) => batchSummary = newSummary;

            AddEvent(TestClass.MethodInfo1);
            _eventScheduler.Increment(_batchIntervalMs / 3);
            Assert.IsNull(batchSummary);

            AddEvent(TestClass.MethodInfo2);
            _eventScheduler.Increment(_batchIntervalMs / 3);
            Assert.IsNull(batchSummary);

            AddEvent(TestClass.MethodInfo3);
            _eventScheduler.Increment(_batchIntervalMs / 3);
            Assert.NotNull(batchSummary);
            Assert.AreEqual(_currentTimestamp, batchSummary.LatencyInMicroseconds);
            CollectionAssert.AreEquivalent(
                new[]
                    {
                        TestClass.MethodInfo1.GetProto(), TestClass.MethodInfo2.GetProto(),
                        TestClass.MethodInfo3.GetProto()
                    }, batchSummary.Proto.DebugEvents.Select(a => a.MethodInfo));
        }

        [Test]
        public void TestMetricsFlushing()
        {
            DebugEventBatchSummary batchSummary = null;
            _batchEventAggregator.BatchSummaryReady += (_, newSummary) => batchSummary = newSummary;

            AddEvent(TestClass.MethodInfo1);
            _eventScheduler.Increment(_batchIntervalMs / 2);
            Assert.IsNull(batchSummary);

            AddEvent(TestClass.MethodInfo2);
            _eventScheduler.Increment(_batchIntervalMs / 2);
            Assert.NotNull(batchSummary);
            Assert.AreEqual(_currentTimestamp, batchSummary.LatencyInMicroseconds);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    TestClass.MethodInfo1.GetProto(), TestClass.MethodInfo2.GetProto()
                }, batchSummary.Proto.DebugEvents.Select(a => a.MethodInfo));

            batchSummary = null;
            _currentTimestamp = 0;

            AddEvent(TestClass.MethodInfo3);
            _eventScheduler.Increment(_batchIntervalMs / 2);
            Assert.IsNull(batchSummary);

            _batchEventAggregator.Flush();
            Assert.NotNull(batchSummary);
            Assert.AreEqual(_currentTimestamp, batchSummary.LatencyInMicroseconds);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    TestClass.MethodInfo3.GetProto()
                }, batchSummary.Proto.DebugEvents.Select(a => a.MethodInfo));
        }

        void AddEvent(MethodInfo methodInfo) =>
            _batchEventAggregator.Add(
                new DebugEventBatchParams(methodInfo, _currentTimestamp, _currentTimestamp += 1));
    }
}
