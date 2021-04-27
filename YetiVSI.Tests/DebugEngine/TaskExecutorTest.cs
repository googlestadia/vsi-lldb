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

using Microsoft.VisualStudio.Threading;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YetiCommon;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class TaskExecutorTest
    {
        JoinableTaskFactory _taskFactory;
        ITaskExecutor _taskExecutor;
        FakeMainThreadContext _mainThreadContext;

        // Dummy names for metrics, not relevant for this test.
        const string _dummyMethodName = "SomeMethod";
        static readonly Type _dummyType = typeof(TaskExecutorTest);

        [SetUp]
        public async Task SetUpAsync()
        {
            _mainThreadContext = new FakeMainThreadContext();
            _taskFactory = _mainThreadContext.JoinableTaskContext.Factory;

            await _taskFactory.SwitchToMainThreadAsync();
            _taskExecutor = new TaskExecutor(_taskFactory);
            _taskExecutor.StartAsyncTasks(null);
        }

        [TearDown]
        public void TearDown()
        {
            _taskExecutor.AbortAsyncTasks();
            _mainThreadContext.Dispose();
        }

        [Test]
        public async Task SubmitAsyncTaskAsync()
        {
            Task<bool> task = _taskExecutor.SubmitAsync(async () =>
            {
                await DoSomethingAsync();
                return true;
            }, CancellationToken.None, _dummyMethodName, _dummyType);

            Assert.True(await task.WithTimeout(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public async Task RunTaskAsync()
        {
            await _taskFactory.SwitchToMainThreadAsync();

            Func<bool> taskA = () => true;
            Assert.True(_taskExecutor.Run(taskA));
        }

        [Test]
        public async Task RunSyncTaskOnBackgroundThreadThrowsAsync()
        {
            await TaskScheduler.Default;
            Assert.Throws<InvalidOperationException>(
                () => _taskExecutor.Run(async () => await DoSomethingAsync()));
        }

        [Test]
        public async Task RunActionAsync()
        {
            await _taskFactory.SwitchToMainThreadAsync();

            var runA = false;
            Action taskA = () => { runA = true; };
            _taskExecutor.Run(taskA);
            Assert.True(runA);
        }

        [Test]
        public async Task RunAsyncAsSyncDoesNotWrapExceptionAsync()
        {
            await _taskFactory.SwitchToMainThreadAsync();

            Assert.Throws<NullReferenceException>(() => _taskExecutor.Run(async () =>
            {
                await DoSomethingAsync();
                throw new NullReferenceException();
            }));
        }

        [Test]
        public async Task RunAsyncFunctionSynchronouslyAsync()
        {
            await _taskFactory.SwitchToMainThreadAsync();

            var runA = false;
            Func<Task> taskA = async () =>
            {
                runA = true;
                await Task.CompletedTask;
            };
            _taskExecutor.Run(taskA);
            Assert.True(runA);
        }

        [Test]
        public async Task RunIsReentrantAsync()
        {
            await _taskFactory.SwitchToMainThreadAsync();

            var syncTaskSubmitted = new AsyncManualResetEvent();
            var asyncTaskStarted = new AsyncManualResetEvent();

            var asyncTaskCompleted = false;
            Task asyncTask = _taskExecutor.SubmitAsync(async () =>
            {
                asyncTaskStarted.Set();
                await syncTaskSubmitted.WaitAsync();
                asyncTaskCompleted = true;
            }, CancellationToken.None, _dummyMethodName, _dummyType);
            await asyncTaskStarted.WaitAsync();

            var syncTaskRan = false;
            syncTaskSubmitted.Set();
            Assert.False(asyncTaskCompleted);
            _taskExecutor.Run(() => { syncTaskRan = _taskExecutor.Run(() => true); });

            Assert.True(asyncTaskCompleted);
            await asyncTask;

            Assert.True(syncTaskRan);
        }

        [Test]
        public async Task RunWhenInsideAsyncTaskAsync()
        {
            await _taskFactory.SwitchToMainThreadAsync();
            var asyncTaskStarted = new AsyncManualResetEvent();
            var runAsyncTask = new AsyncManualResetEvent();

            Task<bool> asyncTask = _taskExecutor.SubmitAsync(async () =>
            {
                asyncTaskStarted.Set();
                await runAsyncTask.WaitAsync();
                return _taskExecutor.Run(() => true);
            }, CancellationToken.None, _dummyMethodName, _dummyType);

            // Make sure async task gave access to synchronous code before taskExecutor.Run task is
            // invoked. Otherwise taskFactory.RunAsync invoked after submitting async task can
            // immediately complete async task before assigning currentAsyncTask, and when
            // taskExecutor.Run is invoked, currentAsyncTask is null, sync task is not blocked,
            // but this is not the scenario we want to test.
            await asyncTaskStarted.WaitAsync();
            runAsyncTask.Set();

            Assert.True(await asyncTask);
        }

        [Test]
        public async Task RunIsBlockedWhenAnotherAsyncTaskIsRunningAsync()
        {
            await _taskFactory.SwitchToMainThreadAsync();
            var asyncTaskStarted = new AsyncManualResetEvent();
            var runAsyncTask = new AsyncManualResetEvent();
            Task asyncTask = _taskExecutor.SubmitAsync(async () =>
            {
                asyncTaskStarted.Set();
                await runAsyncTask.WaitAsync();
            }, CancellationToken.None, _dummyMethodName, _dummyType);

            // Make sure async task actually started before submitting sync task.
            await asyncTaskStarted.WaitAsync();
            runAsyncTask.Set();

            // Should be false, since the async task can't run to completion without access to the
            // main thread.
            Assert.False(asyncTask.IsCompleted);

            _taskExecutor.Run(() =>
            {
                // Should be true if and only if taskExecutor.Run() joined the async task before
                // executing the sync task, thus letting the async task access the main thread and
                // run to completion.
                Assert.True(asyncTask.IsCompleted);
            });
            await asyncTask;
        }

        [Test]
        public async Task AsyncOperationCancelledIfRequestedAsync()
        {
            await _taskFactory.SwitchToMainThreadAsync();

            var asyncOperationStarted = new AsyncManualResetEvent();
            var taskCancelled = new AsyncManualResetEvent();
            var source = new CancellationTokenSource();

            Task asyncOperation = _taskExecutor.SubmitAsync(async () =>
            {
                asyncOperationStarted.Set();
                await taskCancelled.WaitAsync();
                Assert.Throws<OperationCanceledException>(
                    () => _taskExecutor.CancelAsyncOperationIfRequested());
            }, source.Token, _dummyMethodName, _dummyType);

            await asyncOperationStarted.WaitAsync();

            source.Cancel();
            taskCancelled.Set();

            Assert.ThrowsAsync<TaskCanceledException>(async () => await asyncOperation);
        }

        [Test]
        public async Task
            CancelAsyncOperationIfRequestedDoesNotThrowWhenOutsideOfAsyncOperationAsync()
        {
            await _taskFactory.SwitchToMainThreadAsync();

            var asyncOperationStarted = new AsyncManualResetEvent();
            var taskCancelled = new AsyncManualResetEvent();
            var source = new CancellationTokenSource();

            Task asyncOperation = _taskExecutor.SubmitAsync(async () =>
            {
                asyncOperationStarted.Set();
                await taskCancelled.WaitAsync();
            }, source.Token, _dummyMethodName, _dummyType);

            await asyncOperationStarted.WaitAsync();

            source.Cancel();
            _taskExecutor.CancelAsyncOperationIfRequested();
            taskCancelled.Set();

            Assert.ThrowsAsync<TaskCanceledException>(async () => await asyncOperation);
        }

        [Test]
        public void CancelAsyncTask()
        {
            var run = false;
            var source = new CancellationTokenSource();
            source.Cancel();
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await _taskExecutor.SubmitAsync(async () =>
                {
                    await DoSomethingAsync();
                    run = true;
                }, source.Token, _dummyMethodName, _dummyType);
            });
            source.Dispose();
            Assert.False(run);
        }

        [Test]
        public async Task SyncCodeIsExecutedWithHigherPriorityAsync()
        {
            await _taskFactory.SwitchToMainThreadAsync();

            var results = new List<string>();
            var asyncTaskStarted = new AsyncManualResetEvent(false);
            var syncTaskSubmitted = new AsyncManualResetEvent(false);
            Task taskA = _taskExecutor.SubmitAsync(async () =>
            {
                results.Add("A_STARTED");
                asyncTaskStarted.Set();
                await syncTaskSubmitted.WaitAsync().WithTimeout(TimeSpan.FromSeconds(5));
                results.Add("A_FINISHED");
            }, CancellationToken.None, _dummyMethodName, _dummyType);
            Task taskB = _taskExecutor.SubmitAsync(async () =>
            {
                results.Add("B_STARTED");
                await DoSomethingAsync();
                results.Add("B_FINISHED");
            }, CancellationToken.None, _dummyMethodName, _dummyType);

            await asyncTaskStarted.WaitAsync().WithTimeout(TimeSpan.FromSeconds(5));
            syncTaskSubmitted.Set();
            _taskExecutor.Run(() =>
            {
                results.Add("C_SYNC_STARTED");
                results.Add("C_SYNC_FINISHED");
            });

            await Task.WhenAll(taskA, taskB).WithTimeout(TimeSpan.FromSeconds(5));

            Assert.AreEqual(new List<string>
            {
                "A_STARTED", "A_FINISHED", "C_SYNC_STARTED", "C_SYNC_FINISHED", "B_STARTED",
                "B_FINISHED"
            }, results);
        }

        [Test]
        public async Task ValidateEventTraceFlowWhenSubmitAsync()
        {
            await _taskFactory.SwitchToMainThreadAsync();

            bool startEventInvoked = false;
            bool taskExecuted = false;
            bool endEventInvoked = false;

            _taskExecutor.OnAsyncTaskStarted += (sender, args) =>
            {
                startEventInvoked = !taskExecuted && !endEventInvoked;
            };
            _taskExecutor.OnAsyncTaskEnded += (sender, args) =>
            {
                endEventInvoked = startEventInvoked && taskExecuted;
            };

            Task task = _taskExecutor.SubmitAsync(async () =>
            {
                await DoSomethingAsync();
                taskExecuted = true;
            }, CancellationToken.None, _dummyMethodName, _dummyType);
            await task;
            Assert.True(endEventInvoked);
        }


        async Task DoSomethingAsync()
        {
            await Task.Yield();
        }
    }
}