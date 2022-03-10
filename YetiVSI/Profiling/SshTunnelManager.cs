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
using YetiVSI.GameLaunch;

namespace YetiVSI.Profiling
{
    // Manages SSH tunnels for RenderDoc, RGP and Dive.
    // The tunnels are kept alive as long as the game is alive.
    public interface ISshTunnelManager
    {
        /// <summary>
        /// Starts SSH tunnel processes for profilers.
        /// </summary>
        /// <param name="target">SSH target of the gamelet instance</param>
        /// <param name="rgpEnabled">Whether to enable tunnels for the Radeon GPU Profiler</param>
        /// <param name="diveEnabled">Whether to enable tunnels for the Dive GPU Profiler</param>
        /// <param name="renderDocEnabled">Whether to enable tunnels for RenderDoc</param>
        void StartTunnelProcesses(SshTarget target, bool rgpEnabled, bool diveEnabled,
                                  bool renderDocEnabled);

        /// <summary>
        /// Uses the game launch to figure out when/if the game launches, monitors the game
        /// process and shuts down the tunnels when the game stopped. Must be called as soon as
        /// the launch is available or else the tunnels will be turned down.
        /// No-op if no tunnels have been started.
        /// </summary>
        /// <param name="target">SSH target of the gamelet instance</param>
        /// <param name="launch">The object to track the game launch via gRPC</param>
        void MonitorGameLifetime(SshTarget target, IVsiGameLaunch launch);
    }

    public class SshTunnelManager : ISshTunnelManager
    {
        readonly ManagedProcess.Factory _managedProcessFactory;
        readonly object _locker = new object();

        /// <summary>
        /// SshTunnelManager manages separate tunnels per gamelet instance. This class keeps track
        /// of that.
        /// </summary>
        class Instance
        {
            public List<SshTunnelProcess> Tunnels = new List<SshTunnelProcess>();
            public GameLifetimeWatcher LifetimeWatcher;
            public System.Timers.Timer LaunchTimer;

            public void StopLifetimeWatcher()
            {
                if (LifetimeWatcher != null)
                {
                    LifetimeWatcher.Stop();
                    LifetimeWatcher = null;
                }
            }

            public void StopLaunchTimer()
            {
                if (LaunchTimer != null)
                {
                    LaunchTimer.Stop();
                    LaunchTimer.Dispose();
                    LaunchTimer = null;
                }
            }

            public void StopTunnels()
            {
                Tunnels.ForEach(p => p.Stop());
                Tunnels.Clear();
            }

            public void AssertEmpty()
            {
                Debug.Assert(Tunnels.Count == 0);
                Debug.Assert(LifetimeWatcher == null);
                Debug.Assert(LaunchTimer == null);
            }
        }

        Dictionary<string, Instance> _ipToInstances = new Dictionary<string, Instance>();
        static TimeSpan _gameStartupTimeout = TimeSpan.FromSeconds(60);
        static TimeSpan _gameLaunchTimeout = TimeSpan.FromSeconds(20);

        public delegate void ShutdownEventHandler(object sender, ShutdownEventArgs args);

        public class ShutdownEventArgs : EventArgs
        {
            public bool Success { get; }
            public string ErrorMsg { get; }

            public ShutdownEventArgs(bool success, string errorMsg)
            {
                Success = success;
                ErrorMsg = errorMsg;
            }
        }

        public event ShutdownEventHandler ShutdownForTesting;
        public event EventHandler LaunchTimerTriggeredForTesting;

        public SshTunnelManager(ManagedProcess.Factory managedProcessFactory)
        {
            _managedProcessFactory = managedProcessFactory;
        }

        public void StartTunnelProcesses(SshTarget target, bool rgpEnabled, bool diveEnabled,
                                         bool renderDocEnabled)
        {
            GameLifetimeWatcher stoppedLifetimeWatcher = null;
            lock (_locker)
            {
                string key = Key(target);
                if (!_ipToInstances.TryGetValue(key, out Instance inst))
                {
                    inst = _ipToInstances[key] = new Instance();
                }

                // Stop all existing processes.
                stoppedLifetimeWatcher = inst.LifetimeWatcher;
                inst.StopLifetimeWatcher();
                inst.StopLaunchTimer();
                inst.StopTunnels();

                // Start new processes, if any.
                if (rgpEnabled)
                {
                    inst.Tunnels.Add(new SshTunnelProcess(WorkstationPorts.RGP_LOCAL,
                                                          WorkstationPorts.RGP_REMOTE, "RGP",
                                                          target, _managedProcessFactory));
                }

                if (diveEnabled)
                {
                    inst.Tunnels.Add(new SshTunnelProcess(WorkstationPorts.DIVE_LOCAL,
                                                          WorkstationPorts.DIVE_REMOTE, "Dive",
                                                          target, _managedProcessFactory));
                }

                if (renderDocEnabled)
                {
                    inst.Tunnels.Add(new SshTunnelProcess(WorkstationPorts.RENDERDOC_LOCAL,
                                                          WorkstationPorts.RENDERDOC_REMOTE,
                                                          "RenderDoc", target,
                                                          _managedProcessFactory));
                }

                if (inst.Tunnels.Count == 0)
                {
                    // No tunnels, so clean up map.
                    inst.AssertEmpty();
                    _ipToInstances.Remove(key);
                }
                else
                {
                    // Initiate tunnel self-destruction in case MonitorGameLifetime() isn't called.
                    inst.LaunchTimer = new System.Timers.Timer(_gameLaunchTimeout.TotalMilliseconds)
                    {
                        AutoReset = false, Enabled = true
                    };
                    inst.LaunchTimer.Elapsed += (sender, args) => OnLaunchTimeout(sender, target);
                }
            }

            // Wait for the watcher task to finish. This must be done outside of the lock to prevent
            // deadlocks!
            stoppedLifetimeWatcher?.Join();
        }

        public void MonitorGameLifetime(SshTarget target, IVsiGameLaunch launch)
        {
            lock (_locker)
            {
                if (!_ipToInstances.TryGetValue(Key(target), out Instance inst))
                {
                    // OnLaunchTimeout() was called concurrently and already stopped the tunnels.
                    return;
                }

                if (inst.LaunchTimer == null)
                {
                    Trace.WriteLine("MonitorGameLifetime() called too late. " +
                                    "profiler SSH tunnels already stopped.");
                    return;
                }

                Trace.WriteLine("Starting to monitor game lifetime for profiler SSH tunnels.");

                Debug.Assert(inst.Tunnels.Count > 0);
                Debug.Assert(inst.LifetimeWatcher == null);

                // Stop self-destruction.
                inst.StopLaunchTimer();

                inst.LifetimeWatcher = new GameLifetimeWatcher();
                GameLifetimeWatcher.DoneHandler onShutdown = (sender, success, errorMsg) =>
                {
                    OnShutdown(sender, target, success, errorMsg);
                };
                inst.LifetimeWatcher.Start(launch, _gameStartupTimeout, onShutdown);
            }
        }

        /// <summary>
        /// Stops the tunnels of the corresponding target. Called if StartTunnelProcesses() was
        /// run, but MonitorGameLifetime() was not called after some timeout. This means that the
        /// launch probably went wrong and was cancelled for some reason.
        /// </summary>
        void OnLaunchTimeout(object sender, SshTarget target)
        {
            lock (_locker)
            {
                string key = Key(target);
                if (!_ipToInstances.TryGetValue(key, out Instance inst))
                {
                    // StartTunnelProcesses() was called concurrently and removed the instance.
                    return;
                }

                if (inst.LaunchTimer != sender)
                {
                    // Either StartTunnelProcesses() was called concurrently, reset the object and
                    // started a new timer, or MonitorGameLifetime() was called and removed the
                    // timer. Ignore this callback.
                    return;
                }

                Debug.Assert(inst.LifetimeWatcher == null);

                Trace.WriteLine($"Launch was not available after {_gameLaunchTimeout}. " +
                                    "Stopping  SSH tunnels for profilers.");

                inst.StopTunnels();
                inst.StopLaunchTimer();
                inst.AssertEmpty();
                _ipToInstances.Remove(key);
                LaunchTimerTriggeredForTesting?.Invoke(this, new EventArgs());
            }
        }

        /// <summary>
        /// Starts the shutdown watcher on success or stops all tunnels on failure.
        /// Called in a background thread when the game lifetime watcher finishes.
        /// In the success case, pid is set to the game process ID.
        /// In the failure case (e.g. timeout, watcher unexpectedly exited), errorMsg is set.
        /// </summary>
        /// <param name="sender">The object calling this callback</param>
        /// <param name="target">SSH target of the gamelet instance</param>
        /// <param name="success">Whether the game shutdown was successfully detected</param>
        /// <param name="errorMsg">Error message in case of failure, null if success.</param>
        void OnShutdown(GameLifetimeWatcher sender, SshTarget target, bool success, string errorMsg)
        {
            lock (_locker)
            {
                string key = Key(target);
                if (!_ipToInstances.TryGetValue(key, out Instance inst))
                {
                    // StartTunnelProcesses() was called concurrently and removed the instance.
                    return;
                }

                if (inst.LifetimeWatcher != sender)
                {
                    // StartTunnelProcesses() was called concurrently and reset the process.
                    return;
                }

                inst.LifetimeWatcher = null;
                Debug.Assert(inst.LaunchTimer == null);

                // Game exited or error, stop wall tunnels.
                inst.StopTunnels();
                inst.AssertEmpty();
                _ipToInstances.Remove(key);
                ShutdownForTesting?.Invoke(this, new ShutdownEventArgs(success, errorMsg));

                if (!success)
                {
                    Trace.WriteLine($"Failed to detect game lifetime: {errorMsg}");
                }
            }
        }

        string Key(SshTarget target)
        {
            return $"{target.IpAddress}:{target.Port}";
        }

        public void SetStartupTimeoutForTesting(TimeSpan timeout)
        {
            _gameStartupTimeout = timeout;
        }

        public void SetLaunchTimeoutForTesting(TimeSpan timeout)
        {
            _gameLaunchTimeout = timeout;
        }
    }
}