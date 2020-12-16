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

﻿using Castle.DynamicProxy;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Reflection;
using YetiCommon.ExceptionRecorder;
using YetiCommon.Tests.CastleAspects.TestSupport;

namespace YetiCommon.Tests.ExceptionRecorder
{
    [TestFixture]
    public class ExceptionRecorderAspectTests
    {
        class TestException : Exception { }

        MethodInfo methodInfo;
        IInvocation mockInvocation;
        IExceptionRecorder mockRecorder;

        ExceptionRecorderAspect exceptionRecorderAspect;

        [SetUp]
        public void SetUp()
        {
            methodInfo = MethodInfoUtil.GetMethodInfo<DummyObject>(x => x.SetValue(1));
            mockInvocation = Substitute.For<IInvocation>();
            mockInvocation.MethodInvocationTarget.Returns(methodInfo);

            mockRecorder = Substitute.For<IExceptionRecorder>();
            exceptionRecorderAspect = new ExceptionRecorderAspect(mockRecorder);
        }

        [Test]
        public void TestInterceptWhenExecutionIsNormal()
        {
            exceptionRecorderAspect.Intercept(mockInvocation);
            mockRecorder.DidNotReceiveWithAnyArgs().Record(null, null);
        }

        [Test]
        public void TestInterceptWhenMethodThrows()
        {
            var ex = new TestException();
            mockInvocation.When(x => x.Proceed()).Do(x => { throw ex; });

            Assert.Throws<TestException>(() => exceptionRecorderAspect.Intercept(mockInvocation));

            mockRecorder.Received().Record(methodInfo, ex);
        }
    }
}
