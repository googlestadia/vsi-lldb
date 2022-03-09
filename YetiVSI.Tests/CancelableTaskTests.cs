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

﻿using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YetiVSI.Test
{
    abstract class CancelableTaskTests
    {
        [TestFixture]
        public class WithInfiniteDelay : CancelableTaskTests
        {
            protected override TimeSpan DialogDelay => Timeout.InfiniteTimeSpan;

            protected override TimeSpan ProgressPeriod => TimeSpan.Zero;

            [Test]
            public void DialogIsNotShown()
            {
                var task = taskFactory.Create(Text, _ => { });
                task.Run();
                mockProgressDialog.DidNotReceive().ShowModal();
            }
        }

        [TestFixture]
        public class WithNoDelay : CancelableTaskTests
        {
            protected override TimeSpan DialogDelay => TimeSpan.Zero;

            protected override TimeSpan ProgressPeriod => TimeSpan.Zero;

            [Test]
            public void DialogIsShown()
            {
                var task = taskFactory.Create(Text, _ => { });
                task.Run();
                mockProgressDialog.Received().ShowModal();
            }

            [Test]
            public void CanceledByUser()
            {
                closeDialogSource.SetResult(false);
                var task = taskFactory.Create(Text, _ => { });

                Assert.IsFalse(task.Run());
                Assert.IsTrue(task.IsCanceled);
            }
        }

        [TestFixture]
        public class WithReportPeriod : CancelableTaskTests
        {
            protected override TimeSpan DialogDelay => TimeSpan.Zero;

            protected override TimeSpan ProgressPeriod => TimeSpan.FromMilliseconds(1);
        }

        [TestFixture]
        public class WithReportPeriodAndInfiniteDelay : CancelableTaskTests
        {
            protected override TimeSpan DialogDelay => Timeout.InfiniteTimeSpan;

            protected override TimeSpan ProgressPeriod => TimeSpan.FromMilliseconds(1);
        }

        class TestException : Exception { }

        const string Title = "Dialog title";
        const string Text = "Dialog text";

        protected abstract TimeSpan DialogDelay { get; }

        protected abstract TimeSpan ProgressPeriod { get; }

        JoinableTaskContext taskContext;
        TaskCompletionSource<bool> closeDialogSource;
        IProgressDialog mockProgressDialog;
        ProgressDialog.Factory mockProgressDialogFactory;
        CancelableTask.Factory taskFactory;

        [SetUp]
        public void SetUp()
        {
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            taskContext = new JoinableTaskContext();
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

            closeDialogSource = new TaskCompletionSource<bool>();

            mockProgressDialog = Substitute.For<IProgressDialog>();
            mockProgressDialog.ShowModal()
                .Returns(x => taskContext.Factory.Run(() => closeDialogSource.Task));

            mockProgressDialog.When(x => x.Complete())
                .Do(x => closeDialogSource.TrySetResult(true));

            mockProgressDialogFactory = Substitute.For<ProgressDialog.Factory>();
            mockProgressDialogFactory.Create(Title, Text).Returns(mockProgressDialog);

            taskFactory = new CancelableTask.Factory(taskContext, mockProgressDialogFactory, Title,
                DialogDelay, ProgressPeriod);
        }

        public void RunComplete()
        {
            bool ranToCompletion = false;
            var task = taskFactory.Create(Text, t => {
                t.ThrowIfCancellationRequested();
                ranToCompletion = true;
            });
            Assert.IsTrue(task.Run());

            Assert.IsFalse(task.IsCanceled);
            Assert.IsTrue(ranToCompletion);
        }

        [Test]
        public void RunError()
        {
            var task = taskFactory.Create(Text, _ => Task.FromException(new TestException()));
            Assert.Throws<TestException>(() => task.Run());

            Assert.IsFalse(task.IsCanceled);
        }

        [Test]
        public void RunWithResult()
        {
            var result = "foo";
            var task = taskFactory.Create(Text, () => Task.FromResult(result));
            Assert.IsTrue(task.Run());

            Assert.IsFalse(task.IsCanceled);
            Assert.AreEqual(result, task.Result);
        }

        [Test]
        public void RunWithResultError()
        {
            var task = taskFactory.Create(Text,
                () => Task.FromException<string>(new TestException()));
            Assert.Throws<TestException>(() => task.Run());

            Assert.IsFalse(task.IsCanceled);
            Assert.IsNull(task.Result);
        }

        [Test]
        public void RunTwice()
        {
            var task = taskFactory.Create(Text, _ => { });
            task.Run();
            Assert.Throws<InvalidOperationException>(() => task.Run());
        }

        [Test]
        public void CanceledBeforeStart()
        {
            bool taskRan = false;
            var task = taskFactory.Create(Text, _ => { taskRan = true; });
            task.Cancel();
            Assert.IsFalse(task.Run());

            Assert.IsTrue(task.IsCanceled);
            Assert.IsFalse(taskRan);
        }

        [Test]
        public void CancelWhenComplete()
        {
            var task = taskFactory.Create(Text, _ => { } );
            task.Run();
            task.Cancel();

            Assert.IsTrue(task.IsCanceled);
        }

        [Test]
        public void CancelWhenCancelled()
        {
            var task = taskFactory.Create(Text, _ => { });
            task.Cancel();
            task.Run();
            task.Cancel();

            Assert.IsTrue(task.IsCanceled);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExternalSignalWithCooperation(bool abort)
        {
            // Bi-directional condition variable for coordination.
            bool taskStarted = false;
            bool signaledCanceled = false;
            bool taskCompleted = false;
            var cv = new object();

            // Set up a task to check cancellation and exit cooperatively.
            bool failedToCancel = false;
            Exception inTaskException = null;
            var task = taskFactory.Create(Text, t => {
                Monitor.Enter(cv);
                taskStarted = true;
                Monitor.Pulse(cv);
                while (!signaledCanceled)
                {
                    Monitor.Wait(cv);
                }
                try
                {
                    t.ThrowIfCancellationRequested();
                    failedToCancel = true;
                }
                catch (Exception e)
                {
                    inTaskException = e;
                    throw;
                }
                finally
                {
                    taskCompleted = true;
                    Monitor.Pulse(cv);
                    Monitor.Exit(cv);
                }
            });

            // Set up a separate thread to cancel the task.
            Task makeThreadingAnalyzerHappy = Task.Run(() =>
            {
                Monitor.Enter(cv);
                while (!taskStarted)
                {
                    Monitor.Wait(cv);
                }
                if (abort)
                {
                    task.Abort(new TestException());
                } else
                {
                    task.Cancel();
                }
                signaledCanceled = true;
                Monitor.Pulse(cv);
                Monitor.Exit(cv);
            });

            // Run and wait for task to exit.
            if (abort)
            {
                var e = Assert.Throws<TaskAbortedException>(() => task.Run());
                Assert.IsInstanceOf<TestException>(e.InnerException);
            }
            else
            {
                Assert.IsFalse(task.Run());
            }
            Assert.IsTrue(task.IsCanceled);

            // Check properties that are set in the task, after the task is canceled.
            Monitor.Enter(cv);
            while (!taskCompleted)
            {
                Monitor.Wait(cv);
            }
            if (abort)
            {
                Assert.IsInstanceOf<TaskAbortedException>(inTaskException);
            }
            else
            {
                Assert.IsInstanceOf<OperationCanceledException>(inTaskException);
            }
            Assert.IsFalse(failedToCancel);
            Monitor.Exit(cv);
        }

        [Test]
        public void CanceledWithResult()
        {
            var task = taskFactory.Create(Text, () => Task.FromResult("foo"));
            task.Cancel();
            Assert.IsFalse(task.Run());

            Assert.IsNull(task.Result);
            Assert.IsTrue(task.IsCanceled);
        }

        [Test]
        public void LongRunningCanceledWitResult()
        {
            bool taskRan = false;
            var task = taskFactory.Create(Text, t =>
            {
                taskRan = true;
                return Task.FromResult("foo");
            });
            task.Cancel();
            Assert.IsFalse(task.Run());

            Assert.IsFalse(taskRan);
            Assert.IsNull(task.Result);
            Assert.IsTrue(task.IsCanceled);
        }

        [Test]
        public void LongRunningWitResult()
        {
            var task = taskFactory.Create(Text, t =>
            {
                t.ThrowIfCancellationRequested();
                return Task.FromResult("foo");
            });

            Assert.IsTrue(task.Run());

            Assert.That(task.Result, Is.EqualTo("foo"));
            Assert.IsFalse(task.IsCanceled);
        }

        [Test]
        public void CancelByException()
        {
            var task = taskFactory.Create(Text, _ => { throw new OperationCanceledException(); } );
            Assert.IsFalse(task.Run());

            Assert.IsTrue(task.IsCanceled);
        }

        [Test]
        public void AbortedBeforeStart()
        {
            bool taskRan = false;
            var task = taskFactory.Create(Text, _ => { taskRan = true; });
            task.Abort(new TestException());
            try
            {
                task.Run();
            }
            catch (TaskAbortedException e)
            {
                Assert.IsInstanceOf<TestException>(e.InnerException);
            }

            Assert.IsTrue(task.IsCanceled);
            Assert.IsFalse(taskRan);
        }

        [Test]
        public void AbortedWhenComplete()
        {
            var task = taskFactory.Create(Text, _ => {});
            task.Run();
            task.Abort(new TestException());

            Assert.IsTrue(task.IsCanceled);
        }

        [Test]
        public void AbortedWhenCanceled()
        {
            var task = taskFactory.Create(Text, _ => { });
            task.Cancel();
            task.Run();
            task.Abort(new TestException());

            Assert.IsTrue(task.IsCanceled);
        }

        [Test]
        public async Task ProgressAsync()
        {
            var progress = "progress";
            var task = taskFactory.Create(Text, t => {
                t.Progress.Report(progress);

                SpinWait.SpinUntil(() => mockProgressDialog.Message == progress,
                                   TimeSpan.FromMilliseconds(100));
            });
            task.Run();

            mockProgressDialog.Received().Message = progress;

            // Make sure we do not keep receiving progress reports after the task is done.
            mockProgressDialog.ClearReceivedCalls();
            await Task.Delay(100);
            Assert.That(mockProgressDialog.ReceivedCalls().Count, Is.LessThanOrEqualTo(1));
        }
    }
}
