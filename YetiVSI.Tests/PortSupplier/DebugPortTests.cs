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
using Metrics.Shared;
using Microsoft.VisualStudio.Threading;
using YetiVSI.DebugEngine;
using YetiVSI.PortSupplier;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.Metrics;
using YetiVSITestsCommon;

namespace YetiVSI.Test.PortSupplier
{
    [TestFixture]
    class DebugPortTests
    {
        const string _reserverAccount = "reserver@test.com";
        const string _testGameletIp = "1.2.3.4";
        const string _testGameletTarget = _testGameletIp + ":44722";
        const string _testGameletId = "gameletid";
        const string _testGameletName = "gamelet name";
        const string _testDebugSessionId = "abc123";

        IProcessListRequest _processListRequest;
        IDialogUtil _dialogUtil;
        ISshManager _sshManager;
        IVsiMetrics _metrics;

        DebugPort.Factory _portFactory;
        IDebugPortSupplier2 _portSupplier;

        [SetUp]
        public void SetUp()
        {
            _dialogUtil = Substitute.For<IDialogUtil>();
            _sshManager = Substitute.For<ISshManager>();

            _processListRequest = Substitute.For<IProcessListRequest>();
            var processListRequestFactory = Substitute.For<ProcessListRequest.Factory>();
            processListRequestFactory.Create().Returns(_processListRequest);

            var cancelableTaskFactory =
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
                FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false);
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

            _metrics = Substitute.For<IVsiMetrics>();

            _portFactory = new DebugPort.Factory(
                processListRequestFactory, cancelableTaskFactory, _dialogUtil, _sshManager,
                _metrics, _reserverAccount);
            _portSupplier = Substitute.For<IDebugPortSupplier2>();
        }

        [Test]
        public void GetPortRequest()
        {
            var port = _portFactory.Create(new Gamelet(), _portSupplier, _testDebugSessionId);

            Assert.AreEqual(AD7Constants.E_PORT_NO_REQUEST,
                            port.GetPortRequest(out IDebugPortRequest2 request));
            Assert.IsNull(request);
        }

        [Test]
        public void GetPortSupplier()
        {
            var port = _portFactory.Create(new Gamelet(), this._portSupplier, _testDebugSessionId);

            Assert.AreEqual(VSConstants.S_OK,
                            port.GetPortSupplier(out IDebugPortSupplier2 portSupplier));
            Assert.AreEqual(this._portSupplier, portSupplier);
        }

        [Test]
        public void GetPortNameById()
        {
            var port = _portFactory.Create(new Gamelet { Id = _testGameletId, DisplayName = "",
                                                         ReserverEmail = _reserverAccount },
                                           _portSupplier, _testDebugSessionId);

            Assert.AreEqual(VSConstants.S_OK, port.GetPortName(out string name));
            Assert.AreEqual(_testGameletId, name);
        }

        [Test]
        public void GetPortNameByNameForReserver()
        {
            var port = _portFactory.Create(new Gamelet { Id = _testGameletId,
                                                         DisplayName = _testGameletName,
                                                         ReserverEmail = _reserverAccount },
                                           _portSupplier, _testDebugSessionId);

            Assert.AreEqual(VSConstants.S_OK, port.GetPortName(out string name));
            Assert.AreEqual("gamelet name [gameletid]", name);
        }

        [Test]
        public void GetPortNameForNonReserver()
        {
            var port = _portFactory.Create(
                new Gamelet { Id = _testGameletId, DisplayName = _testGameletName,
                              ReserverEmail = "anotherReserver@test.com" },
                _portSupplier, _testDebugSessionId);

            Assert.AreEqual(VSConstants.S_OK, port.GetPortName(out string name));
            Assert.AreEqual("Reserver: anotherReserver@test.com; Instance: gamelet name", name);
        }

        [Test]
        public void GetDebugSessionId()
        {
            var port = _portFactory.Create(new Gamelet(), _portSupplier, _testDebugSessionId);

            Assert.AreEqual(_testDebugSessionId, ((DebugPort)port).DebugSessionId);
        }

        [Test]
        public void EnumProcesses()
        {
            var gamelet = new Gamelet { Id = _testGameletId, IpAddr = _testGameletIp };
            var port = _portFactory.Create(gamelet, _portSupplier, _testDebugSessionId);

            _sshManager.EnableSshAsync(gamelet, Arg.Any<IAction>()).Returns(Task.FromResult(true));

            var entry1 = new ProcessListEntry {Pid = 101, Title = "title1", Command = "command 1"};
            var entry2 = new ProcessListEntry {Pid = 102, Title = "title2", Command = "command 2"};
            _processListRequest.GetBySshAsync(new SshTarget(_testGameletTarget))
                .Returns(new List<ProcessListEntry>() { entry1, entry2 });

            Assert.AreEqual(VSConstants.S_OK,
                            port.EnumProcesses(out IEnumDebugProcesses2 processesEnum));

            var processes = new IDebugProcess2[2];
            uint numFetched = 0;
            Assert.AreEqual(VSConstants.S_OK,
                            processesEnum.Next((uint) processes.Length, processes, ref numFetched));
            Assert.AreEqual(2, numFetched);

            processes[0].GetName(enum_GETNAME_TYPE.GN_TITLE, out string title1);
            Assert.AreEqual(title1, entry1.Title);
            processes[1].GetName(enum_GETNAME_TYPE.GN_TITLE, out string title2);
            Assert.AreEqual(title2, entry2.Title);

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsEnableSsh,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiProcessList,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public void EnumProcessesError()
        {
            var gamelet = new Gamelet { Id = _testGameletId, IpAddr = _testGameletIp };
            var port = _portFactory.Create(gamelet, _portSupplier, _testDebugSessionId);

            _sshManager.EnableSshAsync(gamelet, Arg.Any<IAction>()).Returns(Task.FromResult(true));

            _processListRequest.When(x => x.GetBySshAsync(new SshTarget(_testGameletTarget)))
                .Do(x => { throw new ProcessException("test exception"); });

            Assert.AreEqual(VSConstants.S_OK,
                            port.EnumProcesses(out IEnumDebugProcesses2 processesEnum));

            Assert.AreEqual(VSConstants.S_OK, processesEnum.GetCount(out uint count));
            Assert.AreEqual(0, count);

            _dialogUtil.Received().ShowError(Arg.Is<string>(x => x.Contains("test exception")),
                                             Arg.Any<ProcessException>());
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsEnableSsh,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiProcessList,
                                 DeveloperEventStatus.Types.Code.ExternalToolUnavailable);
        }

        [Test]
        public void EnableSshError()
        {
            var gamelet = new Gamelet { Id = _testGameletId, IpAddr = _testGameletIp };
            var port = _portFactory.Create(gamelet, _portSupplier, _testDebugSessionId);

            _sshManager.When(x => x.EnableSshAsync(gamelet, Arg.Any<IAction>())).Do(x => {
                throw new SshKeyException("test exception");
            });

            Assert.AreEqual(VSConstants.S_OK,
                            port.EnumProcesses(out IEnumDebugProcesses2 processesEnum));

            Assert.AreEqual(VSConstants.S_OK, processesEnum.GetCount(out uint count));
            Assert.AreEqual(0, count);

            _dialogUtil.Received().ShowError(Arg.Is<string>(x => x.Contains("test exception")),
                                             Arg.Any<SshKeyException>());
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsEnableSsh,
                                 DeveloperEventStatus.Types.Code.InternalError);
        }

        void AssertMetricRecorded(DeveloperEventType.Types.Type type,
                                  DeveloperEventStatus.Types.Code status)
        {
            _metrics.Received().RecordEvent(
                type, Arg.Is<DeveloperLogEvent>(p => p.StatusCode == status &&
                                                     p.DebugSessionIdStr == _testDebugSessionId));
        }
    }
}