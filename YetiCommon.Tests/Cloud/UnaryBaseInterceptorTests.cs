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
using NUnit.Framework;
using System;

namespace YetiCommon.Tests.Cloud
{
    [TestFixture]
    class UnaryBaseInterceptorTests
    {
        class FakeUnaryInterceptor : UnaryInterceptorBase
        {
        }

        const string FakeRequest = "request";
        const string FakeServiceName = "service";
        const string FakeMethodName = "method";
        const string FakeHost = "test.com";

        // Fakes
        Method<string, string> method;
        ClientInterceptorContext<string, string> context;

        UnaryInterceptorBase classUnderTest;

        [SetUp]
        public void SetUp()
        {
            method = new Method<string, string>(MethodType.Unary,
                FakeServiceName, FakeMethodName, Marshallers.StringMarshaller,
                Marshallers.StringMarshaller);
            context = new ClientInterceptorContext<string, string>(method, FakeHost,
                new CallOptions());
            classUnderTest = new FakeUnaryInterceptor();
        }

        [Test]
        public void NotImplemented()
        {
            Assert.Throws<NotImplementedException>(() =>
                classUnderTest.AsyncServerStreamingCall(FakeRequest, context,
                    (_, __) => { throw new InvalidOperationException(); }));
            Assert.Throws<NotImplementedException>(() =>
                classUnderTest.AsyncClientStreamingCall<string, string>(context,
                    (_) => { throw new InvalidOperationException(); }));
            Assert.Throws<NotImplementedException>(() =>
                classUnderTest.AsyncDuplexStreamingCall(context,
                    (_) => { throw new InvalidOperationException(); }));
        }
    }
}
