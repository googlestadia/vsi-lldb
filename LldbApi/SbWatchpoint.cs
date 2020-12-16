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
    // Interface mirrors the SBWatchpoint API as closely as possible.
    public interface SbWatchpoint
    {
        // Get the SBWatchpoint's ID.
        int GetId();

        // Get the hit count of the breakpoint.
        uint GetHitCount();

        // Enable / disable the watchpoint.
        void SetEnabled(bool enabled);

        // Set or change the condtion associated with the breakpoint.
        void SetCondition(string condition);

        // Set the breakpoint to ignore the next |ignoreCount| hits.
        void SetIgnoreCount(uint ignoreCount);
    }
}