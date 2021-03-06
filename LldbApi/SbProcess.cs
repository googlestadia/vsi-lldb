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
    // Interface mirrors the SBProcess API as closely as possible.
    public interface SbProcess
    {
        // Get the target that this process is assocatied with.
        SbTarget GetTarget();

        // Get the total number of threads in the thread list.
        int GetNumThreads();

        // Get a thread at the specified index in the thread list.
        SbThread GetThreadAtIndex(int index);

        // Get the thread with the specified id.
        SbThread GetThreadById(ulong id);

        // Returns the currently selected thread.
        SbThread GetSelectedThread();

        // Set the currently selected thread by its thread ID.
        bool SetSelectedThreadById(ulong threadId);

        // Continues the process.
        // Returns true if successful, false otherwise.
        bool Continue();

        // Pauses the process.
        // Retruns true is successful, false otherwise.
        bool Stop();

        // Kills the process.
        // Returns true if successful, false otherwise.
        bool Kill();

        // Detaches the process.
        // Retruns true if successful, false otherwise.
        bool Detach();

        // Gets a unique ID across all process instances.
        int GetUniqueId();

        // Gets the Unix signals.
        SbUnixSignals GetUnixSignals();

        // Reads memory from the current process's address space and removes any traps that may
        // have been inserted into the memory.
        ulong ReadMemory(ulong address, byte[] buffer, ulong size, out SbError error);

        // Writes memory to the current process's address space and maintains any traps that might
        // be present due to software breakpoints.
        ulong WriteMemory(ulong address, byte[] buffer, ulong size, out SbError error);

        // Queries |address| and stores the details of the memory region that contains it
        // in |memoryRegion|.
        // Returns an error object which describes any error that occured while querying |address|.
        SbError GetMemoryRegionInfo(ulong address, out SbMemoryRegionInfo memoryRegion);
    }
}
