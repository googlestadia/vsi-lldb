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

﻿using System;
using Newtonsoft.Json;
using NLog;
using NSubstitute;
using NUnit.Framework;
using YetiCommon.PerformanceTracing;
using YetiCommon.Tests.PerformanceTracing.TestSupport;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class TaskExecutorTracingHelperTests
    {
        ILogger _logger;
        ChromeTracingLogger _chromeTracingLogger;
        ITimeSource _timeSource;

        int _processId;
        Func<bool> _insideAsync;

        Func<string, bool> _verifyNotMetadataEvent;

        [SetUp]
        public void SetUp()
        {
            _processId = 0;
            _insideAsync = () => true;

            _logger = Substitute.For<ILogger>();
            _chromeTracingLogger = new ChromeTracingLogger(_processId, _logger, _insideAsync);
            _timeSource = new MonotonicTimeSource();

            _verifyNotMetadataEvent = (eventTraced) =>
            {
                var deserializedTrace =
                    JsonConvert.DeserializeObject<ChromeTracingLogger.Event>(eventTraced);
                return deserializedTrace.Phase != 'M';
            };
        }

        [Test]
        public void EventTracedWhenCallingStartAndEndEvents()
        {
            const string name = "ToString";
            Type type = typeof(int);

            var helper = new TaskExecutorTracingHelper(_chromeTracingLogger, _timeSource);

            helper.OnAsyncTaskStarted(this, EventArgs.Empty);
            helper.OnAsyncTaskEnded(
                this, new AsyncTaskEndedEventArgs {CallerName = name, CallerType = type});

            _logger.Received(1)
                .Trace(Arg.Is<string>(eventTraced => _verifyNotMetadataEvent(eventTraced)));
        }

        [Test]
        public void ThrowsAndEventNotTracedIfInconsistentStart()
        {
            var helper = new TaskExecutorTracingHelper(_chromeTracingLogger, _timeSource);

            helper.OnAsyncTaskStarted(this, EventArgs.Empty);
            Assert.Throws<InvalidOperationException>(
                () => helper.OnAsyncTaskStarted(this, EventArgs.Empty));

            _logger.DidNotReceive()
                .Trace(Arg.Is<string>(eventTraced => _verifyNotMetadataEvent(eventTraced)));
        }

        [Test]
        public void ThrowsAndEventNotTracedIfStartEventWasNotCalled()
        {
            const string name = "ToString";
            Type type = typeof(int);

            var helper = new TaskExecutorTracingHelper(_chromeTracingLogger, _timeSource);

            Assert.Throws<InvalidOperationException>(
                () => helper.OnAsyncTaskEnded(
                    this, new AsyncTaskEndedEventArgs {CallerName = name, CallerType = type}));

            _logger.DidNotReceive()
                .Trace(Arg.Is<string>(eventTraced => _verifyNotMetadataEvent(eventTraced)));
        }
    }
}
