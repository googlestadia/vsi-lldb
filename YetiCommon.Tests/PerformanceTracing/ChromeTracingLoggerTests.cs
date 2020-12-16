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

﻿using NLog;
using NSubstitute;
using NUnit.Framework;
using System;
using YetiCommon.PerformanceTracing;

namespace YetiCommon.Tests.PerformanceTracing
{
    [TestFixture]
    public class ChromeTracingLoggerTests
    {
        const string _expectedCategoryAsync = "Async";
        const string _expectedCategorySync = "Sync";
        const int _threadIdAsyncEvent = -1;

        long _timestamp;
        int _threadId;
        int _processId;
        Func<bool> _notInsideAsync;

        [SetUp]
        public void SetUp()
        {
            _timestamp = 0;
            _threadId = 1;
            _processId = 0;
            _notInsideAsync = () => false;
        }

        [Test]
        public void ShouldSendSyncEventToNLogLoggerInJsonFormatWhenNotInsideAsyncScope()
        {
            const string traceName = "ToString";
            const long durationUs = 1000000;
            Type type = typeof(int);
            var logger = Substitute.For<ILogger>();

            var classUnderTest = new ChromeTracingLogger(_processId, logger, _notInsideAsync);

            classUnderTest.TraceEvent(traceName, EventType.Sync, type, durationUs, _timestamp,
                                      _threadId);

            Func<ChromeTracingLogger.Event, bool> verifyFormat = (traceEvent) =>
                traceEvent.Name.Contains(traceName) && traceEvent.Name.Contains(type.FullName) &&
                durationUs == traceEvent.DurationUs && _timestamp.Equals(traceEvent.TimestampUs) &&
                _threadId == traceEvent.ThreadId && _processId == traceEvent.ProcessId;

            // TODO: Test NLog ILogger state without using NSubstitute
            logger.Received(1).Trace(Arg.Is<string>(message => verifyFormat(
                                                        Newtonsoft.Json.JsonConvert
                                                            .DeserializeObject<
                                                                ChromeTracingLogger.Event>(
                                                                message))));
        }

        [Test]
        public void ShouldSendAsyncEventToNLoggerInJsonFormatWhenInsideAsyncScope()
        {
            const string name = "ToString";
            const long durationUs = 1000000;
            Type type = typeof(int);
            var logger = Substitute.For<ILogger>();
            bool InsideAsync() => true;
            var classUnderTest = new ChromeTracingLogger(_processId, logger, InsideAsync);

            classUnderTest.TraceEvent(name, EventType.Async, type, durationUs, _timestamp,
                                      _threadId);

            Func<ChromeTracingLogger.Event, bool> verifyFormat = (traceEvent) =>
                traceEvent.Name.Contains(name) && traceEvent.Name.Contains(type.FullName) &&
                durationUs == traceEvent.DurationUs && _timestamp.Equals(traceEvent.TimestampUs) &&
                _threadIdAsyncEvent == traceEvent.ThreadId && _processId == traceEvent.ProcessId &&
                traceEvent.Category.Contains(_expectedCategoryAsync);

            logger.Received(1).Trace(Arg.Is<string>(message => verifyFormat(
                                                        Newtonsoft.Json.JsonConvert
                                                            .DeserializeObject<
                                                                ChromeTracingLogger.Event>(
                                                                message))));
        }

        [Test]
        public void ShouldSendSyncEventWhenInsideAsyncWithSyncEventSpecified()
        {
            const string name = "ToString";
            const long durationUs = 1000000;
            Type type = typeof(int);
            var logger = Substitute.For<ILogger>();
            bool InsideAsync() => true;
            var classUnderTest = new ChromeTracingLogger(_processId, logger, InsideAsync);

            classUnderTest.TraceEvent(name, EventType.Sync, type, durationUs, _timestamp,
                                      _threadId);

            Func<ChromeTracingLogger.Event, bool> verifyFormat = (traceEvent) =>
                traceEvent.Name.Contains(name) && traceEvent.Name.Contains(type.FullName) &&
                durationUs == traceEvent.DurationUs && _timestamp.Equals(traceEvent.TimestampUs) &&
                _threadIdAsyncEvent == traceEvent.ThreadId && _processId == traceEvent.ProcessId &&
                traceEvent.Category.Contains(_expectedCategorySync);

            logger.Received(1).Trace(Arg.Is<string>(message => verifyFormat(
                                                        Newtonsoft.Json.JsonConvert
                                                            .DeserializeObject<
                                                                ChromeTracingLogger.Event>(
                                                                message))));
        }
    }
}