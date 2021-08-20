// Copyright 2021 Google LLC
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

using Google.VisualStudioFake.API.UI;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using Google.VisualStudioFake.Internal.Jobs;

namespace Google.VisualStudioFake.Internal.UI
{
    public class Breakpoint : IBreakpoint
    {
        readonly Func<IBreakpointRequest> _requestCreator;
        readonly string _description;
        IJobQueue _jobQueue;

        public Breakpoint(Func<IBreakpointRequest> requestCreator, string description,
                          IJobQueue jobQueue)
        {
            _requestCreator = requestCreator;
            _description = description;
            _jobQueue = jobQueue;
        }

        #region IBreakpoint

        public BreakpointState State { get; set; } = BreakpointState.RequestedPreSession;

        public IDebugPendingBreakpoint2 PendingBreakpoint { get; set; }

        public void SetPassCount(uint count, PasscountStyle style)
        {
            _jobQueue.Push(new GenericJob(() =>
            {
                // Set passcount on pending breakpoint.
                BP_PASSCOUNT pc = new BP_PASSCOUNT();
                pc.dwPassCount = count;
                pc.stylePassCount = ToVsStyle(style);
                HResultChecker.Check(PendingBreakpoint.SetPassCount(pc));

                // Also set passcount on all bound breakpoints.
                HResultChecker.Check(
                    PendingBreakpoint.EnumBoundBreakpoints(
                        out IEnumDebugBoundBreakpoints2 boundBreakpointsEnum));
                HResultChecker.Check(boundBreakpointsEnum.Reset());
                HResultChecker.Check(boundBreakpointsEnum.GetCount(out uint numBound));
                var boundBreakpoints = new IDebugBoundBreakpoint2[numBound];
                uint actual = 0;
                HResultChecker.Check(
                    boundBreakpointsEnum.Next(numBound, boundBreakpoints, ref actual));
                if (actual != numBound)
                {
                    throw new VSFakeException("Could not fetch all bound breakpoints. " +
                                              $"Expected: {numBound}, got: {actual}");
                }

                foreach (var bp in boundBreakpoints)
                {
                    HResultChecker.Check(bp.SetPassCount(pc));
                }
            }));
        }

        public string Error { get; set; }

        public bool Ready
        {
            get
            {
                switch (State)
                {
                    case BreakpointState.Deleted:
                    case BreakpointState.Disabled:
                    case BreakpointState.Enabled:
                    case BreakpointState.Error:
                        return true;
                    case BreakpointState.RequestedPreSession:
                    case BreakpointState.Pending:
                        return false;
                    default:
                        throw CreateInvalidStateException();
                }
            }
        }

        #endregion

        public IBreakpointRequest CreateRequest() => _requestCreator();

        public override string ToString() => _description;

        enum_BP_PASSCOUNT_STYLE ToVsStyle(PasscountStyle style)
        {
            switch (style)
            {
                case PasscountStyle.None: return enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_NONE;
                case PasscountStyle.Equal: return enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
                case PasscountStyle.EqualOrGreater:
                    return enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL_OR_GREATER;
                case PasscountStyle.Mod: return enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_MOD;
            }

            throw new InvalidOperationException($"Unhandled passcount style {style}");
        }

        InvalidOperationException CreateInvalidStateException() =>
            new InvalidOperationException($"The breakpoint state ({State}) cannot be " +
                                          $"handled by {nameof(Breakpoint)} class.");
    }
}