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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Diagnostics;
using YetiCommon.CastleAspects;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Watchpoint represents a data breakpoint. A data breakpoint is a type of breakpoint that's
    /// bound to an address and size. It breaks when the memory it watches is changes.
    /// </summary>
    public interface IWatchpoint : IDebugPendingBreakpoint2, IDebugBoundBreakpoint2
    {
        /// <summary>
        /// Get the ID of the watchpoint.
        /// </summary>
        int GetId();

        // Marked as new to avoid casts at IWatchpoint::Delete() callsites because both of
        // IDebugPendingBreakpoint2 and IDebugBoundBreakpoint2 declare Delete().
        new int Delete();

        // Marked as new to avoid casts at IWatchpoint::Delete() callsites because both of
        // IDebugPendingBreakpoint2 and IDebugBoundBreakpoint2 declare Enable(int).
        new int Enable(int fEnable);
    }

    /// <summary>
    /// Implementation of a data breakpoint.
    /// </summary>
    public class DebugWatchpoint : SimpleDecoratorSelf<IWatchpoint>, IWatchpoint
    {
        // Creates IWatchpoint objects.
        public class Factory
        {
            readonly JoinableTaskContext _taskContext;
            readonly DebugWatchpointResolution.Factory _resolutionFactory;
            readonly BreakpointErrorEnumFactory _breakpointErrorEnumFactory;
            readonly BoundBreakpointEnumFactory _boundBreakpointEnumFactory;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            public Factory(JoinableTaskContext taskContext,
                           DebugWatchpointResolution.Factory resolutionFactory,
                           BreakpointErrorEnumFactory breakpointErrorEnumFactory,
                           BoundBreakpointEnumFactory boundBreakpointEnumFactory)
            {
                _taskContext = taskContext;
                _resolutionFactory = resolutionFactory;
                _breakpointErrorEnumFactory = breakpointErrorEnumFactory;
                _boundBreakpointEnumFactory = boundBreakpointEnumFactory;
            }

            public virtual IWatchpoint Create(IBreakpointManager breakpointManager,
                                              IDebugBreakpointRequest2 request, RemoteTarget target,
                                              IDebugProgram2 program)
            {
                _taskContext.ThrowIfNotOnMainThread();
                return Create(breakpointManager, request, target, program, new Marshal());
            }

            public virtual IWatchpoint Create(IBreakpointManager breakpointManager,
                                              IDebugBreakpointRequest2 request, RemoteTarget target,
                                              IDebugProgram2 program, Marshal marshal)
            {
                _taskContext.ThrowIfNotOnMainThread();
                return new DebugWatchpoint(_taskContext, _resolutionFactory,
                                           _breakpointErrorEnumFactory, _boundBreakpointEnumFactory,
                                           breakpointManager, request, target, program, marshal);
            }
        }

        const string _watchpointNotSupported = "Breakpoint type is not supported.";

        readonly Marshal _marshal;
        readonly IDebugBreakpointRequest2 _request;
        readonly BP_REQUEST_INFO _requestInfo;
        readonly RemoteTarget _target;
        SbWatchpoint _lldbWatchpoint;
        readonly IBreakpointManager _breakpointManager;
        readonly IDebugProgram2 _program;
        IDebugBreakpointResolution2 _resolution;
        readonly DebugWatchpointResolution.Factory _resolutionFactory;
        readonly BreakpointErrorEnumFactory _breakpointErrorEnumFactory;
        readonly BoundBreakpointEnumFactory _boundBreakpointEnumFactory;

        DebugBreakpointError _breakpointError;
        bool _enabled;
        bool _deleted;

        int _baseHitCount = 0;

        // LLDB doesn't support the equal style pass count natively. We implement it by disabling
        // the lldb breakpointlocation once its hit count reaches its pass count. While that
        // happens, we don't modify |enabled| so the status appears unchanged on the UI.
        // DebugBoundBreakpoint.Enable does not reenable the underlying breakpoint or breakpoint
        // locations if it is disabled by reaching its pass count.
        bool _disabledByPassCount;
        BP_PASSCOUNT _passCount;

        DebugWatchpoint(JoinableTaskContext taskContext,
                        DebugWatchpointResolution.Factory resolutionFactory,
                        BreakpointErrorEnumFactory breakpointErrorEnumFactory,
                        BoundBreakpointEnumFactory boundBreakpointEnumFactory,
                        IBreakpointManager breakpointManager, IDebugBreakpointRequest2 request,
                        RemoteTarget target, IDebugProgram2 program, Marshal marshal)
        {
            taskContext.ThrowIfNotOnMainThread();

            _request = request;
            _target = target;
            _breakpointManager = breakpointManager;
            _resolutionFactory = resolutionFactory;
            _breakpointErrorEnumFactory = breakpointErrorEnumFactory;
            _boundBreakpointEnumFactory = boundBreakpointEnumFactory;
            _disabledByPassCount = false;

            BP_REQUEST_INFO[] breakpointRequestInfo = new BP_REQUEST_INFO[1];
            request.GetRequestInfo(enum_BPREQI_FIELDS.BPREQI_BPLOCATION |
                                       enum_BPREQI_FIELDS.BPREQI_CONDITION |
                                       enum_BPREQI_FIELDS.BPREQI_PASSCOUNT,
                                   breakpointRequestInfo);
            _requestInfo = breakpointRequestInfo[0];

            _enabled = true;
            _deleted = false;
            _program = program;
            _marshal = marshal;
        }

#region IWatchpoint functions

        public int GetId()
        {
            if (_lldbWatchpoint == null)
            {
                return -1;
            }
            return _lldbWatchpoint.GetId();
        }

#endregion

#region IDebugPendingBreakpoint2 functions

        public int Bind()
        {
            if (_requestInfo.bpLocation.bpLocationType !=
                (uint)enum_BP_LOCATION_TYPE.BPLT_DATA_STRING)
            {
                SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, _watchpointNotSupported);
                return VSConstants.S_FALSE;
            }
            string dataExpression =
                _marshal.GetStringFromIntPtr(_requestInfo.bpLocation.unionmember3);
            uint size = (uint)_requestInfo.bpLocation.unionmember4;
            _lldbWatchpoint =
                _target.WatchAddress(Convert.ToInt64(dataExpression, 16), size, false /* read */,
                                     true /* write */, out SbError error);
            if (error.Fail())
            {
                SetError(enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, error.GetCString());
                return VSConstants.S_FALSE;
            }

            _lldbWatchpoint.SetEnabled(_enabled);
            if ((_requestInfo.dwFields & enum_BPREQI_FIELDS.BPREQI_CONDITION) != 0)
            {
                SetCondition(_requestInfo.bpCondition);
            }
            if ((_requestInfo.dwFields & enum_BPREQI_FIELDS.BPREQI_PASSCOUNT) != 0)
            {
                SetPassCount(_requestInfo.bpPassCount);
            }
            _resolution = _resolutionFactory.Create(dataExpression, _program);
            _breakpointManager.RegisterWatchpoint(Self);
            return VSConstants.S_OK;
        }

        public int CanBind(out IEnumDebugErrorBreakpoints2 errorBreakpointsEnum)
        {
            errorBreakpointsEnum = null;
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }

            bool notDataStringLocation = _requestInfo.bpLocation.bpLocationType !=
                                         (uint)enum_BP_LOCATION_TYPE.BPLT_DATA_STRING;

            bool conditionalWhenChangedBp =
                (_requestInfo.dwFields & enum_BPREQI_FIELDS.BPREQI_CONDITION) != 0 &&
                _requestInfo.bpCondition.styleCondition == enum_BP_COND_STYLE.BP_COND_WHEN_CHANGED;

            if (notDataStringLocation || conditionalWhenChangedBp)
            {
                IDebugErrorBreakpoint2[] breakpointErrors = new IDebugErrorBreakpoint2[1];
                breakpointErrors[0] = new DebugBreakpointError(
                    Self, enum_BP_ERROR_TYPE.BPET_GENERAL_WARNING, _watchpointNotSupported);
                errorBreakpointsEnum = _breakpointErrorEnumFactory.Create(breakpointErrors);
                return VSConstants.S_FALSE;
            }
            return VSConstants.S_OK;
        }

        // When changing existing watchpoint, VS creates a new watchpoint and deletes the
        // previous one. But LLDB doesn't create a new watchpoint if it points to the same
        // address. This may result in several DebugWatchpoint objects holding the same
        // watchpoint, and if one of the instances deletes it all the related DebugWatchpoint
        // become invalid. So we need to keep tracking of who actually using the watchpoint
        // by calling RegisterWatchpoint / UnregisterWatchpoint methods.
        public int Delete()
        {
            if (_deleted)
            {
                return VSConstants.S_OK;
            }

            _deleted = true;
            _breakpointManager.UnregisterWatchpoint(Self);

            if (_lldbWatchpoint != null && _breakpointManager.GetWatchpointRefCount(Self) == 0)
            {
                _target.DeleteWatchpoint(_lldbWatchpoint.GetId());
            }
            return VSConstants.S_OK;
        }

        public int Enable(int enable)
        {
            _enabled = Convert.ToBoolean(enable);
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }
            if (_lldbWatchpoint != null && !_disabledByPassCount)
            {
                _lldbWatchpoint.SetEnabled(_enabled);
            }
            return VSConstants.S_OK;
        }

        public int EnumBoundBreakpoints(out IEnumDebugBoundBreakpoints2 enumBoundBreakpoints)
        {
            IDebugBoundBreakpoint2[] boundBreakpoints = { Self };
            enumBoundBreakpoints = _boundBreakpointEnumFactory.Create(boundBreakpoints);
            return VSConstants.S_OK;
        }

        public int EnumErrorBreakpoints(enum_BP_ERROR_TYPE breakpointErrorType,
                                        out IEnumDebugErrorBreakpoints2 errorBreakpointsEnum)
        {
            IDebugErrorBreakpoint2[] breakpointErrors = new IDebugErrorBreakpoint2[1];
            breakpointErrors[0] = _breakpointError;
            errorBreakpointsEnum = _breakpointErrorEnumFactory.Create(breakpointErrors);
            return VSConstants.S_OK;
        }

        public int GetBreakpointRequest(out IDebugBreakpointRequest2 breakpointRequest)
        {
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

        public int SetCondition(BP_CONDITION bpCondition)
        {
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }
            if (_lldbWatchpoint == null)
            {
                return VSConstants.S_FALSE;
            }
            switch (bpCondition.styleCondition)
            {
            case enum_BP_COND_STYLE.BP_COND_NONE:
                _lldbWatchpoint.SetCondition("");
                break;
            case enum_BP_COND_STYLE.BP_COND_WHEN_TRUE:
                _lldbWatchpoint.SetCondition(bpCondition.bstrCondition);
                break;
            default:
                return VSConstants.E_NOTIMPL;
            }
            return VSConstants.S_OK;
        }

        // SetIgnoreCount method works differently for watchpoints and
        // breakpoints. For breakpoints it configures the number of times the
        // breakpoint is skipped before hitting, starting from the moments this
        // property is set up. For watchpoints it counts all hits that already
        // happened. For example, if the current hit count for breakpoint is 5
        // and you set ignore count to 5, the next time it will stop on 10.
        //  But for watchpoint it will stop on 6.
        public int SetPassCount(BP_PASSCOUNT breakpointPassCount)
        {
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }
            if (_lldbWatchpoint == null)
            {
                return VSConstants.S_OK;
            }
            _passCount = breakpointPassCount;
            _lldbWatchpoint.SetEnabled(_enabled);
            _disabledByPassCount = false;
            GetHitCount(out uint hitCount);
            switch (breakpointPassCount.stylePassCount)
            {
            case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_NONE:
                _lldbWatchpoint.SetIgnoreCount(0);
                break;
            case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL_OR_GREATER:
                _lldbWatchpoint.SetIgnoreCount(
                    (uint)Math.Max(0, _baseHitCount + (int)breakpointPassCount.dwPassCount - 1));
                break;
            case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL:
                if (breakpointPassCount.dwPassCount > hitCount)
                {
                    _lldbWatchpoint.SetIgnoreCount((uint)Math.Max(
                        0, _baseHitCount + (int)breakpointPassCount.dwPassCount - 1));
                }
                else
                {
                    // Current hit count is already beyond the specified pass count.
                    // The watchpoint will not break.
                    _lldbWatchpoint.SetEnabled(false);
                    _disabledByPassCount = true;
                }
                break;
            case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_MOD:
                _lldbWatchpoint.SetIgnoreCount((uint)Math.Max(
                    0, _baseHitCount + (int)hitCount + (int)breakpointPassCount.dwPassCount -
                           (int)hitCount % breakpointPassCount.dwPassCount - 1));
                break;
            }
            return VSConstants.S_OK;
        }

        public void OnHit()
        {
            if (_lldbWatchpoint == null)
            {
                return;
            }

            GetHitCount(out uint hitCount);
            switch (_passCount.stylePassCount)
            {
            case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL:
                if (hitCount != _passCount.dwPassCount)
                {
                    Trace.WriteLine(
                        "Error: watchpoint's hit count != its pass count on hit: " +
                        $"{hitCount} != {_passCount.dwPassCount}");
                }

                // The watchpoint has reached its pass count.
                _disabledByPassCount = true;
                _lldbWatchpoint.SetEnabled(false);
                break;
            case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_MOD:
                _lldbWatchpoint.SetIgnoreCount(_passCount.dwPassCount - 1);
                break;
            }
        }

        public int Virtualize(int fVirtualize)
        {
            return VSConstants.E_NOTIMPL;
        }

#endregion

#region IDebugBoundBreakpoint2 functions

        public int GetPendingBreakpoint(out IDebugPendingBreakpoint2 pendingBreakpoint)
        {
            pendingBreakpoint = Self;
            return VSConstants.S_OK;
        }

        public int GetState(enum_BP_STATE[] state)
        {
            state[0] = enum_BP_STATE.BPS_NONE;
            if (_deleted)
            {
                state[0] = enum_BP_STATE.BPS_DELETED;
            }
            else if (_enabled)
            {
                state[0] = enum_BP_STATE.BPS_ENABLED;
            }
            else if (!_enabled)
            {
                state[0] = enum_BP_STATE.BPS_DISABLED;
            }
            return VSConstants.S_OK;
        }

        public int GetHitCount(out uint hitCount)
        {
            hitCount = 0;
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }
            if (_lldbWatchpoint != null)
            {
                hitCount = _lldbWatchpoint.GetHitCount();
                if (hitCount < _baseHitCount)
                {
                    Trace.WriteLine(
                        $"Error: Inconsistent hitcount - base hit count ({_baseHitCount}) is " +
                        $"smaller than actual hitcount {hitCount}.");
                    hitCount = 0;
                }
                else
                {
                    hitCount = Convert.ToUInt32(Convert.ToInt32(hitCount) - _baseHitCount);
                }
            }
            return VSConstants.S_OK;
        }
        public int GetBreakpointResolution(out IDebugBreakpointResolution2 resolution)
        {
            resolution = null;
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }
            if (_resolution == null)
            {
                return VSConstants.E_FAIL;
            }
            resolution = _resolution;
            return VSConstants.S_OK;
        }

        public int SetHitCount(uint hitCount)
        {
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }
            if (_lldbWatchpoint != null)
            {
                uint totalHitCount = _lldbWatchpoint.GetHitCount();
                _baseHitCount = (int)totalHitCount - (int)hitCount;

                // Recalculate the criteria that depend on hit count.
                SetPassCount(_passCount);
            }
            return VSConstants.S_OK;
        }

#endregion

        /// <summary>
        /// Caches the error, and notifies the DebugEngine that an error occurred with the pending
        /// breakpoint.  Calling this multiple times will overwrite the previously cached error.
        /// </summary>
        void SetError(enum_BP_ERROR_TYPE errorType, string errorMessage)
        {
            _breakpointError = new DebugBreakpointError(Self, errorType, errorMessage);
            _breakpointManager.ReportBreakpointError(_breakpointError);
        }
    }
}
