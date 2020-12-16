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

namespace LldbApi
{
    // Interface mirrors the SBBreakpoint API as closely as possible.
    public interface SbBreakpoint
    {
        // Enable / disable the breakpoint.
        void SetEnabled(bool enabled);

        // Get the number of locations to which this breakpoint is bound.
        uint GetNumLocations();

        // Get the location at the specific index.
        SbBreakpointLocation GetLocationAtIndex(uint index);

        // Return the breakpoint location with the provided id.
        SbBreakpointLocation FindLocationById(int id);

        // Get the hit count of the breakpoint.
        uint GetHitCount();

        // Get the breakpoint's ID.
        int GetId();

        // Set the breakpoint to ignore the next |ignoreCount| hits.
        void SetIgnoreCount(uint ignoreCount);

        // Set if the breakpoint is one shot. If |isOneShot| is true, the breakpoint
        // gets hit for one time.
        void SetOneShot(bool isOneShot);

        // Set or change the condtion associated with the breakpoint.
        void SetCondition(string condition);

        // Set the commands to be executed when the breakpoint is hit.
        void SetCommandLineCommands(List<string> commands);
    }
}
