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
using Marshal = System.Runtime.InteropServices.Marshal;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugBreakpointResolutionTest
    {
        BP_RESOLUTION_INFO expectedResolutionInfo;

        IDebugCodeContext2 mockCodeContext;
        IDebugProgram2 mockProgram;
        IDebugBreakpointResolution2 breakpointResolution;

        [SetUp]
        public void SetUp()
        {
            mockCodeContext = Substitute.For<IDebugCodeContext2>();
            mockProgram = Substitute.For<IDebugProgram2>();
            expectedResolutionInfo.dwFields = enum_BPRESI_FIELDS.BPRESI_BPRESLOCATION |
                                      enum_BPRESI_FIELDS.BPRESI_PROGRAM;
            expectedResolutionInfo.bpResLocation.bpType = (uint)enum_BP_TYPE.BPT_CODE;
            expectedResolutionInfo.bpResLocation.unionmember1 =
                Marshal.GetComInterfaceForObject(
                    mockCodeContext, typeof(IDebugCodeContext2));
            expectedResolutionInfo.pProgram = mockProgram;
            breakpointResolution = new DebugBreakpointResolution.Factory().Create(
                mockCodeContext, mockProgram);
        }

        [Test]
        public void GetBreakpointType()
        {
            enum_BP_TYPE[] breakpointTypes = new enum_BP_TYPE[1];
            Assert.AreEqual(VSConstants.S_OK,
                breakpointResolution.GetBreakpointType(breakpointTypes));
            Assert.AreEqual(enum_BP_TYPE.BPT_CODE, breakpointTypes[0]);
        }

        [Test]
        public void GetResolutionInfo()
        {
            enum_BPRESI_FIELDS fields = enum_BPRESI_FIELDS.BPRESI_ALLFIELDS;
            BP_RESOLUTION_INFO[] resolutionInfos = new BP_RESOLUTION_INFO[1];
            Assert.AreEqual(VSConstants.S_OK,
                breakpointResolution.GetResolutionInfo(fields, resolutionInfos));
            Assert.AreEqual(expectedResolutionInfo, resolutionInfos[0]);
        }
    }
}