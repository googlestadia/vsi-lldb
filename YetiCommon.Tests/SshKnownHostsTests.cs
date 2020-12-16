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
using System;
using System.Linq;
using GgpGrpc.Models;
using YetiCommon.SSH;

namespace YetiCommon.Tests
{
    [TestFixture]
    class SshKnownHostsTests
    {
        static readonly Gamelet TestGamelet = new Gamelet
        {
            IpAddr = "1.2.3.4",
            PublicKeys =
            {
                new SshHostPublicKey { Algorithm = SshKeyGenAlgorithm.Dsa, PublicKey = "abcd" },
                new SshHostPublicKey { Algorithm = SshKeyGenAlgorithm.Ecdsa, PublicKey = "efg" },
                new SshHostPublicKey { Algorithm = SshKeyGenAlgorithm.Ed25519, PublicKey = "hijk" },
                new SshHostPublicKey { Algorithm = SshKeyGenAlgorithm.Rsa, PublicKey = "lmnop" },
                new SshHostPublicKey
                    { Algorithm = SshKeyGenAlgorithm.Unspecified, PublicKey = "qrstuv" }
            }
        };

        string tempPath;
        string knownHostsPath;

        SshKnownHostsWriter sshKnownHostsWriter;

        [SetUp]
        public void SetUp()
        {
            tempPath = Path.Combine(Path.GetTempPath(), "yetivsitest" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath);

            knownHostsPath = Path.Combine(tempPath, "testknownhosts");

            sshKnownHostsWriter = new SshKnownHostsWriter(knownHostsPath);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(tempPath, true);
        }

        [Test]
        public void CreateOrUpdate()
        {
            sshKnownHostsWriter.CreateOrUpdate(TestGamelet);
            var contents = File.ReadAllLines(knownHostsPath);

            var expected = new string[] {
                "[1.2.3.4]:44722 ssh-dss abcd",
                "[1.2.3.4]:44722 ecdsa-sha2-nistp256 efg",
                "[1.2.3.4]:44722 ssh-ed25519 hijk",
                "[1.2.3.4]:44722 ssh-rsa lmnop",
                "[1.2.3.4]:44722 unknown qrstuv",
            };
            Assert.AreEqual(expected.Length, contents.Length);
            for (int i=0; i<expected.Length; i++)
            {
                Assert.AreEqual(expected[i], contents[i]);
            }
        }

        [Test]
        public void CreateOrUpdateFails()
        {
            using (var file = File.Open(
                knownHostsPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Throws<SshKnownHostsException>(
                    () => sshKnownHostsWriter.CreateOrUpdate(TestGamelet));
            }
        }
    }
}