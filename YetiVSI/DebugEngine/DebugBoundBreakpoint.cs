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
using DebuggerCommonApi;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Diagnostics;

namespace YetiVSI.DebugEngine
{
    public interface IBoundBreakpoint : IDebugBoundBreakpoint2
    {
        int GetId();

        /// <summary>
        /// Update the state of the pending breakpoint upon a hit.
        /// </summary>
        void OnHit();
    }

    // This represents a breakpoint that has been bound in the debugger.
    public class DebugBoundBreakpoint : IBoundBreakpoint
    {
        public class Factory
        {
            readonly DebugDocumentContext.Factory _documentContextFactory;
            readonly DebugCodeContext.Factory _codeContextFactory;
            readonly DebugBreakpointResolution.Factory _breakpointResolutionFactory;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            public Factory(DebugDocumentContext.Factory documentContextFactory,
                           DebugCodeContext.Factory codeContextFactory,
                           DebugBreakpointResolution.Factory breakpointResolutionFactory)
            {
                _documentContextFactory = documentContextFactory;
                _codeContextFactory = codeContextFactory;
                _breakpointResolutionFactory = breakpointResolutionFactory;
            }

            public virtual IBoundBreakpoint Create(
                IDebugPendingBreakpoint2 pendingBreakpoint, SbBreakpointLocation breakpointLocation,
                IDebugProgram2 program,
                Guid languageGuid) => new DebugBoundBreakpoint(_documentContextFactory,
                                                               _codeContextFactory,
                                                               _breakpointResolutionFactory,
                                                               pendingBreakpoint,
                                                               breakpointLocation, program,
                                                               languageGuid);
        }

        readonly IDebugPendingBreakpoint2 _pendingBreakpoint;
        readonly SbBreakpointLocation _breakpointLocation;
        readonly IDebugBreakpointResolution2 _breakpointResolution;

        bool _enabled;
        bool _deleted;

        // LLDB doesn't support the equal style pass count natively. We implement it by disabling
        // the lldb breakpoint location once its hit count reaches its pass count. While that
        // happens, we don't modify |enabled| so the status appears unchanged on the UI.
        // DebugBoundBreakpoint.Enable does not reenable the underlying breakpoint or breakpoint
        // locations if it is disabled by reaching its pass count.
        bool _disabledByPassCount;
        BP_PASSCOUNT _passCount;

        // Hit count at the last hit count reset. Since lldb does not support resetting hit count,
        // we just remember the lldb hit count value at the last reset (to subtract it from
        // the lldb hit count on GetHitCount queries).
        int _baseHitCount = 0;

        // Constructor with factories for tests.
        DebugBoundBreakpoint(DebugDocumentContext.Factory documentContextFactory,
                             DebugCodeContext.Factory codeContextFactory,
                             DebugBreakpointResolution.Factory breakpointResolutionFactory,
                             IDebugPendingBreakpoint2 pendingBreakpoint,
                             SbBreakpointLocation breakpointLocation, IDebugProgram2 program,
                             Guid languageGuid)
        {
            _pendingBreakpoint = pendingBreakpoint;
            _breakpointLocation = breakpointLocation;

            _enabled = true;
            _deleted = false;
            _disabledByPassCount = false;

            SbAddress address = breakpointLocation.GetAddress();

            if (address != null)
            {
                LineEntryInfo lineEntry = address.GetLineEntry();
                IDebugDocumentContext2 documentContext = null;
                string name = "";

                // |lineEntry| is null if the breakpoint is set on an external function.
                if (lineEntry != null)
                {
                    documentContext = documentContextFactory.Create(lineEntry);
                    documentContext.GetName(enum_GETNAME_TYPE.GN_NAME, out name);
                }
                IDebugCodeContext2 codeContext = codeContextFactory.Create(
                    breakpointLocation.GetLoadAddress(), name, documentContext, languageGuid);
                _breakpointResolution = breakpointResolutionFactory.Create(codeContext, program);
            }
            else
            {
                Trace.WriteLine("Warning: Unable to obtain address from breakpoint location." +
                                " No breakpoint resolution created.");
            }
        }

#region IBoundBreakpoint functions

        public int GetId() => _breakpointLocation.GetId();

        public void OnHit()
        {
            GetHitCount(out uint hitCount);
            switch (_passCount.stylePassCount)
            {
            case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL:
                if (hitCount != _passCount.dwPassCount)
                {
                    Trace.WriteLine($"Error: breakpoint's hit count {hitCount} != its " +
                                    $"pass count {_passCount.dwPassCount} on hit");
                }

                // The breakpoint has reached its pass count. Disable |breakpointLocation| and
                // make sure it doesn't get re-enabled until the pass count is reset.
                _disabledByPassCount = true;
                _breakpointLocation.SetEnabled(false);
                break;
            case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_MOD:
                _breakpointLocation.SetIgnoreCount(_passCount.dwPassCount - 1);
                break;
            }
        }

#endregion IBoundBreakpoint functions

#region IDebugBoundBreakpoint2 functions

        public int Delete()
        {
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }
            // TODO: Figure out if it's possible for VS to delete a single call site,
            // as it doesn't look like LLDB supports that.  If that's the case we'll need to fake it
            // by disabling the breakpoint.  Currently this is only coded to handle being called
            // from DebugPendingBreakpoint::Delete().
            _deleted = true;
            return VSConstants.S_OK;
        }

        public int Enable(int enable)
        {
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }

            _enabled = Convert.ToBoolean(enable);
            if (!_disabledByPassCount)
            {
                _breakpointLocation.SetEnabled(_enabled);
            }
            return VSConstants.S_OK;
        }

        public int GetBreakpointResolution(out IDebugBreakpointResolution2 breakpointResolution)
        {
            breakpointResolution = null;
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }
            if (_breakpointResolution == null)
            {
                return VSConstants.E_FAIL;
            }
            breakpointResolution = _breakpointResolution;
            return VSConstants.S_OK;
        }

        public int GetHitCount(out uint hitCount)
        {
            hitCount = 0;
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }
            hitCount = _breakpointLocation.GetHitCount();
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
            return VSConstants.S_OK;
        }

        public int GetPendingBreakpoint(out IDebugPendingBreakpoint2 pendingBreakpoint)
        {
            pendingBreakpoint = _pendingBreakpoint;
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

        public int SetCondition(BP_CONDITION breakpointCondition)
        {
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }
            switch (breakpointCondition.styleCondition)
            {
            case enum_BP_COND_STYLE.BP_COND_NONE:
                _breakpointLocation.SetCondition("");
                break;
            case enum_BP_COND_STYLE.BP_COND_WHEN_TRUE:
                _breakpointLocation.SetCondition(breakpointCondition.bstrCondition);
                break;
            default:
                return VSConstants.E_NOTIMPL;
            }
            return VSConstants.S_OK;
        }

        public int SetHitCount(uint hitCount)
        {
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }
            var totalHitCount = _breakpointLocation.GetHitCount();
            _baseHitCount = Convert.ToInt32(totalHitCount) - Convert.ToInt32(hitCount);
            // Recalculate the criteria that depend on hit count.
            SetPassCount(_passCount);
            return VSConstants.S_OK;
        }

        public int SetPassCount(BP_PASSCOUNT breakpointPassCount)
        {
            if (_deleted)
            {
                return AD7Constants.E_BP_DELETED;
            }
            _passCount = breakpointPassCount;
            _breakpointLocation.SetEnabled(_enabled);
            _disabledByPassCount = false;
            GetHitCount(out uint hitCount);
            switch (breakpointPassCount.stylePassCount)
            {
            case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_NONE:
                _breakpointLocation.SetIgnoreCount(0);
                break;
            case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL_OR_GREATER:
                _breakpointLocation.SetIgnoreCount(
                    (uint)Math.Max(0, (int)breakpointPassCount.dwPassCount - (int)hitCount - 1));
                break;
            case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL:
                if (breakpointPassCount.dwPassCount > hitCount)
                {
                    _breakpointLocation.SetIgnoreCount(breakpointPassCount.dwPassCount - hitCount -
                                                       1);
                }
                else
                {
                    // Current hit count is already beyond the specified pass count.
                    // The breakpoint will not break.
                    _breakpointLocation.SetEnabled(false);
                    _disabledByPassCount = true;
                }
                break;
            case enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_MOD:
                _breakpointLocation.SetIgnoreCount(breakpointPassCount.dwPassCount -
                                                   hitCount % breakpointPassCount.dwPassCount - 1);
                break;
            }
            return VSConstants.S_OK;
        }

#endregion IDebugBoundBreakpoint2 functions
    }
}
