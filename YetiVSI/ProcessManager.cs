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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using YetiCommon;
using YetiVSI.DebugEngine.Exit;

namespace YetiVSI
{
    /// <summary>
    /// ProcessManager launches and monitors processes necessary for the debugger to work
    /// (debug proxy client, game client, port forwarder).
    /// If any component exits, this is responsible for reporting it and cleaning up everything else.
    /// </summary>
    class ProcessManager
    {
        public delegate void ProcessExitHandler(string processName, int exitCode);
        public event ProcessExitHandler OnProcessExit;

        public delegate void ProcessStopHandler(IProcess process, ExitReason exitReason);
        readonly Dictionary<IProcess, ProcessStopHandler> _processes;

        public ProcessManager()
        {
            _processes = new Dictionary<IProcess, ProcessStopHandler>();
        }

        // Note that any process registered with the manager will be killed on task cancellation.
        public static ProcessManager CreateForCancelableTask(ICancelable task)
        {
            var processManager = new ProcessManager();
            task.Token.Register(() => processManager.StopAll(ExitReason.AttachCanceled));
            return processManager;
        }

        /// <summary>
        /// Adds a running process to be managed.
        /// </summary>
        /// <param name="process">The process to watch.</param>
        /// <param name="stopHandler">
        /// If set, the handler is called during StopAll(). It may be used to attempt a graceful
        /// shutdown. In any case, the process is killed after calling the stopHandler (rule #2:
        /// double tap).
        /// </param>
        public void AddProcess(IProcess process, ProcessStopHandler stopHandler = null)
        {
            _processes[process] = stopHandler;
        }

        /// <summary>
        /// Attempts to stop all processes, then clears the process list.
        /// </summary>        
        public void StopAll(ExitReason exitReason)
        {
            List<KeyValuePair<IProcess, ProcessStopHandler>> processesCopy;
            lock (_processes)
            {
                processesCopy = _processes.ToList();
                _processes.Clear();
            }
            foreach (var (process, stopHandler) in processesCopy.Select(x => (x.Key, x.Value)))
            {
                try
                {
                    try
                    {
                        stopHandler?.Invoke(process, exitReason);
                    }
                    finally
                    {
                        process.Kill();
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        public void OnExit(object sender, EventArgs e)
        {
            var process = (IProcess)sender;

            lock (_processes)
            {
                if (!_processes.ContainsKey(process))
                {
                    return;
                }

                _processes.Remove(process);
            }

            try
            {
                OnProcessExit?.Invoke(process.StartInfo.FileName, process.ExitCode);
            }
            // This should never happen, but for some reason it does, see (internal).
            catch (InvalidOperationException exception)
            {
                Trace.WriteLine("Failed to read an exit code of the process " +
                    $"{process.StartInfo.FileName} due to `{exception.Message}`, " +
                    "the process is already disposed.");
                OnProcessExit?.Invoke(process.StartInfo.FileName, 0);
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
