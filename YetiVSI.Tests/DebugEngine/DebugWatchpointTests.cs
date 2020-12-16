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
using Microsoft.VisualStudio;
using NUnit.Framework;
using NSubstitute;
using System;
using YetiVSI.DebugEngine;
using Microsoft.VisualStudio.Threading;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.Test
{
    // TODO: Switch to DebugBreakpointErrorFactory when it's available.
    [TestFixture]
    class DebugWatchpointTests
    {
        const string TEST_ADDRESS_STR = "0xdeadbeef";
        const long TEST_ADDRESS = 0xdeadbeef;
        const uint WATCH_SIZE = 4;
        const int INVALID_ID = -1;
        const int EXPECTED_ID = 1;

        DebugWatchpoint.Factory watchpointFactory;
        IWatchpoint watchpoint;
        IBreakpointManager mockBreakpointManager;
        IDebugBreakpointRequest2 mockBreakpointRequest;
        IDebugProgram2 mockProgram;
        IDebugBreakpointResolution2 mockResolution;
        DebugWatchpointResolution.Factory mockResolutionFactory;
        SbError mockError;
        RemoteTarget mockTarget;
        Marshal mockMarshal;
        SbWatchpoint mockLldbWatchpoint;
        BP_REQUEST_INFO requestInfo;

        [SetUp]
        public void SetUp()
        {
            var taskContext = new JoinableTaskContext();
            mockBreakpointManager = Substitute.For<IBreakpointManager>();
            mockBreakpointRequest = Substitute.For<IDebugBreakpointRequest2>();
            mockProgram = Substitute.For<IDebugProgram2>();
            mockResolution = Substitute.For<IDebugBreakpointResolution2>();
            mockResolutionFactory = Substitute.For<DebugWatchpointResolution.Factory>();
            mockResolutionFactory.Create(TEST_ADDRESS_STR, mockProgram).Returns(mockResolution);
            mockTarget = Substitute.For<RemoteTarget>();
            SbError error;
            mockError = Substitute.For<SbError>();
            mockTarget.WatchAddress(TEST_ADDRESS, WATCH_SIZE, false, true, out error).Returns(x =>
            {
                x[4] = mockError;
                return mockLldbWatchpoint;
            });
            mockMarshal = Substitute.For<Marshal>();
            mockMarshal.GetStringFromIntPtr(Arg.Any<IntPtr>()).Returns(TEST_ADDRESS_STR);
            mockLldbWatchpoint = Substitute.For<SbWatchpoint>();
            requestInfo = new BP_REQUEST_INFO();
            requestInfo.bpLocation.unionmember4 = (IntPtr)4;
            mockBreakpointRequest.GetRequestInfo(Arg.Any<enum_BPREQI_FIELDS>(),
                Arg.Any<BP_REQUEST_INFO[]>()).Returns(x =>
                {
                    enum_BPREQI_FIELDS fields = (enum_BPREQI_FIELDS)x[0];
                    BP_REQUEST_INFO[] breakpointRequestInfo = (BP_REQUEST_INFO[])x[1];
                    if (breakpointRequestInfo == null || breakpointRequestInfo.Length == 0)
                    {
                        return 1;
                    }
                    return BuildBreakpointRequestInfo(fields, out breakpointRequestInfo[0]);
                });
            mockLldbWatchpoint.GetId().Returns(EXPECTED_ID);
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_DATA_STRING);

            watchpointFactory = new DebugWatchpoint.Factory(taskContext, mockResolutionFactory,
                new BreakpointErrorEnumFactory(), new BoundBreakpointEnumFactory());
            watchpoint = watchpointFactory.Create(mockBreakpointManager, mockBreakpointRequest,
                mockTarget, mockProgram, mockMarshal);
        }

        [Test]
        public void BindUnsupportedType()
        {
            // Set a unsupported breakpoint type and create a new pending watchpoint.
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_NONE);
            watchpoint = watchpointFactory.Create(mockBreakpointManager, mockBreakpointRequest,
                mockTarget, mockProgram, mockMarshal);

            var result = watchpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetWatchpointError();

            mockBreakpointManager.Received().ReportBreakpointError(
                Arg.Any<DebugBreakpointError>());
            Assert.AreNotEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_FALSE, result);
        }

        [Test]
        public void BindWatchpoint()
        {
            SbError error;
            Assert.AreEqual(VSConstants.S_OK, watchpoint.Bind());
            mockLldbWatchpoint.Received(1).SetEnabled(true);
            mockTarget.Received(1).WatchAddress(TEST_ADDRESS, WATCH_SIZE, false, true, out error);
            mockBreakpointManager.Received(1).RegisterWatchpoint(watchpoint);
            IEnumDebugBoundBreakpoints2 enumBoundBreakpoints;
            watchpoint.EnumBoundBreakpoints(out enumBoundBreakpoints);
            uint countBoundBreakpoint;
            enumBoundBreakpoints.GetCount(out countBoundBreakpoint);
            Assert.AreEqual(1, countBoundBreakpoint);
            IDebugBoundBreakpoint2[] boundBreakpoint = new IDebugBoundBreakpoint2[1];
            uint actualCount = 0;
            enumBoundBreakpoints.Next(1, boundBreakpoint, ref actualCount);
            Assert.AreEqual(watchpoint, boundBreakpoint[0]);
        }

        [Test]
        public void BindWatchpointFailed()
        {
            SbError error;
            SbError mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(true);
            mockTarget.WatchAddress(TEST_ADDRESS, WATCH_SIZE, false, true, out error).Returns(
                x =>
                {
                    x[4] = mockError;
                    return null;
                });

            var result = watchpoint.Bind();
            IDebugErrorBreakpoint2 watchpointError = GetWatchpointError();

            mockBreakpointManager.Received().ReportBreakpointError(
                Arg.Any<DebugBreakpointError>());
            Assert.AreNotEqual(null, watchpointError);
            Assert.AreEqual(VSConstants.S_FALSE, result);
        }

        [Test]
        public void BindDisabled()
        {
            watchpoint.Enable(0);
            var result = watchpoint.Bind();
            IDebugErrorBreakpoint2 watchpointError = GetWatchpointError();

            mockBreakpointManager.Received().RegisterWatchpoint(watchpoint);
            Assert.AreEqual(null, watchpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
            mockLldbWatchpoint.Received(1).SetEnabled(false);
        }

        [Test]
        public void BindCondition()
        {
            const string testCondition = "true";
            SetCondition(enum_BP_COND_STYLE.BP_COND_WHEN_TRUE, testCondition);
            watchpoint = watchpointFactory.Create(mockBreakpointManager, mockBreakpointRequest,
                mockTarget, mockProgram, mockMarshal);
            var result = watchpoint.Bind();
            IDebugErrorBreakpoint2 watchpointError = GetWatchpointError();

            mockBreakpointManager.Received().RegisterWatchpoint(watchpoint);
            Assert.AreEqual(null, watchpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
            mockLldbWatchpoint.Received(1).SetCondition(testCondition);
        }

        [Test]
        public void BindPassCountEqual()
        {
            const uint PASS_COUNT = 3u;
            SetPassCount(enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL, PASS_COUNT);
            watchpoint = watchpointFactory.Create(mockBreakpointManager, mockBreakpointRequest,
                mockTarget, mockProgram, mockMarshal);
            var result = watchpoint.Bind();
            IDebugErrorBreakpoint2 watchpointError = GetWatchpointError();

            mockBreakpointManager.Received().RegisterWatchpoint(watchpoint);
            Assert.AreEqual(null, watchpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
            mockLldbWatchpoint.Received(1).SetIgnoreCount(PASS_COUNT - 1);
        }

        [Test]
        public void BindPassCountEqualExceeds()
        {
            const uint PASS_COUNT = 3u;
            const uint HIT_COUNT = 5u;

            SetPassCount(enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL, PASS_COUNT);
            watchpoint = watchpointFactory.Create(mockBreakpointManager, mockBreakpointRequest,
                mockTarget, mockProgram, mockMarshal);
            mockLldbWatchpoint.GetHitCount().Returns(HIT_COUNT);

            var result = watchpoint.Bind();
            IDebugErrorBreakpoint2 watchpointError = GetWatchpointError();

            mockBreakpointManager.Received().RegisterWatchpoint(watchpoint);
            Assert.AreEqual(null, watchpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
            mockLldbWatchpoint.DidNotReceiveWithAnyArgs().SetIgnoreCount(PASS_COUNT);
            mockLldbWatchpoint.Received().SetEnabled(false);
        }

        [Test]
        public void BindPassCountNone()
        {
            const uint PASS_COUNT = 3u;
            SetPassCount(enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_NONE, PASS_COUNT);
            watchpoint = watchpointFactory.Create(mockBreakpointManager, mockBreakpointRequest,
                mockTarget, mockProgram, mockMarshal);
            var result = watchpoint.Bind();
            IDebugErrorBreakpoint2 watchpointError = GetWatchpointError();

            mockBreakpointManager.Received().RegisterWatchpoint(watchpoint);
            Assert.AreEqual(null, watchpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
            mockLldbWatchpoint.Received(1).SetIgnoreCount(0);
        }

        [Test]
        public void BindPassCountEqualOrGreater()
        {
            const uint PASS_COUNT = 3u;
            SetPassCount(enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL_OR_GREATER, PASS_COUNT);
            watchpoint = watchpointFactory.Create(mockBreakpointManager, mockBreakpointRequest,
                mockTarget, mockProgram, mockMarshal);
            var result = watchpoint.Bind();
            IDebugErrorBreakpoint2 watchpointError = GetWatchpointError();

            mockBreakpointManager.Received().RegisterWatchpoint(watchpoint);
            Assert.AreEqual(null, watchpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
            mockLldbWatchpoint.Received(1).SetIgnoreCount(PASS_COUNT - 1);
        }

        [Test]
        public void BindPassCountMod()
        {
            const uint PASS_COUNT = 5u;
            const uint HIT_COUNT = 6u;

            SetPassCount(enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_MOD, PASS_COUNT);
            watchpoint = watchpointFactory.Create(mockBreakpointManager, mockBreakpointRequest,
                mockTarget, mockProgram, mockMarshal);
            mockLldbWatchpoint.GetHitCount().Returns(HIT_COUNT);

            var result = watchpoint.Bind();
            IDebugErrorBreakpoint2 watchpointError = GetWatchpointError();

            mockBreakpointManager.Received().RegisterWatchpoint(watchpoint);
            Assert.AreEqual(null, watchpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
            mockLldbWatchpoint.Received().SetIgnoreCount(9u);
        }

        [Test]
        public void CanBindDeleted()
        {
            watchpoint.Delete();
            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            var result = watchpoint.CanBind(out errorBreakpointsEnum);

            Assert.AreEqual(null, errorBreakpointsEnum);
            Assert.AreEqual(AD7Constants.E_BP_DELETED, result);
        }

        [Test]
        public void CanBindUnsupportedType()
        {
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_NONE);
            watchpoint = watchpointFactory.Create(mockBreakpointManager, mockBreakpointRequest,
                mockTarget, mockProgram, mockMarshal);
            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            Assert.AreEqual(VSConstants.S_FALSE, watchpoint.CanBind(out errorBreakpointsEnum));
            uint errorCount;
            errorBreakpointsEnum.GetCount(out errorCount);
            Assert.AreEqual(1, errorCount);
        }

        [Test]
        public void CanBindCondition()
        {
            SetCondition(enum_BP_COND_STYLE.BP_COND_WHEN_TRUE, "true");
            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            Assert.AreEqual(VSConstants.S_OK, watchpoint.CanBind(out errorBreakpointsEnum));
        }

        [Test]
        public void CanBindPassCount()
        {
            SetPassCount(enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL, 5);
            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            Assert.AreEqual(VSConstants.S_OK, watchpoint.CanBind(out errorBreakpointsEnum));
        }

        [Test]
        public void CanBindConditionWhenChanged()
        {
            SetCondition(enum_BP_COND_STYLE.BP_COND_WHEN_CHANGED, "true");
            watchpoint = watchpointFactory.Create(mockBreakpointManager, mockBreakpointRequest,
                mockTarget, mockProgram, mockMarshal);
            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            Assert.AreEqual(VSConstants.S_FALSE, watchpoint.CanBind(out errorBreakpointsEnum));
            uint errorCount;
            errorBreakpointsEnum.GetCount(out errorCount);
            Assert.AreEqual(1, errorCount);
        }

        [Test]
        public void CanBind()
        {
            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            Assert.AreEqual(VSConstants.S_OK, watchpoint.CanBind(out errorBreakpointsEnum));
            Assert.AreEqual(null, errorBreakpointsEnum);
        }

        [Test]
        public void Delete()
        {
            var result = watchpoint.Delete();
            PENDING_BP_STATE_INFO[] state = new PENDING_BP_STATE_INFO[1];
            watchpoint.GetState(state);

            mockBreakpointManager.Received(1).UnregisterWatchpoint(watchpoint);
            Assert.AreEqual(enum_PENDING_BP_STATE.PBPS_DELETED, state[0].state);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindAndDelete()
        {
            watchpoint.Bind();
            var result = watchpoint.Delete();
            PENDING_BP_STATE_INFO[] state = new PENDING_BP_STATE_INFO[1];
            watchpoint.GetState(state);

            Assert.AreEqual(enum_PENDING_BP_STATE.PBPS_DELETED, state[0].state);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void Enable()
        {
            var result = watchpoint.Enable(1);
            watchpoint.Bind();
            PENDING_BP_STATE_INFO[] state = new PENDING_BP_STATE_INFO[1];
            watchpoint.GetState(state);

            mockLldbWatchpoint.Received(1).SetEnabled(true);
            Assert.AreEqual(enum_PENDING_BP_STATE.PBPS_ENABLED, state[0].state);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void Disable()
        {
            var result = watchpoint.Enable(0);
            watchpoint.Bind();
            PENDING_BP_STATE_INFO[] state = new PENDING_BP_STATE_INFO[1];
            watchpoint.GetState(state);

            mockLldbWatchpoint.Received(1).SetEnabled(false);
            Assert.AreEqual(enum_PENDING_BP_STATE.PBPS_DISABLED, state[0].state);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindAndEnable()
        {
            watchpoint.Bind();
            var result = watchpoint.Enable(1);
            PENDING_BP_STATE_INFO[] state = new PENDING_BP_STATE_INFO[1];
            watchpoint.GetState(state);
            Assert.AreEqual(enum_PENDING_BP_STATE.PBPS_ENABLED, state[0].state);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindAndDisable()
        {
            watchpoint.Bind();
            var result = watchpoint.Enable(0);
            PENDING_BP_STATE_INFO[] state = new PENDING_BP_STATE_INFO[1];
            watchpoint.GetState(state);
            Assert.AreEqual(enum_PENDING_BP_STATE.PBPS_DISABLED, state[0].state);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void GetWatchpointId()
        {
            watchpoint.Bind();
            int id = watchpoint.GetId();
            Assert.AreEqual(EXPECTED_ID, id);
        }

        [Test]
        public void GetBreakpointResolution()
        {
            watchpoint.Bind();
            IDebugBreakpointResolution2 output;
            Assert.AreEqual(VSConstants.S_OK, watchpoint.GetBreakpointResolution(out output));
            Assert.AreEqual(mockResolution, output);
        }

        [Test]
        public void GetBrekapointResolutionUnBound()
        {
            IDebugBreakpointResolution2 output;
            Assert.AreEqual(VSConstants.E_FAIL, watchpoint.GetBreakpointResolution(out output));
            Assert.AreEqual(null, output);
        }

        private int BuildBreakpointRequestInfo(enum_BPREQI_FIELDS fields,
            out BP_REQUEST_INFO breakpointRequestInfo)
        {
            breakpointRequestInfo = new BP_REQUEST_INFO();
            if ((fields & enum_BPREQI_FIELDS.BPREQI_BPLOCATION) != 0 &&
                (requestInfo.dwFields & enum_BPREQI_FIELDS.BPREQI_BPLOCATION) != 0)
            {
                breakpointRequestInfo.dwFields |= enum_BPREQI_FIELDS.BPREQI_BPLOCATION;
                breakpointRequestInfo.bpLocation = requestInfo.bpLocation;
            }
            if ((fields & enum_BPREQI_FIELDS.BPREQI_CONDITION) != 0 &&
                (requestInfo.dwFields & enum_BPREQI_FIELDS.BPREQI_CONDITION) != 0)
            {
                breakpointRequestInfo.dwFields |= enum_BPREQI_FIELDS.BPREQI_CONDITION;
                breakpointRequestInfo.bpCondition = requestInfo.bpCondition;
            }
            if ((fields & enum_BPREQI_FIELDS.BPREQI_PASSCOUNT) != 0 &&
                (requestInfo.dwFields & enum_BPREQI_FIELDS.BPREQI_PASSCOUNT) != 0)
            {
                breakpointRequestInfo.dwFields |= enum_BPREQI_FIELDS.BPREQI_PASSCOUNT;
                breakpointRequestInfo.bpPassCount = requestInfo.bpPassCount;
            }
            return 0;
        }

        // Update the mock breakpoint request to return a specific breakpoint type.  This must be
        // called before constructing the pending breakpoint.
        private void SetBreakpointType(enum_BP_LOCATION_TYPE type)
        {
            requestInfo.dwFields |= enum_BPREQI_FIELDS.BPREQI_BPLOCATION;
            requestInfo.bpLocation.bpLocationType = (uint)type;
        }

        private IDebugErrorBreakpoint2 GetWatchpointError()
        {
            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            watchpoint.EnumErrorBreakpoints(enum_BP_ERROR_TYPE.BPET_ALL,
                out errorBreakpointsEnum);
            IDebugErrorBreakpoint2[] breakpointErrors = new IDebugErrorBreakpoint2[1];
            uint fetchedIndex = 0;
            errorBreakpointsEnum.Next(1, breakpointErrors, ref fetchedIndex);
            return breakpointErrors[0];
        }

        // Update the mock breakpoint request to return a condition.  This must be called before
        // constructing the pending breakpoint.
        private void SetCondition(enum_BP_COND_STYLE conditionStyle, string conditionString)
        {
            requestInfo.dwFields |= enum_BPREQI_FIELDS.BPREQI_CONDITION;
            requestInfo.bpCondition = new BP_CONDITION
            {
                styleCondition = conditionStyle,
                bstrCondition = conditionString,
            };
        }

        private void SetPassCount(enum_BP_PASSCOUNT_STYLE passCountStyle, uint passCount)
        {
            requestInfo.dwFields |= enum_BPREQI_FIELDS.BPREQI_PASSCOUNT;
            requestInfo.bpPassCount = new BP_PASSCOUNT
            {
                stylePassCount = passCountStyle,
                dwPassCount = passCount,
            };
        }
    }
}
