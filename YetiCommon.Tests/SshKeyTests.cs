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

using NUnit.Framework;
using NSubstitute;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;

namespace YetiCommon.Tests
{
    [TestFixture]
    class SshKeyTests
    {
        string tempPath;
        string keyPath;

        ManagedProcess.Factory managedProcessFactory;
        SshKeyLoader sshKeyLoader;

        [SetUp]
        public void SetUp()
        {
            tempPath = Path.Combine(Path.GetTempPath(), "yetivsitest" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath);

            keyPath = Path.Combine(tempPath, "tempKeyFile");

            managedProcessFactory = Substitute.For<ManagedProcess.Factory>();
            sshKeyLoader = new SshKeyLoader(managedProcessFactory, keyPath);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(tempPath, true);
        }

        [Test]
        public async Task LoadOrCreateDoesCreateAsync()
        {
            const string keyContents = "key contents";

            var process = Substitute.For<IProcess>();
            managedProcessFactory.Create(Arg.Any<ProcessStartInfo>()).Returns(process);

            process.RunToExitAsync().Returns(x =>
            {
                File.WriteAllText(keyPath + ".pub", keyContents);
                return 0;
            });

            var key = await sshKeyLoader.LoadOrCreateAsync();
            Assert.AreEqual(keyContents, key.PublicKey);
        }

        [Test]
        public async Task LoadOrCreateDoesLoadAsync()
        {
            const string keyContents = "public key contents";
            File.WriteAllText(keyPath, "private key contents");
            File.WriteAllText(keyPath + ".pub", keyContents);

            var key = await sshKeyLoader.LoadOrCreateAsync();
            Assert.AreEqual(keyContents, key.PublicKey);

            managedProcessFactory.Create(Arg.Any<ProcessStartInfo>()).DidNotReceive();
        }

        [Test]
        public void LoadOrCreateFails()
        {
            var process = Substitute.For<IProcess>();
            managedProcessFactory.Create(Arg.Any<ProcessStartInfo>()).Returns(process);

            process.RunToExitAsync().Returns(x =>
            {
                return 1;
            });

            Assert.ThrowsAsync<SshKeyException>(() => sshKeyLoader.LoadOrCreateAsync());
        }
    }
}
