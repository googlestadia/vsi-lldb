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
using YetiCommon.CastleAspects;

namespace YetiVSI.DebugEngine
{
    public interface IDebugBreakpointError : IDebugErrorBreakpoint2,
                                             IDebugErrorBreakpointResolution2
    {
    }

    // This represents an error that occurred when creating / binding a breakpoint.
    public class DebugBreakpointError : SimpleDecoratorSelf<IDebugBreakpointError>,
                                        IDebugBreakpointError
    {
        readonly IDebugPendingBreakpoint2 _pendingBreakpoint;
        readonly enum_BP_ERROR_TYPE _errorType;
        readonly string _errorMessage;

        public DebugBreakpointError(IDebugPendingBreakpoint2 pendingBreakpoint,
                                    enum_BP_ERROR_TYPE errorType, string errorMessage)
        {
            _pendingBreakpoint = pendingBreakpoint;
            _errorType = errorType;
            _errorMessage = errorMessage;
        }

#region IDebugErrorBreakpoint2 functions
        public int GetBreakpointResolution(out IDebugErrorBreakpointResolution2 errorResolution)
        {
            errorResolution = Self;
            return VSConstants.S_OK;
        }

        public int GetPendingBreakpoint(out IDebugPendingBreakpoint2 pendingBreakpoint)
        {
            pendingBreakpoint = _pendingBreakpoint;
            return VSConstants.S_OK;
        }
#endregion

#region IDebugErrorBreakpointResolution2
        public int GetBreakpointType(enum_BP_TYPE[] breakpointType) => VSConstants.E_NOTIMPL;

        public int GetResolutionInfo(enum_BPERESI_FIELDS fields,
                                     BP_ERROR_RESOLUTION_INFO[] errorResolutionInfo)
        {
            if ((fields & enum_BPERESI_FIELDS.BPERESI_MESSAGE) != 0)
            {
                errorResolutionInfo[0].dwFields |= enum_BPERESI_FIELDS.BPERESI_MESSAGE;
                errorResolutionInfo[0].bstrMessage = _errorMessage;
            }

            if ((fields & enum_BPERESI_FIELDS.BPERESI_TYPE) != 0)
            {
                errorResolutionInfo[0].dwFields |= enum_BPERESI_FIELDS.BPERESI_TYPE;
                errorResolutionInfo[0].dwType = _errorType;
            }
            return VSConstants.S_OK;
        }
#endregion
    }
}
