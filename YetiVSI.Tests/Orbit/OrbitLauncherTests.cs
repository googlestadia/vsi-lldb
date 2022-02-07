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

using System.IO.Abstractions.TestingHelpers;
using NSubstitute;
using NUnit.Framework;
using YetiCommon;
using YetiVSI.Orbit;

namespace YetiVSI.Test.Orbit
{
    class OrbitLauncherTests
    {
        BackgroundProcess.Factory _processFactory;
        MockFileSystem _mockFileSystem;
        OrbitLauncher _orbitLauncher;

        [SetUp]
        public void SetUp()
        {
            _processFactory = Substitute.For<BackgroundProcess.Factory>();
            _mockFileSystem = new MockFileSystem();
            _orbitLauncher = new OrbitLauncher(_processFactory, _mockFileSystem);
        }

        [Test]
        public void Launch()
        {
            string gameletExecutablePath = "/srv/game/assets/larry_vs_sergey";
            string gameletId = "gamelet_id";

            var orbitProcess = Substitute.For<IBackgroundProcess>();
            var argMatcher =
                Arg.Is<string>(x => x.Contains(gameletExecutablePath) && x.Contains(gameletId));
            _processFactory
                .Create(_orbitLauncher.OrbitBinaryPath, argMatcher, SDKUtil.GetOrbitPath())
                .Returns(orbitProcess);

            _orbitLauncher.Launch(gameletExecutablePath, gameletId);
            orbitProcess.Received().Start();
        }

        [Test]
        public void IsOrbitInstalled()
        {
            Assert.That(_orbitLauncher.IsOrbitInstalled(), Is.False);
            _mockFileSystem.AddFile(_orbitLauncher.OrbitBinaryPath, null);
            Assert.That(_orbitLauncher.IsOrbitInstalled(), Is.True);
        }
    }
}