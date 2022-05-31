// Copyright 2022 Google LLC
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

using System.Diagnostics;

namespace YetiCommon
{
    public enum ModuleFormat
    {
        Elf,
        Pe,
        Pdb
    }

    public class ModuleSearchQuery
    {
        public ModuleSearchQuery(string filename, BuildId buildId,
                                 ModuleFormat format)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(filename));
            FileName = filename;
            BuildId = buildId;
            ModuleFormat = format;
        }

        /// <summary>
        /// Module filename (we are looking for exact match).
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Module BuildId.
        /// </summary>
        public BuildId BuildId { get; }

        /// <summary>
        /// Should contain debug information or not.
        /// </summary>
        public bool RequireDebugInfo { get; set; }

        /// <summary>
        /// When set to False and previous search of this symbol in a remote
        /// SymbolStore failed, we won't try to look again and immediately return
        /// NOT_FOUND result.
        /// </summary>
        public bool ForceLoad { get; set; }

        /// <summary>
        /// Specifies in which format the file is stored (ELF/PE/PDB).
        /// </summary>
        public ModuleFormat ModuleFormat { get; }
    }
}