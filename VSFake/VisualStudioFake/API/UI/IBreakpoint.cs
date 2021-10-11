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

using Microsoft.VisualStudio.Debugger.Interop;

namespace Google.VisualStudioFake.API.UI
{
    /// <summary>
    /// Possible states of a breakpoint.
    /// </summary>
    public enum BreakpointState
    {
        /// <summary>
        /// Transient state until a debug engine is available.
        /// </summary>
        RequestedPreSession,

        /// <summary>
        /// Transient state between the following events:
        ///   - Breakpoint gets added to view -> Gets bound or fails.
        ///   - Breakpoint.DeleteAll is called -> They get actually deleted.
        /// </summary>
        Pending,
        Deleted,
        Disabled,
        Enabled,
        Error,
    }

    public enum PassCountStyle
    {
        /// <summary>
        /// Breakpoint always fires.
        /// </summary>
        None,

        /// <summary>
        /// Breakpoint fires when it is hit exactly N times.
        /// </summary>
        Equal,

        /// <summary>
        /// Breakpoint fires when it is hit N times or more.
        /// </summary>
        EqualOrGreater,

        /// <summary>
        /// Breakpoint fires when it is hit a multiple of N times.
        /// </summary>
        Mod,
    }

    public interface IBreakpoint
    {
        /// <summary>
        /// Gets the pass count setting for this breakpoint.
        /// </summary>
        uint PassCount { get; }

        /// <summary>
        /// Gets the way PassCount is interpreted.
        /// </summary>
        PassCountStyle PassCountStyle { get; }

        /// <summary>
        /// Gets the current state of this breakpoint.
        /// </summary>
        BreakpointState State { get; }

        /// <summary>
        /// Gets a reference to the inner interop breakpoint. It should only be used for
        /// debugging purposes.
        /// </summary>
        IDebugPendingBreakpoint2 PendingBreakpoint { get; }

        /// <summary>
        /// Returns true if either the breakpoint has been bound or an error occured while doing so.
        /// </summary>
        bool Ready { get; }

        /// <summary>
        /// Gets the error message returned by the debug engine when it tries to
        /// bind/ enable/ disable/ delete this breakpoint.
        /// When no error occurs, this string should be null.
        /// </summary>
        string Error { get; }
    }
}