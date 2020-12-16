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
using System;
using System.Collections.Generic;
using System.IO;
using YetiCommon;
using YetiCommon.SSH;

namespace YetiCommon.Tests
{
    [TestFixture]
    class ProcessStartInfoBuilderTests
    {
        [Test]
        public void BuildForSsh()
        {
            var startInfo = ProcessStartInfoBuilder.BuildForSsh(
                "test_command arg1 arg2", new List<string>(), new SshTarget("1.2.3.4:56"));
            Assert.AreEqual(GetSshPath(), startInfo.FileName);
            Assert.True(startInfo.Arguments.Contains(
                "cloudcast@1.2.3.4 -p 56 -- \"test_command arg1 arg2\""));
            Assert.True(startInfo.Arguments.Contains(
                $"-oUserKnownHostsFile=\"\"\"{GetKnownHostsPath()}\"\"\""));
        }

        [Test]
        public void BuildForSshEnv()
        {
            var startInfo = ProcessStartInfoBuilder.BuildForSsh(
                "test_command arg1 arg2", new List<string>() { "VAR1=X", "VAR2=Y" },
                new SshTarget("1.2.3.4:56"));
            Assert.AreEqual(GetSshPath(), startInfo.FileName);
            Assert.True(startInfo.Arguments.Contains(
                "cloudcast@1.2.3.4 -p 56 -- \"VAR1=X VAR2=Y test_command arg1 arg2\""));
        }

        [Test]
        public void BuildForSshPortForward()
        {
            var ports = new List<ProcessStartInfoBuilder.PortForwardEntry>()
            {
                new ProcessStartInfoBuilder.PortForwardEntry()
                {
                    LocalPort = 123,
                    RemotePort = 234,
                },
                new ProcessStartInfoBuilder.PortForwardEntry()
                {
                    LocalPort = 567,
                    RemotePort = 678,
                },
            };
            var startInfo =
                ProcessStartInfoBuilder.BuildForSshPortForward(ports, new SshTarget("1.2.3.4:56"));
            Assert.AreEqual(GetSshPath(), startInfo.FileName);
            Assert.True(startInfo.Arguments.Contains("-L123:localhost:234 -L567:localhost:678"));
            Assert.True(startInfo.Arguments.Contains("cloudcast@1.2.3.4 -p 56"));
            Assert.True(startInfo.Arguments.Contains(
                $"-oUserKnownHostsFile=\"\"\"{GetKnownHostsPath()}\"\"\""));
        }

        [Test]
        public void BuildForSshPortForwardAndCommand()
        {
            var ports = new List<ProcessStartInfoBuilder.PortForwardEntry>()
            {
                new ProcessStartInfoBuilder.PortForwardEntry()
                {
                    LocalPort = 123,
                    RemotePort = 234,
                },
            };
            var startInfo =
                ProcessStartInfoBuilder.BuildForSshPortForwardAndCommand(
                    ports, new SshTarget("1.2.3.4:56"), "cmd");
            Assert.AreEqual(GetSshPath(), startInfo.FileName);
            StringAssert.Contains("-tt", startInfo.Arguments);
            StringAssert.Contains("-L123:localhost:234", startInfo.Arguments);
            StringAssert.Contains("cloudcast@1.2.3.4 -p 56", startInfo.Arguments);
            StringAssert.Contains(
                $"-oUserKnownHostsFile=\"\"\"{GetKnownHostsPath()}\"\"\"", startInfo.Arguments);
            StringAssert.Contains("-- \"cmd\"", startInfo.Arguments);
        }

        [Test]
        public void BuildForScpPut()
        {
            var startInfo = ProcessStartInfoBuilder.BuildForScpPut(
                "path/to/file", new SshTarget("1.2.3.4:56"), "/mnt/developer/");
            Assert.AreEqual(GetScpPath(), startInfo.FileName);
            Assert.True(startInfo.Arguments.Contains("-T"));
            Assert.True(startInfo.Arguments.Contains("-P 56"));
            Assert.True(startInfo.Arguments.Contains(
                "\"path/to/file\" cloudcast@1.2.3.4:\"'/mnt/developer/'\""));
            Assert.True(startInfo.Arguments.Contains(
                $"-oUserKnownHostsFile=\"\"\"{GetKnownHostsPath()}\"\"\""));
        }

        [Test]
        public void BuildForScpPut_PathEndsInBackslash()
        {
            var startInfo = ProcessStartInfoBuilder.BuildForScpPut(
                "path\\to\\file\\", new SshTarget("1.2.3.4:56"), "/mnt/developer/");
            Assert.AreEqual(GetScpPath(), startInfo.FileName);
            Assert.True(startInfo.Arguments.Contains("-T"));
            Assert.True(startInfo.Arguments.Contains("-P 56"));
            Assert.True(startInfo.Arguments.Contains(
                "\"path\\to\\file\\\\\" cloudcast@1.2.3.4:\"'/mnt/developer/'\""));
            Assert.True(startInfo.Arguments.Contains(
                $"-oUserKnownHostsFile=\"\"\"{GetKnownHostsPath()}\"\"\""));
        }

        [Test]
        public void BuildForScpGet()
        {
            var startInfo = ProcessStartInfoBuilder.BuildForScpGet(
                "path/to/file", new SshTarget("1.2.3.4:56"), "outDir");
            Assert.AreEqual(GetScpPath(), startInfo.FileName);
            Assert.True(startInfo.Arguments.Contains("-T"));
            Assert.True(startInfo.Arguments.Contains("-P 56"));
            StringAssert.Contains("cloudcast@1.2.3.4:\"'path/to/file'\" \"outDir\"",
                startInfo.Arguments);
            Assert.True(startInfo.Arguments.Contains(
                $"-oUserKnownHostsFile=\"\"\"{GetKnownHostsPath()}\"\"\""));
        }

        [Test]
        public void BuildForScpGet_PathEndsInBackslash()
        {
            var startInfo = ProcessStartInfoBuilder.BuildForScpGet(
                "path/to/file", new SshTarget("1.2.3.4:56"), "path\\to\\outDir\\");
            Assert.AreEqual(GetScpPath(), startInfo.FileName);
            Assert.True(startInfo.Arguments.Contains("-T"));
            Assert.True(startInfo.Arguments.Contains("-P 56"));
            StringAssert.Contains("cloudcast@1.2.3.4:\"'path/to/file'\" \"path\\to\\outDir\\\\\"",
                startInfo.Arguments);
            Assert.True(startInfo.Arguments.Contains(
                $"-oUserKnownHostsFile=\"\"\"{GetKnownHostsPath()}\"\"\""));
        }

        [Test]
        public void BuildForScpGet_SpaceInFileName()
        {
            var startInfo = ProcessStartInfoBuilder.BuildForScpGet(
                "path/to/file with space", new SshTarget("1.2.3.4:56"), "outDir");
            Assert.AreEqual(GetScpPath(), startInfo.FileName);
            Assert.True(startInfo.Arguments.Contains("-T"));
            Assert.True(startInfo.Arguments.Contains("-P 56"));
            StringAssert.Contains("cloudcast@1.2.3.4:\"'path/to/file with space'\" \"outDir\"",
                startInfo.Arguments);
            Assert.True(startInfo.Arguments.Contains(
                $"-oUserKnownHostsFile=\"\"\"{GetKnownHostsPath()}\"\"\""));
        }

        [Test]
        public void BuildForCompress()
        {
            var startInfo = ProcessStartInfoBuilder.BuildForCompress(
                "C:\\a\\b c.elf");
            StringAssert.Contains(YetiConstants.PigzExecutable, startInfo.FileName);
            StringAssert.Contains("-k --fast --stdout", startInfo.Arguments);
            StringAssert.Contains("\"C:\\a\\b c.elf\"", startInfo.Arguments);
        }

        private string GetSshPath()
        {
            return Path.Combine(SDKUtil.GetSshPath(), YetiConstants.SshWinExecutable);
        }

        private string GetScpPath()
        {
            return Path.Combine(SDKUtil.GetSshPath(), YetiConstants.ScpWinExecutable);
        }

        private string GetKnownHostsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GGP", "ssh", "known_hosts");
        }
    }
}
