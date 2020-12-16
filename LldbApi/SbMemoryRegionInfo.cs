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

﻿namespace LldbApi
{
    // Interface mirrors SbMemoryRegionInfo as closely as possible.
    public interface SbMemoryRegionInfo
    {
        // <summary>
        // Get the end address of this memory range.
        // </summary>
        ulong GetRegionEnd();

        // <summary>
        // Returns true if this memory region is mapped into the process address space.
        // </summary>
        bool IsMapped();
    }
}
