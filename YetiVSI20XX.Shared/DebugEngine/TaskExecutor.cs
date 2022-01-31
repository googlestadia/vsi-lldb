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
using System;
using System.Threading;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine
{
    public sealed class TaskExecutor : ITaskExecutor
    {
        readonly AsyncQueue<Func<Task>> _asyncTasksQueue = new AsyncQueue<Func<Task>>();
        readonly JoinableTaskFactory _taskFactory;

        // Used to provide reentrancy when Run is called from async task.
        readonly System.Threading.AsyncLocal<bool> _insideAsync
            = new System.Threading.AsyncLocal<bool>();
        CancellationToken _currentOperationCancellationToken;

        bool _started = false;
        JoinableTask _currentAsyncTask;

        public event EventHandler OnAsyncTaskStarted;
        public event EventHandler<AsyncTaskEndedEventArgs> OnAsyncTaskEnded;

        public TaskExecutor(JoinableTaskFactory taskFactory)
        {
            _taskFactory = taskFactory;
        }

        public void StartAsyncTasks(Action<Exception> OnError)
        {
            if (_started)
            {
                throw new InvalidOperationException("TaskExecutor has already started.");
            }

            _started = true;

#pragma warning disable VSTHRD110 // Observe result of async calls
            _taskFactory.RunAsync(async delegate
#pragma warning restore VSTHRD110 // Observe result of async calls
            {
                try
                {
                    await ExecuteAsync();
                }
                catch (Exception e)
                {
                    // ExecuteAsync should never throw any exceptions under normal circumstances.
                    // Exceptions happening in async tasks should be processed by async tasks
                    // themselves and communicated to VS. If exception is happening in
                    // ExecuteAsync, no more async tasks will be executed, so we should terminate
                    // the extension. Throwing exception from inside of taskFactory.RunAsync
                    // won't be processed.
                    OnError.Invoke(e);
                }
            });
        }

        public void AbortAsyncTasks()
        {
            _asyncTasksQueue.Complete();
        }

        public Task<T> SubmitAsync<T>(Func<Task<T>> asyncTask, CancellationToken token,
                                      string callerMethodName, Type callerType)
        {
            var completionSource = new TaskCompletionSource<T>();
            token.Register(() => completionSource.TrySetCanceled(token));

            _asyncTasksQueue.Enqueue(async () =>
            {
                OnAsyncTaskStarted?.Invoke(this, EventArgs.Empty);
                try
                {
                    if (!token.IsCancellationRequested)
                    {
                        _insideAsync.Value = true;
                        _currentOperationCancellationToken = token;
                        completionSource.TrySetResult(await asyncTask());
                    }
                }
                catch (Exception e)
                {
                    completionSource.TrySetException(e);
                }
                finally
                {
                    var eventArgs = new AsyncTaskEndedEventArgs
                    {
                        CallerName = callerMethodName, CallerType = callerType
                    };
                    OnAsyncTaskEnded?.Invoke(this, eventArgs);
                    _currentOperationCancellationToken = CancellationToken.None;
                    _insideAsync.Value = false;
                }
            });
            return completionSource.Task;
        }

        public Task SubmitAsync(Func<Task> asyncTask, CancellationToken token,
                                string callerMethodName, Type callerType) =>
            SubmitAsync(async () =>
            {
                await asyncTask();
                return true;
            }, token, callerMethodName, callerType);

        public T Run<T>(Func<T> task)
        {
            _taskFactory.Context.ThrowIfNotOnMainThread();
            WaitForAsyncTaskToComplete();
            return task();
        }

        public void Run(Action task) =>
            Run(() =>
            {
                task();
                return true;
            });

        public void Run(Func<Task> task) =>
            Run(async () =>
            {
                await task();
                return true;
            });

        public T Run<T>(Func<Task<T>> task)
        {
            _taskFactory.Context.ThrowIfNotOnMainThread();
            WaitForAsyncTaskToComplete();
            return _taskFactory.Run(task);
        }

        public void CancelAsyncOperationIfRequested()
        {
            if (_insideAsync.Value)
            {
                _currentOperationCancellationToken.ThrowIfCancellationRequested();
            }
        }

        async Task ExecuteAsync()
        {
            await _taskFactory.SwitchToMainThreadAsync();

            while (true)
            {
                Func<Task> currentTask;
                try
                {
                    currentTask = await _asyncTasksQueue.DequeueAsync();
                }
                catch (TaskCanceledException)
                {
                    // asyncTasksQueue.Complete() has been called.
                    return;
                }

                _currentAsyncTask = _taskFactory.RunAsync(currentTask);
                await _currentAsyncTask;
                _currentAsyncTask = null;
            }
        }

        void WaitForAsyncTaskToComplete()
        {
            if (!_insideAsync.Value)
            {
                _currentAsyncTask?.Join();
            }
        }

        public bool IsInsideAsyncContext() => _insideAsync.Value;
    }

    public class AsyncTaskEndedEventArgs : EventArgs
    {
        public string CallerName { get; set; }
        public Type CallerType { get; set; }
    }
}