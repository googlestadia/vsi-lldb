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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugBreakpointErrorTest
    {
        private const enum_BP_ERROR_TYPE errorType = enum_BP_ERROR_TYPE.BPET_GENERAL_ERROR;
        private const string errorMessage = "error message";
        IDebugPendingBreakpoint2 mockPendingBreakpoint;

        private DebugBreakpointError breakpointError;

        [SetUp]
        public void SetUp()
        {
            mockPendingBreakpoint = Substitute.For<IDebugPendingBreakpoint2>();
            breakpointError = new DebugBreakpointError(mockPendingBreakpoint, errorType,
                errorMessage);
        }

        [Test]
        public void GetBreakpointResolution()
        {
            IDebugErrorBreakpointResolution2 errorBreakpointResolution;
            Assert.AreEqual(VSConstants.S_OK, breakpointError.GetBreakpointResolution(
                out errorBreakpointResolution));
            Assert.AreEqual(breakpointError, errorBreakpointResolution);
        }

        [Test]
        public void GetPendingBreakpoint()
        {
            IDebugPendingBreakpoint2 pendingBreakpoint;
            Assert.AreEqual(VSConstants.S_OK, breakpointError.GetPendingBreakpoint(
                out pendingBreakpoint));
            Assert.AreEqual(mockPendingBreakpoint, pendingBreakpoint);
        }

        [Test]
        public void GetResolutionInfo()
        {
            var fields = enum_BPERESI_FIELDS.BPERESI_ALLFIELDS;
            var errorResolutionInfo = new BP_ERROR_RESOLUTION_INFO[1];
            Assert.AreEqual(VSConstants.S_OK, breakpointError.GetResolutionInfo(fields,
                errorResolutionInfo));
            Assert.AreEqual(enum_BPERESI_FIELDS.BPERESI_MESSAGE | enum_BPERESI_FIELDS.BPERESI_TYPE,
                errorResolutionInfo[0].dwFields);
            Assert.AreEqual(errorType, errorResolutionInfo[0].dwType);
            Assert.AreEqual(errorMessage, errorResolutionInfo[0].bstrMessage);
        }
    }
}
