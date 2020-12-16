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
using System.Windows.Threading;

namespace YetiVSITestsCommon
{
    /// <summary>
    /// Class that can be used to create a "main thread" and corresponding JoinableTaskContext for
    /// use in tests.
    /// </summary>
    /// <remarks>
    /// When code under test does not use the main thread or is completely synchronous, it should
    /// work fine to create a JoinableTaskContext directly instead of using this class. When testing
    /// code that uses JoinableTaskFactory.SwitchToMainThreadAsync(), this class should be used.
    /// </remarks>
    /// <example>
    /// var mainThreadContext = new FakeMainThreadContext();
    /// var foo = new ObjectThatCanSwitchToMainThread(mainThreadContext.JoinableTaskContext);
    ///
    /// // Some code that exercises foo
    ///
    /// mainThreadcontext.Dispose();
    /// </example>
    public class FakeMainThreadContext : IDisposable
    {
        /// <summary>
        /// JoinableTaskContext instance that considers the thread started by this class to be its
        /// "main thread".
        /// </summary>
        public JoinableTaskContext JoinableTaskContext { get; }

        private readonly Dispatcher dispatcher;

        /// <summary>
        /// Creates a new FakeMainThreadContext and spins up a new thread.
        /// </summary>
        public FakeMainThreadContext()
        {
            var dispatcherSource = new TaskCompletionSource<Dispatcher>();
            var thread = new Thread(() =>
            {
                // Create a dispatcher for the current thread.
                dispatcherSource.SetResult(Dispatcher.CurrentDispatcher);

                // Tell the current dispatcher to process work items until it is shut down.
                Dispatcher.Run();
            });
            thread.Name = nameof(FakeMainThreadContext);
            thread.IsBackground = true;
            thread.Start();

            // Synchronously waiting on tasks or awaiters may cause deadlocks. Use await or
            // JoinableTaskFactory.Run instead.
#pragma warning disable VSTHRD002
            dispatcher = dispatcherSource.Task.Result;
#pragma warning restore VSTHRD002

            JoinableTaskContext = new JoinableTaskContext(thread,
                new DispatcherSynchronizationContext(dispatcher));
        }

        /// <summary>
        /// Disposes of the FakeMainThreadContext, and signals that the underlying thread should
        /// stop running.
        /// </summary>
        public void Dispose() => dispatcher.InvokeShutdown();
    }
}
