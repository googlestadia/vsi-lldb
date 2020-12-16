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

﻿using Grpc.Core;
using Grpc.Core.Interceptors;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using YetiCommon.PerformanceTracing;
using YetiCommon.Tests.PerformanceTracing.TestSupport;

namespace YetiCommon.Tests.PerformanceTracing
{
    [TestFixture]
    class TracingGrpcInterceptorTests
    {
        const string FakeRequest = "request";
        const string FakeResponse = "response";
        const string FakeServiceName = "service";
        const string FakeMethodName = "method";
        const string FakeHost = "test.com";

        ITracingLogger mockTraceLogger;
        Method<string, string> method;
        ClientInterceptorContext<string, string> context;
        MonotonicTimeSource fakeTimeSource;
        long expectedStartTimestampTicks;
        long expectedEndTimestampTicks;
        long expectedStartTimestampUs;
        long expectedDurationUs;

        TracingGrpcInterceptor classUnderTest;

        [SetUp]
        public void SetUp()
        {
            mockTraceLogger = Substitute.For<ITracingLogger>();
            method = new Method<string, string>(MethodType.Unary,
                FakeServiceName, FakeMethodName, Marshallers.StringMarshaller,
                Marshallers.StringMarshaller);
            context = new ClientInterceptorContext<string, string>(method, FakeHost,
                new CallOptions());
            fakeTimeSource = new MonotonicTimeSource();
            expectedStartTimestampTicks =
                fakeTimeSource.TimestampTicks + MonotonicTimeSource.TicksPerUs;
            expectedEndTimestampTicks =
                expectedStartTimestampTicks + MonotonicTimeSource.TicksPerUs;
            expectedStartTimestampUs =
                fakeTimeSource.ConvertTicksToUs(expectedStartTimestampTicks);
            expectedDurationUs = fakeTimeSource.GetDurationUs(
                expectedStartTimestampTicks, expectedEndTimestampTicks);
            classUnderTest = new TracingGrpcInterceptor(mockTraceLogger, fakeTimeSource);
        }

        [Test]
        public void BlockingUnaryCallSuccess()
        {
            string response = classUnderTest.BlockingUnaryCall(FakeRequest, context, (req, ctx) =>
            {
                Assert.AreEqual(context, ctx);
                Assert.AreEqual(FakeRequest, req);
                return FakeResponse;
            });

            Assert.AreEqual(FakeResponse, response);
            mockTraceLogger.Received(1).TraceEvent(FakeMethodName, EventType.Sync,
                                                   FakeRequest.GetType(), expectedDurationUs,
                                                   expectedStartTimestampUs, Arg.Any<int>());
        }

        [Test]
        public async Task AsyncUnaryCallSuccessAsync()
        {
            AsyncUnaryCall<string> call = classUnderTest.AsyncUnaryCall(
                FakeRequest, context, (req, ctx) =>
                {
                    Assert.AreEqual(context, ctx);
                    Assert.AreEqual(FakeRequest, req);
                    return MakeAsyncUnaryCall(Task.FromResult(FakeResponse));
                });

            string response = await call.ResponseAsync;

            Assert.AreEqual(FakeResponse, response);
            mockTraceLogger.Received(1).TraceEvent(FakeMethodName, EventType.Async,
                                                   FakeRequest.GetType(), expectedDurationUs,
                                                   expectedStartTimestampUs, Arg.Any<int>());
        }

        AsyncUnaryCall<string> MakeAsyncUnaryCall(Task<string> result)
        {
            return new AsyncUnaryCall<string>(result,
                Task.FromResult(new Metadata()), () => Status.DefaultSuccess,
                () => new Metadata(), () => { });
        }
    }
}
