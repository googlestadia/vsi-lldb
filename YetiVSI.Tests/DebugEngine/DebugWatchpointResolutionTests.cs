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
    public class DebugWatchpointResolutionTests
    {
        const string EXPRESSION = "";
        IDebugProgram2 mockProgram;
        DebugWatchpointResolution resolution;

        [SetUp]
        public void SetUp()
        {
            mockProgram = Substitute.For<IDebugProgram2>();

            resolution = new DebugWatchpointResolution(EXPRESSION, mockProgram);
        }

        [Test]
        public void GetBreakpointType()
        {
            enum_BP_TYPE[] types = new enum_BP_TYPE[1];
            Assert.AreEqual(VSConstants.S_OK, resolution.GetBreakpointType(types));
            Assert.AreEqual(enum_BP_TYPE.BPT_DATA, types[0]);
        }

        [Test]
        public void GetResolutionInfo()
        {
            var fields = enum_BPRESI_FIELDS.BPRESI_ALLFIELDS;
            var expectedInfo = new BP_RESOLUTION_INFO
            {
                dwFields = enum_BPRESI_FIELDS.BPRESI_BPRESLOCATION |
                           enum_BPRESI_FIELDS.BPRESI_PROGRAM,
                bpResLocation = new BP_RESOLUTION_LOCATION
                {
                    unionmember1 = System.Runtime.InteropServices.Marshal.StringToBSTR(EXPRESSION)
                },
                pProgram = mockProgram
            };
            var infos = new BP_RESOLUTION_INFO[1];
            Assert.AreEqual(VSConstants.S_OK, resolution.GetResolutionInfo(fields, infos));
            Assert.AreEqual(EXPRESSION, System.Runtime.InteropServices.Marshal.PtrToStringBSTR(
                infos[0].bpResLocation.unionmember1));
            Assert.AreEqual(mockProgram, infos[0].pProgram);
        }
    }
}
