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
using System.Linq;
using System.Threading.Tasks;
using GgpGrpc.Models;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.GameLaunch;
using YetiVSI.Profiling;

namespace YetiVSI.Test.Profiling
{
    class SshTunnelManagerTests
    {
        const string _targetIp = "1.2.3.4";
        const string _targetPort = "567";
        static readonly TimeSpan _eventTimeout = TimeSpan.FromSeconds(5);

        static readonly GgpGrpc.Models.GameLaunch _launchPrepping = new GgpGrpc.Models.GameLaunch
            { GameLaunchState = GameLaunchState.ReadyToPlay, GameletName = "gamelet" };

        static readonly GgpGrpc.Models.GameLaunch _launchRunning = new GgpGrpc.Models.GameLaunch
            { GameLaunchState = GameLaunchState.RunningGame, GameletName = "gamelet" };

        static readonly GgpGrpc.Models.GameLaunch _launchEnded = new GgpGrpc.Models.GameLaunch
        {
            GameLaunchState = GameLaunchState.GameLaunchEnded,
            GameLaunchEnded = new GameLaunchEnded(EndReason.ExitedByUser),
            GameletName = "gamelet"
        };

        IVsiGameLaunch _launch;
        SshTarget _target;
        ManagedProcess.Factory _processFactory;
        IProcess _rgpTunnel;
        IProcess _diveTunnel;
        IProcess _renderdocTunnel;
        SshTunnelManager _manager;

        // Populated when the GameLifetimeWatcher calls the handler, i.e. when the game shuts down
        // or the watcher times out.
        bool _shutdownSuccess;
        string _shutdownErrorMsg;
        readonly AsyncManualResetEvent _shutdownReceived = new AsyncManualResetEvent();

        [SetUp]
        public void SetUp()
        {
            _launch = Substitute.For<IVsiGameLaunch>();

            _target = new SshTarget($"{_targetIp}:{_targetPort}");
            _processFactory = Substitute.For<ManagedProcess.Factory>();

            _rgpTunnel = Substitute.For<IProcess>();
            SetupMockProcess(_rgpTunnel, WorkstationPorts.RGP_LOCAL.ToString(), _targetIp);

            _diveTunnel = Substitute.For<IProcess>();
            SetupMockProcess(_diveTunnel, WorkstationPorts.DIVE_LOCAL.ToString(), _targetIp);

            _renderdocTunnel = Substitute.For<IProcess>();
            SetupMockProcess(_renderdocTunnel, WorkstationPorts.RENDERDOC_LOCAL.ToString(),
                             _targetIp);

            _shutdownReceived.Reset();
            _shutdownSuccess = false;
            _shutdownErrorMsg = null;

            _manager = new SshTunnelManager(_processFactory);
            _manager.ShutdownForTesting += (sender, args) =>
            {
                _shutdownSuccess = args.Success;
                _shutdownErrorMsg = args.ErrorMsg;
                _shutdownReceived.Set();
            };
        }

        [Test]
        public void StartWithNoProfilers()
        {
            _manager.StartTunnelProcesses(_target, false, false, false);
            _processFactory.DidNotReceive().Create(Arg.Any<ProcessStartInfo>(), Arg.Any<int>());
        }

        [Test]
        public void StartRgp()
        {
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchPrepping));
            _manager.StartTunnelProcesses(_target, rgpEnabled: true, false, false);
            _rgpTunnel.Received().Start();
            _rgpTunnel.DidNotReceive().Kill();
        }

        [Test]
        public void StartDive()
        {
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchPrepping));
            _manager.StartTunnelProcesses(_target, false, diveEnabled: true, false);
            _diveTunnel.Received().Start();
            _diveTunnel.DidNotReceive().Kill();
        }

        [Test]
        public void StartRenderdoc()
        {
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchPrepping));
            _manager.StartTunnelProcesses(_target, false, false, renderDocEnabled: true);
            _renderdocTunnel.Received().Start();
            _renderdocTunnel.DidNotReceive().Kill();
        }

        [Test]
        public async Task TunnelsStoppedIfMonitorGameLifetimeNotCalledAsync()
        {
            // Set timeout to zero. This will cause the LaunchTimer to fire immediately and
            // stop all tunnels.
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchPrepping));
            _manager.SetLaunchTimeoutForTesting(TimeSpan.FromMilliseconds(1));
            _manager.StartTunnelProcesses(_target, true, true, true);
            AsyncManualResetEvent launchTimerTriggered = new AsyncManualResetEvent();
            _manager.LaunchTimerTriggeredForTesting += (s, a) => { launchTimerTriggered.Set(); };

            // Wait for the timer to fire that shuts down the processes.
            await launchTimerTriggered.WaitAsync().WithTimeout(_eventTimeout);
            _rgpTunnel.Received().Kill();
            _diveTunnel.Received().Kill();
            _renderdocTunnel.Received().Kill();
        }

        [Test]
        public async Task LifetimeWatcherStopsTunnelsOnTimeoutAsync()
        {
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchPrepping));
            _manager.SetStartupTimeoutForTesting(TimeSpan.Zero);

            _manager.StartTunnelProcesses(_target, true, true, true);
            _manager.MonitorGameLifetime(_target, _launch);

            await _shutdownReceived.WaitAsync().WithTimeout(_eventTimeout);
            Assert.IsFalse(_shutdownSuccess);
            StringAssert.Contains("Timed out", _shutdownErrorMsg);
            _rgpTunnel.Received().Kill();
            _diveTunnel.Received().Kill();
            _renderdocTunnel.Received().Kill();
        }

        [Test]
        public async Task LifetimeWatcherStopsTunnelsOnGameExitAsync()
        {
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchEnded));
            _manager.StartTunnelProcesses(_target, true, true, true);
            _manager.MonitorGameLifetime(_target, _launch);

            await _shutdownReceived.WaitAsync().WithTimeout(_eventTimeout);
            Assert.IsTrue(_shutdownSuccess);
            _rgpTunnel.Received().Kill();
            _diveTunnel.Received().Kill();
            _renderdocTunnel.Received().Kill();
        }

        [Test]
        public void RestartStopsTunnelsAndStartupWatcher()
        {
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchPrepping));
            _manager.StartTunnelProcesses(_target, true, true, true);
            _manager.MonitorGameLifetime(_target, _launch);

            _rgpTunnel.Received().Start();
            _diveTunnel.Received().Start();
            _renderdocTunnel.Received().Start();

            _manager.StartTunnelProcesses(_target, false, false, false);
            _manager.MonitorGameLifetime(_target, _launch);

            _rgpTunnel.Received().Kill();
            _diveTunnel.Received().Kill();
            _renderdocTunnel.Received().Kill();
        }

        [Test]
        public async Task TracksGameletsIndependentlyAsync()
        {
            var target2 = new SshTarget("4.3.2.1:599");
            var launch2 = Substitute.For<IVsiGameLaunch>();
            var rgpTunnel2 = Substitute.For<IProcess>();
            SetupMockProcess(rgpTunnel2, WorkstationPorts.RGP_LOCAL.ToString(), target2.IpAddress);

            _manager.StartTunnelProcesses(_target, true, true, true);
            _rgpTunnel.Received().Start();
            rgpTunnel2.DidNotReceive().Start();

            _manager.StartTunnelProcesses(target2, true, true, true);
            rgpTunnel2.Received().Start();

            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchEnded));
            _manager.MonitorGameLifetime(_target, _launch);
            await _shutdownReceived.WaitAsync().WithTimeout(_eventTimeout);
            Assert.IsTrue(_shutdownSuccess);
            _rgpTunnel.Received().Kill();
            rgpTunnel2.DidNotReceive().Kill();

            launch2.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchRunning));
            _manager.MonitorGameLifetime(target2, launch2);
            rgpTunnel2.DidNotReceive().Kill();

            _manager.StartTunnelProcesses(target2, false, false, false);
            rgpTunnel2.Received().Kill();
        }

        /// <summary>
        /// Sets up _processFactory.Create() to return |process| if the process startup args contain
        /// all strings in |startupArgsContain|.
        /// </summary>
        void SetupMockProcess(IProcess process, params string[] startupArgsContain)
        {
            _processFactory
                .Create(
                    Arg.Is<ProcessStartInfo>(
                        x => startupArgsContain.All(s => x.Arguments.Contains(s))), Arg.Any<int>())
                .Returns(process);
        }
    }
}