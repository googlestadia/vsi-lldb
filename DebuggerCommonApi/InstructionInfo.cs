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

namespace DebuggerCommonApi
{
    public class InstructionInfo
    {
        // Essential data for displaying disassembly
        public ulong Address;
        public string Operands;
        public string Comment;
        public string Mnemonic;
        public string SymbolName;
        public LineEntryInfo LineEntry;

        public InstructionInfo()
        {
        }

        public InstructionInfo(ulong address, string operands, string comment, string mnemonic,
                               string symbolName, LineEntryInfo lineEntry)
        {
            Address = address;
            Operands = operands;
            Comment = comment;
            Mnemonic = mnemonic;
            SymbolName = symbolName;
            LineEntry = lineEntry;
        }
    }
}