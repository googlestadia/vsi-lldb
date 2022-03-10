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
using System.Threading;
using System.Threading.Tasks;
using GgpGrpc.Models;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using YetiVSI.GameLaunch;
using YetiVSI.Profiling;

namespace YetiVSI.Test.Profiling
{
    class GameLifetimeWatcherTests
    {
        static readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(60);

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
        AsyncManualResetEvent _onDoneCalled;
        GameLifetimeWatcher.DoneHandler _onDone;
        GameLifetimeWatcher _watcher;

        [SetUp]
        public void SetUp()
        {
            _launch = Substitute.For<IVsiGameLaunch>();
            _onDoneCalled = new AsyncManualResetEvent();
            _watcher = new GameLifetimeWatcher();
            _onDone = Substitute.For<GameLifetimeWatcher.DoneHandler>();
            _onDone.When(x => x.Invoke(_watcher, Arg.Any<bool>(), Arg.Any<string>()))
                .Do(x => _onDoneCalled.Set());
        }

        [Test]
        public void StopWithoutStart()
        {
            _watcher.Stop();
        }

        [Test]
        public void StartStartThrows()
        {
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchPrepping));
            _watcher.Start(_launch, _startupTimeout, _onDone);
            Assert.Throws(typeof(InvalidOperationException),
                          () => _watcher.Start(_launch, _startupTimeout, _onDone));
        }

        [Test]
        public void StartStopNotLaunched()
        {
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchPrepping));

            _watcher.Start(_launch, _startupTimeout, _onDone);
            _watcher.Stop();

            _onDone.DidNotReceive().Invoke(_watcher, Arg.Any<bool>(), Arg.Any<string>());
        }

        [Test]
        public void DoubleStop()
        {
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchPrepping));
            _watcher.Start(_launch, _startupTimeout, _onDone);

            _watcher.Stop();
            _watcher.Stop();
        }

        [Test]
        public async Task StartLaunchedSucceedsAsync()
        {
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchEnded));

            _watcher.Start(_launch, _startupTimeout, _onDone);

            await _onDoneCalled.WaitAsync().WithTimeout(TimeSpan.FromSeconds(5));
            _onDone.Received().Invoke(_watcher, true, null);
        }

        [Test]
        public async Task StartupTimesOutWhenNotRunningAsync()
        {
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchPrepping));

            _watcher.Start(_launch, TimeSpan.Zero, _onDone);

            await _onDoneCalled.WaitAsync().WithTimeout(TimeSpan.FromSeconds(5));
            _onDone.Received().Invoke(_watcher, false,
                                      Arg.Do<string>(errorMsg =>
                                                         StringAssert.Contains(
                                                             "Timed out", errorMsg)));
        }

        [Test]
        public void StartupDoesNotTimeOutWhenRunning()
        {
            _launch.GetLaunchStateAsync(null).Returns(Task.FromResult(_launchRunning));

            _watcher.Start(_launch, TimeSpan.Zero, _onDone);

            // How do you test that something will never happen?
            Thread.Sleep(10);
            Assert.False(_onDoneCalled.IsSet);
        }
    }
}