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

﻿using GgpGrpc.Cloud.Interceptors;
using Grpc.Core;
using Grpc.Core.Interceptors;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace YetiCommon.Tests.Cloud.Interceptors
{
    [TestFixture]
    class MetricsInterceptorTests
    {
        const string FakeRequest = "request";
        const string FakeResponse = "response";
        const string FakeServiceName = "service";
        const string FakeMethodName = "method";
        const string FakeHost = "test.com";

        // Substitutions
        RpcRecorder recorder;

        // Fakes
        Method<string, string> method;
        ClientInterceptorContext<string, string> context;

        // Object under test
        MetricsInterceptor interceptor;

        [SetUp]
        public void SetUp()
        {
            recorder = Substitute.For<RpcRecorder>();
            method = new Method<string, string>(MethodType.Unary,
                FakeServiceName, FakeMethodName, Marshallers.StringMarshaller,
                Marshallers.StringMarshaller);
            context = new ClientInterceptorContext<string, string>(method, FakeHost,
                new CallOptions());
            interceptor = new MetricsInterceptor(recorder);
        }

        [Test]
        public void BlockingUnaryCallSuccess()
        {
            var response = interceptor.BlockingUnaryCall(FakeRequest, context,
                (req, ctx) =>
                {
                    Assert.AreEqual(context, ctx);
                    Assert.AreEqual(FakeRequest, req);
                    return FakeResponse;
                });
            Assert.AreEqual(FakeResponse, response);
            recorder.Received(1).Record(method, Status.DefaultSuccess, Arg.Any<long>());
        }

        [Test]
        public void BlockingUnaryCallFailure()
        {
            Status error = new Status(StatusCode.Internal, "Error");
            Assert.Throws<RpcException>(() => interceptor.BlockingUnaryCall(FakeRequest, context,
                (req, ctx) =>
                {
                    Assert.AreEqual(context, ctx);
                    Assert.AreEqual(FakeRequest, req);
                    throw new RpcException(error);
                }));
            recorder.Received(1).Record(method, error, Arg.Any<long>());
        }

        [Test]
        public void BlockingUnaryCallFailureUnknown()
        {
            Assert.Throws<NotImplementedException>(
                () => interceptor.BlockingUnaryCall(FakeRequest, context,
                    (req, ctx) =>
                    {
                        Assert.AreEqual(context, ctx);
                        Assert.AreEqual(FakeRequest, req);
                        throw new NotImplementedException();
                    }));
            recorder.Received(1).Record(method,
                Arg.Is<Status>(s => s.StatusCode == StatusCode.Unknown),
                Arg.Any<long>());
        }

        [Test]
        public async Task AsyncUnaryCallSuccessAsync()
        {
            var call = interceptor.AsyncUnaryCall(FakeRequest, context,
                (req, ctx) =>
                {
                    Assert.AreEqual(context, ctx);
                    Assert.AreEqual(FakeRequest, req);
                    return MakeAsyncUnaryCall(Task.FromResult(FakeResponse));
                });
            var response = await call.ResponseAsync;
            Assert.AreEqual(FakeResponse, response);
            recorder.Received(1).Record(method, Status.DefaultSuccess, Arg.Any<long>());
        }

        [Test]
        public void AsyncUnaryCallFailed()
        {
            Status error = new Status(StatusCode.Internal, "Error");
            var call = interceptor.AsyncUnaryCall(FakeRequest, context,
                (req, ctx) =>
                {
                    Assert.AreEqual(context, ctx);
                    Assert.AreEqual(FakeRequest, req);
                    return MakeAsyncUnaryCall(Task.FromException<string>(new RpcException(error)));
                });
            Assert.ThrowsAsync<RpcException>(() => call.ResponseAsync);
            recorder.Received(1).Record(method, error, Arg.Any<long>());
        }

        [Test]
        public void AsyncUnaryCallFailedUnknown()
        {
            Status error = new Status(StatusCode.Unknown, "Error");
            var call = interceptor.AsyncUnaryCall(FakeRequest, context,
                (req, ctx) =>
                {
                    Assert.AreEqual(context, ctx);
                    Assert.AreEqual(FakeRequest, req);
                    return MakeAsyncUnaryCall(
                        Task.FromException<string>(new NotImplementedException()));
                });
            Assert.ThrowsAsync<NotImplementedException>(() => call.ResponseAsync);
            recorder.Received(1).Record(method,
                Arg.Is<Status>(s => s.StatusCode == StatusCode.Unknown),
                Arg.Any<long>());
        }

        [Test]
        public void AsyncUnaryCallCancelled()
        {
            var token = new System.Threading.CancellationToken(true);
            var call = interceptor.AsyncUnaryCall(FakeRequest, context,
                (req, ctx) =>
                {
                    Assert.AreEqual(context, ctx);
                    Assert.AreEqual(FakeRequest, req);
                    return MakeAsyncUnaryCall(Task.FromCanceled<string>(token));
                });
            Assert.ThrowsAsync<TaskCanceledException>(() => call.ResponseAsync);
            recorder.Received(1).Record(method, Status.DefaultCancelled, Arg.Any<long>());
        }

        AsyncUnaryCall<string> MakeAsyncUnaryCall(Task<string> result)
        {
            return new AsyncUnaryCall<string>(result,
                Task.FromResult(new Metadata()), () => Status.DefaultSuccess,
                () => new Metadata(), () => { });
        }
    }
}
