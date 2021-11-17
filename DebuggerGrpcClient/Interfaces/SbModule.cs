// Copyright 2021 Google LLC
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
    /// Interface mirrors the SBModule API as closely as possible.
    /// </summary>
    public interface SbModule
    {
        /// <summary>
        /// Returns the file specification for the local binary file
        /// </summary>
        SbFileSpec GetFileSpec();

        /// <summary>
        /// Returns the file specification for the file on the target platform.
        /// </summary>
        SbFileSpec GetPlatformFileSpec();

        /// <summary>
        /// Sets the file specification for the file on the target platform.
        /// </summary>
        bool SetPlatformFileSpec(SbFileSpec fileSpec);

        /// <summary>
        /// Sets the file specification for the file on the target platform.
        /// </summary>
        bool SetPlatformFileSpec(string fileDirectory, string fileName);

        /// <summary>
        /// Returns the file specification for the symbol file.
        /// </summary>
        SbFileSpec GetSymbolFileSpec();

        /// <summary>
        /// Returns the starting load address of this module's code section.
        /// </summary>
        ulong GetCodeLoadAddress();

        /// <summary>
        /// Returns the starting address of the module's object file.
        /// </summary>
        SbAddress GetObjectFileHeaderAddress();

        /// <summary>
        /// Returns the size in bytes of this module's code section.
        /// </summary>
        ulong GetCodeSize();

        /// <summary>
        /// Returns true if the target architecture is 64-bit, false otherwise.
        /// </summary>
        bool Is64Bit();

        /// <summary>
        /// Returns true if this module has symbols, false otherwise.
        /// </summary>
        bool HasSymbols();

        /// <summary>
        /// Returns true if this module has compile units, false otherwise.
        /// </summary>
        bool HasCompileUnits();

        /// <summary>
        /// Returns the number of compile units. 
        /// </summary>
        uint GetNumCompileUnits();

        /// <summary>
        /// Returns the module's build id.
        /// </summary>
        string GetUUIDString();
        
        /// <summary>
        /// Returns the module's triple info. 
        /// </summary>
        string GetTriple();

        /// <summary>
        /// Returns the section with the name |name| if it exists, null otherwise.
        /// </summary>
        SbSection FindSection(string name);

        /// <summary>
        /// Returns the number of sections.
        /// </summary>
        ulong GetNumSections();

        /// <summary>
        /// Returns the section at |index|.
        /// </summary>
        SbSection GetSectionAtIndex(ulong index);

        /// <summary>
        /// A unique identifier for the module. Not a part of the LLDB API.
        /// </summary>
        long GetId();
    }
}
