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
using YetiVSI.PortSupplier;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using YetiCommon;
using YetiCommon.SSH;

namespace YetiVSI.Test.PortSupplier
{
    [TestFixture]
    class ProcessListRequestTests
    {
        const string TEST_WORKING_DIR = "test working dir";
        const string TEST_IP_ADDRESS = "1.2.3.4";
        const string TEST_TARGET = TEST_IP_ADDRESS + ":44722";

        ManagedProcess.Factory managedProcessFactory;
        ProcessListRequest.Factory processListRequestFactory;

        [SetUp]
        public void SetUp()
        {
            managedProcessFactory = Substitute.For<ManagedProcess.Factory>();
            processListRequestFactory = new ProcessListRequest.Factory(managedProcessFactory);
        }

        [Test]
        public async Task GetBySshAsync()
        {
            var process = Substitute.For<IProcess>();
            managedProcessFactory.Create(
                Arg.Is<ProcessStartInfo>(
                    x => x.FileName.Contains("ssh") && x.Arguments.Contains(TEST_IP_ADDRESS)))
                .Returns(process);

            process.When(x => x.RunToExitAsync()).Do(x => OutputTestData(process));

            VerifyTestData(
                await processListRequestFactory.Create().GetBySshAsync(new SshTarget(TEST_TARGET)));
        }

        [Test]
        public void GetBySshError()
        {
            var process = Substitute.For<IProcess>();
            managedProcessFactory.Create(Arg.Any<ProcessStartInfo>()).Returns(process);

            process.When(x => x.RunToExitAsync()).Do(x =>
            {
                throw new ProcessException("test exception");
            });

            Assert.ThrowsAsync<ProcessException>(async delegate
            { await processListRequestFactory.Create().GetBySshAsync(new SshTarget(TEST_TARGET)); });
        }

        void OutputTestData(IProcess process)
        {
            string data =
                "   PID  PPID COMMAND         CMD\n" +
                " 12345     1 process1        full command1\n" +
                // ps command removed from results
                "   123   234 ps              full ps command\n" +
                // parent of ps command removed from results
                "   234     3 psparent        full ps parent\n" +
                " 23456     2 process2        full command2";
            foreach (var line in data.Split('\n'))
            {
                process.OutputDataReceived +=
                    Raise.Event<TextReceivedEventHandler>(
                        this, new TextReceivedEventArgs(line));
            }
        }

        void VerifyTestData(List<ProcessListEntry> results)
        {
            var expected = new List<ProcessListEntry>
            {
                new ProcessListEntry {
                    Pid = 12345,
                    Ppid = 1,
                    Title = "process1",
                    Command = "full command1",
                },
                new ProcessListEntry {
                    Pid = 23456,
                    Ppid = 2,
                    Title = "process2",
                    Command = "full command2",
                },
            };
            CollectionAssert.AreEqual(expected, results);
        }
    }
}
