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

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// This class represents the information that describes a data breakpoint.
    /// </summary>
    public class DebugWatchpointResolution : IDebugBreakpointResolution2
    {
        public class Factory
        {
            public virtual IDebugBreakpointResolution2 Create(string expression,
                                                              IDebugProgram2 program)
            {
                return new DebugWatchpointResolution(expression, program);
            }
        }

        readonly BP_RESOLUTION_LOCATION _location;
        readonly IDebugProgram2 _program;

        public DebugWatchpointResolution(string expression, IDebugProgram2 program)
        {
            _location.unionmember1 =
                System.Runtime.InteropServices.Marshal.StringToBSTR(expression);
            _location.bpType = (uint)enum_BP_TYPE.BPT_DATA;
            _program = program;
        }

#region IDebugBreakpointResolution2 functions

        public int GetBreakpointType(enum_BP_TYPE[] pBpType)
        {
            pBpType[0] = enum_BP_TYPE.BPT_DATA;
            return VSConstants.S_OK;
        }

        public int GetResolutionInfo(enum_BPRESI_FIELDS dwFields,
                                     BP_RESOLUTION_INFO[] pBpResolutionInfo)
        {
            if ((dwFields & enum_BPRESI_FIELDS.BPRESI_BPRESLOCATION) != 0)
            {
                pBpResolutionInfo[0].bpResLocation = _location;
                pBpResolutionInfo[0].dwFields |= enum_BPRESI_FIELDS.BPRESI_BPRESLOCATION;
            }
            if ((dwFields & enum_BPRESI_FIELDS.BPRESI_PROGRAM) != 0)
            {
                pBpResolutionInfo[0].pProgram = _program;
                pBpResolutionInfo[0].dwFields |= enum_BPRESI_FIELDS.BPRESI_PROGRAM;
            }
            return VSConstants.S_OK;
        }

#endregion
    }
}
