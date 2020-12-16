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

using GgpGrpc;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YetiCommon;
using YetiVSI.PortSupplier;
using YetiVSI.Shared.Metrics;
using YetiVSITestsCommon;

namespace YetiVSI.Test.PortSupplier
{
    [TestFixture]
    class DebugPortSupplierTests
    {
        const string TEST_DEBUG_SESSION_ID = "abc123";

        IMetrics metrics;
        DebugPort.Factory debugPortFactory;
        IGameletClient gameletClient;
        IDialogUtil dialogUtil;
        IExtensionOptions options;

        DebugPortSupplier portSupplier;

        [SetUp]
        public void SetUp()
        {
            debugPortFactory = Substitute.For<DebugPort.Factory>();
            gameletClient = Substitute.For<IGameletClient>();
            var gameletClientFactory = Substitute.For<GameletClient.Factory>();
            gameletClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(gameletClient);
            dialogUtil = Substitute.For<IDialogUtil>();
            options = Substitute.For<IExtensionOptions>();

            var cancelableTaskRunnerFactory =
                FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false);
            metrics = Substitute.For<IMetrics>();
            metrics.NewDebugSessionId().Returns(TEST_DEBUG_SESSION_ID);

            var cloudRunner = Substitute.For<ICloudRunner>();

            portSupplier = new DebugPortSupplier(
                debugPortFactory, gameletClientFactory, options, dialogUtil,
                cancelableTaskRunnerFactory, metrics, cloudRunner);
        }

        [Test]
        public void AddPort()
        {
            var request = Substitute.For<IDebugPortRequest2>();
            string portName = "test port";
            string outName;
            request.GetPortName(out outName).Returns(x =>
            {
                x[0] = portName;
                return VSConstants.S_OK;
            });

            var gamelet = new Gamelet { Id = "test id" };
            gameletClient.LoadByNameOrIdAsync(portName).Returns(gamelet);

            var port = Substitute.For<IDebugPort2>();
            debugPortFactory.Create(gamelet, portSupplier, TEST_DEBUG_SESSION_ID).Returns(port);

            IDebugPort2 debugPort;
            Assert.AreEqual(VSConstants.S_OK, portSupplier.AddPort(request, out debugPort));
            Assert.AreEqual(port, debugPort);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsGet,
                DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public void AddPortMultipleTimes()
        {
            var debugSessionIds = new[] { "session1", "session2" };

            metrics.NewDebugSessionId().Returns(debugSessionIds[0], debugSessionIds[1]);

            var request = Substitute.For<IDebugPortRequest2>();
            string portName = "test port";
            string outName;
            request.GetPortName(out outName).Returns(x =>
            {
                x[0] = portName;
                return VSConstants.S_OK;
            });

            var gamelet = new Gamelet { Id = "test id" };
            gameletClient.LoadByNameOrIdAsync(portName).Returns(gamelet);

            foreach (var sessionId in debugSessionIds)
            {
                var port = Substitute.For<IDebugPort2>();
                debugPortFactory.Create(gamelet, portSupplier, sessionId).Returns(port);

                IDebugPort2 debugPort;
                Assert.AreEqual(VSConstants.S_OK, portSupplier.AddPort(request, out debugPort));
                Assert.AreSame(port, debugPort);
                AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsGet,
                    DeveloperEventStatus.Types.Code.Success, sessionId);
            }
        }

        [Test]
        public void CanAddPort()
        {
            Assert.AreEqual(VSConstants.S_OK, portSupplier.CanAddPort());
        }

        [Test]
        public void EnumPorts()
        {
            var gamelets = new List<Gamelet>
            {
                new Gamelet { Id = "test id1" },
                new Gamelet { Id = "test id2" },
            };
            gameletClient.ListGameletsAsync().Returns(x => Task.FromResult(gamelets));

            var port1 = Substitute.For<IDebugPort2>();
            debugPortFactory.Create(gamelets[0], portSupplier, TEST_DEBUG_SESSION_ID)
                .Returns(port1);

            var port2 = Substitute.For<IDebugPort2>();
            debugPortFactory.Create(gamelets[1], portSupplier, TEST_DEBUG_SESSION_ID)
                .Returns(port2);

            IEnumDebugPorts2 portsEnum;
            Assert.AreEqual(VSConstants.S_OK, portSupplier.EnumPorts(out portsEnum));

            var ports = new IDebugPort2[2];
            uint numFetched = 0;
            Assert.AreEqual(
                VSConstants.S_OK, portsEnum.Next((uint)ports.Length, ports, ref numFetched));
            Assert.AreEqual(2, numFetched);
            Assert.AreEqual(ports[0], port1);
            Assert.AreEqual(ports[1], port2);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsList,
                DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public void EnumPortsMultipleTimes()
        {
            var debugSessionIds = new[] { "session1", "session2" };

            metrics.NewDebugSessionId().Returns(debugSessionIds[0], debugSessionIds[1]);

            var gamelet = new Gamelet { Id = "test id1" };
            gameletClient.ListGameletsAsync().Returns(x =>
                Task.FromResult(new List<Gamelet> { gamelet }));

            foreach (var sessionId in debugSessionIds)
            {
                var port = Substitute.For<IDebugPort2>();
                debugPortFactory.Create(gamelet, portSupplier, sessionId)
                    .Returns(port);

                IEnumDebugPorts2 portsEnum;
                Assert.AreEqual(VSConstants.S_OK, portSupplier.EnumPorts(out portsEnum));

                var ports = new IDebugPort2[1];
                uint numFetched = 0;
                Assert.AreEqual(
                    VSConstants.S_OK, portsEnum.Next((uint)ports.Length, ports, ref numFetched));
                Assert.AreEqual(1, numFetched);
                Assert.AreSame(ports[0], port);
                AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsList,
                    DeveloperEventStatus.Types.Code.Success, sessionId);
            }
        }

        [Test]
        public void EnumPortsError()
        {
            gameletClient.ListGameletsAsync()
                .Returns<List<Gamelet>>(x => { throw new CloudException("test exception"); });

            IEnumDebugPorts2 portsEnum;
            Assert.AreEqual(VSConstants.S_OK, portSupplier.EnumPorts(out portsEnum));

            dialogUtil.Received().ShowError("test exception");

            uint count = 0;
            Assert.AreEqual(VSConstants.S_OK, portsEnum.GetCount(out count));
            Assert.AreEqual(0, count);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsList,
                DeveloperEventStatus.Types.Code.InternalError);
        }

        [Test]
        public void GetPortNotFound()
        {
            var guid = new Guid();
            IDebugPort2 port;
            Assert.AreEqual(YetiVSI.DebugEngine.AD7Constants.E_PORTSUPPLIER_NO_PORT,
                portSupplier.GetPort(ref guid, out port));
        }

        [Test]
        public void GetPortSupplierId()
        {
            Guid guid;
            Assert.AreEqual(VSConstants.S_OK, portSupplier.GetPortSupplierId(out guid));
            Assert.AreEqual(YetiConstants.PortSupplierGuid, guid);
        }

        [Test]
        public void GetPortSupplierName()
        {
            string name;
            Assert.AreEqual(VSConstants.S_OK, portSupplier.GetPortSupplierName(out name));
            Assert.AreEqual(YetiConstants.YetiTitle, name);
        }

        [Test]
        public void RemovePort()
        {
            var port = Substitute.For<IDebugPort2>();
            Assert.AreEqual(VSConstants.E_NOTIMPL, portSupplier.RemovePort(port));
        }

        [Test]
        public void GetDescription()
        {
            string text;
            var flags = new enum_PORT_SUPPLIER_DESCRIPTION_FLAGS[1];
            Assert.AreEqual(VSConstants.S_OK, portSupplier.GetDescription(flags, out text));
            Assert.AreEqual(
                "The Stadia transport lets you select an instance from the Qualifier drop-down " + 
                "menu and remotely attach to an existing process on that instance", text);
        }

        private void AssertMetricRecorded(
            DeveloperEventType.Types.Type type, DeveloperEventStatus.Types.Code status,
            string debugSessionId = TEST_DEBUG_SESSION_ID)
        {
            metrics.Received().RecordEvent(type, Arg.Is<DeveloperLogEvent>(
                p =>
                    p.StatusCode == status &&
                    p.DebugSessionIdStr == debugSessionId
                ));
        }
    }
}
