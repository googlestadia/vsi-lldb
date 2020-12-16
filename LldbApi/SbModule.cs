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
using System.Collections.Generic;

namespace LldbApi
{
    // Interface mirrors the SBModule API as closely as possible.
    public interface SbModule
    {
        // Returns the file specification for the local binary file
        SbFileSpec GetFileSpec();

        // Returns the file specification for the file on the target platform.
        SbFileSpec GetPlatformFileSpec();

        // Sets the file specification for the file on the target platform.
        bool SetPlatformFileSpec(SbFileSpec fileSpec);

        // Returns the file specification for the symbol file.
        SbFileSpec GetSymbolFileSpec();

        // Returns the starting load address of this module's code section.
        ulong GetCodeLoadAddress();

        // Returns the size in bytes of this module's code section.
        ulong GetCodeSize();

        // Returns true if the target architecture is 64-bit, false otherwise.
        bool Is64Bit();

        // Returns true if this module has symbols, false otherwise.
        bool HasSymbols();

        // Returns true if this module has compile units, false otherwise.
        bool HasCompileUnits();

        // Returns the number of compile units.
        uint GetNumCompileUnits();

        // Returns the module's build id
        string GetUUIDString();

        // Returns the section with the name |name| if it exists, null otherwise.
        SbSection FindSection(string name);

        // Returns the number of sections.
        ulong GetNumSections();

        // Returns the section at |index|.
        SbSection GetSectionAtIndex(ulong index);

        // Returns the first code section.
        SbSection GetFirstCodeSection();

        // Returns true if this module and the given module are equal, false otherwise.
        bool EqualTo(SbModule other);
    }

    /// <summary>
    /// Equality comparer that considers two SbModules to be equal if they represent the same
    /// underlying module in LLDB.
    /// </summary>
    public class SbModuleEqualityComparer : IEqualityComparer<SbModule>
    {
        public bool Equals(SbModule x, SbModule y)
        {
            return x.EqualTo(y);
        }

        public int GetHashCode(SbModule module)
        {
            return module.GetUUIDString().GetHashCode();
        }

        public static SbModuleEqualityComparer Instance { get; } = new SbModuleEqualityComparer();
    }
}
