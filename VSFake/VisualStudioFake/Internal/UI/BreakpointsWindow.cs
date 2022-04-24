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

using Google.VisualStudioFake.API;
using Google.VisualStudioFake.API.UI;
using Google.VisualStudioFake.Internal.Interop;
using Google.VisualStudioFake.Internal.Jobs;
using Google.VisualStudioFake.Util;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Google.VisualStudioFake.Internal.UI
{
    /// <summary>
    /// Contains logic to set fired breakpoints.
    /// </summary>
    public interface IFiredBreakpointSetter
    {
        /// <summary>
        /// Sets the bound breakpoints that fired at the current code location.
        /// </summary>
        void Set(IEnumerable<IDebugBoundBreakpoint2> boundBreakpoints);
    }

    public class BreakpointsWindow : IBreakpointsWindow, IFiredBreakpointSetter
    {
        readonly IList<Breakpoint> _breakpoints = new List<Breakpoint>();

        readonly IDictionary<IDebugPendingBreakpoint2, Breakpoint> _pendingToBreakpoint =
            new Dictionary<IDebugPendingBreakpoint2, Breakpoint>();

        readonly IDebugSessionContext _debugSessionContext;
        readonly IJobQueue _jobQueue;
        readonly JoinableTaskContext _taskContext;

        IBreakpoint _firedBreakpoint;

        public BreakpointsWindow(IDebugSessionContext debugSessionContext, IJobQueue jobQueue,
                                 JoinableTaskContext taskContext)
        {
            _debugSessionContext = debugSessionContext;
            _jobQueue = jobQueue;
            _taskContext = taskContext;
        }

        #region IBreakpointsWindow

        public IBreakpoint Add(string filename, int lineNumber) =>
            Add(new Breakpoint(() => new FileLineBreakpointRequest(filename, lineNumber),
                               $"Breakpoint at {filename}:{lineNumber}"));

        public IBreakpoint SetPassCount(IBreakpoint breakpoint, uint count, PassCountStyle style)
        {
            CheckState(breakpoint, "set pass count for");

            Breakpoint bp = _breakpoints.FirstOrDefault(b => b == breakpoint);
            bp.PassCount = count;
            bp.PassCountStyle = style;

            if (_debugSessionContext.ProgramState == ProgramState.Running ||
                _debugSessionContext.ProgramState == ProgramState.AtBreak)
            {
                _jobQueue.Push(new GenericJob(() => SetPassCount(bp),
                                              $"Set pass count {bp.PassCount}, style " +
                                              $"{bp.PassCountStyle} for breakpoint {bp}"));
            }

            return breakpoint;
        }

        public IList<IBreakpoint> GetBreakpoints() => new List<IBreakpoint>(_breakpoints);

        public void Delete(IBreakpoint breakpoint)
        {
            CheckState(breakpoint, "delete");
            QueueDelete(_breakpoints.FirstOrDefault(b => b == breakpoint));
        }

        public void DeleteAll()
        {
            foreach (var bp in _breakpoints)
            {
                QueueDelete(bp);
            }
        }

        public IBreakpoint FiredBreakpoint
        {
            get =>
                _debugSessionContext.ProgramState == ProgramState.AtBreak
                    ? _firedBreakpoint
                    : null;
            private set => _firedBreakpoint = value;
        }

        #endregion

        #region Internal API

        public IEnumerable<IBreakpoint> BindPendingBreakpoints()
        {
            IEnumerable<Breakpoint> pendingBreakpoints = _breakpoints.Where(
                b => b.State == BreakpointState.RequestedPreSession);
            foreach (Breakpoint bp in pendingBreakpoints)
            {
                Bind(bp);
                if (bp.PassCount != 0 && bp.PassCountStyle != PassCountStyle.None)
                {
                    SetPassCount(bp);
                }
            }

            return pendingBreakpoints;
        }

        public void HandleBindResultEvent(DebugEventArgs args)
        {
            if (args.Event is IDebugBreakpointBoundEvent2)
            {
                HandleBoundEvent((IDebugBreakpointBoundEvent2) args.Event);
            }
            else if (args.Event is IDebugBreakpointErrorEvent2)
            {
                HandleErrorEvent((IDebugBreakpointErrorEvent2) args.Event);
            }
        }

        #endregion

        #region IFiredBreakpointSetter

        public void Set(IEnumerable<IDebugBoundBreakpoint2> boundBreakpoints)
        {
            if (!boundBreakpoints.Any())
            {
                FiredBreakpoint = null;
                return;
            }

            IDebugPendingBreakpoint2 pendingBreakpoint;
            HResultChecker.Check(boundBreakpoints.First()
                                     .GetPendingBreakpoint(out pendingBreakpoint));
            FiredBreakpoint = _pendingToBreakpoint[pendingBreakpoint];
        }

        #endregion

        void HandleBoundEvent(IDebugBreakpointBoundEvent2 evnt)
        {
            IDebugPendingBreakpoint2 pendingBreakpoint;
            HResultChecker.Check(evnt.GetPendingBreakpoint(out pendingBreakpoint));
            var state = new PENDING_BP_STATE_INFO[1];
            HResultChecker.Check(pendingBreakpoint.GetState(state));
            var breakpoint = _pendingToBreakpoint[pendingBreakpoint];
            breakpoint.State = ToBreakpointState(state[0].state);
        }

        void HandleErrorEvent(IDebugBreakpointErrorEvent2 evnt)
        {
            IDebugErrorBreakpoint2 errorBreakpoint;
            HResultChecker.Check(evnt.GetErrorBreakpoint(out errorBreakpoint));
            IDebugErrorBreakpointResolution2 errorBreakpointResolution;
            HResultChecker.Check(
                errorBreakpoint.GetBreakpointResolution(out errorBreakpointResolution));
            var errorResolutionInfo = new BP_ERROR_RESOLUTION_INFO[1];
            HResultChecker.Check(errorBreakpointResolution.GetResolutionInfo(
                                     enum_BPERESI_FIELDS.BPERESI_MESSAGE, errorResolutionInfo));
            IDebugPendingBreakpoint2 pendingBreakpoint;
            HResultChecker.Check(errorBreakpoint.GetPendingBreakpoint(out pendingBreakpoint));
            var breakpoint = _pendingToBreakpoint[pendingBreakpoint];
            breakpoint.Error = errorResolutionInfo[0].bstrMessage;
            breakpoint.State = BreakpointState.Error;
        }

        void Bind(Breakpoint breakpoint)
        {
            using (var breakpointRequest = breakpoint.CreateRequest())
            {
                IDebugPendingBreakpoint2 pendingBreakpoint = null;
                _taskContext.RunOnMainThread(() => HResultChecker.Check(
                                                 _debugSessionContext.DebugEngine
                                                     .CreatePendingBreakpoint(
                                                         breakpointRequest,
                                                         out pendingBreakpoint)));
                breakpoint.PendingBreakpoint = pendingBreakpoint;
                _pendingToBreakpoint[pendingBreakpoint] = breakpoint;
                HResultChecker.Check(pendingBreakpoint.Enable(1));
                if (pendingBreakpoint.Virtualize(1) != VSConstants.E_NOTIMPL)
                {
                    throw new InvalidOperationException("VSFake should be updated to handle " +
                                                        $"{nameof(pendingBreakpoint.Virtualize)}.");
                }

                pendingBreakpoint.Bind();
            }
        }

        IBreakpoint Add(Breakpoint breakpoint)
        {
            if (_debugSessionContext.ProgramState == ProgramState.Terminated)
            {
                throw new InvalidOperationException(
                    "Breakpoint cannot be added because the program has terminated.");
            }

            _breakpoints.Add(breakpoint);
            if (_debugSessionContext.ProgramState == ProgramState.Running ||
                _debugSessionContext.ProgramState == ProgramState.AtBreak)
            {
                _jobQueue.Push(new GenericJob(() => Bind(breakpoint)));
            }

            return breakpoint;
        }

        BreakpointState ToBreakpointState(enum_PENDING_BP_STATE state)
        {
            switch (state)
            {
                case enum_PENDING_BP_STATE.PBPS_DELETED:
                    return BreakpointState.Deleted;
                case enum_PENDING_BP_STATE.PBPS_DISABLED:
                    return BreakpointState.Disabled;
                case enum_PENDING_BP_STATE.PBPS_ENABLED:
                    return BreakpointState.Enabled;
                default:
                    return BreakpointState.Error;
            }
        }

        void SetPassCount(Breakpoint breakpoint)
        {
            // Set pass count on pending breakpoint.
            BP_PASSCOUNT pc = new BP_PASSCOUNT();
            pc.dwPassCount = breakpoint.PassCount;
            pc.stylePassCount = ToVsStyle(breakpoint.PassCountStyle);
            HResultChecker.Check(breakpoint.PendingBreakpoint.SetPassCount(pc));

            // Also set pass count on all bound breakpoints.
            HResultChecker.Check(
                breakpoint.PendingBreakpoint.EnumBoundBreakpoints(
                    out IEnumDebugBoundBreakpoints2 boundBreakpointsEnum));
            HResultChecker.Check(boundBreakpointsEnum.Reset());
            HResultChecker.Check(boundBreakpointsEnum.GetCount(out uint numBound));
            var boundBreakpoints = new IDebugBoundBreakpoint2[numBound];
            uint actual = 0;
            HResultChecker.Check(boundBreakpointsEnum.Next(numBound, boundBreakpoints, ref actual));
            if (actual != numBound)
            {
                throw new VSFakeException("Could not fetch all bound breakpoints. " +
                                          $"Expected: {numBound}, got: {actual}");
            }

            foreach (var bp in boundBreakpoints)
            {
                HResultChecker.Check(bp.SetPassCount(pc));
            }
        }

        void QueueDelete(Breakpoint breakpoint)
        {
            CheckState(breakpoint, "delete");

            bool shouldDeleteInteropBreakpoint = breakpoint.State == BreakpointState.Disabled ||
                breakpoint.State == BreakpointState.Enabled;
            breakpoint.State = BreakpointState.Pending;
            _jobQueue.Push(new GenericJob(() =>
            {
                if (shouldDeleteInteropBreakpoint)
                {
                    HResultChecker.Check(breakpoint.PendingBreakpoint.Delete());
                }

                breakpoint.State = BreakpointState.Deleted;
                _breakpoints.Remove(breakpoint);
                if (breakpoint.PendingBreakpoint != null)
                {
                    _pendingToBreakpoint.Remove(breakpoint.PendingBreakpoint);
                }
            }, $"Delete breakpoint ({breakpoint})"));
        }

        enum_BP_PASSCOUNT_STYLE ToVsStyle(PassCountStyle style)
        {
            switch (style)
            {
                case PassCountStyle.None: return enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_NONE;
                case PassCountStyle.Equal: return enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
                case PassCountStyle.EqualOrGreater:
                    return enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL_OR_GREATER;
                case PassCountStyle.Mod: return enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_MOD;
            }

            throw new InvalidOperationException($"Unhandled pass count style: {style}");
        }

        void CheckState(IBreakpoint breakpoint, string operation)
        {
            if (breakpoint.State == BreakpointState.Pending ||
                breakpoint.State == BreakpointState.Deleted)
            {
                throw new InvalidOperationException(
                    $"Cannot {operation} breakpoint ({breakpoint}); state = {breakpoint.State}.");
            }
        }
    }
}