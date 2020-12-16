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

﻿using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace YetiCommon.Tests
{
    /// <summary>
    /// These tests exercise unusual behaviors of exception handling that we either depend on or
    /// wish to avoid. This ensure we find out if these behaviors changes.
    /// </summary>
    class ExceptionTests
    {
        [Test]
        public void ExceptionInWhenClauseEvaluatesToFalse()
        {
            try
            {
                throw new TestException2();
            }
            catch (TestException2) when (ThrowTestException())
            {
                Assert.Fail("Exception should evaluate 'when' clause to false");
            }
            catch (Exception)
            {
                Assert.Pass("Subsequent catch block executed");
            }
        }

        [Test]
        public void OuterWhenBeforeInnerFinally()
        {
            bool innerFinallyExecuted = false;
            try
            {
                try
                {
                    ThrowTestException();
                }
                finally
                {
                    innerFinallyExecuted = true;
                }
            }
            catch (TestException) when (innerFinallyExecuted)
            {
                Assert.Fail("innerFinallyExecuted should be false in 'when' statement");
            }
            catch (TestException)
            {
                Assert.IsTrue(innerFinallyExecuted);
                Assert.Pass("TestException unconditional handler invoked");
            }
            Assert.Fail("No exception handler invoked");
        }

        [Test]
        public void OuterWhenBeforeInnerDispose()
        {
            Disposable disposable = new Disposable();
            try
            {
                using (disposable)
                {
                    ThrowTestException();
                }
            }
            catch (TestException) when (disposable.Disposed)
            {
                Assert.Fail("disposable.Disposed should be false in 'when' statement");
            }
            catch (TestException)
            {
                Assert.IsTrue(disposable.Disposed);
                Assert.Pass("TestException unconditional handler invoked");
            }
            Assert.Fail("No exception handler invoked");
        }

        [Test]
        public void InnerFinallyBeforeOuterCatch()
        {
            bool outerTestExceptionWhenExecuted = false;
            try
            {
                try
                {
                    try
                    {
                        throw new TestException();
                    }
                    finally
                    {
                        throw new TestException2();
                    }
                }
                catch (TestException2)
                {
                    Assert.IsTrue(outerTestExceptionWhenExecuted);
                    Assert.Pass("Inner TestException2 handler should be invoked");
                }
            }
            catch (TestException2)
            {
                Assert.Fail("Outer TestException2 handler should not be invoked");
            }
            catch (TestException)
                when (SetOutAndReturnTrue(out outerTestExceptionWhenExecuted))
            {
                Assert.Fail("Outer TestException handler should not be invoked");
            }
            Assert.Fail("No exception handler invoked");
        }

        [Test]
        public void SyncMethodThrowsSyncronously()
        {
            Func<Task> syncFailure = () =>
            {
                throw new TestException();
            };

            Assert.Throws<TestException>(() => syncFailure());
        }

        [Test]
        public void AsyncMethodDoesNotThrowSyncronously()
        {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            Func<Task> asyncFailure = async () =>
            {
                throw new TestException();
            };
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

            var task = asyncFailure();
            Assert.ThrowsAsync<TestException>(async () => await task);
        }

        [Test]
        public void TaskRunDoesNotThrowSyncronously()
        {
            var task = Task.Run(() => { throw new TestException(); });
            Assert.ThrowsAsync<TestException>(async () => await task);
        }

        bool ThrowTestException()
        {
            throw new TestException();
        }

        bool SetOutAndReturnTrue(out bool called)
        {
            return called = true;
        }

        class TestException : Exception { }
        class TestException2 : Exception { }

        class Disposable : IDisposable
        {
            public bool Disposed { get; private set; } = false;
            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
