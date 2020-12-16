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

namespace LldbApi
{
    // Interface mirrors the SBBreakpointLocation API as closely as possible.
    public interface SbBreakpointLocation
    {
        // Enable / disable the breakpoint.
        void SetEnabled(bool enabled);

        // Get the parent breakpoint.
        SbBreakpoint GetBreakpoint();

        // Get the breakpoint location's ID.
        int GetId();

        // Get the SbAddress of the breakpoint location.
        SbAddress GetAddress();

        // Get the address in memory of the breakpoint location.
        ulong GetLoadAddress();

        // Set or change the condition associated to this breakpoint location.
        void SetCondition(string condition);

        // Set the breakpoint location to ignore the next |ignoreCount| hits.
        void SetIgnoreCount(uint ignoreCount);
    }
}
