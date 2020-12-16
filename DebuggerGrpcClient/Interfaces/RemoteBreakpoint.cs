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

namespace DebuggerApi
{
    /// <summary>
    /// Interface based off the SbBreakpoint API.
    /// </summary>
    public interface RemoteBreakpoint
    {
        /// <summary>
        /// Enable / disable the breakpoint.
        /// </summary>
        void SetEnabled(bool enabled);

        /// <summary>
        /// Get the number of locations to which this breakpoint is bound.
        /// </summary>
        uint GetNumLocations();

        /// <summary>
        /// Get the location at the specific index.
        /// </summary>
        SbBreakpointLocation GetLocationAtIndex(uint index);

        /// <summary>
        /// Return the breakpoint location with the provided id.
        /// </summary>
        SbBreakpointLocation FindLocationById(int id);

        /// <summary>
        /// Get the hit count of the breakpoint.
        /// </summary>
        uint GetHitCount();

        /// <summary>
        /// Get the breakpoint's ID.
        /// </summary>
        int GetId();

        /// <summary>
        /// Set the breakpoint to ignore the next |ignoreCount| hits.
        /// </summary>
        void SetIgnoreCount(uint ignoreCount);

        /// <summary>
        /// Set if the breakpoint is one shot. If |isOneShot| is true, the breakpoint
        /// gets hit for one time.
        /// </summary>
        void SetOneShot(bool isOneShot);

        /// <summary>
        /// Set or change the condition associated with the breakpoint.
        /// </summary>
        void SetCondition(string condition);

        /// <summary>
        /// Set the commands to be executed when the breakpoint is hit.
        /// </summary>
        void SetCommandLineCommands(IEnumerable<string> commands);
    }
}
