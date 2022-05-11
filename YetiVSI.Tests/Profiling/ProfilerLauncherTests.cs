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

using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using NSubstitute;
using NUnit.Framework;
using YetiCommon;
using YetiVSI.Profiling;

namespace YetiVSI.Test.Profiling
{
    class ProfilerLauncherTests
    {
        BackgroundProcess.Factory _processFactory;
        MockFileSystem _mockFileSystem;
        IProfilerLauncher<OrbitArgs> _orbitLauncher;
        IProfilerLauncher<DiveArgs> _diveLauncher;

        [SetUp]
        public void SetUp()
        {
            _processFactory = Substitute.For<BackgroundProcess.Factory>();
            _mockFileSystem = new MockFileSystem();
            _orbitLauncher =
                ProfilerLauncher<OrbitArgs>.CreateForOrbit(_processFactory, _mockFileSystem);
            _diveLauncher =
                ProfilerLauncher<DiveArgs>.CreateForDive(_processFactory, _mockFileSystem);
        }

        [Test]
        public void LaunchOrbit()
        {
            string gameletExecutablePath = "/srv/game/assets/larry_vs_sergey";
            string gameletId = "gamelet_id";

            var orbitProcess = Substitute.For<IBackgroundProcess>();
            var argMatcher =
                Arg.Is<string>(x => x.Contains(gameletExecutablePath) && x.Contains(gameletId));
            _processFactory.Create(_orbitLauncher.BinaryPath, argMatcher, SDKUtil.GetOrbitPath())
                .Returns(orbitProcess);

            _orbitLauncher.Launch(
                new OrbitArgs(gameletExecutablePath, gameletId, new HashSet<string>()));
            orbitProcess.Received().Start();
        }

        [Test]
        public void IsOrbitInstalled()
        {
            string binPath = Path.Combine(SDKUtil.GetOrbitPath(), "Orbit.exe");
            Assert.That(_orbitLauncher.IsInstalled, Is.False);
            _mockFileSystem.AddFile(binPath, null);
            Assert.That(_orbitLauncher.IsInstalled, Is.True);
        }

        [Test]
        public void OrbitKillsPreviousProcessEvenForDifferentInstance()
        {
            var orbitProcess = Substitute.For<IBackgroundProcess>();
            _processFactory.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(orbitProcess);

            _orbitLauncher.Launch(new OrbitArgs("", "", new HashSet<string>()));
            orbitProcess.Received().Start();
            orbitProcess.DidNotReceive().Kill();

            var orbitProcess2 = Substitute.For<IBackgroundProcess>();
            _processFactory.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(orbitProcess2);
            var orbitLauncher2 =
                ProfilerLauncher<OrbitArgs>.CreateForOrbit(_processFactory, _mockFileSystem);

            orbitLauncher2.Launch(new OrbitArgs("", "", new HashSet<string>()));
            orbitProcess.Received().Kill();
            orbitProcess2.Received().Start();
            orbitProcess2.DidNotReceive().Kill();
        }

        [Test]
        public void LaunchDive()
        {
            var diveProcess = Substitute.For<IBackgroundProcess>();
            _processFactory.Create(_diveLauncher.BinaryPath, "", SDKUtil.GetDivePath())
                .Returns(diveProcess);

            _diveLauncher.Launch(new DiveArgs());
            diveProcess.Received().Start();
        }

        [Test]
        public void IsDiveInstalled()
        {
            string binPath = Path.Combine(SDKUtil.GetDivePath(), "dive.exe");
            Assert.That(_diveLauncher.IsInstalled, Is.False);
            _mockFileSystem.AddFile(binPath, null);
            Assert.That(_diveLauncher.IsInstalled, Is.True);
        }

        [Test]
        public void DiveDoesNotKillPreviousProcess()
        {
            var diveProcess = Substitute.For<IBackgroundProcess>();
            _processFactory.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(diveProcess);

            _diveLauncher.Launch(new DiveArgs());
            diveProcess.Received().Start();
            diveProcess.DidNotReceive().Kill();

            var diveProcess2 = Substitute.For<IBackgroundProcess>();
            _processFactory.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(diveProcess2);

            _diveLauncher.Launch(new DiveArgs());
            diveProcess.DidNotReceive().Kill();
            diveProcess2.Received().Start();
            diveProcess2.DidNotReceive().Kill();
        }
    }
}