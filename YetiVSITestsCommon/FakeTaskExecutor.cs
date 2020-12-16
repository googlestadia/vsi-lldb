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

﻿using System;
using System.Threading;
using System.Threading.Tasks;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSITestsCommon
{
    /// <summary>
    /// Task Executor stub which executes all the submitted tasks immediately.
    /// </summary>
    public class FakeTaskExecutor : ITaskExecutor
    {
        public event EventHandler OnAsyncTaskStarted;

        public event EventHandler<AsyncTaskEndedEventArgs> OnAsyncTaskEnded;

        public void AbortAsyncTasks()
        {
        }

        public void Run(Func<Task> task)
        {
            // Synchronously waiting on tasks or awaiters may cause deadlocks.
            // Use await or JoinableTaskFactory.Run instead.
#pragma warning disable VSTHRD002
            task.Invoke().Wait();
#pragma warning restore VSTHRD002
        }

        public void Run(Action task)
        {
            task.Invoke();
        }

        public T Run<T>(Func<Task<T>> task)
        {
            try
            {
                Task<T> internalTask = Task.Run(task);
                // Ignore "Synchronously waiting on tasks or awaiters may cause deadlocks. Use await
                // or JoinableTaskFactory.Run instead.". This fake synchronous method execution for
                // ITaskExecutor. Since this class always completes all the tasks immediately
                // (including asynchronous) the thread won't be blocked when reaching Wait()."
#pragma warning disable VSTHRD002
                internalTask.Wait();
                return internalTask.Result;
#pragma warning restore VSTHRD002
            }
            // Unwrap exception wrapped due to using Wait.
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    throw e.InnerException;
                }

                throw;
            }
        }

        public void CancelAsyncOperationIfRequested() => throw new NotImplementedException();

        public T Run<T>(Func<T> task)
        {
            return task.Invoke();
        }

        public void StartAsyncTasks(Action<Exception> OnError)
        {
        }

        public Task SubmitAsync(Func<Task> asyncTask, CancellationToken token,
                                string callerMethodName, Type callerType) =>
            SubmitAsync(async () =>
            {
                await asyncTask();
                return true;
            }, token, callerMethodName, callerType);

        public virtual Task<T> SubmitAsync<T>(Func<Task<T>> asyncTask, CancellationToken token,
                                              string callerMethodName, Type callerType)
        {
            try
            {
                OnAsyncTaskStarted?.Invoke(this, EventArgs.Empty);
                Task<T> internalTask = Task.Run(asyncTask);
#pragma warning disable VSTHRD103  // Wait synchronously blocks. Use await instead.
                internalTask.Wait();
#pragma warning restore VSTHRD103
                return internalTask;
            }
            // Unwrap exception wrapped due to using Wait.
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    throw e.InnerException;
                }

                throw;
            }
            finally
            {
                OnAsyncTaskEnded?.Invoke(this, new AsyncTaskEndedEventArgs());
            }
        }

        public bool IsInsideAsyncContext() => false;
    }
}
