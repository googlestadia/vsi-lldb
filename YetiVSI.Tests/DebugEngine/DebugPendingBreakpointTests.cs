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
using NUnit.Framework;
using NSubstitute;
using Microsoft.VisualStudio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI.DebugEngine;
using Microsoft.VisualStudio.Threading;
using DebuggerGrpcClient.Interfaces;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugPendingBreakpoint_DeletedTests
    {
        IPendingBreakpoint pendingBreakpoint;

        [SetUp]
        public void SetUp()
        {
            var taskContext = new JoinableTaskContext();
            IDebugBreakpointRequest2 mockBreakpointRequest =
                Substitute.For<IDebugBreakpointRequest2>();
            mockBreakpointRequest.GetRequestInfo(Arg.Any<enum_BPREQI_FIELDS>(),
                  Arg.Any<BP_REQUEST_INFO[]>()).Returns(x =>
                  {
                      return VSConstants.S_OK;
                  });

            pendingBreakpoint = new DebugPendingBreakpoint.Factory(taskContext, null, null, null)
                .Create(null, null, mockBreakpointRequest, null);
            pendingBreakpoint.Delete();
        }

        [Test]
        public void Bind()
        {
            Assert.AreEqual(AD7Constants.E_BP_DELETED, pendingBreakpoint.Bind());
        }

        [Test]
        public void CanBind()
        {
            IEnumDebugErrorBreakpoints2 ppErrorEnum;
            Assert.AreEqual(AD7Constants.E_BP_DELETED, pendingBreakpoint.CanBind(out ppErrorEnum));
            Assert.IsNull(ppErrorEnum);
        }

        [Test]
        public void Delete()
        {
            Assert.AreEqual(AD7Constants.E_BP_DELETED, pendingBreakpoint.Delete());
        }

        [Test]
        public void Enable()
        {
            Assert.AreEqual(AD7Constants.E_BP_DELETED, pendingBreakpoint.Enable(1));
        }

        [Test]
        public void EnumBoundBreakpoints()
        {
            IEnumDebugBoundBreakpoints2 boundBreakpointsEnum;
            Assert.AreEqual(AD7Constants.E_BP_DELETED, pendingBreakpoint.EnumBoundBreakpoints(
                out boundBreakpointsEnum));
            Assert.IsNull(boundBreakpointsEnum);
        }

        [Test]
        public void EnumErrorBreakpoints()
        {
            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            Assert.AreEqual(AD7Constants.E_BP_DELETED, pendingBreakpoint.EnumErrorBreakpoints(
                enum_BP_ERROR_TYPE.BPET_ALL, out errorBreakpointsEnum));
            Assert.IsNull(errorBreakpointsEnum);
        }

        [Test]
        public void GetBreakpointRequest()
        {
            IDebugBreakpointRequest2 breakpointRequest;
            Assert.AreEqual(AD7Constants.E_BP_DELETED, pendingBreakpoint.GetBreakpointRequest(
                out breakpointRequest));
            Assert.IsNull(breakpointRequest);
        }

        [Test]
        public void SetPassCount()
        {
            Assert.AreEqual(AD7Constants.E_BP_DELETED, pendingBreakpoint.SetPassCount(
                new BP_PASSCOUNT()));
        }
    }

    [TestFixture]
    class DebugPendingBreakpointTests
    {
        const int INVALID_ID = -1;
        const int EXPECTED_ID = 1;
        const int BOUND_BREAKPOINT_ID = 0;
        const string TEST_FUNCTION_NAME = "testFunctionName";
        const string TEST_FUNCTION_NAME_WITH_OFFSET = " { testFunctionName , , } + 10 ";
        const string TEST_FUNCTION_NAME_WITHOUT_OFFSET = "{testFunctionName,,}";
        const string TEST_FILE_NAME = "testFileName";
        uint COLUMN_NUMBER = 1;
        uint LINE_NUMBER = 2;
        ulong TEST_ADDRESS = 0xdeadbeef;
        string STR_TEST_ADDRESS = "0xdeadbeef";

        DebugPendingBreakpoint.Factory debugPendingBreakpointFactory;
        DebugBoundBreakpoint.Factory mockBoundBreakpointFactory;
        IPendingBreakpoint pendingBreakpoint;
        IBreakpointManager mockBreakpointManager;
        IDebugBreakpointRequest2 mockBreakpointRequest;
        RemoteTarget mockTarget;
        IDebugProgram2 mockProgram;
        Marshal mockMarshal;
        RemoteBreakpoint mockLldbBreakpoint;
        BP_REQUEST_INFO requestInfo;

        [SetUp]
        public void SetUp()
        {
            var taskContext = new JoinableTaskContext();
            mockBreakpointManager = Substitute.For<IBreakpointManager>();
            mockBreakpointRequest = Substitute.For<IDebugBreakpointRequest2>();
            mockTarget = Substitute.For<RemoteTarget>();
            mockProgram = Substitute.For<IDebugProgram2>();
            mockMarshal = Substitute.For<Marshal>();
            mockLldbBreakpoint = Substitute.For<RemoteBreakpoint>();
            requestInfo = new BP_REQUEST_INFO();
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
            mockLldbBreakpoint.GetId().Returns(EXPECTED_ID);
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_CODE_FILE_LINE);

            mockBoundBreakpointFactory = Substitute.For<DebugBoundBreakpoint.Factory>();

            debugPendingBreakpointFactory = new DebugPendingBreakpoint.Factory(taskContext,
                mockBoundBreakpointFactory, new BreakpointErrorEnumFactory(),
                new BoundBreakpointEnumFactory());

            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);
        }

        [Test]
        public void BindUnsupportedType()
        {
            // Set a unsupported breakpoint type and create a new pending breakpoint.
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_NONE);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();

            mockBreakpointManager.Received().ReportBreakpointError(
                Arg.Any<DebugBreakpointError>());
            Assert.AreNotEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_FALSE, result);
        }

        [Test]
        public void BindLineBreakpoint()
        {
            MockBreakpoint(1);
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();
            var boundBreakpoints = GetBoundBreakpoints();

            Assert.AreEqual(1, boundBreakpoints.Count);
            mockBreakpointManager.Received().RegisterPendingBreakpoint(pendingBreakpoint);
            Assert.AreEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindInvalidFile()
        {
            MockDocumentPosition(null, LINE_NUMBER, COLUMN_NUMBER);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();

            mockBreakpointManager.Received().ReportBreakpointError(
                Arg.Any<DebugBreakpointError>());
            Assert.AreNotEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_FALSE, result);
        }

        [Test]
        public void BindFunctionBreakpoint()
        {
            // Set a function breakpoint type and create a new pending breakpoint.
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_CODE_FUNC_OFFSET);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);

            MockFunctionBreakpoint(1);
            MockFunctionPosition(TEST_FUNCTION_NAME);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();
            var boundBreakpoints = GetBoundBreakpoints();

            Assert.AreEqual(1, boundBreakpoints.Count);
            mockBreakpointManager.Received().RegisterPendingBreakpoint(pendingBreakpoint);
            Assert.AreEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindFunctionBreakpointWithOffset()
        {
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_CODE_FUNC_OFFSET);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);

            MockFunctionBreakpoint(1);
            MockFunctionPosition(TEST_FUNCTION_NAME_WITH_OFFSET);

            uint offset = 10;

            mockTarget.CreateFunctionOffsetBreakpoint(TEST_FUNCTION_NAME, offset).Returns(
                new BreakpointErrorPair(mockLldbBreakpoint, BreakpointError.Success));

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();
            var boundBreakpoints = GetBoundBreakpoints();

            Assert.AreEqual(1, boundBreakpoints.Count);
            mockBreakpointManager.Received().RegisterPendingBreakpoint(pendingBreakpoint);

            Assert.AreEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindInvalidFunctionBreakpointWithOffset()
        {
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_CODE_FUNC_OFFSET);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);

            MockFunctionBreakpoint(1);
            MockFunctionPosition(TEST_FUNCTION_NAME_WITH_OFFSET);

            // OffsetBreakpoint returns null when it fails
            //mockLldbBreakpoint.OffsetBreakpoint(offset).Returns((RemoteBreakpoint)null);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();
            var boundBreakpoints = GetBoundBreakpoints();

            Assert.AreEqual(0, boundBreakpoints.Count);
            mockBreakpointManager.DidNotReceive().RegisterPendingBreakpoint(pendingBreakpoint);

            Assert.AreNotEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_FALSE, result);
        }

        [Test]
        public void BindFunctionBreakpointWithoutOffset()
        {
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_CODE_FUNC_OFFSET);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);

            MockFunctionBreakpoint(1);
            MockFunctionPosition(TEST_FUNCTION_NAME_WITHOUT_OFFSET);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();
            var boundBreakpoints = GetBoundBreakpoints();

            mockTarget.Received().BreakpointCreateByName(TEST_FUNCTION_NAME);
            Assert.AreEqual(1, boundBreakpoints.Count);
            mockBreakpointManager.Received().RegisterPendingBreakpoint(pendingBreakpoint);

            mockTarget.DidNotReceive().BreakpointDelete(Arg.Any<int>());
            Assert.AreEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindInvalidFunction()
        {
            // Set a function breakpoint type and create a new pending breakpoint.
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_CODE_FUNC_OFFSET);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);

            MockFunctionBreakpoint(1);
            MockFunctionPosition(null);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();

            mockBreakpointManager.Received().ReportBreakpointError(
                Arg.Any<DebugBreakpointError>());
            Assert.AreNotEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_FALSE, result);
        }

        [Test]
        public void BindAssemblyBreakpoint()
        {
            // Set code context breakpoint type and create a new pending breakpoint.
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_CODE_CONTEXT);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);

            MockAssemblyBreakpoint(1);
            MockCodeContext(TEST_ADDRESS);

            var result = pendingBreakpoint.Bind();
            mockTarget.Received(1).BreakpointCreateByAddress(TEST_ADDRESS);
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();
            var boundBreakpoints = GetBoundBreakpoints();

            Assert.AreEqual(1, boundBreakpoints.Count);
            mockBreakpointManager.Received().RegisterPendingBreakpoint(pendingBreakpoint);
            Assert.AreEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindCodeAddressBreakpoint()
        {
            // Set code address breakpoint type and create a new pending breakpoint.
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_CODE_ADDRESS);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);

            MockAssemblyBreakpoint(1);
            MockCodeAddress(STR_TEST_ADDRESS);

            var result = pendingBreakpoint.Bind();
            mockTarget.Received(1).BreakpointCreateByAddress(TEST_ADDRESS);
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();
            var boundBreakpoints = GetBoundBreakpoints();

            Assert.AreEqual(1, boundBreakpoints.Count);
            mockBreakpointManager.Received().RegisterPendingBreakpoint(pendingBreakpoint);
            Assert.AreEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindBreakpointFailed()
        {
            mockTarget.BreakpointCreateByLocation(TEST_FILE_NAME, LINE_NUMBER).Returns(
                (RemoteBreakpoint)null);
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();

            mockBreakpointManager.Received().ReportBreakpointError(
                Arg.Any<DebugBreakpointError>());
            Assert.AreNotEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_FALSE, result);
        }

        [Test]
        public void BindNoLocations()
        {
            MockBreakpoint(0);
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();

            mockBreakpointManager.Received().ReportBreakpointError(
                Arg.Any<DebugBreakpointError>());
            Assert.AreNotEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_FALSE, result);
        }

        [Test]
        public void BindInvalidLocation()
        {
            List<SbBreakpointLocation> invalidBreakpointLocations =
                new List<SbBreakpointLocation> { null };
            MockBreakpoint(invalidBreakpointLocations);
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();

            mockBreakpointManager.Received().ReportBreakpointError(
                Arg.Any<DebugBreakpointError>());
            Assert.AreNotEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_FALSE, result);
        }

        [Test]
        public void BindNoSourceLine()
        {
            var mockDocumentPosition = Substitute.For<IDebugDocumentPosition2>();
            string value;
            mockDocumentPosition.GetFileName(out value).Returns(x =>
            {
                x[0] = TEST_FILE_NAME;
                return 0;
            });
            mockDocumentPosition.GetRange(Arg.Any<TEXT_POSITION[]>(), null).Returns(
                VSConstants.E_FAIL);
            mockMarshal.GetDocumentPositionFromIntPtr(Arg.Any<IntPtr>()).Returns(
                mockDocumentPosition);

            var result = pendingBreakpoint.Bind();

            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();
            Assert.That(GetBreakpointErrorMessage(breakpointError), Does.Contain("line number"));
            mockBreakpointManager.Received().ReportBreakpointError(
                Arg.Any<DebugBreakpointError>());
            Assert.AreEqual(VSConstants.S_FALSE, result);
        }

        [Test]
        public void BindCondition()
        {
            // Set a condition on the request, and create a new pending breakpoint.
            var testCondition = "testCondition";
            SetCondition(enum_BP_COND_STYLE.BP_COND_WHEN_TRUE, testCondition);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);
            List<SbBreakpointLocation> mockBreakpointLocations =
                CreateMockBreakpointLocations(1);
            MockBreakpoint(mockBreakpointLocations);
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);

            var mockBoundBreakpoint = Substitute.For<IBoundBreakpoint>();
            mockBoundBreakpointFactory.Create(pendingBreakpoint, mockBreakpointLocations[0],
                mockProgram, Guid.Empty).Returns(mockBoundBreakpoint);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();

            var boundBreakpoints = GetBoundBreakpoints();
            Assert.AreEqual(1, boundBreakpoints.Count);
            Assert.AreSame(mockBoundBreakpoint, boundBreakpoints[0]);
            mockBoundBreakpoint.Received().SetCondition(requestInfo.bpCondition);

            mockBreakpointManager.Received().RegisterPendingBreakpoint(pendingBreakpoint);
            Assert.AreEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindPassCount()
        {
            // Set a pass count on the request, and create a new pending breakpoint.
            const uint PASS_COUNT = 3u;
            SetPassCount(enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL, PASS_COUNT);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);
            List<SbBreakpointLocation> mockBreakpointLocations =
                CreateMockBreakpointLocations(1);
            MockBreakpoint(mockBreakpointLocations);
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);

            var mockBoundBreakpoint = Substitute.For<IBoundBreakpoint>();
            mockBoundBreakpointFactory.Create(pendingBreakpoint, mockBreakpointLocations[0],
                 mockProgram, Guid.Empty).Returns(mockBoundBreakpoint);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();

            var boundBreakpoints = GetBoundBreakpoints();
            Assert.AreEqual(1, boundBreakpoints.Count);
            Assert.AreSame(mockBoundBreakpoint, boundBreakpoints[0]);
            mockBoundBreakpoint.Received().SetPassCount(requestInfo.bpPassCount);

            mockBreakpointManager.Received().RegisterPendingBreakpoint(pendingBreakpoint);
            Assert.AreEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindDisabled()
        {
            var mockBreakpointLocations = CreateMockBreakpointLocations(1);
            MockBreakpoint(mockBreakpointLocations);
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);

            var mockBoundBreakpoint = Substitute.For<IBoundBreakpoint>();
            mockBoundBreakpointFactory.Create(pendingBreakpoint, mockBreakpointLocations[0],
                 mockProgram, Guid.Empty).Returns(mockBoundBreakpoint);

            pendingBreakpoint.Enable(0);
            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();

            var boundBreakpoints = GetBoundBreakpoints();
            Assert.AreEqual(1, boundBreakpoints.Count);
            Assert.AreSame(mockBoundBreakpoint, boundBreakpoints[0]);
            mockBoundBreakpoint.Received().Enable(0);

            mockBreakpointManager.Received().RegisterPendingBreakpoint(pendingBreakpoint);
            Assert.AreEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void CanBindDeleted()
        {
            pendingBreakpoint.Delete();
            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            var result = pendingBreakpoint.CanBind(out errorBreakpointsEnum);

            Assert.AreEqual(null, errorBreakpointsEnum);
            Assert.AreEqual(AD7Constants.E_BP_DELETED, result);
        }

        [Test]
        public void CanBindLineBreakpoint()
        {
            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            var result = pendingBreakpoint.CanBind(out errorBreakpointsEnum);

            Assert.AreEqual(null, errorBreakpointsEnum);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void CanBindFunctionBreakpoint()
        {
            // Set a function breakpoint type and create a new pending breakpoint.
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_CODE_FUNC_OFFSET);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);

            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            var result = pendingBreakpoint.CanBind(out errorBreakpointsEnum);

            Assert.AreEqual(null, errorBreakpointsEnum);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void CanBindUnsupportedType()
        {
            // Set a unsupported breakpoint type and create a new pending breakpoint.
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_NONE);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);

            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            var result = pendingBreakpoint.CanBind(out errorBreakpointsEnum);

            Assert.AreNotEqual(null, errorBreakpointsEnum);
            Assert.AreEqual(VSConstants.S_FALSE, result);
        }

        [Test]
        public void Delete()
        {
            var result = pendingBreakpoint.Delete();
            PENDING_BP_STATE_INFO[] state = new PENDING_BP_STATE_INFO[1];
            pendingBreakpoint.GetState(state);

            Assert.AreEqual(enum_PENDING_BP_STATE.PBPS_DELETED, state[0].state);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindAndDelete()
        {
            var mockBreakpointLocations = MockBreakpoint(1);
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);
            mockLldbBreakpoint.GetId().Returns(EXPECTED_ID);

            var mockBoundBreakpoint = Substitute.For<IBoundBreakpoint>();
            mockBoundBreakpointFactory.Create(pendingBreakpoint, mockBreakpointLocations[0],
                mockProgram, Guid.Empty).Returns(mockBoundBreakpoint);

            pendingBreakpoint.Bind();
            var result = pendingBreakpoint.Delete();
            PENDING_BP_STATE_INFO[] state = new PENDING_BP_STATE_INFO[1];
            pendingBreakpoint.GetState(state);

            enum_BP_STATE[] bpState = new enum_BP_STATE[1];
            mockTarget.Received().BreakpointDelete(EXPECTED_ID);

            var boundBreakpoints = GetBoundBreakpoints();
            Assert.AreEqual(0, boundBreakpoints.Count);
            mockBoundBreakpoint.Received().Delete();

            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void Enable()
        {
            var result = pendingBreakpoint.Enable(1);
            PENDING_BP_STATE_INFO[] state = new PENDING_BP_STATE_INFO[1];
            pendingBreakpoint.GetState(state);

            Assert.AreEqual(enum_PENDING_BP_STATE.PBPS_ENABLED, state[0].state);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void Disable()
        {
            var result = pendingBreakpoint.Enable(0);
            PENDING_BP_STATE_INFO[] state = new PENDING_BP_STATE_INFO[1];
            pendingBreakpoint.GetState(state);

            Assert.AreEqual(enum_PENDING_BP_STATE.PBPS_DISABLED, state[0].state);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindAndEnable()
        {
            var mockBreakpointLocations = MockBreakpoint(1);
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);

            var mockBoundBreakpoint = Substitute.For<IBoundBreakpoint>();
            mockBoundBreakpointFactory.Create(pendingBreakpoint, mockBreakpointLocations[0],
                mockProgram, Guid.Empty).Returns(mockBoundBreakpoint);

            pendingBreakpoint.Bind();
            var result = pendingBreakpoint.Enable(1);
            PENDING_BP_STATE_INFO[] state = new PENDING_BP_STATE_INFO[1];
            pendingBreakpoint.GetState(state);

            var boundBreakpoints = GetBoundBreakpoints();
            Assert.AreEqual(1, boundBreakpoints.Count);
            Assert.AreSame(mockBoundBreakpoint, boundBreakpoints[0]);
            mockBoundBreakpoint.Received().Enable(1);

            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void BindAndDisable()
        {
            MockBreakpoint(1);
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);
            pendingBreakpoint.Bind();
            var result = pendingBreakpoint.Enable(0);
            PENDING_BP_STATE_INFO[] state = new PENDING_BP_STATE_INFO[1];
            pendingBreakpoint.GetState(state);
            Assert.AreEqual(enum_PENDING_BP_STATE.PBPS_DISABLED, state[0].state);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void GetId()
        {
            int id = pendingBreakpoint.GetId();
            Assert.AreEqual(INVALID_ID, id);
            MockBreakpoint(1);
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);
            pendingBreakpoint.Bind();
            id = pendingBreakpoint.GetId();
            Assert.AreEqual(EXPECTED_ID, id);
        }

        [Test]
        public void GetBoundBreakpointById()
        {
            IBoundBreakpoint boundBreakpoint;
            bool result = pendingBreakpoint.GetBoundBreakpointById(
                BOUND_BREAKPOINT_ID, out boundBreakpoint);
            Assert.IsFalse(result);
            MockBreakpoint(1);
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);
            pendingBreakpoint.Bind();
            result = pendingBreakpoint.GetBoundBreakpointById(
                BOUND_BREAKPOINT_ID, out boundBreakpoint);
            Assert.IsTrue(result);
            Assert.AreEqual(BOUND_BREAKPOINT_ID, boundBreakpoint.GetId());
        }

        [Test]
        public void GetNumLocations()
        {
            int numLocations = 8;
            // Set a function breakpoint type and create a new pending breakpoint.
            SetBreakpointType(enum_BP_LOCATION_TYPE.BPLT_CODE_FUNC_OFFSET);
            pendingBreakpoint = debugPendingBreakpointFactory.Create(
                mockBreakpointManager, mockProgram, mockBreakpointRequest, mockTarget,
                mockMarshal);

            MockFunctionBreakpoint(numLocations);
            MockFunctionPosition(TEST_FUNCTION_NAME);

            var result = pendingBreakpoint.Bind();
            IDebugErrorBreakpoint2 breakpointError = GetBreakpointError();

            Assert.AreEqual(null, breakpointError);
            Assert.AreEqual(VSConstants.S_OK, result);
            Assert.AreEqual(numLocations, pendingBreakpoint.GetNumLocations());
        }

        [Test]
        public void GetNumLocationsNoBreakpoint()
        {
            Assert.AreEqual(0, pendingBreakpoint.GetNumLocations());
        }

        [Test]
        public void UpdateLocationsNewLocationsAdded()
        {
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);
            MockBreakpoint(1);
            pendingBreakpoint.Bind();
            MockBreakpoint(3);

            pendingBreakpoint.UpdateLocations();

            mockBreakpointManager.Received(1).EmitBreakpointBoundEvent(
                pendingBreakpoint, Arg.Is<IEnumerable<IDebugBoundBreakpoint2>>(a => a.Count() == 2),
                Arg.Any<BoundBreakpointEnumFactory>());
            Assert.That(pendingBreakpoint.EnumBoundBreakpoints(out var boundBreakpoints),
                        Is.EqualTo(VSConstants.S_OK));
            Assert.That(boundBreakpoints.GetCount(out uint count), Is.EqualTo(VSConstants.S_OK));
            Assert.That(count, Is.EqualTo(3));

            // Test that locations are not updated and BreakpointBoundEvent is not emitted,
            // when locations state hasn't changed and UpdateLocations is executed.
            mockBreakpointManager.ClearReceivedCalls();
            pendingBreakpoint.UpdateLocations();
            mockBreakpointManager.DidNotReceive().EmitBreakpointBoundEvent(
                pendingBreakpoint, Arg.Any<IEnumerable<IDebugBoundBreakpoint2>>(),
                Arg.Any<BoundBreakpointEnumFactory>());
            Assert.That(pendingBreakpoint.EnumBoundBreakpoints(out boundBreakpoints),
                        Is.EqualTo(VSConstants.S_OK));
            Assert.That(boundBreakpoints.GetCount(out count), Is.EqualTo(VSConstants.S_OK));
            Assert.That(count, Is.EqualTo(3));
        }

        [Test]
        public void UpdateLocationsLocationsRemoved()
        {
            MockDocumentPosition(TEST_FILE_NAME, LINE_NUMBER, COLUMN_NUMBER);
            MockBreakpoint(0);
            pendingBreakpoint.Bind();
            MockBreakpoint(3);
            pendingBreakpoint.UpdateLocations();
            mockBreakpointManager.ClearReceivedCalls();
            MockBreakpoint(0);
            pendingBreakpoint.UpdateLocations();

            mockBreakpointManager.DidNotReceiveWithAnyArgs().EmitBreakpointBoundEvent(
                Arg.Any<IPendingBreakpoint>(), Arg.Any<IEnumerable<IDebugBoundBreakpoint2>>(),
                Arg.Any<BoundBreakpointEnumFactory>());
            mockBreakpointManager.Received(1)
                .ReportBreakpointError(Arg.Any<DebugBreakpointError>());
            Assert.That(pendingBreakpoint.EnumBoundBreakpoints(out var boundBreakpoints),
                        Is.EqualTo(VSConstants.S_OK));
            Assert.That(boundBreakpoints.GetCount(out uint count), Is.EqualTo(VSConstants.S_OK));
            Assert.That(count, Is.EqualTo(0));
        }

        // Update the mock breakpoint request to return a specific breakpoint type.  This must be
        // called before constructing the pending breakpoint.
        void SetBreakpointType(enum_BP_LOCATION_TYPE type)
        {
            requestInfo.dwFields |= enum_BPREQI_FIELDS.BPREQI_BPLOCATION;
            requestInfo.bpLocation.bpLocationType = (uint)type;
        }

        // Update the mock breakpoint request to return a condition.  This must be called before
        // constructing the pending breakpoint.
        void SetCondition(enum_BP_COND_STYLE conditionStyle, string conditionString)
        {
            requestInfo.dwFields |= enum_BPREQI_FIELDS.BPREQI_CONDITION;
            requestInfo.bpCondition = new BP_CONDITION
            {
                styleCondition = conditionStyle,
                bstrCondition = conditionString,
            };
        }

        // Update the mock breakpoint request to return a pass count.  This must be called before
        // constructing the pending breakpoint.
        void SetPassCount(enum_BP_PASSCOUNT_STYLE passCountStyle, uint passCount)
        {
            requestInfo.dwFields |= enum_BPREQI_FIELDS.BPREQI_PASSCOUNT;
            requestInfo.bpPassCount = new BP_PASSCOUNT
            {
                stylePassCount = passCountStyle,
                dwPassCount = passCount,
            };
        }

        IDebugErrorBreakpoint2 GetBreakpointError()
        {
            IEnumDebugErrorBreakpoints2 errorBreakpointsEnum;
            pendingBreakpoint.EnumErrorBreakpoints(enum_BP_ERROR_TYPE.BPET_ALL,
                out errorBreakpointsEnum);
            uint numErrors;
            Assert.AreEqual(VSConstants.S_OK, errorBreakpointsEnum.GetCount(out numErrors));

            if (numErrors == 0)
            {
                return null;
            }
            IDebugErrorBreakpoint2[] breakpointErrors = new IDebugErrorBreakpoint2[1];
            uint fetchedIndex = 0;
            errorBreakpointsEnum.Next(1, breakpointErrors, ref fetchedIndex);
            return breakpointErrors[0];
        }

        string GetBreakpointErrorMessage(IDebugErrorBreakpoint2 breakpointError)
        {
            IDebugErrorBreakpointResolution2 errorBreakpointResolution;
            Assert.NotNull(breakpointError);
            Assert.AreEqual(VSConstants.S_OK,
                breakpointError.GetBreakpointResolution(out errorBreakpointResolution),
                "Unable to verify breakpoint error message. Failed to get breakpoint error " +
                "resolution.");
            BP_ERROR_RESOLUTION_INFO[] errorResolutionInfo = new BP_ERROR_RESOLUTION_INFO[1];
            Assert.AreEqual(VSConstants.S_OK,
                errorBreakpointResolution.GetResolutionInfo(enum_BPERESI_FIELDS.BPERESI_MESSAGE,
                    errorResolutionInfo),
                "Unable to verify breakpoint error message. Failed to get breakpoint error " +
                "resolution info.");
            return errorResolutionInfo[0].bstrMessage;
        }

        List<IDebugBoundBreakpoint2> GetBoundBreakpoints()
        {
            IEnumDebugBoundBreakpoints2 boundBreakpointsEnum;
            pendingBreakpoint.EnumBoundBreakpoints(out boundBreakpointsEnum);

            if (boundBreakpointsEnum  == null)
            {
                return new List<IDebugBoundBreakpoint2>();
            }

            uint numBreakpoints;
            Assert.AreEqual(VSConstants.S_OK, boundBreakpointsEnum.GetCount(out numBreakpoints));
            if (numBreakpoints == 0)
            {
                return new List<IDebugBoundBreakpoint2>();
            }
            uint count, actualCount = 0;
            boundBreakpointsEnum.GetCount(out count);
            IDebugBoundBreakpoint2[] boundBreakpoints = new IDebugBoundBreakpoint2[count];
            boundBreakpointsEnum.Next(count, boundBreakpoints, ref actualCount);
            return boundBreakpoints.ToList<IDebugBoundBreakpoint2>();
        }

        // Create default mocks, and return values for the document position.
        void MockDocumentPosition(string filename, uint lineNumber, uint columnNumber)
        {
            var mockDocumentPosition = Substitute.For<IDebugDocumentPosition2>();
            string value;
            mockDocumentPosition.GetFileName(out value).Returns(x =>
            {
                if (filename != null)
                {
                    x[0] = filename;
                    return 0;
                }
                return 1;
            });
            mockDocumentPosition.GetRange(Arg.Any<TEXT_POSITION[]>(), null).Returns(x =>
            {
                TEXT_POSITION[] startTextPositions = (TEXT_POSITION[])x[0];
                if (startTextPositions != null && startTextPositions.Length >= 1)
                {
                    TEXT_POSITION startTextPosition = new TEXT_POSITION();
                    startTextPosition.dwColumn = columnNumber;
                    startTextPosition.dwLine = lineNumber;
                    startTextPositions[0] = startTextPosition;
                }
                return 0;
            });
            mockMarshal.GetDocumentPositionFromIntPtr(Arg.Any<IntPtr>()).Returns(
                mockDocumentPosition);
        }

        // Create default mocks, and return values for the function position.
        void MockFunctionPosition(string functionName)
        {
            var mockFunctionPosition = Substitute.For<IDebugFunctionPosition2>();
            string value;
            mockFunctionPosition.GetFunctionName(out value).Returns(x =>
            {
                if (functionName != null)
                {
                    x[0] = functionName;
                    return 0;
                }
                return 1;
            });
            mockMarshal.GetFunctionPositionFromIntPtr(Arg.Any<IntPtr>()).Returns(
                mockFunctionPosition);
        }

        // Create default mocks, and return values for the code context position.
        void MockCodeContext(ulong address)
        {
            // Create mock code context with the specified address.
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            System.Action < CONTEXT_INFO[] > setContextInfo = infos =>
            {
                infos[0].bstrAddress = "0x" + address.ToString("x16");
                infos[0].dwFields = enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS;
            };
            ((IDebugMemoryContext2)mockCodeContext)
                .GetInfo(Arg.Any<enum_CONTEXT_INFO_FIELDS>(), Arg.Do(setContextInfo))
                .Returns(VSConstants.S_OK);
            mockCodeContext
                .GetInfo(Arg.Any<enum_CONTEXT_INFO_FIELDS>(), Arg.Do(setContextInfo))
                .Returns(VSConstants.S_OK);

            mockMarshal.GetCodeContextFromIntPtr(Arg.Any<IntPtr>()).Returns(
                mockCodeContext);
        }

        // Create return values for the address position.
        void MockCodeAddress(string address)
        {
            // Code address holds the address as a string
            mockMarshal.GetStringFromIntPtr(Arg.Any<IntPtr>()).Returns(address);
        }

        List<SbBreakpointLocation> CreateMockBreakpointLocations(
            int numBreakpointLocations)
        {
            List<SbBreakpointLocation> breakpointLocations =
                new List<SbBreakpointLocation>(numBreakpointLocations);
            for (int i = 0; i < numBreakpointLocations; i++)
            {
                var mockBreakpointLocation = Substitute.For<SbBreakpointLocation>();
                mockBreakpointLocation.GetId().Returns(i);
                breakpointLocations.Add(mockBreakpointLocation);
            }
            return breakpointLocations;
        }

        // Create default mocks, and return values for the lldb breakpoint and breakpoint locations.
        // numBreakpointLocations specifies how many mock breakpoint locations to return.
        List<SbBreakpointLocation> MockBreakpoint(int numBreakpointLocations)
        {
            List<SbBreakpointLocation> breakpointLocations =
                CreateMockBreakpointLocations(numBreakpointLocations);
            MockBreakpoint(breakpointLocations);
            return breakpointLocations;
        }

        // Create default mocks, and return values for the lldb breakpoint and breakpoint locations.
        // breakpointLocations is a list of mock breakpoint locations that will be returned by the
        // mock lldb breakpoint.
        void MockBreakpoint(List<SbBreakpointLocation> breakpointLocations)
        {
            for (uint i = 0; i < breakpointLocations.Count; i++)
            {
                mockLldbBreakpoint.GetLocationAtIndex(i).Returns(breakpointLocations[(int)i]);
            }
            mockLldbBreakpoint.GetNumLocations().Returns((uint)breakpointLocations.Count);
            mockTarget.BreakpointCreateByLocation(TEST_FILE_NAME, LINE_NUMBER + 1).Returns(
                mockLldbBreakpoint);
        }

        // Create default mocks, and return values for the lldb breakpoint and breakpoint locations
        // for a function breakpoint.  numBreakpointLocations specifies how many mock breakpoint
        // locations to return.
        void MockFunctionBreakpoint(int numBreakpointLocations)
        {
            List<SbBreakpointLocation> breakpointLocations =
                CreateMockBreakpointLocations(numBreakpointLocations);
            MockFunctionBreakpoint(breakpointLocations);
        }

        // Create default mocks, and return values for the lldb breakpoint and breakpoint locations
        // for a function breakpoint.  breakpointLocations is a list of mock breakpoint locations
        // that will be returned by the mock lldb breakpoint.
        void MockFunctionBreakpoint(List<SbBreakpointLocation> breakpointLocations)
        {
            for (uint i = 0; i < breakpointLocations.Count; i++)
            {
                mockLldbBreakpoint.GetLocationAtIndex(i).Returns(breakpointLocations[(int)i]);
            }
            mockLldbBreakpoint.GetNumLocations().Returns((uint)breakpointLocations.Count);
            mockTarget.BreakpointCreateByName(TEST_FUNCTION_NAME).Returns(mockLldbBreakpoint);
        }

        // Create default mocks, and return values for the lldb breakpoint and breakpoint locations
        // for an assembly breakpoint.  numBreakpointLocations specifies how many mock breakpoint
        // locations to return.
        void MockAssemblyBreakpoint(int numBreakpointLocations)
        {
            List<SbBreakpointLocation> breakpointLocations =
                CreateMockBreakpointLocations(numBreakpointLocations);
            MockAssemblyBreakpoint(breakpointLocations);
        }

        // Create default mocks, and return values for the lldb breakpoint and breakpoint locations
        // for an assembly breakpoint. breakpointLocations is a list of mock breakpoint locations
        // that will be returned by the mock lldb breakpoint.
        void MockAssemblyBreakpoint(List<SbBreakpointLocation> breakpointLocations)
        {
            for (uint i = 0; i < breakpointLocations.Count; i++)
            {
                mockLldbBreakpoint.GetLocationAtIndex(i).Returns(breakpointLocations[(int)i]);
            }
            mockLldbBreakpoint.GetNumLocations().Returns((uint)breakpointLocations.Count);
            mockTarget.BreakpointCreateByAddress(TEST_ADDRESS).Returns(mockLldbBreakpoint);
        }

        int BuildBreakpointRequestInfo(enum_BPREQI_FIELDS fields,
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
    }
}