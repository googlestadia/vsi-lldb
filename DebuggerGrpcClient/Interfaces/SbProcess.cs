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

using System;

namespace DebuggerApi
{
    [Flags]
    public enum ProcessEventType : uint
    {
        STATE_CHANGED = (1 << 0),
        INTERRUPT = (1 << 1),
    };

    // Interface mirrors the SBProcess API as closely as possible.
    public interface SbProcess
    {
        // Get the target that this process is assocatied with.
        RemoteTarget GetTarget();

        // Get the total number of threads in the thread list.
        int GetNumThreads();

        // Get a thread at the specified index in the thread list.
        RemoteThread GetThreadAtIndex(int index);

        // Get the thread with the specified id.
        RemoteThread GetThreadById(ulong id);

        // Returns the currently selected thread.
        RemoteThread GetSelectedThread();

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

        /// <summary>
        /// Dumps core to dumpUrl path and returns status of the operation.
        /// </summary>
        /// <param name="dumpUrl">The path where dump will be eventually saved.</param>
        /// <param name="error">The resulting status of a dump saving.</param>
        /// <returns>
        /// Status in error parameter. Either success or error.
        /// </returns>
        void SaveCore(string dumpUrl, out SbError error);
    }
}