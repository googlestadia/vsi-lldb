// Copyright 2022 Google LLC
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

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GgpGrpc.Models;
using Microsoft.VisualStudio.Threading;
using YetiVSI.GameLaunch;

namespace YetiVSI.Profiling
{
    /// <summary>
    /// Watches a game launch and triggers a handler when the game ends finishes.
    /// </summary>
    public class GameLifetimeWatcher
    {
        static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(10);

        public delegate void DoneHandler(GameLifetimeWatcher sender, bool success, string errorMsg);

        Task _watcherTask;
        CancellationTokenSource _cancelSource;
        TimeSpan _startupTimeout;

        /// <summary>Waits for the game to launch and gets its process ID</summary>
        /// <param name="launch">Filename of the process to watch to appear</param>
        /// <param name="startupTimeout">
        /// Timeout for game startup. If the game has not started after this timeout, the handler is
        /// called with an error.
        /// </param>
        /// <param name="onDone">Handler to call when the process appears or timeout occurs.</param>
        public void Start(IVsiGameLaunch launch, TimeSpan startupTimeout, DoneHandler onDone)
        {
            if (_watcherTask != null)
            {
                throw new InvalidOperationException("Start called twice");
            }

            Trace.WriteLine("Starting game lifetime watcher");
            _startupTimeout = startupTimeout;
            _cancelSource = new CancellationTokenSource();
            _watcherTask = Task.Run(() => PollForGameEndAsync(launch, onDone, _cancelSource.Token),
                                    _cancelSource.Token);
        }

        /// <summary>
        /// Stops the game lifetime watcher. No-op if already stopped.
        /// </summary>
        public void Stop()
        {
            if (_cancelSource == null)
            {
                return;
            }

            _cancelSource.Cancel();
            // Don't wait for the task here, it might deadlock!
        }

        /// <summary>
        /// Waits for the watcher to finish.
        /// </summary>
        public void Join()
        {
            if (_cancelSource == null)
            {
                return;
            }

            Task.WaitAll(_watcherTask);
            _watcherTask = null;

            _cancelSource.Dispose();
            _cancelSource = null;
        }

        async Task PollForGameEndAsync(IVsiGameLaunch launch, DoneHandler onDone,
                                       CancellationToken token)
        {
            try
            {
                bool launched = false;
                Stopwatch stopWatch = new Stopwatch();
                while (true)
                {
                    GgpGrpc.Models.GameLaunch state =
                        await launch.GetLaunchStateAsync(null).WithCancellation(token);
                    if (state.GameLaunchState == GameLaunchState.GameLaunchEnded)
                    {
                        onDone.Invoke(this, true, null);
                        return;
                    }

                    if (state.GameLaunchState == GameLaunchState.RunningGame)
                    {
                        launched = true;
                    }

                    if (!launched && stopWatch.Elapsed >= _startupTimeout)
                    {
                        onDone.Invoke(this, false, "Timed out waiting for game to start");
                    }

                    await Task.Delay(_pollInterval, token);
                }
            }
            catch (TaskCanceledException)
            {
                // Stop() was called.
            }
        }
    }
}