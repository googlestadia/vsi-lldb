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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using YetiVSI.DebugEngine;
using YetiVSI.PortSupplier;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSITestsCommon;

namespace YetiVSI.Test.PortSupplier
{
    [TestFixture]
    class DebugPortTests
    {
        const string TEST_GAMELET_IP = "1.2.3.4";
        const string TEST_GAMELET_TARGET = TEST_GAMELET_IP + ":44722";
        const string TEST_GAMELET_ID = "gameletid";
        const string TEST_GAMELET_NAME = "gamelet name";
        const string TEST_DEBUG_SESSION_ID = "abc123";

        DebugProcess.Factory processFactory;
        IProcessListRequest processListRequest;
        IDialogUtil dialogUtil;
        ISshManager sshManager;
        IMetrics metrics;

        DebugPort.Factory portFactory;
        IDebugPortSupplier2 portSupplier;

        [SetUp]
        public void SetUp()
        {
            processFactory = Substitute.For<DebugProcess.Factory>();
            dialogUtil = Substitute.For<IDialogUtil>();
            sshManager = Substitute.For<ISshManager>();

            processListRequest = Substitute.For<IProcessListRequest>();
            var processListRequestFactory = Substitute.For<ProcessListRequest.Factory>();
            processListRequestFactory.Create().Returns(processListRequest);

            var cancelableTaskFactory =
                FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false);

            metrics = Substitute.For<IMetrics>();

            portFactory = new DebugPort.Factory(processFactory, processListRequestFactory,
                                                cancelableTaskFactory, dialogUtil, sshManager,
                                                metrics);
            portSupplier = Substitute.For<IDebugPortSupplier2>();
        }

        [Test]
        public void GetPortRequest()
        {
            var port = portFactory.Create(new Gamelet(), portSupplier, TEST_DEBUG_SESSION_ID);

            IDebugPortRequest2 request;
            Assert.AreEqual(AD7Constants.E_PORT_NO_REQUEST, port.GetPortRequest(out request));
            Assert.IsNull(request);
        }

        [Test]
        public void GetPortSupplier()
        {
            var port = portFactory.Create(new Gamelet(), this.portSupplier, TEST_DEBUG_SESSION_ID);

            IDebugPortSupplier2 portSupplier;
            Assert.AreEqual(VSConstants.S_OK, port.GetPortSupplier(out portSupplier));
            Assert.AreEqual(this.portSupplier, portSupplier);
        }

        [Test]
        public void GetPortNameById()
        {
            var port = portFactory.Create(new Gamelet {Id = TEST_GAMELET_ID, DisplayName = ""},
                                          portSupplier, TEST_DEBUG_SESSION_ID);

            string name;
            Assert.AreEqual(VSConstants.S_OK, port.GetPortName(out name));
            Assert.AreEqual(TEST_GAMELET_ID, name);
        }

        [Test]
        public void GetPortNameByName()
        {
            var port = portFactory.Create(
                new Gamelet {Id = TEST_GAMELET_ID, DisplayName = TEST_GAMELET_NAME}, portSupplier,
                TEST_DEBUG_SESSION_ID);

            string name;
            Assert.AreEqual(VSConstants.S_OK, port.GetPortName(out name));
            Assert.AreEqual("gamelet name [gameletid]", name);
        }

        [Test]
        public void GetDebugSessionId()
        {
            var port = portFactory.Create(new Gamelet(), portSupplier, TEST_DEBUG_SESSION_ID);

            Assert.AreEqual(TEST_DEBUG_SESSION_ID, ((DebugPort) port).DebugSessionId);
        }

        [Test]
        public void EnumProcesses()
        {
            var gamelet = new Gamelet {Id = TEST_GAMELET_ID, IpAddr = TEST_GAMELET_IP};
            var port = portFactory.Create(gamelet, portSupplier, TEST_DEBUG_SESSION_ID);

            sshManager.EnableSshAsync(gamelet, Arg.Any<IAction>()).Returns(Task.FromResult(true));

            var entry1 = new ProcessListEntry {Pid = 101, Title = "title1", Command = "command 1"};
            var entry2 = new ProcessListEntry {Pid = 102, Title = "title2", Command = "command 2"};
            processListRequest.GetBySshAsync(new SshTarget(TEST_GAMELET_TARGET)).Returns(
                new List<ProcessListEntry>() {entry1, entry2});

            var process1 = Substitute.For<IDebugProcess2>();
            processFactory.Create(port, entry1.Pid, entry1.Title, entry1.Command).Returns(process1);

            var process2 = Substitute.For<IDebugProcess2>();
            processFactory.Create(port, entry2.Pid, entry2.Title, entry2.Command).Returns(process2);

            IEnumDebugProcesses2 processesEnum;
            Assert.AreEqual(VSConstants.S_OK, port.EnumProcesses(out processesEnum));

            var processes = new IDebugProcess2[2];
            uint numFetched = 0;
            Assert.AreEqual(VSConstants.S_OK,
                            processesEnum.Next((uint) processes.Length, processes, ref numFetched));
            Assert.AreEqual(2, numFetched);
            Assert.AreEqual(processes[0], process1);
            Assert.AreEqual(processes[1], process2);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsEnableSsh,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiProcessList,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public void EnumProcessesError()
        {
            var gamelet = new Gamelet {Id = TEST_GAMELET_ID, IpAddr = TEST_GAMELET_IP};
            var port = portFactory.Create(gamelet, portSupplier, TEST_DEBUG_SESSION_ID);

            sshManager.EnableSshAsync(gamelet, Arg.Any<IAction>()).Returns(Task.FromResult(true));

            processListRequest.When(x => x.GetBySshAsync(new SshTarget(TEST_GAMELET_TARGET)))
                .Do(x => { throw new ProcessException("test exception"); });

            IEnumDebugProcesses2 processesEnum;
            Assert.AreEqual(VSConstants.S_OK, port.EnumProcesses(out processesEnum));

            uint count = 0;
            Assert.AreEqual(VSConstants.S_OK, processesEnum.GetCount(out count));
            Assert.AreEqual(0, count);

            dialogUtil.Received().ShowError(Arg.Is<string>(x => x.Contains("test exception")),
                                            Arg.Any<string>());
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsEnableSsh,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiProcessList,
                                 DeveloperEventStatus.Types.Code.ExternalToolUnavailable);
        }

        [Test]
        public void EnableSshError()
        {
            var gamelet = new Gamelet {Id = TEST_GAMELET_ID, IpAddr = TEST_GAMELET_IP};
            var port = portFactory.Create(gamelet, portSupplier, TEST_DEBUG_SESSION_ID);

            sshManager.When(x => x.EnableSshAsync(gamelet, Arg.Any<IAction>()))
                .Do(x => { throw new SshKeyException("test exception"); });

            IEnumDebugProcesses2 processesEnum;
            Assert.AreEqual(VSConstants.S_OK, port.EnumProcesses(out processesEnum));

            uint count = 0;
            Assert.AreEqual(VSConstants.S_OK, processesEnum.GetCount(out count));
            Assert.AreEqual(0, count);

            dialogUtil.Received().ShowError(Arg.Is<string>(x => x.Contains("test exception")),
                                            Arg.Any<string>());
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsEnableSsh,
                                 DeveloperEventStatus.Types.Code.InternalError);
        }

        private void AssertMetricRecorded(DeveloperEventType.Types.Type type,
                                          DeveloperEventStatus.Types.Code status)
        {
            metrics.Received()
                .RecordEvent(
                    type,
                    Arg.Is<DeveloperLogEvent>(p => p.StatusCode == status &&
                                                  p.DebugSessionIdStr == TEST_DEBUG_SESSION_ID));
        }
    }
}