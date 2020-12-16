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

using DebuggerCommonApi;
using DebuggerGrpcServer.RemoteInterfaces;
using LldbApi;
using System.Collections.Generic;

namespace DebuggerGrpcServer
{
    // Enumeration of breakpoint errors
    public enum BreakpointError
    {
        Success = 0,
        NoFunctionLocation = 1,
        NoFunctionFound = 2,
        PositionNotAvailable = 3,
    };

    // <summary>
    // Interface based off SBTarget API.
    // </summary>
    public interface RemoteTarget
    {
        // <summary>
        // Attaches to the remote process with the given pid.
        // </summary>
        SbProcess AttachToProcessWithID(SbListener listener, ulong pid, out SbError error);

        // <summary>
        // Set a breakpoint at the specified file and line number.
        // </summary>
        RemoteBreakpoint BreakpointCreateByLocation(string file, uint line);

        // <summary>
        // Set breakpoints at all functions that match the specified symbol name.
        // </summary>
        RemoteBreakpoint BreakpointCreateByName(string symbolName);

        // <summary>
        // Set breakpoint at specified memory address.
        // </summary>
        RemoteBreakpoint BreakpointCreateByAddress(ulong address);

        // <summary>
        // Set breakpoint at the first occurence of a function that matches
        // |symbolName| with offset |offset|.
        // </summary>
        BreakpointErrorPair CreateFunctionOffsetBreakpoint(string symbolName, uint offset);

        // <summary>
        // Returns the existing breakpoint that has the given id.
        // </summary>
        RemoteBreakpoint FindBreakpointById(int id);

        /// <summary>
        /// Delete the breakpoint with |breakpointId|. Return true if said breakpoint is found and
        /// deleted. Return false otherwise.
        /// </summary>
        bool BreakpointDelete(int breakpointId);

        // <summary>
        // Get the total number of modules in the module list.
        // </summary>
        int GetNumModules();

        // <summary>
        // Get a module at the specified index in the module list.
        // </summary>
        SbModule GetModuleAtIndex(int index);

        // <summary>
        // A unique identifier for the listener.  Not part of the LLDB API.
        // </summary>
        long GetId();

        // <summary>
        // Set a watchpoint watching the specified address and size.
        // </summary>
        SbWatchpoint WatchAddress(long address, ulong size, bool read, bool write,
            out SbError error);

        // <summary>
        // Delete the watchpoint with |watchId|. Return true if such watchpoint is found and
        // deleted. Return false otherwise.
        // </summary>
        bool DeleteWatchpoint(int watchId);

        // <summary>
        // Looks up an SbAddress object from the given PC address.
        // </summary>
        SbAddress ResolveLoadAddress(ulong address);

        // <summary>
        // Returns a list of disassembled instructions.
        // </summary>
        List<SbInstruction> ReadInstructions(SbAddress baseAddress, uint count, string flavor);

        // <summary>
        // Load a core dump file.
        // </summary>
        SbProcess LoadCore(string coreFile);

        // <summary>
        // Add the binary located at |path| as a new module. |triple| and |uuid| are optional, and
        // may be empty strings. Return the new module on success. Return null on failure.
        // </summary>
        SbModule AddModule(string path, string triple, string uuid);

        // <summary>
        // Remove |module| from the target. Return true if the module was removed successfully.
        // Return false otherwise.
        // </summary>
        bool RemoveModule(SbModule module);

        // <summary>
        // Slide all file addresses for all module sections so that |module| appears to loaded at
        // these slide addresses.
        // </summary>
        SbError SetModuleLoadAddress(SbModule module, long sectionsOffset);

        // <summary>
        // Fetch all the information required for displaying disassembly in a single gRPC call.
        // </summary>
        List<InstructionInfo> ReadInstructionInfos(SbAddress address, uint count, string flavor);

        // <summary>
        // Returns the underlying SbTarget
        // </summary>
        SbTarget GetSbTarget();
    }
}
