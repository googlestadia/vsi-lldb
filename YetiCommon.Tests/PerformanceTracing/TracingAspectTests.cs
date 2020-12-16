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
using YetiCommon.PerformanceTracing;
using YetiCommon.Tests.CastleAspects.TestSupport;
using YetiCommon.Tests.PerformanceTracing.TestSupport;

namespace YetiCommon.Tests.PerformanceTracing
{
    [TestFixture]
    class TracingAspectTests
    {
        ITracingLogger _mockTraceLogger;
        MonotonicTimeSource _fakeTimeSource;
        IInterceptor _classUnderTest;

        [SetUp]
        public void SetUp()
        {
            _mockTraceLogger = Substitute.For<ITracingLogger>();
            _fakeTimeSource = new MonotonicTimeSource();
            _classUnderTest = new TracingAspect(_mockTraceLogger, _fakeTimeSource);
        }

        [Test]
        public void ShouldSendEventInfoToLogger()
        {
            var mockInvocation = Substitute.For<IInvocation>();
            mockInvocation.Method.Returns(
                MethodInfoUtil.GetMethodInfo<DummyObject>(x => x.SetValue(1)));
            mockInvocation.TargetType.Returns(typeof(DummyObject));

            _classUnderTest.Intercept(mockInvocation);
            _mockTraceLogger.Received().TraceEvent("SetValue", EventType.Sync, typeof(DummyObject),
                                                   Arg.Is<long>(1), Arg.Is<long>(1),
                                                   Arg.Any<int>());
        }

        [Test]
        public void ShouldSendEventInfoForProxiedObjectMethodCalls()
        {
            var dummyObject = new DummyObject.Factory().Create();
            var proxy =
                new ProxyGenerator().CreateInterfaceProxyWithTarget(dummyObject, _classUnderTest);

            proxy.SetValue(1);

            _mockTraceLogger.Received().TraceEvent("SetValue", EventType.Sync, typeof(DummyObject),
                                                   Arg.Is<long>(1), Arg.Is<long>(1),
                                                   Arg.Any<int>());
        }
    }
}