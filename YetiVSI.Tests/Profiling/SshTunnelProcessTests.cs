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
using NSubstitute;
using NUnit.Framework;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.Profiling;

namespace YetiVSI.Test.Profiling
{
    class SshTunnelProcessTests
    {
        const int _localPort = 12345;
        const int _remotePort = 23456;
        const string _targetIp = "1.2.3.4";
        const string _targetPort = "567";

        SshTarget _target;
        ManagedProcess.Factory _processFactory;
        IProcess _process;
        SshTunnelProcess _tunnel;

        [SetUp]
        public void SetUp()
        {
            _target = new SshTarget($"{_targetIp}:{_targetPort}");
            _processFactory = Substitute.For<ManagedProcess.Factory>();
            _process = Substitute.For<IProcess>();
            _processFactory.Create(Arg.Any<ProcessStartInfo>(), Arg.Any<int>()).Returns(_process);
            _tunnel = new SshTunnelProcess(_localPort, _remotePort, "", _target, _processFactory);
        }

        [Test]
        public void Start()
        {
            ProcessStartInfo si = null;
            _processFactory.Create(Arg.Do<ProcessStartInfo>(x => si = x), Arg.Any<int>())
                .Returns(_process);

            var _ = new SshTunnelProcess(_localPort, _remotePort, "", _target, _processFactory);

            _process.Received().Start();
            Assert.True(si != null);
            Assert.Multiple(() =>
            {
                Assert.True(si.FileName.Contains(YetiConstants.SshWinExecutable));
                Assert.True(si.Arguments.Contains(_localPort.ToString()));
                Assert.True(si.Arguments.Contains(_remotePort.ToString()));
                Assert.True(si.Arguments.Contains(_targetIp));
                Assert.True(si.Arguments.Contains(_targetPort));
            });
        }

        [Test]
        public void Stop()
        {
            _tunnel.Stop();

            _process.Received(1).Kill();
            _process.Received(1).Dispose();
        }

        [Test]
        public void DoubleStop()
        {
            _tunnel.Stop();
            _tunnel.Stop();

            _process.Received(1).Kill();
            _process.Received(1).Dispose();
        }

        [Test]
        public void HandleExit()
        {
            // Trigger _tunnel.HandleExit().
            _process.OnExit += Raise.EventWith(new EventArgs());

            _process.Received(1).Dispose();
        }

        [Test]
        public void HandleExitAfterStop()
        {
            _tunnel.Stop();

            _process.OnExit += Raise.EventWith<EventArgs>(null, null);
        }
    }
}