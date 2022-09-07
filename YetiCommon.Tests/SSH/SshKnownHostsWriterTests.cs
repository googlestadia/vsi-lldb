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

using System.Diagnostics;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Text;
using GgpGrpc.Models;
using YetiCommon.SSH;

namespace YetiCommon.Tests.SSH
{
    [TestFixture]
    class SshKnownHostsWriterTests
    {
        static readonly Gamelet _gamelet1 = new Gamelet
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

        string[] _known_hosts1 =
        {
            "[1.2.3.4]:44722 ssh-dss abcd",
            "[1.2.3.4]:44722 ecdsa-sha2-nistp256 efg",
            "[1.2.3.4]:44722 ssh-ed25519 hijk",
            "[1.2.3.4]:44722 ssh-rsa lmnop",
            "[1.2.3.4]:44722 unknown qrstuv",
        };

        static readonly Gamelet _gamelet2 = new Gamelet
        {
            IpAddr = "2.3.4.5",
            PublicKeys =
            {
                new SshHostPublicKey { Algorithm = SshKeyGenAlgorithm.Ed25519, PublicKey = "ab" },
                new SshHostPublicKey { Algorithm = SshKeyGenAlgorithm.Rsa, PublicKey = "cs" },
            }
        };

        string[] _known_hosts2 =
        {
            "[2.3.4.5]:44722 ssh-ed25519 ab",
            "[2.3.4.5]:44722 ssh-rsa cs",
        };

        string _tempPath;
        string _knownHostsPath;
        SshKnownHostsWriter _sshKnownHostsWriter;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), "yetivsitest" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempPath);
            _knownHostsPath = Path.Combine(_tempPath, "testknownhosts");
            _sshKnownHostsWriter = new SshKnownHostsWriter(_knownHostsPath);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_tempPath, true);
        }

        [Test]
        public void CreateOrUpdateOneGamelet()
        {
            _sshKnownHostsWriter.CreateOrUpdate(_gamelet1);
            string[] contents = File.ReadAllLines(_knownHostsPath);

            string[] expected = _known_hosts1;
            Assert.AreEqual(expected, contents);
        }

        [Test]
        public void CreateOrUpdateTwoGamelets()
        {
            _sshKnownHostsWriter.CreateOrUpdate(_gamelet1);
            _sshKnownHostsWriter.CreateOrUpdate(_gamelet2);
            string[] contents = File.ReadAllLines(_knownHostsPath);

            string[] expected = _known_hosts1.Concat(_known_hosts2).ToArray();
            Assert.AreEqual(expected, contents);
        }

        [Test]
        public void CreateOrUpdateNoChange()
        {
            _sshKnownHostsWriter.CreateOrUpdate(_gamelet1);
            _sshKnownHostsWriter.CreateOrUpdate(_gamelet2);
            _sshKnownHostsWriter.CreateOrUpdate(_gamelet1);
            string[] contents = File.ReadAllLines(_knownHostsPath);

            // Adding _gamelet1 should not append the data at the end, i.e. not
            // _known_hosts2.Concat(_known_hosts1).ToArray()
            string[] expected = _known_hosts1.Concat(_known_hosts2).ToArray();
            Assert.AreEqual(expected, contents);
        }

        [Test]
        public void CreateOrUpdateFailsIfFileIsLocked()
        {
            var logs = new StringBuilder();
            Trace.Listeners.Add(new TextWriterTraceListener(new StringWriter(logs)));

            using (File.Open(_knownHostsPath, FileMode.Create, FileAccess.ReadWrite,
                             FileShare.None))
            {
                Assert.Throws<SshKnownHostsException>(
                    () => _sshKnownHostsWriter.CreateOrUpdate(_gamelet1));
            }

            // Check whether we retried.
            StringAssert.Contains("retrying", logs.ToString());
        }
    }
}