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

namespace DebuggerApi
{
    /// <summary>
    /// Interface mirrors the SBBreakpointLocation API as closely as possible.
    /// </summary>
    public interface SbBreakpointLocation
    {
        /// <summary>
        /// Enable / disable the breakpoint.
        /// </summary>
        void SetEnabled(bool enabled);

        /// <summary>
        /// Get the parent breakpoint.
        /// </summary>
        RemoteBreakpoint GetBreakpoint();

        /// <summary>
        /// Get the breakpoint location's ID.
        /// </summary>
        int GetId();

        /// <summary>
        /// Get the SbAddress of the breakpoint location.
        /// </summary>
        SbAddress GetAddress();

        /// <summary>
        /// Get the address in memory of the breakpoint location.
        /// </summary>
        ulong GetLoadAddress();

        /// <summary>
        /// Set or change the condition associated to this breakpoint location.
        /// </summary>
        void SetCondition(string condition);

        /// <summary>
        /// Set the breakpoint location to ignore the next |ignoreCount| hits.
        /// </summary>
        void SetIgnoreCount(uint ignoreCount);
    }
}