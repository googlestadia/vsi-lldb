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
using System.Collections.Generic;
using System.Diagnostics;
using YetiCommon;
using YetiCommon.SSH;

namespace YetiVSI.Profiling
{
    /// <summary>
    /// Wrapper for a managed process that runs an SSH tunnel.
    /// </summary>
    public class SshTunnelProcess
    {
        readonly string _name;

        IProcess _process;

        /// <summary>
        /// Starts a port forwarding process -L[localPort]:[remotePort].
        /// </summary>
        /// <param name="localPort">Local forwarding port</param>
        /// <param name="remotePort">Remote forwarding port</param>
        /// <param name="name">Human readable name used in log messages</param>
        /// <param name="target">SSH target to connect to</param>
        /// <param name="managedProcessFactory">Used to construct underlying tunnel process</param>
        /// <exception cref="ProcessException">
        /// Thrown if there is an error launching a process.
        /// </exception>
        public SshTunnelProcess(int localPort, int remotePort, string name, SshTarget target,
                                ManagedProcess.Factory managedProcessFactory)
        {
            _name = name;

            var ports = new List<ProcessStartInfoBuilder.PortForwardEntry>()
            {
                new ProcessStartInfoBuilder.PortForwardEntry
                {
                    LocalPort = localPort,
                    RemotePort = remotePort,
                }
            };
            ProcessStartInfo startInfo =
                ProcessStartInfoBuilder.BuildForSshPortForward(ports, target);

            Trace.WriteLine($"Starting SSH Tunnel for {_name}, " +
                            $"local port={localPort}, remote port={remotePort}");

            _process = managedProcessFactory.Create(startInfo);
            _process.OnExit += HandleExit;
            _process.Start();
        }

        /// <summary>
        /// Stops the SSH tunnel process. No-op if already stopped.
        /// </summary>
        public void Stop()
        {
            // Lock because HandleExit is called on a background thread.
            lock (this)
            {
                if (_process == null)
                {
                    return;
                }

                Trace.WriteLine($"Stopping SSH Tunnel for {_name}.");

                _process.Kill();
                _process.Dispose();
                _process = null;
            }
        }

        /// <summary>
        /// Prints a warning and disposes the process. Called if the process exited unexpectedly,
        /// i.e. if it lost connection, crashed, or if it was killed.
        /// </summary>
        void HandleExit(object sender, EventArgs e)
        {
            // Lock because HandleExit is called on a background thread.
            lock (this)
            {
                if (_process == null)
                {
                    // Stop() already disposed the process concurrently.
                    return;
                }

                Trace.WriteLine($"SSH Tunnel for {_name} exited unexpectedly.");

                _process.Dispose();
                _process = null;
            }
        }
    }
}