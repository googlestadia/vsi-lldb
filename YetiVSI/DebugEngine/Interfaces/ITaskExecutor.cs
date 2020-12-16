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

namespace YetiVSI.DebugEngine.Interfaces
{
    public interface ITaskExecutor
    {
        /// <summary>
        /// Event triggered from the main thread when an async task starts executing. Since only
        /// one async task is run at the same time, an OnAsyncTaskStarted event is guaranteed to
        /// be followed by an OnAsyncTaskEnded event.
        /// </summary>
        event EventHandler OnAsyncTaskStarted;

        /// <summary>
        /// Event triggered from the main thread when an async task ends executing. Since only
        /// one async task is run at the same time, this event is called after OnAsyncTaskStarted.
        /// </summary>>
        event EventHandler<AsyncTaskEndedEventArgs> OnAsyncTaskEnded;

        /// <summary>
        /// Initiate the execution of async tasks submitted to the ITaskExecutor. The method should
        /// be called only once, otherwise it throws InvalidOperationException.
        /// In case of error occurred during tasks processing the program passed will be used to
        /// terminate the debug session.
        /// </summary>
        /// <param name="OnError">Handler to be invoked on error.</param>
        void StartAsyncTasks(Action<Exception> OnError);

        /// <summary>
        /// Abort processing of async tasks submitted to the ITaskExecutor.
        /// </summary>
        void AbortAsyncTasks();

        /// <summary>
        /// Request asynchronous execution of the task which returns value of type T. Guarantees
        /// that no other tasks submitted to the task executor are executed concurrently.
        /// If the token has been cancelled, the returned task transitions into the Canceled state
        /// immediately. If the operation has not been started, then it will not run.
        /// </summary>
        /// <typeparam name="T">Type of the return value</typeparam>
        /// <param name="asyncTask">Asynchronous task to be executed</param>
        /// <param name="token">Cancellation token</param>
        /// <param name="callerMethodName">Name of the method that the task will execute</param>
        /// <param name="callerType">Type of the class which requests the task execution</param>
        /// <returns>Task to track the execution of asyncTask</returns>
        Task<T> SubmitAsync<T>(Func<Task<T>> asyncTask, CancellationToken token,
                               string callerMethodName, Type callerType);

        /// <summary>
        /// Request asynchronous execution of the task which doesn't return a value. Guarantees
        /// that no other tasks submitted to the task executor are executed concurrently.
        /// If the token has been cancelled, the returned task transitions into the Canceled state
        /// immediately. If the operation has not been started, then it will not run.
        /// </summary>
        /// <param name="asyncTask">Asynchronous task to be executed</param>
        /// <param name="token">Cancellation token</param>
        /// <param name="callerMethodName">Name of the method that the task will execute</param>
        /// <param name="callerType">Type of the class which requests the task execution</param>
        /// <returns>Task to track the execution of asyncTask.</returns>
        Task SubmitAsync(Func<Task> asyncTask, CancellationToken token, string callerMethodName,
                         Type callerType);

        /// <summary>
        /// Run synchronous task which returns value of type T. The task will be run immediately if
        /// there are no asynchronous tasks in progress, or as soon as the current task completes.
        /// Guarantees that the task is that no other tasks submitted to the task executor are
        /// executed concurrently.
        /// Can be used recursively.
        /// </summary>
        /// <typeparam name="T">Type of the return value</typeparam>
        /// <param name="task">Synchronous task to be executed</param>
        /// <returns>Result of the task execution</returns>
        T Run<T>(Func<T> task);

        /// <summary>
        /// Run synchronous task which doesn't return a value. The task will be run immediately if
        /// there are no asynchronous tasks in progress, or as soon as the current task completes.
        /// Guarantees that the task is that no other tasks submitted to the task executor are
        /// executed concurrently.
        /// Can be used recursively.
        /// </summary>
        /// <param name="task">Task to be executed</param>
        void Run(Action task);

        /// <summary>
        /// Synchronously run asynchronous task which doesn't return a value. The task will be run
        /// immediately if there are no asynchronous tasks in progress, or as soon as the current
        /// task completes.
        /// Guarantees that no other tasks submitted to the task executor are executed
        /// concurrently.
        /// Can be used recursively.
        /// </summary>
        /// <param name="task">Task to be executed</param>
        void Run(Func<Task> task);

        /// <summary>
        /// Synchronously run asynchronous task which returns value of type T. The task will be run
        /// immediately if there are no asynchronous tasks in progress, or as soon as the current
        /// task completes.
        /// Guarantees that no other tasks submitted to the task executor are executed
        /// concurrently.
        /// Can be used recursively.
        /// </summary>
        /// <param name="task">Task to be executed</param>
        /// <returns>Result of the task execution</returns>
        T Run<T>(Func<Task<T>> task);

        /// <summary>
        /// Attempt to cancel the currently running async operation if invoked from this operation,
        /// otherwise no-op.
        /// </summary>
        void CancelAsyncOperationIfRequested();

        /// <summary>
        /// Retrieves whether a call is made inside an async context.
        /// </summary>
        /// <returns>Whether the call is made inside an async context or not</returns>
        bool IsInsideAsyncContext();
    }
}