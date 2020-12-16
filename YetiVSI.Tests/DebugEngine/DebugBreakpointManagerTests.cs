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

using DebuggerApi;
using Microsoft.VisualStudio.Debugger.Interop;
using NUnit.Framework;
using NSubstitute;
using YetiVSI.DebugEngine;
using Microsoft.VisualStudio.Threading;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugBreakpointManagerTests
    {
        const int ID = 0;
        JoinableTaskContext taskContext;
        IBreakpointManager breakpointManager;
        IDebugBreakpointRequest2 mockBreakpointRequest;
        RemoteTarget mockTarget;
        IGgpDebugProgram mockProgram;
        IPendingBreakpoint mockPendingBreakpoint;
        IWatchpoint mockWatchpoint;
        DebugPendingBreakpoint.Factory mockPendingBreakpointFactory;
        DebugWatchpoint.Factory mockWatchpointFactory;

        [SetUp]
        public void SetUp()
        {
            taskContext = new JoinableTaskContext();
            mockBreakpointRequest = Substitute.For<IDebugBreakpointRequest2>();
            mockTarget = Substitute.For<RemoteTarget>();
            mockProgram = Substitute.For<IGgpDebugProgram>();
            mockPendingBreakpoint = Substitute.For<IPendingBreakpoint>();
            mockPendingBreakpoint.GetId().Returns(ID);
            mockWatchpoint = Substitute.For<IWatchpoint>();
            mockWatchpoint.GetId().Returns(ID);
            mockPendingBreakpointFactory = Substitute.For<DebugPendingBreakpoint.Factory>();
            mockWatchpointFactory = Substitute.For<DebugWatchpoint.Factory>();
            var mockDebugEngineHandler = Substitute.For<IDebugEngineHandler>();
            breakpointManager = new LldbBreakpointManager.Factory(taskContext,
                mockPendingBreakpointFactory, mockWatchpointFactory).Create(mockDebugEngineHandler,
                    mockProgram);

            mockPendingBreakpointFactory.Create(breakpointManager, mockProgram,
                mockBreakpointRequest, mockTarget).ReturnsForAnyArgs(mockPendingBreakpoint);
            mockWatchpointFactory.Create(breakpointManager, mockBreakpointRequest, mockTarget,
                mockProgram).ReturnsForAnyArgs(mockWatchpoint);
        }

        [Test]
        public void CreatePendingBreakpoint()
        {
            IDebugPendingBreakpoint2 output;
            breakpointManager.CreatePendingBreakpoint(
                mockBreakpointRequest, mockTarget, out output);
            Assert.AreEqual(mockPendingBreakpoint, output);
        }

        [Test]
        public void RegisterAndRetrievePendingBreakpoint()
        {
            IPendingBreakpoint pendingBreakpoint;
            bool result = breakpointManager.GetPendingBreakpointById(ID, out pendingBreakpoint);
            Assert.IsFalse(result);
            breakpointManager.RegisterPendingBreakpoint(mockPendingBreakpoint);
            result = breakpointManager.GetPendingBreakpointById(ID, out pendingBreakpoint);
            Assert.IsTrue(result);
            Assert.AreEqual(ID, pendingBreakpoint.GetId());
        }

        [Test]
        public void CreateWatchpoint()
        {
            IDebugPendingBreakpoint2 pendingBreakpoint;
            mockBreakpointRequest.GetRequestInfo(enum_BPREQI_FIELDS.BPREQI_BPLOCATION, Arg.Do(
                delegate (BP_REQUEST_INFO[] x)
                {
                    x[0].bpLocation.bpLocationType = (uint)enum_BP_LOCATION_TYPE.BPLT_DATA_STRING;
                }));
            breakpointManager.CreatePendingBreakpoint(
                mockBreakpointRequest, mockTarget, out pendingBreakpoint);
            mockWatchpointFactory.Received(1).Create(breakpointManager, mockBreakpointRequest,
                mockTarget, mockProgram);
        }

        [Test]
        public void RegisterAndRetrieveWatchpoint()
        {
            IWatchpoint watchpoint;
            bool result = breakpointManager.GetWatchpointById(ID, out watchpoint);
            Assert.IsFalse(result);
            breakpointManager.RegisterWatchpoint(mockWatchpoint);
            result = breakpointManager.GetWatchpointById(ID, out watchpoint);
            Assert.IsTrue(result);
            Assert.AreEqual(ID, watchpoint.GetId());

            int watchpointCount = breakpointManager.GetWatchpointRefCount(mockWatchpoint);
            Assert.AreEqual(1, watchpointCount);
        }

        [Test]
        public void UnregisterWatchpoint()
        {
            int watchpointCount = breakpointManager.GetWatchpointRefCount(mockWatchpoint);
            Assert.AreEqual(0, watchpointCount);

            breakpointManager.RegisterWatchpoint(mockWatchpoint);

            watchpointCount = breakpointManager.GetWatchpointRefCount(mockWatchpoint);
            Assert.AreEqual(1, watchpointCount);

            breakpointManager.UnregisterWatchpoint(mockWatchpoint);

            watchpointCount = breakpointManager.GetWatchpointRefCount(mockWatchpoint);
            Assert.AreEqual(0, watchpointCount);
        }

        [Test]
        public void GetNumPendingBreakpoints()
        {
            int numPendingBreakpoints = 10;
            RegisterBreakpoints(numPendingBreakpoints);

            Assert.AreEqual(numPendingBreakpoints, breakpointManager.GetNumPendingBreakpoints());
        }

        [Test]
        public void GetNumBoundBreakpoints()
        {
            int numPendingBreakpoints = 8;
            uint numLocations = 0;

            for (int i = 0; i < numPendingBreakpoints; i++)
            {
                uint locations = (uint)i + 1u;
                RegisterBreakpointWithLocations(i, locations);
                numLocations += locations;
            }

            Assert.AreEqual(numLocations, breakpointManager.GetNumBoundBreakpoints());
        }

        public void RegisterBreakpoints(int count)
        {
            for (int i = 0; i < count; i++)
            {
                RegisterBreakpointWithLocations(i, 1);
            }
        }

        public void RegisterBreakpointWithLocations(int id, uint locations)
        {
            IPendingBreakpoint pendingBreakpoint = Substitute.For<IPendingBreakpoint>();
            pendingBreakpoint.GetId().Returns(id);
            pendingBreakpoint.GetNumLocations().Returns(locations);
            breakpointManager.RegisterPendingBreakpoint(pendingBreakpoint);
        }
    }
}