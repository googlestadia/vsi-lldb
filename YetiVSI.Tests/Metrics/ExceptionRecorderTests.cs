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
using System;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSI.Test.Metrics.TestSupport;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class ExceptionRecorderTests
    {
        const uint MaxExceptionsChainLength = 2;

        IMetrics fakeMetrics;

        ExceptionRecorder exceptionRecorder;

        [SetUp]
        public void SetUp()
        {
            fakeMetrics = Substitute.For<IMetrics>();
            exceptionRecorder = new ExceptionRecorder(fakeMetrics, MaxExceptionsChainLength);
        }

        [Test]
        public void Record()
        {
            var ex = new TestException1("outer", new TestException2());

            exceptionRecorder.Record(TestClass.MethodInfo1, ex);

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

            fakeMetrics.Received().RecordEvent(
                DeveloperEventType.Types.Type.VsiException, logEvent);
        }

        [Test]
        public void RecordExceptionChainTooLong()
        {
            var ex = new TestException1("level1",
                new TestException1("level2",
                    new TestException1("level3",
                        new TestException2())));

            exceptionRecorder.Record(TestClass.MethodInfo1, ex);

            fakeMetrics.Received().RecordEvent(
                DeveloperEventType.Types.Type.VsiException,
                Arg.Is<DeveloperLogEvent>(
                    p => ExceptionChainOverflowRecorded(p.ExceptionsData[0])));
        }

        [Test]
        public void RecordNullArgumentsFail()
        {
            Assert.Catch<ArgumentNullException>(
                () => exceptionRecorder.Record(TestClass.MethodInfo1, null));
            Assert.Catch<ArgumentNullException>(
                () => exceptionRecorder.Record(null, new TestException2()));
        }

        [Test]
        public void NewRecorderWithNullMetricsFail()
        {
            Assert.Catch<ArgumentNullException>(() => new ExceptionRecorder(null));
        }

        static bool ExceptionChainOverflowRecorded(VSIExceptionData data)
        {
            return data.ExceptionsChain.Count == MaxExceptionsChainLength + 1 &&
                data.ExceptionsChain[(int)MaxExceptionsChainLength].ExceptionType.Equals(
                    typeof(ExceptionRecorder.ChainTooLongException).GetProto());
        }

        class TestException1 : Exception
        {
            public TestException1(string message, Exception inner) : base(message, inner) { }
        }

        class TestException2 : Exception { }
    }
}
