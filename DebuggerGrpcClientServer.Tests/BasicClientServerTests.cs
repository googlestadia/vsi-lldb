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

﻿using Debugger.Test;
using Grpc.Core;
using NSubstitute;
using NUnit.Framework;
using System;

namespace DebuggerGrpcClientServer.Tests
{
    // These tests require x64 test architecture. To run them locally, set
    //   Test > Test Settings > DefaultArchitecture > x64
    // and recompile.

    /// <summary>
    /// Close-to-the-metal test of client-server communication.
    /// </summary>
    [TestFixture]
    [Timeout(5000)]
    class BasicClientServerTests : BaseIntegrationTests
    {
        TestRpcService.TestRpcServiceClient client;
        TestRpcService.TestRpcServiceBase mockService;

        [SetUp]
        public void SetUp()
        {
            BaseSetUp();
            mockService = Substitute.For<TestRpcService.TestRpcServiceBase>();
            client = new TestRpcService.TestRpcServiceClient(Connection.CallInvoker);
        }

        [TearDown]
        public void TearDown()
        {
            BaseTearDown();
        }

        [Test]
        public void TestCallSucceeds()
        {
            TestRpcService.BindService(Server, mockService);
            TestRequest request = new TestRequest() { Message = "hello" };
            mockService.TestCall(request, Arg.Any<ServerCallContext>()).Returns(
                new TestResponse { Message = "(internal)" + request.Message });

            TestResponse response = client.TestCall(request);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Message, Is.Not.Null);
            Assert.That(response.Message, Is.EqualTo("(internal)"));
        }

        /// <summary>
        /// Tests that error cases when the server-side handler throws an exception are handled
        /// correctly by throwing an RpcException on the client.
        /// </summary>
        [Test]
        public void ThrowsRpcExceptionIfHandlerThrows()
        {
            TestRpcService.BindService(Server, mockService);
            TestRequest request = new TestRequest();
            bool handlerDidRun = false;
            mockService.TestCall(request, Arg.Any<ServerCallContext>()).Returns<TestResponse>(
                x =>
                {
                    handlerDidRun = true;
                    throw new InvalidOperationException();
                });
            Assert.Throws<RpcException>(() => client.TestCall(request));
            Assert.True(handlerDidRun);
        }

        [Test]
        public void ThrowsRpcExceptionIfHandlerIsNotBound()
        {
            // Note: Do not call BindService() here!
            Assert.Throws<RpcException>(() => client.TestCall(new TestRequest()));
        }
    }
}
