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

using System.Collections.Generic;
using DebuggerApi;
using Microsoft.VisualStudio.Debugger.Interop;

namespace YetiVSI.DebugEngine.Interfaces
{
    public interface IBreakpointManager
    {
        /// <summary>
        /// Create a new pending breakpoint.
        /// </summary>
        /// <param name="breakpointRequest">Breakpoint request.</param>
        /// <param name="target">Target.</param>
        /// <param name="pendingBreakpoint">Created pending breakpoint.</param>
        void CreatePendingBreakpoint(IDebugBreakpointRequest2 breakpointRequest,
            RemoteTarget target, out IDebugPendingBreakpoint2 pendingBreakpoint);

        /// <summary>
        /// Register a breakpoint.  A breakpoint must have an ID
        /// (so it must be bound) before it can be registered.
        /// </summary>
        /// <param name="pendingBreakpoint">Breakpoint to be registered.</param>
        void RegisterPendingBreakpoint(IPendingBreakpoint pendingBreakpoint);

        /// <summary>
        /// Register a watchpoint. |pendingWatchpoint| must have an ID
        /// (so it must be bound) before it can be registered.
        /// </summary>
        /// <param name="pendingWatchpoint">Watchpoint to be registered.</param>
        void RegisterWatchpoint(IWatchpoint pendingWatchpoint);

        /// <summary>
        /// Unregister a watchpoint. This method should be invoked if
        /// a watchpoint is not used anymore and can be safely deleted.
        /// </summary>
        /// <param name="watchpoint">Watchpoint to be unregistered.</param>
        void UnregisterWatchpoint(IWatchpoint watchpoint);

        /// <summary>
        /// Getter for watchpoint references count.
        /// </summary>
        /// <param name="watchpoint">Watchpoint to get the ref count.</param>
        /// <returns>The number of objects which still use the watchpoint.</returns>
        int GetWatchpointRefCount(IWatchpoint watchpoint);

        /// <summary>
        /// Look up a registered breakpoint by its ID.
        /// </summary>
        /// <param name="id">Breakpoint id.</param>
        /// <param name="pendingBreakpoint">Pending breakpoint (if exists).</param>
        /// <returns>True if succeeded to get breakpoint by id, false otherwise.</returns>
        bool GetPendingBreakpointById(int id, out IPendingBreakpoint pendingBreakpoint);

        /// <summary>
        /// Look up a registered watchpoint by its ID.
        /// </summary>
        /// <param name="id">Watchpoint id.</param>
        /// <param name="watchPoint">Watchpoint (if exists).</param>
        /// <returns>True if succeeded to get watchpoint by id, false otherwise.</returns>
        bool GetWatchpointById(int id, out IWatchpoint watchPoint);

        /// <summary>
        /// Report a breakpoint error. Errors occur when registering/binding breakpoints.
        /// </summary>
        /// <param name="error">Error.</param>
        void ReportBreakpointError(DebugBreakpointError error);

        /// <summary>
        /// Getter for number of pending breakpoints.
        /// </summary>
        uint GetNumPendingBreakpoints();

        /// <summary>
        /// Getter for number of bound breakpoints.
        /// </summary>
        uint GetNumBoundBreakpoints();

        /// <summary>
        /// Remove the breakpoint.
        /// </summary>
        void RemovePendingBreakpoint(IPendingBreakpoint breakpoint);

        /// <summary>
        /// This event should be emitted when new breakpoint locations have been added.
        /// </summary>
        void EmitBreakpointBoundEvent(
            IPendingBreakpoint breakpoint,
            IEnumerable<IDebugBoundBreakpoint2> newlyBoundBreakpoints);
    }
}
