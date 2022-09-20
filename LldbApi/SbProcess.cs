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
    /// <summary>
    /// Interface mirrors the SBProcess API as closely as possible.
    /// </summary>
    public interface SbProcess
    {
        /// <summary>
        /// Returns a read only property that represents the target
        /// (lldb.SBTarget) that owns this process.
        /// </summary>
        SbTarget GetTarget();

        /// <summary>
        /// Returns the number of threads in this process as an integer.
        /// </summary>
        int GetNumThreads();

        /// <summary>
        /// Returns the index'th thread from the list of current threads. The index
        /// of a thread is only valid for the current stop. For a persistent thread
        /// identifier use either the thread ID or the IndexID.
        /// </summary>
        SbThread GetThreadAtIndex(int index);

        /// <summary>
        /// Returns the thread with the given thread ID.
        /// </summary>
        SbThread GetThreadById(ulong id);

        /// <summary>
        /// Returns the currently selected thread.
        /// </summary>
        SbThread GetSelectedThread();

        /// <summary>
        /// Sets the currently selected thread in this process by its thread ID.
        /// </summary>
        bool SetSelectedThreadById(ulong threadId);

        /// <summary>
        /// Continues the process.
        /// </summary>
        bool Continue();

        /// <summary>
        /// Pauses the process.
        /// </summary>
        bool Stop();

        /// <summary>
        /// Kills the process and shuts down all threads that were spawned to
        /// track and monitor process.
        /// </summary>
        bool Kill();

        /// <summary>
        /// Detaches from the process and, optionally, keeps it stopped.
        /// </summary>
        /// <param name="keepStopped">Should the process be stopped after Detach.</param>
        /// <returns>Whether the operation succeeded.</returns>
        bool Detach(bool keepStopped);

        /// <summary>
        /// Gets the unique ID associated with this process object.
        ///
        /// Unique IDs start at 1 and increment up with each new process
        /// instance. Since starting a process on a system might always
        /// create a process with the same process ID, there needs to be a
        /// way to tell two process instances apart.
        /// </summary>
        /// <returns>
        /// A non-zero integer ID if this object contains a
        /// valid process object, zero if this object does not contain
        /// a valid process object.
        /// </returns>
        int GetUniqueId();

        /// <summary>
        /// Gets the Unix signals.
        /// </summary>
        SbUnixSignals GetUnixSignals();

        /// <summary>
        /// Reads memory from the current process's address space and removes any
        /// traps that may have been inserted into the memory.
        /// </summary>
        ulong ReadMemory(ulong address, byte[] buffer, ulong size, out SbError error);

        /// <summary>
        /// Writes memory to the current process's address space and maintains any
        /// traps that might be present due to software breakpoints.
        /// </summary>
        ulong WriteMemory(ulong address, byte[] buffer, ulong size, out SbError error);

        /// <summary>
        /// Queries |address| and stores the details of the memory region that contains it
        /// in |memoryRegion|.
        /// </summary>
        /// <returns>
        /// Returns an error object which describes any error that occurred while querying
        /// |address|.
        /// </returns>
        SbError GetMemoryRegionInfo(ulong address, out SbMemoryRegionInfo memoryRegion);

        /// <summary>
        /// Saves dump of a current process to |file_name|.
        /// </summary>
        /// <returns>
        /// An error object that describes any error that occurred during the process.
        /// </returns>
        SbError SaveCore(string fileName);
    }
}
