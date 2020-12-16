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
using YetiCommon;
using YetiVSI.Util;
using Task = System.Threading.Tasks.Task;

namespace YetiVSI
{
    /// <summary>
    /// An exception thrown by tasks that have been explicitly aborted.
    /// The inner exception contains the abort reason provided to Abort.
    /// </summary>
    public class TaskAbortedException : Exception
    {
        public TaskAbortedException(Exception reason) : base("Task aborted", reason)
        {
        }
    }

    /// <summary>
    /// The handle to a cancelable operation within the operation itself.
    /// </summary>
    public interface ICancelable
    {
        /// <summary>
        /// Indicates if this operation has been canceled - via ICancelableTask.Cancel,
        /// ICancelabeTask.Abort, or by the user through a UI dialog.
        /// </summary>
        bool IsCanceled { get; }

        /// <summary>
        /// Returns the interface which is used to report progress of this operation to the user.
        /// </summary>
        IProgress<string> Progress { get; }

        /// <summary>
        /// Returns cancelation token which allows the client subscribe to cancel event.
        /// </summary>
        CancellationToken Token { get; }

        /// <summary>
        /// If this operation is canceled, then stop its execution.
        /// </summary>
        ///
        /// <exception cref="OperationCanceledException">
        /// Thrown if the task was canceled via ICancelableTask.Cancel or a UI dialog.
        /// </exception>
        /// <exception cref="TaskAbortedException">
        /// Thrown if the task was aborted via ICancelableTask.Abort.
        /// </exception>
        ///
        /// <remarks>
        /// Use IsCanceled to check for canceled/aborted state without throwing.
        /// </remarks>
        void ThrowIfCancellationRequested();
    }

    public class NothingToCancel : ICancelable
    {
        public bool IsCanceled => false;

        public IProgress<string> Progress { get; } = new Progress<string>();

        public CancellationToken Token => CancellationToken.None;

        public void ThrowIfCancellationRequested()
        {
        }
    }

    /// <summary>
    /// Encapsulates a task that runs on another thread, while blocking the current thread with a
    /// UI dialog that can cancel the operation.
    /// </summary>
    public interface ICancelableTask : ICancelable
    {
        /// <summary>
        /// Runs the encapsulated action on a background thread and blocks the UI with a dialog.
        /// </summary>
        ///
        /// <returns>
        /// 'true' is returned if the operation completes, and 'false' is returned if it was
        /// canceled by the user, by calling Cancel, or by throwing OperationCanceledException
        /// inside the task.
        /// </returns>
        ///
        /// <exception cref="InvalidOperationException">
        /// Thrown if Run is called more than once on this task.
        /// </exception>
        /// <exception cref="TaskAbortedException">
        /// Thrown if the task was aborted via Abort.
        /// </exception>
        /// <exception cref="Exception">
        /// Any exception thrown by the task (except OperationCanceledException) is re-thrown.
        /// </exception>
        bool Run();

        /// <summary>
        /// Cancels the operation. Doesn't actually stop execution, but dismisses the UI dialog and
        /// returns control to the call site where the operation was started.
        /// </summary>
        ///
        /// <remarks>
        /// This method is intended to be used outside the task itself. The task can cancel itself
        /// by throwing an OperationCanceledException.
        ///
        /// Calling Cancel() when IsCancelled is true results in a no-op.
        /// </remarks>
        void Cancel();

        /// <summary>
        /// Similar to Cancel except that Run throws a TaskAbortedException at the call site where
        /// the operation was started.
        /// </summary>
        ///
        /// <param name="reason">Used as the InnerException of TaskAbortedException</param>
        ///
        /// <remarks>
        /// This method is intended to be used outside the task itself. The task can abort itself by
        /// throwing the exception directly.
        ///
        /// Calling Abort when IsCanceled is true results in a no-op.
        /// </remarks>
        void Abort(Exception reason);
    }

    /// <summary>
    /// Encapsulates a cancelable task that runs on another thread and returns a value.
    /// </summary>
    /// <typeparam name="T">The type of the value returned by the operation</typeparam>
    public interface ICancelableTask<T> : ICancelableTask
    {
        /// <summary>
        /// Contains the result of running the encapsulated action, if it completed successfully.
        /// If the action failed or the task was canceled, the result is default(T).
        /// </summary>
        T Result { get; }
    }

    // Implements cancelable tasks. Use CancelableTask.Factory to instantiate a new task.
    public abstract class CancelableTask : ICancelableTask, IProgress<string>
    {
        // Creates cancelable tasks. This class is substituted for testing.
        public class Factory
        {
            // Default delay before showing the progress dialog, chosen for consistency with the
            // previously used CommonMessagePump-based wait dialog.
            static readonly TimeSpan defaultDelay = TimeSpan.FromSeconds(2);

            static readonly TimeSpan defaultReportPeriod = TimeSpan.FromMilliseconds(100);

            readonly JoinableTaskContext taskContext;
            readonly ProgressDialog.Factory progressDialogFactory;
            readonly string title;
            readonly TimeSpan delay;
            readonly TimeSpan reportPeriod;

            // Instantiate a factory, setting YetiConstants.Title for the progress dialog title text
            // and using a default delay.
            public Factory(JoinableTaskContext taskContext,
                           ProgressDialog.Factory waitDialogFactory) : this(
                taskContext, waitDialogFactory, YetiConstants.YetiTitle, defaultDelay,
                defaultReportPeriod)
            {
                taskContext.ThrowIfNotOnMainThread();
            }

            public Factory(JoinableTaskContext taskContext,
                           ProgressDialog.Factory progressDialogFactory, string title,
                           TimeSpan delay, TimeSpan reportPeriod)
            {
                this.taskContext = taskContext;
                this.progressDialogFactory = progressDialogFactory;
                this.title = title;
                this.delay = delay;
                this.reportPeriod = reportPeriod;
            }

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            // Create a task that runs the given synchronous action on a background thread,
            // and waits on the resulting async task.
            public virtual ICancelableTask Create(string text, Func<ICancelable, Task> action)
            {
                return new NoResult(taskContext, progressDialogFactory.Create(title, text), action,
                                    delay, reportPeriod);
            }

            // Create a task that runs the given synchronous action on a background thread,
            // and waits on the resulting async task, and captures its result.
            public virtual ICancelableTask<T> Create<T>(string text,
                                                        Func<ICancelable, Task<T>> action)
            {
                return new WithResult<T>(taskContext, progressDialogFactory.Create(title, text),
                                         action, delay, reportPeriod);
            }
        }

        public bool IsCanceled => cancellationSource.IsCancellationRequested;
        public IProgress<string> Progress => this;

        public CancellationToken Token => cancellationSource.Token;

        readonly JoinableTaskContext taskContext;
        readonly IProgressDialog progressDialog;
        readonly TimeSpan delay;
        readonly CancellationTokenSource cancellationSource = new CancellationTokenSource();
        readonly TimeSpan reportPeriod;

        string reportValue = string.Empty;
        bool isStarted = false;
        bool isCompleted = false;
        Exception abortReason;

        CancelableTask(JoinableTaskContext taskContext, IProgressDialog progressDialog,
                       TimeSpan delay, TimeSpan reportPeriod)
        {
            this.taskContext = taskContext;
            this.progressDialog = progressDialog;
            this.delay = delay;
            this.reportPeriod = reportPeriod;
        }

        public bool Run()
        {
            taskContext.ThrowIfNotOnMainThread();

            if (isStarted)
            {
                throw new InvalidOperationException("Task can only be started once");
            }

            isStarted = true;
            if (!cancellationSource.IsCancellationRequested)
            {
                try
                {
                    var joinableTask = taskContext.Factory.RunAsync(
                        () => RunTaskAsync().WithCancellation(cancellationSource.Token));

                    if (reportPeriod > TimeSpan.Zero)
                    {
                        taskContext.Factory.RunAsync(() => GetReportTaskAsync());
                    }

                    if (!TryJoin(joinableTask, delay))
                    {
                        JoinWithProgressDialog(joinableTask);
                    }
                }
                catch (OperationCanceledException)
                {
                    cancellationSource.Cancel();
                }
            }

            // Handle aborted tasks. Aborted tasks are also canceled, so check abort first.
            if (abortReason != null)
            {
                throw new TaskAbortedException(abortReason);
            }

            // Report external Cancel() events.
            return !cancellationSource.IsCancellationRequested;
        }

        public void Cancel() => cancellationSource.Cancel();

        public void Abort(Exception reason)
        {
            abortReason = reason;
            Cancel();
        }

        void IProgress<string>.Report(string value)
        {
            reportValue = value;

            if (reportPeriod == TimeSpan.Zero)
            {
                ReportProgress();
            }
        }

        async Task GetReportTaskAsync()
        {
            while (!IsProgressCompletedOrCancelled())
            {
                await Task.Delay(reportPeriod, Token);
                ReportProgress();
            }
        }

        void ReportProgress()
        {
            if (!IsProgressCompletedOrCancelled())
            {
                taskContext.Factory.RunAsync(async () =>
                {
                    await taskContext.Factory.SwitchToMainThreadAsync();
                    progressDialog.Message = reportValue;
                });
            }
        }

        bool IsProgressCompletedOrCancelled()
        {
            return Token.IsCancellationRequested || isCompleted;
        }

        public void ThrowIfCancellationRequested()
        {
            if (IsCanceled)
            {
                if (abortReason != null)
                {
                    throw new TaskAbortedException(abortReason);
                }
                else
                {
                    throw new OperationCanceledException(cancellationSource.Token);
                }
            }
        }

        // Derived classes implement this method to start executing the task on a background thread.
        protected abstract Task RunTaskAsync();

        /// <summary>
        /// Try to join a task, with a timeout.
        /// </summary>
        /// <returns>
        /// True if the task ran to completion in the time allotted, false if the task is still
        /// running.
        /// </returns>
        bool TryJoin(JoinableTask joinableTask, TimeSpan timeout)
        {
            // Task.Delay is used instead of a CancellationToken because `Task.Delay(TimeSpan.Zero)`
            // always completes synchronously, whereas `new CancellationTokenSource(TimeSpan.Zero)`
            // may or may not be canceled by the time it returns.
            return taskContext.Factory.Run(async () =>
            {
                var task = joinableTask.JoinAsync();
                if (await Task.WhenAny(Task.Delay(delay), task) == task)
                {
                    await task; // Propagate exceptions
                    return true;
                }

                return false;
            });
        }

        /// <summary>
        /// Joins a task while displaying a progress dialog. If the dialog is closed by the user
        /// the entire CancelableTask is canceled.
        /// </summary>
        void JoinWithProgressDialog(JoinableTask joinableTask)
        {
            taskContext.ThrowIfNotOnMainThread();

            // Kick off a task that will mark the dialog as complete after joinableTask completes.
            var dialogTask = taskContext.Factory.RunAsync(async () =>
            {
                await taskContext.Factory.SwitchToMainThreadAsync();
                try
                {
                    await joinableTask.JoinAsync();
                }
                finally
                {
                    progressDialog.Complete();
                    isCompleted = true;
                }
            });

            // Display the dialog and block until it's closed. The dialog spins up its own message
            // pump, which allows tasks to continue executing continuations on the main thread
            // despite the fact that we're making a blocking call.
            // If dialogTask succeeds in marking the dialog as complete, it closes and ShowModal
            // returns true.
            // If the dialog is canceled by the user, it closes, ShowModal returns false, and we
            // cancel the task using the cancellation source.
            if (!progressDialog.ShowModal())
            {
                cancellationSource.Cancel();
            }

            dialogTask.Join();
        }

        // Implement a task that runs actions on a background thread.
        class NoResult : CancelableTask
        {
            readonly Func<Task> task;

            public NoResult(JoinableTaskContext taskContext, IProgressDialog dialog,
                            Func<ICancelable, Task> action, TimeSpan delay, TimeSpan reportPeriod)
                : base(taskContext, dialog, delay, reportPeriod)
            {
                task = () => action(this);
            }

            protected override Task RunTaskAsync()
            {
                return Task.Run(task); // Runs the task on a background thread
            }
        }

        // Implements a task that runs actions on a background thread and captures the results.
        class WithResult<T> : CancelableTask, ICancelableTask<T>
        {
            readonly Func<Task> task;

            public T Result { get; private set; }

            public WithResult(JoinableTaskContext taskContext, IProgressDialog dialog,
                              Func<ICancelable, Task<T>> func, TimeSpan delay,
                              TimeSpan reportPeriod) : base(taskContext, dialog, delay,
                                                            reportPeriod)
            {
                task = async () =>
                {
                    var result = await func(this);
                    if (!cancellationSource.IsCancellationRequested)
                    {
                        Result = result;
                    }
                };
            }

            protected override Task RunTaskAsync()
            {
                return Task.Run(task); // Runs the task on a background thread
            }
        }
    }

    public static class CancelableTaskFactoryExtensions
    {
        // Create a task that runs the given synchronous action on a background thread.
        public static ICancelableTask Create(this CancelableTask.Factory factory, string text,
                                             Action<ICancelable> action) => factory.Create(
            text, t =>
            {
                action(t);
                return Task.CompletedTask;
            });

        // Create a task that runs the given synchronous action on a background thread and returns
        // a value.
        public static ICancelableTask<T> Create<T>(this CancelableTask.Factory factory, string text,
                                                   Func<ICancelable, T> action) =>
            factory.Create(text, t => { return Task.FromResult(action(t)); });

        // Create a task that runs the given synchronous action on a background thread,
        // waits on the resulting async task, and captures its result.
        public static ICancelableTask<T> Create<T>(this CancelableTask.Factory factory, string text,
                                                   Func<Task<T>> action) =>
            factory.Create(text, _ => action());
    }
}