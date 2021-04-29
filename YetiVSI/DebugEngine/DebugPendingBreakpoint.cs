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
using DebuggerGrpcClient.Interfaces;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using YetiCommon.CastleAspects;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine
{
    public interface IPendingBreakpoint : IDebugPendingBreakpoint2
    {
        /// <summary>
        ///  Get the pending breakpoint's ID.
        /// </summary>
        int GetId();

        /// <summary>
        /// Get the IDebugBoundBreakpoint with |id| in |boundBreakpoint|. Return false if such
        /// breakpoint doesn't exist.
        /// </summary>
        bool GetBoundBreakpointById(int id, out IBoundBreakpoint boundBreakpoint);

        /// <summary>
        /// Get the number of bound breakpoints for the pending breakpoint.
        /// </summary>
        uint GetNumLocations();

        /// <summary>
        /// Update the list of bound breakpoints.
        /// </summary>
        void UpdateLocations();
    }

    /// <summary>
    /// This class represents a pending breakpoint which is an abstract representation of a
    /// breakpoint before it is bound. When a user creates a new breakpoint, the pending breakpoint
    /// is created and is later bound. The bound breakpoints become children of the pending
    /// breakpoint.
    /// </summary>
    public class DebugPendingBreakpoint : SimpleDecoratorSelf<IPendingBreakpoint>,
                                          IPendingBreakpoint
    {
        public class Factory
        {
            readonly JoinableTaskContext _taskContext;
            readonly DebugBoundBreakpoint.Factory _debugBoundBreakpointFactory;
            readonly BreakpointErrorEnumFactory _breakpointErrorEnumFactory;
            readonly BoundBreakpointEnumFactory _breakpointBoundEnumFactory;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            public Factory(JoinableTaskContext taskContext,
                           DebugBoundBreakpoint.Factory debugBoundBreakpointFactory,
                           BreakpointErrorEnumFactory breakpointErrorEnumFactory,
                           BoundBreakpointEnumFactory breakpointBoundEnumFactory)
            {
                _taskContext = taskContext;
                _debugBoundBreakpointFactory = debugBoundBreakpointFactory;
                _breakpointErrorEnumFactory = breakpointErrorEnumFactory;
                _breakpointBoundEnumFactory = breakpointBoundEnumFactory;
            }

            public virtual IPendingBreakpoint Create(IBreakpointManager breakpointManager,
                                                     IDebugProgram2 program,
                                                     IDebugBreakpointRequest2 request,
                                                     RemoteTarget target)
            {
                _taskContext.ThrowIfNotOnMainThread();
                return Create(breakpointManager, program, request, target, new Marshal());
            }

            public virtual IPendingBreakpoint Create(IBreakpointManager breakpointManager,
                                                     IDebugProgram2 program,
                                                     IDebugBreakpointRequest2 request,
                                                     RemoteTarget target, Marshal marshal)
            {
                _taskContext.ThrowIfNotOnMainThread();
                return new DebugPendingBreakpoint(_taskContext, _debugBoundBreakpointFactory,
                                                  _breakpointErrorEnumFactory,
                                                  _breakpointBoundEnumFactory, breakpointManager,
                                                  program, request, target, marshal);
            }
        }

        const string _breakpointNotSupported = "Breakpoint type is not supported.";
        const string _breakpointNotSet = "Unable to bind breakpoint.";
        const string _breakpointLocationNotSet =
            "Unable to find a valid address to bind breakpoint.";
        const string _noSourceFilename = "Unable to retrieve source code filename.";
        const string _noSourceLineNumber = "Unable to retrieve source code line number.";
        const string _noFunctionName = "Unable to find function name.";
        const string _noCodeContext = "Unable to retrieve code context.";
        const string _noCodeAddress = "Unable to retrieve code address.";
        const string _positionNotAvailable = "Unable to set breakpoint for the specified position.";
        const string _noFunctionFound = "Unable to retrieve function information.";

        // Matching the function name and the offset:
        // {function, , [module - currently not supported]} [+<line offset from start of method>]
        // More in Set function breakpoints section:
        // https://github.com/MicrosoftDocs/visualstudio-docs/blob/master/docs/debugger/using-breakpoints.md
        static readonly Regex _funcOffsetRegex =
            new Regex(@"^\s*{\s*" + // the beginning of the expression: " { "
                      @"(?<name>[a-zA-Z_][a-zA-Z0-9_:]*)" + // catching function name
                      @"\s*,\s*,\s*}\s*\+?\s*" +            // ", , } + "
                      @"(?<offset>\d+)?" +                  // catching offset
                      @"\s*$");                             // spaces in the end

        readonly DebugBoundBreakpoint.Factory _debugBoundBreakpointFactory;
        readonly BreakpointErrorEnumFactory _breakpointErrorEnumFactory;
        readonly BoundBreakpointEnumFactory _breakpointBoundEnumFactory;

        readonly Marshal _marshal;
        readonly IDebugBreakpointRequest2 _request;
        readonly BP_REQUEST_INFO _requestInfo;
        readonly RemoteTarget _target;
        RemoteBreakpoint _lldbBreakpoint;
        readonly IBreakpointManager _breakpointManager;
        readonly IDebugProgram2 _program;

        readonly Dictionary<int, IBoundBreakpoint> _boundBreakpoints;
        DebugBreakpointError _breakpointError;
        bool _enabled;
        bool _deleted;

        DebugPendingBreakpoint(JoinableTaskContext taskContext,
                               DebugBoundBreakpoint.Factory debugBoundBreakpointFactory,
                               BreakpointErrorEnumFactory breakpointErrorEnumFactory,
                               BoundBreakpointEnumFactory breakpointBoundEnumFactory,
                               IBreakpointManager breakpointManager, IDebugProgram2 program,
                               IDebugBreakpointRequest2 request, RemoteTarget target,
                               Marshal marshal)
        {
            taskContext.ThrowIfNotOnMainThread();

            _debugBoundBreakpointFactory = debugBoundBreakpointFactory;
            _breakpointErrorEnumFactory = breakpointErrorEnumFactory;
            _breakpointBoundEnumFactory = breakpointBoundEnumFactory;
            _breakpointManager = breakpointManager;
            _program = program;
            _request = request;
            _target = target;
            _marshal = marshal;

            _boundBreakpoints = new Dictionary<int, IBoundBreakpoint>();

            BP_REQUEST_INFO[] breakpointRequestInfo = new BP_REQUEST_INFO[1];
            request.GetRequestInfo(enum_BPREQI_FIELDS.BPREQI_BPLOCATION |
                                       enum_BPREQI_FIELDS.BPREQI_CONDITION |
                                       enum_BPREQI_FIELDS.BPREQI_PASSCOUNT |
                                       enum_BPREQI_FIELDS.BPREQI_LANGUAGE,
                                   breakpointRequestInfo);
            _requestInfo = breakpointRequestInfo[0];

            _enabled = false;
            _deleted = false;
        }

        // Verifies the type of breakpoint that is being created is supported.
        bool IsSupportedType()
        {
            // LLDB only supports conditional breakpoints with WHEN_TRUE condition style.
            // TODO: Add support for enum_BP_COND_STYLE.BP_COND_WHEN_CHANGED.
            if ((_requestInfo.dwFields & enum_BPREQI_FIELDS.BPREQI_CONDITION) != 0 &&
                _requestInfo.bpCondition.styleCondition == enum_BP_COND_STYLE.BP_COND_WHEN_CHANGED)
            {
                return false;
            }

            switch ((enum_BP_LOCATION_TYPE)_requestInfo.bpLocation.bpLocationType)
            {
            case enum_BP_LOCATION_TYPE.BPLT_CODE_FILE_LINE:
            // fall-through
            case enum_BP_LOCATION_TYPE.BPLT_CODE_FUNC_OFFSET:
            // fall-through
            case enum_BP_LOCATION_TYPE.BPLT_CODE_CONTEXT:
            // fall-through
            case enum_BP_LOCATION_TYPE.BPLT_CODE_ADDRESS:
                return true;
            default:
                return false;
            }
        }

        // Caches the error, and notifies the DebugEngine that an error occurred with the pending
        // breakpoint.  Calling this multiple times will overwrite the previously cached error.
        void SetError(enum_BP_ERROR_TYPE errorType, string errorMessage)
        {
            _breakpointError = new DebugBreakpointError(Self, errorType, errorMessage);
            _breakpointManager.ReportBreakpointError(_breakpointError);
        }

#region IPendingBreakpoint functions

        // Gets the ID from SbBreakpoint. LLDB assigns IDs starting from 0. Returns -1 if the
        // pending breakpoint hasn't been bound.
        public int GetId()
        {
            if (_lldbBreakpoint == null)
            {
                return -1;
            }

            return _lldbBreakpoint.GetId();
        }

        public bool GetBoundBreakpointById(int id, out IBoundBreakpoint boundBreakpoint) =>
            _boundBreakpoints.TryGetValue(id, out boundBreakpoint);

        #endregion

        #region IDebugPendingBreakpoint2 functions
        public int Bind()
        {
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }

            switch ((enum_BP_LOCATION_TYPE)_requestInfo.bpLocation.bpLocationType)
            {
            case enum_BP_LOCATION_TYPE.BPLT_CODE_FILE_LINE:
                IDebugDocumentPosition2 documentPosition =
                    _marshal.GetDocumentPositionFromIntPtr(_requestInfo.bpLocation.unionmember2);
                if (documentPosition.GetFileName(out string fileName) != 0)
                {
                    SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, _noSourceFilename);
                    return VSConstants.S_FALSE;
                }

                TEXT_POSITION[] startPosition = new TEXT_POSITION[1];
                // TODO: Check if we need the end position or not.  This might
                // matter when setting a breakpoint on a comment.  It's possible LLDB will just
                // handle this for us and we don't need to worry about the end position.
                if (documentPosition.GetRange(startPosition, null) != VSConstants.S_OK)
                {
                    SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, _noSourceLineNumber);
                    return VSConstants.S_FALSE;
                }

                // Visual Studio uses a zero based index for line numbers, where LLDB uses a one
                // based index for line numbers.  We need to add one to the line number here to
                // convert visual studio line numbers to LLDB line numbers.
                _lldbBreakpoint =
                    _target.BreakpointCreateByLocation(fileName, startPosition[0].dwLine + 1);
                break;
            case enum_BP_LOCATION_TYPE.BPLT_CODE_FUNC_OFFSET:
                IDebugFunctionPosition2 functionPosition =
                    _marshal.GetFunctionPositionFromIntPtr(_requestInfo.bpLocation.unionmember2);
                uint offset = 0;
                if (functionPosition.GetFunctionName(out string functionName) != 0)
                {
                    SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, _noFunctionName);
                    return VSConstants.S_FALSE;
                }
                MatchCollection matches = _funcOffsetRegex.Matches(functionName);
                if (matches.Count == 1)
                {
                    functionName = matches[0].Groups["name"].Value;
                    string offsetString = matches[0].Groups["offset"].Value;
                    if (!string.IsNullOrWhiteSpace(offsetString))
                    {
                        offset = uint.Parse(offsetString);
                    }
                }

                if (offset > 0)
                {
                    BreakpointErrorPair breakpointErrorPair =
                        _target.CreateFunctionOffsetBreakpoint(functionName, offset);
                    _lldbBreakpoint = breakpointErrorPair.breakpoint;
                    if (_lldbBreakpoint == null)
                    {
                        switch (breakpointErrorPair.error)
                        {
                        case BreakpointError.NoFunctionFound:
                            SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, _noFunctionFound);
                            break;
                        case BreakpointError.NoFunctionLocation:
                            SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING,
                                     _breakpointLocationNotSet);
                            break;
                        case BreakpointError.PositionNotAvailable:
                            SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING,
                                     _positionNotAvailable);
                            break;
                        default:
                            SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING,
                                     _positionNotAvailable);
                            break;
                        }
                        return VSConstants.S_FALSE;
                    }
                }
                else
                {
                    _lldbBreakpoint = _target.BreakpointCreateByName(functionName);
                }
                break;
            case enum_BP_LOCATION_TYPE.BPLT_CODE_CONTEXT:
                IDebugCodeContext2 codeContext =
                    _marshal.GetCodeContextFromIntPtr(_requestInfo.bpLocation.unionmember1);
                if (codeContext == null)
                {
                    SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, _noCodeContext);
                    return VSConstants.S_FALSE;
                }
                ulong address = codeContext.GetAddress();
                _lldbBreakpoint = _target.BreakpointCreateByAddress(address);
                break;
            case enum_BP_LOCATION_TYPE.BPLT_CODE_ADDRESS:
                string strAddress =
                    _marshal.GetStringFromIntPtr(_requestInfo.bpLocation.unionmember4);
                if (strAddress == null)
                {
                    SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, _noCodeAddress);
                    return VSConstants.S_FALSE;
                }
                ulong address2 = Convert.ToUInt64(strAddress, 16);
                _lldbBreakpoint = _target.BreakpointCreateByAddress(address2);
                break;
            default:
                SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, _breakpointNotSupported);
                return VSConstants.S_FALSE;
            }

            if (_lldbBreakpoint == null)
            {
                SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, _breakpointNotSet);
                return VSConstants.S_FALSE;
            }

            UpdateLocations();
            _breakpointManager.RegisterPendingBreakpoint(Self);

            return _boundBreakpoints.Count == 0
                ? VSConstants.S_FALSE
                : VSConstants.S_OK;
        }

        public void UpdateLocations()
        {
            var remoteLocations = new Dictionary<int, SbBreakpointLocation>();
            uint lldbBreakpointLocationNum = _lldbBreakpoint.GetNumLocations();
            for (uint i = 0; i < lldbBreakpointLocationNum; i++)
            {
                SbBreakpointLocation breakpointLocation = _lldbBreakpoint.GetLocationAtIndex(i);
                if (breakpointLocation == null)
                {
                    Trace.WriteLine("Failed to get breakpoint location.");
                    continue;
                }

                remoteLocations.Add(breakpointLocation.GetId(), breakpointLocation);
            }

            foreach (int boundBreakpointId in _boundBreakpoints.Keys.ToList())
            {
                if (!remoteLocations.ContainsKey(boundBreakpointId))
                {
                    _boundBreakpoints[boundBreakpointId].Delete();
                    _boundBreakpoints.Remove(boundBreakpointId);
                }
            }

            List<IDebugBoundBreakpoint2> newLocations = new List<IDebugBoundBreakpoint2>();

            foreach (SbBreakpointLocation remoteLocation in remoteLocations.Values)
            {
                if (!_boundBreakpoints.ContainsKey(remoteLocation.GetId()))
                {
                    // Make sure the newly created bound breakpoints have the same
                    // enabled state as the pending breakpoint.
                    IBoundBreakpoint boundBreakpoint =
                        _debugBoundBreakpointFactory.Create(Self, remoteLocation, _program,
                                                            _requestInfo.guidLanguage);
                    boundBreakpoint.Enable(Convert.ToInt32(_enabled));
                    if ((_requestInfo.dwFields & enum_BPREQI_FIELDS.BPREQI_CONDITION) != 0)
                    {
                        boundBreakpoint.SetCondition(_requestInfo.bpCondition);
                    }

                    if ((_requestInfo.dwFields & enum_BPREQI_FIELDS.BPREQI_PASSCOUNT) != 0)
                    {
                        boundBreakpoint.SetPassCount(_requestInfo.bpPassCount);
                    }

                    _boundBreakpoints.Add(remoteLocation.GetId(), boundBreakpoint);
                    newLocations.Add(boundBreakpoint);
                }
            }

            if (_boundBreakpoints.Count == 0)
            {
                SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, _breakpointLocationNotSet);
            }
            else
            {
                _breakpointError = null;
            }

            if (newLocations.Any())
            {
                _breakpointManager.EmitBreakpointBoundEvent(
                    Self, newLocations, _breakpointBoundEnumFactory);
            }
        }

        public int CanBind(out IEnumDebugErrorBreakpoints2 errorBreakpointsEnum)
        {
            errorBreakpointsEnum = null;
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }

            // Check the breakpoint type, and make sure it's supported.
            if (IsSupportedType())
            {
                return VSConstants.S_OK;
            }

            var breakpointErrors = new IDebugErrorBreakpoint2[1];
            breakpointErrors[0] = new DebugBreakpointError(
                Self, enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, _breakpointNotSupported);
            errorBreakpointsEnum = _breakpointErrorEnumFactory.Create(breakpointErrors);
            return VSConstants.S_FALSE;
        }

        public int Delete()
        {
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }

            _deleted = true;
            if (_lldbBreakpoint != null)
            {
                _breakpointManager.RemovePendingBreakpoint(Self);
                _target.BreakpointDelete(_lldbBreakpoint.GetId());
                _lldbBreakpoint = null;
            }
            foreach (IBoundBreakpoint boundBreakpoint in _boundBreakpoints.Values)
            {
                boundBreakpoint.Delete();
            }
            _boundBreakpoints.Clear();
            return VSConstants.S_OK;
        }

        public int Enable(int enable)
        {
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }

            _enabled = Convert.ToBoolean(enable);

            foreach (IBoundBreakpoint boundBreakpointsValue in _boundBreakpoints.Values)
            {
                boundBreakpointsValue.Enable(enable);
            }

            return VSConstants.S_OK;
        }

        public int EnumBoundBreakpoints(out IEnumDebugBoundBreakpoints2 boundBreakpointsEnum)
        {
            boundBreakpointsEnum = null;
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }

            IBoundBreakpoint[] boundBreakpoints = _boundBreakpoints.Values.ToArray();
            boundBreakpointsEnum = _breakpointBoundEnumFactory.Create(boundBreakpoints);
            return VSConstants.S_OK;
        }

        public int EnumErrorBreakpoints(enum_BP_ERROR_TYPE breakpointErrorType,
                                        out IEnumDebugErrorBreakpoints2 errorBreakpointsEnum)
        {
            errorBreakpointsEnum = null;
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }

            IDebugErrorBreakpoint2[] breakpointErrors;
            if (_breakpointError == null)
            {
                breakpointErrors = new IDebugErrorBreakpoint2[0];
            }
            else
            {
                breakpointErrors = new IDebugErrorBreakpoint2[1];
                breakpointErrors[0] = _breakpointError;
            }

            errorBreakpointsEnum = _breakpointErrorEnumFactory.Create(breakpointErrors);
            return VSConstants.S_OK;
        }

        public int GetBreakpointRequest(out IDebugBreakpointRequest2 breakpointRequest)
        {
            breakpointRequest = null;
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }

            breakpointRequest = _request;
            return VSConstants.S_OK;
        }

        public int GetState(PENDING_BP_STATE_INFO[] state)
        {
            if (_deleted)
            {
                state[0].state = enum_PENDING_BP_STATE.PBPS_DELETED;
            }
            else if (_enabled)
            {
                state[0].state = enum_PENDING_BP_STATE.PBPS_ENABLED;
            }
            else if (!_enabled)
            {
                state[0].state = enum_PENDING_BP_STATE.PBPS_DISABLED;
            }
            return VSConstants.S_OK;
        }

        // We don't need to do anything here because SDM calls SetCondition on all bound
        // breakpoints.
        public int SetCondition(BP_CONDITION breakpointCondition) => VSConstants.S_OK;

        // We don't need to do anything here because SDM calls SetPassCount on all bound
        // breakpoints.
        public int SetPassCount(BP_PASSCOUNT breakpointPassCount) => _deleted
                                                                         ? AD7Constants.E_BP_DELETED
                                                                         : VSConstants.S_OK;

        public int Virtualize(int virtualize) => VSConstants.E_NOTIMPL;

        public uint GetNumLocations() => _lldbBreakpoint?.GetNumLocations() ?? 0;

#endregion
    }
}
