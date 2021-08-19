// Copyright 2021 Google LLC
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

using System.Collections.Generic;

namespace Google.VisualStudioFake.API.UI
{
    public enum ThreadsWindowState
    {
        /// <summary>
        /// Threads are stale and have to be refreshed.
        /// </summary>
        NotRefreshed,
        /// <summary>
        /// Threads are ready to be retrieved.
        /// </summary>
        Ready,
        /// <summary>
        /// Threads are queued to be refresh. Must wait until ready.
        /// </summary>
        Pending,
    }

    public interface IThreadsWindow
    {
        /// <summary>
        /// Queues a job to get all threads.
        ///
        /// Example usage:
        ///
        /// vsFake.DebugSession.ThreadsWindow.Refresh();
        /// vsFake.RunUntil(() => vsFake.DebugSession.ThreadsWindow.Ready);
        /// Assert.That(var.GetThreads().Count, Is.EqualTo(1));
        ///
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if another refresh operation is pending or no stack frame is selected.
        /// </exception>
        /// <remarks>
        /// This method runs asynchronously.
        /// </remarks>
        void Refresh();

        /// <summary>
        /// Gets all threads in the currently selected program.
        /// Must be in Ready state.
        /// </summary>
        /// <returns>List of available threads</returns>
        List<IThread> GetThreads();

        /// <summary>
        /// Gets the current state of the threads window.
        /// </summary>
        ThreadsWindowState State { get; }

        /// <summary>
        /// Shortcut for State == ThreadsWindowState.Ready.
        /// </summary>
        bool Ready { get; }
    }
}
