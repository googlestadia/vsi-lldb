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
using Metrics.Shared;
using YetiCommon;
using YetiVSI.PortSupplier;
using YetiVSITestsCommon;

namespace YetiVSI.Test.PortSupplier
{
    [TestFixture]
    class DebugPortSupplierTests
    {
        const string _testDebugSessionId = "abc123";

        IVsiMetrics _metrics;
        DebugPort.Factory _debugPortFactory;
        IGameletClient _gameletClient;
        IDialogUtil _dialogUtil;
        IExtensionOptions _options;

        DebugPortSupplier _portSupplier;
        const string _reserver = "reserver@test.com";

        [SetUp]
        public void SetUp()
        {
            _debugPortFactory = Substitute.For<DebugPort.Factory>();
            _gameletClient = Substitute.For<IGameletClient>();
            var gameletClientFactory = Substitute.For<GameletClient.Factory>();
            gameletClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(_gameletClient);
            _dialogUtil = Substitute.For<IDialogUtil>();
            _options = Substitute.For<IExtensionOptions>();

            var cancelableTaskRunnerFactory =
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
                FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false);
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
            _metrics = Substitute.For<IVsiMetrics>();
            _metrics.NewDebugSessionId().Returns(_testDebugSessionId);

            var cloudRunner = Substitute.For<ICloudRunner>();

            _portSupplier = new DebugPortSupplier(_debugPortFactory, gameletClientFactory, _options,
                                                  _dialogUtil, cancelableTaskRunnerFactory,
                                                  _metrics, cloudRunner, _reserver);
        }

        [Test]
        public void AddPort()
        {
            var request = Substitute.For<IDebugPortRequest2>();
            string portName = "test port";
            request.GetPortName(out string _).Returns(x =>
            {
                x[0] = portName;
                return VSConstants.S_OK;
            });

            var gamelet = new Gamelet { Id = "test id" };
            _gameletClient.LoadByNameOrIdAsync(portName).Returns(gamelet);

            var port = Substitute.For<IDebugPort2>();
            _debugPortFactory.Create(gamelet, _portSupplier, _testDebugSessionId).Returns(port);

            Assert.AreEqual(VSConstants.S_OK,
                            _portSupplier.AddPort(request, out IDebugPort2 debugPort));
            Assert.AreEqual(port, debugPort);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsGet,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public void AddPortMultipleTimes()
        {
            var debugSessionIds = new[] { "session1", "session2" };

            _metrics.NewDebugSessionId().Returns(debugSessionIds[0], debugSessionIds[1]);

            var request = Substitute.For<IDebugPortRequest2>();
            string portName = "test port";
            request.GetPortName(out string _).Returns(x =>
            {
                x[0] = portName;
                return VSConstants.S_OK;
            });

            var gamelet = new Gamelet { Id = "test id" };
            _gameletClient.LoadByNameOrIdAsync(portName).Returns(gamelet);

            foreach (string sessionId in debugSessionIds)
            {
                var port = Substitute.For<IDebugPort2>();
                _debugPortFactory.Create(gamelet, _portSupplier, sessionId).Returns(port);

                Assert.AreEqual(VSConstants.S_OK,
                                _portSupplier.AddPort(request, out IDebugPort2 debugPort));
                Assert.AreSame(port, debugPort);
                AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsGet,
                                     DeveloperEventStatus.Types.Code.Success, sessionId);
            }
        }

        [Test]
        public void CanAddPort()
        {
            Assert.AreEqual(VSConstants.S_OK, _portSupplier.CanAddPort());
        }

        [Test]
        public void EnumPorts()
        {
            var gamelets = new List<Gamelet>
            {
                new Gamelet { Id = "test id1", DisplayName = "name1", ReserverEmail = _reserver },
                new Gamelet
                {
                    Id = "test id2", DisplayName = "name2",
                    ReserverEmail = "anotherReserver@test.com"
                },
                new Gamelet { Id = "test id3", DisplayName = "name3", ReserverEmail = _reserver },
            };
            _gameletClient.ListGameletsAsync(onlyOwned: false)
                .Returns(x => Task.FromResult(gamelets));

            var ownedInstancePort1 = Substitute.For<IDebugPort2>();
            _debugPortFactory.Create(gamelets[0], _portSupplier, _testDebugSessionId)
                .Returns(ownedInstancePort1);

            var projectInstancePort = Substitute.For<IDebugPort2>();
            _debugPortFactory.Create(gamelets[1], _portSupplier, _testDebugSessionId)
                .Returns(projectInstancePort);

            var ownedInstancePort2 = Substitute.For<IDebugPort2>();
            _debugPortFactory.Create(gamelets[2], _portSupplier, _testDebugSessionId)
                .Returns(ownedInstancePort2);

            Assert.AreEqual(VSConstants.S_OK,
                            _portSupplier.EnumPorts(out IEnumDebugPorts2 portsEnum));

            var ports = new IDebugPort2[3];
            uint numFetched = 0;
            Assert.AreEqual(VSConstants.S_OK,
                            portsEnum.Next((uint) ports.Length, ports, ref numFetched));
            CollectionAssert.AreEqual(
                new[] { projectInstancePort, ownedInstancePort2, ownedInstancePort1 }, ports);

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsList,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public void EnumPortsMultipleTimes()
        {
            var debugSessionIds = new[] { "session1", "session2" };

            _metrics.NewDebugSessionId().Returns(debugSessionIds[0], debugSessionIds[1]);

            var gamelet = new Gamelet { Id = "test id1" };
            _gameletClient.ListGameletsAsync(onlyOwned: false)
                .Returns(x => Task.FromResult(new List<Gamelet> { gamelet }));

            foreach (string sessionId in debugSessionIds)
            {
                var port = Substitute.For<IDebugPort2>();
                _debugPortFactory.Create(gamelet, _portSupplier, sessionId).Returns(port);

                Assert.AreEqual(VSConstants.S_OK,
                                _portSupplier.EnumPorts(out IEnumDebugPorts2 portsEnum));

                var ports = new IDebugPort2[1];
                uint numFetched = 0;
                Assert.AreEqual(VSConstants.S_OK,
                                portsEnum.Next((uint)ports.Length, ports, ref numFetched));
                Assert.AreEqual(1, numFetched);
                Assert.AreSame(ports[0], port);
                AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsList,
                                     DeveloperEventStatus.Types.Code.Success, sessionId);
            }
        }

        [Test]
        public void EnumPortsError()
        {
            _gameletClient.ListGameletsAsync(onlyOwned: false)
                .Returns<List<Gamelet>>(x => throw new CloudException("test exception"));

            Assert.AreEqual(VSConstants.S_OK,
                            _portSupplier.EnumPorts(out IEnumDebugPorts2 portsEnum));

            _dialogUtil.Received().ShowError("test exception");

            Assert.AreEqual(VSConstants.S_OK, portsEnum.GetCount(out uint count));
            Assert.AreEqual(0, count);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsList,
                                 DeveloperEventStatus.Types.Code.InternalError);
        }

        [Test]
        public void GetPortNotFound()
        {
            var guid = new Guid();
            Assert.AreEqual(YetiVSI.DebugEngine.AD7Constants.E_PORTSUPPLIER_NO_PORT,
                            _portSupplier.GetPort(ref guid, out IDebugPort2 _));
        }

        [Test]
        public void GetPortSupplierId()
        {
            Assert.AreEqual(VSConstants.S_OK, _portSupplier.GetPortSupplierId(out Guid guid));
            Assert.AreEqual(YetiConstants.PortSupplierGuid, guid);
        }

        [Test]
        public void GetPortSupplierName()
        {
            Assert.AreEqual(VSConstants.S_OK, _portSupplier.GetPortSupplierName(out string name));
            Assert.AreEqual(YetiConstants.YetiTitle, name);
        }

        [Test]
        public void RemovePort()
        {
            var port = Substitute.For<IDebugPort2>();
            Assert.AreEqual(VSConstants.E_NOTIMPL, _portSupplier.RemovePort(port));
        }

        [Test]
        public void GetDescription()
        {
            var flags = new enum_PORT_SUPPLIER_DESCRIPTION_FLAGS[1];
            Assert.AreEqual(VSConstants.S_OK, _portSupplier.GetDescription(flags, out string text));
            Assert.AreEqual(
                "The Stadia transport lets you select an instance from the Qualifier drop-down " +
                    "menu and remotely attach to an existing process on that instance",
                text);
        }

        void AssertMetricRecorded(DeveloperEventType.Types.Type type,
                                  DeveloperEventStatus.Types.Code status,
                                  string debugSessionId = _testDebugSessionId)
        {
            _metrics.Received().RecordEvent(
                type, Arg.Is<DeveloperLogEvent>(p => p.StatusCode == status &&
                                                     p.DebugSessionIdStr == debugSessionId));
        }
    }
}