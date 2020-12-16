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
using System.Threading.Tasks;
using TestsCommon.TestSupport;

namespace YetiCommon.Tests
{
    [TestFixture]
    class SafeErrorUtilTests
    {
        LogSpy logSpy;
        Action<Exception> handler;

        [SetUp]
        public void SetUp()
        {
            logSpy = new LogSpy();
            logSpy.Attach();
            handler = Substitute.For<Action<Exception>>();
        }

        [TearDown]
        public void TearDown()
        {
            logSpy.Detach();
        }

        [Test]
        public async Task SafelyHandleErrorAsync_DoesNotCallHandlerOnSuccessAsync()
        {
            await SafeErrorUtil.SafelyHandleErrorAsync(() => Task.CompletedTask, handler);
            handler.DidNotReceiveWithAnyArgs().Invoke(null);
        }

        [Test]
        public async Task SafelyHandleErrorAsync_CallsHandlerOnSynchronousErrorAsync()
        {
            // Use a delegate to bypass warning that we don't use await.
            Func<Task> throwSynchronously = delegate
            {
                throw new TestException();
            };

            await SafeErrorUtil.SafelyHandleErrorAsync(throwSynchronously, handler);
            handler.Received().Invoke(Arg.Is<Exception>(e => e is TestException));
        }

        [Test]
        public async Task SafelyHandleErrorAsync_CallsHandlerOnAsyncErrorAsync()
        {
            await SafeErrorUtil.SafelyHandleErrorAsync(async () => {
                await Task.Yield();
                throw new TestException();
            }, handler);
            handler.Received().Invoke(Arg.Is<Exception>(e => e is TestException));
        }

        [Test]
        public async Task SafelyHandleErrorAsync_HandlerThrowsAsync()
        {
            handler.When(x => x.Invoke(Arg.Any<Exception>())).Throw<HandlerException>();
            await SafeErrorUtil.SafelyHandleErrorAsync(
                () => Task.FromException(new TestException()), handler);
        }

        [Test]
        public void SafelyHandleError_DoesNotCallHandlerOnSuccess()
        {
            SafeErrorUtil.SafelyHandleError(() => { }, handler);
            handler.DidNotReceiveWithAnyArgs().Invoke(null);
        }

        [Test]
        public void SafelyHandleError_CallsHandlerOnError()
        {
            SafeErrorUtil.SafelyHandleError(() => { throw new TestException(); }, handler);
            handler.Received().Invoke(Arg.Is<Exception>(e => e is TestException));
        }

        [Test]
        public void SafelyHandleError_HandlerThrows()
        {
            handler.When(x => x.Invoke(Arg.Any<Exception>())).Throw<HandlerException>();
            SafeErrorUtil.SafelyHandleError(() => { throw new TestException(); }, handler);
        }

        [Test]
        public async Task SafelyLogErrorAsync()
        {
            await SafeErrorUtil.SafelyLogErrorAsync(async () =>
            {
                await Task.Yield();
                throw new TestException();
            }, "message");
            Assert.That(logSpy.GetOutput(), Contains.Substring("message"));
            Assert.That(logSpy.GetOutput(), Contains.Substring("TestException"));
        }

        [Test]
        public void SafelyLogError()
        {
            SafeErrorUtil.SafelyLogError(() => { throw new TestException(); }, "message");
            Assert.That(logSpy.GetOutput(), Contains.Substring("message"));
            Assert.That(logSpy.GetOutput(), Contains.Substring("TestException"));
        }

        class TestException : Exception { }
        class HandlerException : Exception { }
    }
}
