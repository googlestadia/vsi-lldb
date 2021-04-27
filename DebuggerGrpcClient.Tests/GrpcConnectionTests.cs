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

using Grpc.Core;
using Microsoft.VisualStudio.Threading;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YetiCommon;

namespace DebuggerGrpcClient.Tests
{
    [TestFixture]
    [Timeout(5000)]
    class GrpcConnectionTests
    {
        readonly Status rpcStatus = new Status(StatusCode.Aborted, "test error");

        GrpcConnection connection;
        FakeMainThreadContext mainThreadContext;
        JoinableTaskContext taskContext;

        [SetUp]
        public void SetUp()
        {
            var callInvokerFactory = new PipeCallInvokerFactory();
            mainThreadContext = new FakeMainThreadContext();
            taskContext = mainThreadContext.JoinableTaskContext;
            connection = new GrpcConnection(taskContext.Factory, callInvokerFactory.Create());
        }

        [TearDown]
        public void TearDown()
        {
            connection.Shutdown();
            mainThreadContext.Dispose();
        }

        [Test]
        public void InvokeRpcSuccess()
        {
            connection.RpcException +=
                e => Assert.Fail("Unexpected error: " + e.ToString());

            bool taskRan = false;
            bool result = connection.InvokeRpc(() => { taskRan = true; });
            Assert.That(result, Is.True);
            Assert.That(taskRan, Is.True);
        }

        [Test]
        public async Task InvokeRpcAsyncSuccessAsync()
        {
            connection.RpcException +=
                e => Assert.Fail("Unexpected error: " + e);

            var taskRan = false;
            var result = await connection.InvokeRpcAsync(() =>
            {
                taskRan = true;
                return Task.CompletedTask;
            }).WithTimeout(TimeSpan.FromSeconds(5));
            Assert.That(result, Is.True);
            Assert.That(taskRan, Is.True);
        }

        [Test]
        public async Task EventHandlerIsInvokedOnAsyncRpcCompletionAsync()
        {
            bool handlerInvoked = false;
            connection.AsyncRpcCompleted += () => handlerInvoked = true;
            await connection.InvokeRpcAsync(() => Task.CompletedTask);
            Assert.That(handlerInvoked, Is.True);
        }

        [Test]
        public void InvokeRpcAsyncThrowsExceptions()
        {
            connection.RpcException +=
                e => Assert.Fail("Unexpected error: " + e);

            Assert.ThrowsAsync<BadException>(async () =>
            {
                await connection.InvokeRpcAsync(() => throw new BadException())
                    .WithTimeout(TimeSpan.FromSeconds(5));
            });
        }

        [Test]
        public async Task InvokeRpcSucceedWhenAsyncThrowsAsync()
        {
            await taskContext.Factory.SwitchToMainThreadAsync();

            connection.RpcException +=
                e => Assert.Fail("Unexpected error: " + e);

            var completeFirstTask = new AsyncManualResetEvent(false);
            var taskRanList = new List<int>();

            Task<bool> task = connection.InvokeRpcAsync(async () =>
            {
                await completeFirstTask.WaitAsync();
                taskRanList.Add(2);
                throw new BadException();
            });

            completeFirstTask.Set();
            taskRanList.Add(1);
            var result = connection.InvokeRpc(() => { taskRanList.Add(3); });

            try
            {
                await task;
            }
            catch (BadException)
            {
            }

            Assert.That(task.Exception?.InnerExceptions[0] is BadException);
            Assert.That(result, Is.True);
            Assert.AreEqual(new List<int> { 1, 2, 3 }, taskRanList);
        }

        [Test]
        public async Task InvokeRpcAsyncTwiceAsync()
        {
            connection.RpcException +=
                e => Assert.Fail("Unexpected error: " + e);

            var completeFirstTask = new AsyncManualResetEvent(false);
            var taskRanList = new List<int>();

            Task<bool> task1 = connection.InvokeRpcAsync(async () =>
            {
                await completeFirstTask.WaitAsync().WithTimeout(TimeSpan.FromSeconds(10));
                taskRanList.Add(1);
                throw new BadException();
            });

            Task<bool> task2 = connection.InvokeRpcAsync(async () =>
            {
                taskRanList.Add(2);
                await Task.CompletedTask;
            });

            completeFirstTask.Set();

            Assert.ThrowsAsync<BadException>(async () =>
            {
                await task1.WithTimeout(TimeSpan.FromSeconds(10));
            });

            Assert.That(task1.Exception?.InnerExceptions[0] is BadException);
            Assert.That(await task2, Is.True);
            Assert.AreEqual(new List<int> { 1, 2 }, taskRanList);
        }

        [Test]
        public void InvokeRpcFailure()
        {
            Exception reportedError = null;
            connection.RpcException += e => reportedError = e;
            bool result = connection.InvokeRpc(() => throw new RpcException(rpcStatus));

            Assert.That(result, Is.False);
            Assert.That(reportedError, Is.AssignableTo<RpcException>());

            RpcException rpcError = (RpcException)reportedError;
            Assert.That(rpcError.Status, Is.EqualTo(rpcStatus));
        }

        [Test]
        public void InvokeRpcIgnoredAfterShutdown()
        {
            connection.Shutdown();

            bool taskRan = false;
            bool result = connection.InvokeRpc(() => { taskRan = true; });
            Assert.That(result, Is.False);
            Assert.That(taskRan, Is.False);
        }

        [Test]
        public void InvokeRpcErrorIgnoredInShutdownRace()
        {
            connection.RpcException +=
                e => Assert.Fail("Unexpected error: " + e);

            bool result = connection.InvokeRpc(() =>
            {
                connection.Shutdown();
                throw new RpcException(rpcStatus);
            });
            Assert.That(result, Is.False);
        }

        [Test]
        public void InvokeRpcErrorIgnoredWithoutHandler()
        {
            bool result = connection.InvokeRpc(() => throw new RpcException(rpcStatus));
            Assert.That(result, Is.False);
        }

        class BadException : Exception
        {
        }
    }
}