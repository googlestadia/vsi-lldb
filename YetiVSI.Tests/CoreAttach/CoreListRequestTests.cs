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

using GgpGrpc.Models;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using YetiVSI.CoreAttach;
using YetiCommon;
using YetiCommon.SSH;

namespace YetiVSI.Test.CoreAttach
{
    class CoreListRequestTests
    {
        ManagedProcess.Factory managedProcessFactory;
        CoreListRequest.Factory coreListRequestFactory;
        const string TEST_GAMELET_ID = "test_gamelet";
        const string TEST_IP = "1.2.3.4";
        SshTarget sshTarget;

        [SetUp]
        public void SetUp()
        {
            var testGamelet = new Gamelet { Id = TEST_GAMELET_ID, IpAddr = TEST_IP };
            sshTarget = new SshTarget(testGamelet);
            managedProcessFactory = Substitute.For<ManagedProcess.Factory>();
            coreListRequestFactory = new CoreListRequest.Factory(managedProcessFactory);
        }

        [Test]
        public void GetCoreListInvalidOperationException()
        {
            var mockProcess = Substitute.For<IProcess>();
            managedProcessFactory.Create(Arg.Is<ProcessStartInfo>(
                x => x.FileName.Contains(
                    YetiConstants.SshWinExecutable))).Returns(mockProcess);
            mockProcess.When(x => x.RunToExitAsync()).Do(x =>
            {
                throw new ProcessException("test exception");
            });
            Assert.ThrowsAsync<ProcessException>(async delegate
            {
                await coreListRequestFactory.Create().GetCoreListAsync(sshTarget);
            });
        }

        [Test]
        public async Task GetCoreListAsync()
        {
            var mockProcess = Substitute.For<IProcess>();
            managedProcessFactory.Create(Arg.Is<ProcessStartInfo>(
                x => x.FileName.Contains(
                    YetiConstants.SshWinExecutable))).Returns(mockProcess);
            mockProcess.When(x => x.RunToExitAsync()).Do(x => OutputTestData(mockProcess));
            VeryfyTestData(
                await coreListRequestFactory.Create().GetCoreListAsync(sshTarget));
        }

        void OutputTestData(IProcess process)
        {
            string data =
                "total 2\n" +
                "-rw-r--r-- 1 cloudcast 1234567  1234 1526494820 dummy.core\n" +
                "-rw-r--r-- 1 cloudcast 1234567  1234 1526494821 some_file\n" +
                "-rw-r--r-- 1 cloudcast 1234567 1234 1526494822 file with spaces.core.dmp\n" +
                "-rw-r--r-- 1 random_user 1234567 1234 1526494823 random.core";
            foreach (var line in data.Split('\n'))
            {
                process.OutputDataReceived +=
                    Raise.Event<TextReceivedEventHandler>(
                        this, new TextReceivedEventArgs(line));
            }
        }

        void VeryfyTestData(List<CoreListEntry> coreListEntries)
        {
            var expectEntries = new List<CoreListEntry>
            {
                new CoreListEntry
                {
                    Name = "dummy.core",
                    Date = DateTimeOffset.FromUnixTimeSeconds(1526494820).DateTime.ToLocalTime()
                },
                new CoreListEntry
                {
                    Name = "some_file",
                    Date = DateTimeOffset.FromUnixTimeSeconds(1526494821).DateTime.ToLocalTime()
                },
                new CoreListEntry
                {
                    Name = "file with spaces.core.dmp",
                    Date = DateTimeOffset.FromUnixTimeSeconds(1526494822).DateTime.ToLocalTime()
                }
            };
            CollectionAssert.AreEqual(expectEntries, coreListEntries);
        }
    }
}
