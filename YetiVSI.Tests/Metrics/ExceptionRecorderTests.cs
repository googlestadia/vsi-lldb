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
using System;
using Metrics.Shared;
using YetiVSI.Metrics;
using YetiVSI.Test.Metrics.TestSupport;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class ExceptionRecorderTests
    {
        const int _maxExceptionsChainLength = 2;
        const int _maxStackTraceFrames = 2;

        IVsiMetrics _fakeMetrics;
        IExceptionWriter _fakeWriter;
        ExceptionRecorder _exceptionRecorder;

        [SetUp]
        public void SetUp()
        {
            _fakeMetrics = Substitute.For<IVsiMetrics>();
            _fakeWriter = Substitute.For<IExceptionWriter>();
            _exceptionRecorder = new ExceptionRecorder(_fakeMetrics, _fakeWriter,
                                                       _maxExceptionsChainLength,
                                                       _maxStackTraceFrames);
        }

        [Test]
        public void Record()
        {
            var ex = new TestException1("outer", new TestException2());

            _exceptionRecorder.Record(TestClass.MethodInfo1, ex);

            var logEvent = new DeveloperLogEvent
            {
                StatusCode = DeveloperEventStatus.Types.Code.InternalError
            };
            logEvent.ExceptionsData.Add(new VSIExceptionData());

            _fakeMetrics.Received()
                .RecordEvent(DeveloperEventType.Types.Type.VsiException, logEvent);
        }

        [Test]
        public void RecordNullArgumentsFail()
        {
            Assert.Catch<ArgumentNullException>(
                () => _exceptionRecorder.Record(TestClass.MethodInfo1, null));
            Assert.Catch<ArgumentNullException>(
                () => _exceptionRecorder.Record(null, new TestException2()));
        }

        [Test]
        public void NewRecorderWithNullMetricsFail()
        {
            Assert.Catch<ArgumentNullException>(() => new ExceptionRecorder(null));
        }

        class TestException1 : Exception
        {
            public TestException1(string message, Exception inner) : base(message, inner)
            {
            }
        }

        class TestException2 : Exception
        {
        }
    }
}
