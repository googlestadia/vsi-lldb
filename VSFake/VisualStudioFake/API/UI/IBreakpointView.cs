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

﻿using Google.VisualStudioFake.Internal.ExecutionSyncPoint;
using System.Collections.Generic;

namespace Google.VisualStudioFake.API.UI
{
    public interface IBreakpointView
    {
        /// <summary>
        /// Adds a new breakpoint specified by a filename and a line number.
        /// This can be called either when the program has not started or while it is running.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown if the program execution has already terminated.
        /// </exception>
        [SyncPoint(ExecutionSyncPoint.IDLE, Timeout=VSFakeTimeout.Medium)]
        IBreakpoint Add(string filename, int lineNumber);

        /// <summary>
        /// Gets the breakpoints currently managed by this view.
        /// </summary>
        IList<IBreakpoint> GetBreakpoints();

        /// <summary>
        /// Deletes all the breakpoints currently managed by this view.
        /// </summary>
        [SyncPoint(ExecutionSyncPoint.IDLE, Timeout = VSFakeTimeout.Medium)]
        IList<IBreakpoint> DeleteAllBreakpoints();

        /// <summary>
        /// Gets the breakpoint that fired at the current code location.
        /// Returns null if no breakpoint was fired. For instance, this will return null if called
        /// after a user pause at a location with no breakpoint, or if a step is performed after
        /// a breapoint stop and ends up at a location that does not trigger any new breakpoints.
        /// Additionally, this property returns a null value if the program is not at a break.
        /// </summary>
        IBreakpoint FiredBreakpoint { get; }
    }
}
