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

using Metrics.Shared;
using NSubstitute;
using NUnit.Framework;
using YetiVSI.Metrics;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class DebugSessionMetricsTests
    {
        const long ElapsedMilliseconds = 1234;
        const DeveloperEventType.Types.Type EventType =
            DeveloperEventType.Types.Type.VsiApplicationsGet;

        // Prototype of expected event proto
        DeveloperLogEvent logEvent;

        // Substitutions
        IMetrics metrics;

        // Object under test
        DebugSessionMetrics debugSessionMetrics;

        [SetUp]
        public void SetUp()
        {
            logEvent = new DeveloperLogEvent
            {
                StatusCode = DeveloperEventStatus.Types.Code.Success,
                LatencyMilliseconds = ElapsedMilliseconds,
                LatencyType = DeveloperLogEvent.Types.LatencyType.LatencyTool
            };

            metrics = Substitute.For<IMetrics>();
            debugSessionMetrics = new DebugSessionMetrics(metrics);
        }

        [Test]
        public void RecordWithoutDebugSessionId()
        {
            debugSessionMetrics.RecordEvent(EventType, logEvent);

            metrics.Received(1).RecordEvent(EventType, logEvent);
        }

        [Test]
        public void RecordWithDebugSessionId()
        {
            const string DebugSessionId1 = "abc123";
            const string DebugSessionId2 = "def456";

            metrics.NewDebugSessionId().Returns(DebugSessionId1);

            debugSessionMetrics.UseNewDebugSessionId();
            Assert.AreEqual(DebugSessionId1, debugSessionMetrics.DebugSessionId);
            debugSessionMetrics.RecordEvent(EventType, logEvent);
            logEvent.DebugSessionIdStr = DebugSessionId1;
            metrics.Received(1).RecordEvent(EventType, logEvent);

            metrics.ClearReceivedCalls();

            debugSessionMetrics.DebugSessionId = DebugSessionId2;
            debugSessionMetrics.RecordEvent(EventType, logEvent);
            logEvent.DebugSessionIdStr = DebugSessionId2;
            metrics.Received(1).RecordEvent(EventType, logEvent);
        }
    }
}
