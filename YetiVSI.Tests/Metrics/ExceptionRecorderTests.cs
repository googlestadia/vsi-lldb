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
using System.Diagnostics;
using System.IO;
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

        IMetrics _fakeMetrics;
        ExceptionRecorder _exceptionRecorder;

        [SetUp]
        public void SetUp()
        {
            _fakeMetrics = Substitute.For<IMetrics>();
            _exceptionRecorder =
                new ExceptionRecorder(_fakeMetrics, _maxExceptionsChainLength,
                                      _maxStackTraceFrames);
        }

        [Test]
        public void Record()
        {
            var ex = new TestException1("outer", new TestException2());

            _exceptionRecorder.Record(TestClass.MethodInfo1, ex);

            var exceptionData = new VSIExceptionData
            {
                CatchSite = TestClass.MethodInfo1.GetProto()
            };
            exceptionData.ExceptionsChain.Add(
                new VSIExceptionData.Types.Exception
                {
                    ExceptionType = typeof(TestException1).GetProto()
                });
            exceptionData.ExceptionsChain.Add(
                new VSIExceptionData.Types.Exception
                {
                    ExceptionType = typeof(TestException2).GetProto()
                });

            var logEvent = new DeveloperLogEvent
            {
                StatusCode = DeveloperEventStatus.Types.Code.InternalError
            };
            logEvent.ExceptionsData.Add(exceptionData);

            _fakeMetrics.Received()
                .RecordEvent(DeveloperEventType.Types.Type.VsiException, logEvent);
        }

        [Test]
        public void RecordWithStackTrace()
        {
            var ex = new TestException2();

            // Throw exception to capture the stack trace
            try
            {
                throw ex;
            }
            catch (TestException2)
            {
            }

            _exceptionRecorder.Record(TestClass.MethodInfo1, ex);

            var exceptionData = new VSIExceptionData
            {
                CatchSite = TestClass.MethodInfo1.GetProto()
            };
            var firstExceptionInChain = new VSIExceptionData.Types.Exception
            {
                ExceptionType = typeof(TestException2).GetProto()
            };
            var stackTraceFrame = new StackTrace(ex, true).GetFrame(0);
            firstExceptionInChain.ExceptionStackTraceFrames.Add(
                new VSIExceptionData.Types.Exception.Types.StackTraceFrame
                {
                    AllowedNamespace = true,
                    Method = stackTraceFrame.GetMethod().GetProto(),
                    Filename = Path.GetFileName(stackTraceFrame.GetFileName()),
                    LineNumber = (uint?) stackTraceFrame.GetFileLineNumber()
                });
            exceptionData.ExceptionsChain.Add(firstExceptionInChain);

            var logEvent = new DeveloperLogEvent
            {
                StatusCode = DeveloperEventStatus.Types.Code.InternalError
            };
            logEvent.ExceptionsData.Add(exceptionData);

            _fakeMetrics.Received()
                .RecordEvent(DeveloperEventType.Types.Type.VsiException, logEvent);
        }

        [Test]
        public void RecordExceptionChainTooLong()
        {
            var ex = new TestException1("level1",
                                        new TestException1(
                                            "level2",
                                            new TestException1("level3", new TestException2())));

            _exceptionRecorder.Record(TestClass.MethodInfo1, ex);

            _fakeMetrics.Received()
                .RecordEvent(DeveloperEventType.Types.Type.VsiException,
                             Arg.Is<DeveloperLogEvent>(
                                 p => ExceptionChainOverflowRecorded(p.ExceptionsData[0])));
        }

        [Test]
        public void RecordExceptionInNotAllowedNamespace()
        {
            var ex = NotAllowedNamespace.Test.ThrowException();
            _exceptionRecorder.Record(TestClass.MethodInfo1, ex);

            var exceptionData = new VSIExceptionData
            {
                CatchSite = TestClass.MethodInfo1.GetProto()
            };

            var firstExceptionInChain = new VSIExceptionData.Types.Exception
            {
                ExceptionType = typeof(Exception).GetProto()
            };
            firstExceptionInChain.ExceptionStackTraceFrames.Add(
                new VSIExceptionData.Types.Exception.Types.StackTraceFrame
                {
                    AllowedNamespace = false
                });
            exceptionData.ExceptionsChain.Add(firstExceptionInChain);

            var logEvent = new DeveloperLogEvent
            {
                StatusCode = DeveloperEventStatus.Types.Code.InternalError
            };
            logEvent.ExceptionsData.Add(exceptionData);

            _fakeMetrics.Received(1)
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

        static bool ExceptionChainOverflowRecorded(VSIExceptionData data)
        {
            return data.ExceptionsChain.Count == _maxExceptionsChainLength + 1 && data
                .ExceptionsChain[(int) _maxExceptionsChainLength]
                .ExceptionType.Equals(typeof(ExceptionRecorder.ChainTooLongException).GetProto());
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

namespace NotAllowedNamespace
{
    static class Test
    {
        public static Exception ThrowException()
        {
            try
            {
                throw new Exception("Test");
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}