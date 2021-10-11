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

        public Breakpoint(Func<IBreakpointRequest> requestCreator, string description)
        {
            _requestCreator = requestCreator;
            _description = description;
        }

        #region IBreakpoint

        public uint PassCount { get; set; }

        public PassCountStyle PassCountStyle { get; set; } = PassCountStyle.None;

        public BreakpointState State { get; set; } = BreakpointState.RequestedPreSession;

        public IDebugPendingBreakpoint2 PendingBreakpoint { get; set; }

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

        InvalidOperationException CreateInvalidStateException() =>
            new InvalidOperationException($"The breakpoint state ({State}) cannot be " +
                                          $"handled by {nameof(Breakpoint)} class.");
    }
}