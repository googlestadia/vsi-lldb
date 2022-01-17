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

using DebuggerApi;
using DebuggerCommonApi;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace YetiVSI.DebugEngine
{
    // Can be used to compare |InstructionInfo| objects by their address.
    public class InstructionComparator : IComparer<InstructionInfo>
    {
        public int Compare(InstructionInfo x, InstructionInfo y)
        {
            return x.Address.CompareTo(y.Address);
        }
    }

    /// <summary>
    /// DebugDisassemblyStream allows VS to query for instruction disassembly at a specific PC
    /// (IDebugCodeContext2). This implementation uses the PC address as the 'Code Location ID'.
    /// Seek and Read operations will modify the current location.
    /// </summary>
    public class DebugDisassemblyStream : IDebugDisassemblyStream2
    {
        public class Factory
        {
            readonly DebugCodeContext.Factory _codeContextFactory;
            readonly DebugDocumentContext.Factory _documentContextFactory;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            public Factory(DebugCodeContext.Factory codeContextFactory,
                           DebugDocumentContext.Factory documentContextFactory)
            {
                _codeContextFactory = codeContextFactory;
                _documentContextFactory = documentContextFactory;
            }

            public virtual IDebugDisassemblyStream2 Create(enum_DISASSEMBLY_STREAM_SCOPE scope,
                                                           IDebugCodeContext2 codeContext,
                                                           RemoteTarget target)
            {
                return new DebugDisassemblyStream(_codeContextFactory, _documentContextFactory,
                                                  scope, codeContext, target);
            }
        }

        const int _kMaxDisassemblySourceBlockLines = 10;
        readonly string _codeContextName = string.Empty;
        readonly DebugCodeContext.Factory _codeContextFactory;
        readonly DebugDocumentContext.Factory _documentContextFactory;
        readonly enum_DISASSEMBLY_STREAM_SCOPE _scope;
        readonly RemoteTarget _target;
        ulong _address;
        readonly Dictionary<ulong, LineEntryInfo> _lineEntryCache;

        DebugDisassemblyStream(DebugCodeContext.Factory codeContextFactory,
                               DebugDocumentContext.Factory documentContextFactory,
                               enum_DISASSEMBLY_STREAM_SCOPE scope, IDebugCodeContext2 codeContext,
                               RemoteTarget target)
        {
            _codeContextFactory = codeContextFactory;
            _documentContextFactory = documentContextFactory;
            _scope = scope;
            _target = target;

            // Used to cache line entries from the last read call
            _lineEntryCache = new Dictionary<ulong, LineEntryInfo>();

            // Extract the address from {codeContext}.
            _address = codeContext.GetAddress();
        }

        public int GetCodeContext(ulong codeLocationId, out IDebugCodeContext2 codeContext)
        {
            // The codeContext is used, among other things, to link assembly instructions and
            // source code. The "Go To Source Code" functionality in disassembly view needs this
            // to be properly implemented.
            IDebugDocumentContext2 documentContext = null;
            if (_lineEntryCache.TryGetValue(codeLocationId, out LineEntryInfo lineEntry))
            {
                documentContext = _documentContextFactory.Create(lineEntry);
            }
            else
            {
                SbAddress address = _target.ResolveLoadAddress(codeLocationId);
                if (address != null)
                {
                    lineEntry = address.GetLineEntry();
                    documentContext = _documentContextFactory.Create(lineEntry);
                }
            }

            string AddressToFuncName() =>
                _target.ResolveLoadAddress(codeLocationId)?.GetFunction()?.GetName() ??
                _codeContextName;

            codeContext = _codeContextFactory.Create(codeLocationId,
                                                     new Lazy<string>(AddressToFuncName),
                                                     documentContext, Guid.Empty);
            return VSConstants.S_OK;
        }

        public int GetCodeLocationId(IDebugCodeContext2 codeContext, out ulong codeLocationId)
        {
            codeLocationId = codeContext.GetAddress();
            return VSConstants.S_OK;
        }

        public int GetCurrentLocation(out ulong codeLocationId)
        {
            codeLocationId = _address;
            return VSConstants.S_OK;
        }

        public int GetDocument(string documentUrl, out IDebugDocument2 document)
        {
            document = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetScope(enum_DISASSEMBLY_STREAM_SCOPE[] scope)
        {
            scope[0] = _scope;
            return VSConstants.S_OK;
        }

        public int GetSize(out ulong size)
        {
            size = 0xFFFFFFFF;
            return VSConstants.S_OK;
        }

        struct Instruction
        {
            public ulong Address;
            public string Text;
            public string Symbol;

            public bool HasSource;
            public bool DocumentChanged;
            public string Document;
            public uint ByteOffset;
            public uint StartLine;
            public uint EndLine;
        }

        struct SourcePosition
        {
            // Url of the source file.
            public string Url;
            // Zero-based line number.
            public uint Line;

            public bool IsEmpty() => Url.Length == 0;

            public static readonly SourcePosition Empty = new SourcePosition { Url = "", Line = 0 };
        }

        static SourcePosition GetPositionForAddress(SbAddress address) =>
            GetPositionFor(address?.GetLineEntry());

        static SourcePosition GetPositionFor(LineEntryInfo lineEntry)
        {
            if (lineEntry == null)
            {
                return SourcePosition.Empty;
            }

            string directory = lineEntry.Directory;
            string filename = lineEntry.FileName;
            uint line = lineEntry.Line;
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(filename) || line == 0)
            {
                return SourcePosition.Empty;
            }

            // Note: VS (at least VS2017) does not handle file paths with forward slashes
            // well - opening of disassembly gets very slow with them. Let's just use
            // backslashes (prepended with file://).
            string url = "file://" + System.IO.Path.Combine(directory, filename);
            return new SourcePosition { Url = url, Line = line - 1 };
        }

        static readonly string _flavor = "intel";
        List<Instruction> GetInstructions(SbAddress sbAddress, uint numInstructions,
                                          bool withSource)
        {
            _lineEntryCache.Clear();
            var instructions = new List<Instruction>();

            // If we need source information, we look at the previous instruction to find out
            // if it is on the same line and/or in the same file.
            var position = SourcePosition.Empty;
            if (withSource)
            {
                // Find previous instruction and initialize lineAddress and lastLine here.
                SbAddress previousSbAddress =
                    _target.ResolveLoadAddress(sbAddress.GetLoadAddress(_target) - 1);
                if (previousSbAddress != null)
                {
                    position = GetPositionForAddress(previousSbAddress);
                }
            }

            uint numInstructionsRead = 0;
            List<InstructionInfo> cachedInstructions =
                _target.ReadInstructionInfos(sbAddress, numInstructions, _flavor);

            for (int i = 0; i < cachedInstructions.Count; i++)
            {
                Instruction currentInstruction = new Instruction();

                if (numInstructionsRead >= numInstructions)
                {
                    break;
                }

                numInstructionsRead++;

                InstructionInfo instruction = cachedInstructions[i];

                _lineEntryCache.Add(instruction.Address, instruction.LineEntry);

                currentInstruction.Address = instruction.Address;

                // Since Visual Studio doesn't do a good job formatting opcode and operands, in
                // addition to not providing a field to show instruction comments, do all the
                // formatting ourselves and put the entire string in the opcode field.
                string operands = instruction.Operands;
                string comment = instruction.Comment;
                string instructionString = $"{instruction.Mnemonic,-10}";
                if (string.IsNullOrEmpty(comment))
                {
                    instructionString += $" {operands}";
                }
                else
                {
                    instructionString += $" {operands,-30} # {comment}";
                }
                currentInstruction.Text = instructionString;

                if (!string.IsNullOrEmpty(instruction.SymbolName))
                {
                    currentInstruction.Symbol = instruction.SymbolName;
                }

                // If we so far believe we should get source position, let us get it here.
                if (withSource)
                {
                    SourcePosition lastPosition = position;
                    position = GetPositionFor(instruction.LineEntry);
                    WritePositionToInstruction(lastPosition, position, i == 0,
                                               ref currentInstruction);
                }
                instructions.Add(currentInstruction);
            }
            return instructions;
        }

        static uint GetDefaultStartLineFromEndLine(uint endLine)
        {
            return endLine > _kMaxDisassemblySourceBlockLines
                       ? endLine - _kMaxDisassemblySourceBlockLines
                       : 0u;
        }

        void WritePositionToInstruction(SourcePosition previousPosition,
                                        SourcePosition currentPosition, bool forceExplicitDocument,
                                        ref Instruction instruction)
        {
            instruction.EndLine = currentPosition.Line;
            instruction.StartLine = currentPosition.Line;

            instruction.HasSource = !currentPosition.IsEmpty();
            if (currentPosition.Url == previousPosition.Url)
            {
                instruction.ByteOffset = currentPosition.Line == previousPosition.Line ? 1u : 0u;
                if (previousPosition.Line < currentPosition.Line)
                {
                    instruction.StartLine =
                        Math.Max(GetDefaultStartLineFromEndLine(currentPosition.Line),
                                 previousPosition.Line + 1);
                    instruction.ByteOffset = 0;
                }
                instruction.DocumentChanged = false;
                instruction.Document = forceExplicitDocument ? currentPosition.Url : null;
            }
            else
            {
                // We are switching files (or going from location with position to one without
                // position or vice versa).
                instruction.ByteOffset = 0;
                instruction.StartLine = GetDefaultStartLineFromEndLine(currentPosition.Line);
                instruction.DocumentChanged = true;
                instruction.Document = currentPosition.Url;
            }
        }

        public int Read(uint numInstructions, enum_DISASSEMBLY_STREAM_FIELDS fields,
                        out uint numInstructionsRead, DisassemblyData[] disassembly)
        {
            numInstructionsRead = 0;
            SbAddress sbAddress = _target.ResolveLoadAddress(_address);
            if (sbAddress == null)
            {
                return VSConstants.S_FALSE;
            }

            bool withSource = ((fields & enum_DISASSEMBLY_STREAM_FIELDS.DSF_POSITION) != 0) ||
                              ((fields & enum_DISASSEMBLY_STREAM_FIELDS.DSF_DOCUMENTURL) != 0) ||
                              ((fields & enum_DISASSEMBLY_STREAM_FIELDS.DSF_BYTEOFFSET) != 0);

            List<Instruction> instructions =
                GetInstructions(sbAddress, numInstructions, withSource);

            for (int i = 0; i < instructions.Count; i++)
            {
                Instruction instruction = instructions[i];
                _address = instruction.Address;
                if ((fields & enum_DISASSEMBLY_STREAM_FIELDS.DSF_ADDRESS) != 0)
                {
                    disassembly[i].dwFields |= enum_DISASSEMBLY_STREAM_FIELDS.DSF_ADDRESS;
                    disassembly[i].bstrAddress = $"0x{instruction.Address:x16}";
                }

                if ((fields & enum_DISASSEMBLY_STREAM_FIELDS.DSF_CODELOCATIONID) != 0)
                {
                    disassembly[i].dwFields |= enum_DISASSEMBLY_STREAM_FIELDS.DSF_CODELOCATIONID;
                    disassembly[i].uCodeLocationId = instruction.Address;
                }

                if ((fields & enum_DISASSEMBLY_STREAM_FIELDS.DSF_OPCODE) != 0)
                {
                    disassembly[i].dwFields |= enum_DISASSEMBLY_STREAM_FIELDS.DSF_OPCODE;
                    disassembly[i].bstrOpcode = instruction.Text;
                }
                if ((fields & enum_DISASSEMBLY_STREAM_FIELDS.DSF_SYMBOL) != 0)
                {
                    if (instruction.Symbol != null)
                    {
                        disassembly[i].dwFields |= enum_DISASSEMBLY_STREAM_FIELDS.DSF_SYMBOL;
                        disassembly[i].bstrSymbol = instruction.Symbol;
                    }
                }
                if ((fields & enum_DISASSEMBLY_STREAM_FIELDS.DSF_DOCUMENTURL) != 0)
                {
                    disassembly[i].bstrDocumentUrl = instruction.Document;
                    disassembly[i].dwFields |= enum_DISASSEMBLY_STREAM_FIELDS.DSF_DOCUMENTURL;
                }
                if ((fields & enum_DISASSEMBLY_STREAM_FIELDS.DSF_POSITION) != 0)
                {
                    if (instruction.HasSource)
                    {
                        disassembly[i].posBeg.dwColumn = 0;
                        disassembly[i].posBeg.dwLine = instruction.StartLine;
                        disassembly[i].posEnd.dwColumn = 0;
                        disassembly[i].posEnd.dwLine = instruction.EndLine;
                        disassembly[i].dwFields |= enum_DISASSEMBLY_STREAM_FIELDS.DSF_POSITION;
                    }
                }
                if ((fields & enum_DISASSEMBLY_STREAM_FIELDS.DSF_FLAGS) != 0)
                {
                    disassembly[i].dwFlags =
                        instruction.HasSource ? enum_DISASSEMBLY_FLAGS.DF_HASSOURCE : 0;
                    if (instruction.DocumentChanged)
                    {
                        disassembly[i].dwFlags |= enum_DISASSEMBLY_FLAGS.DF_DOCUMENTCHANGE;
                    }
                    disassembly[i].dwFields |= enum_DISASSEMBLY_STREAM_FIELDS.DSF_FLAGS;
                }
                if ((fields & enum_DISASSEMBLY_STREAM_FIELDS.DSF_BYTEOFFSET) != 0)
                {
                    if (instruction.HasSource)
                    {
                        disassembly[i].dwByteOffset = instruction.ByteOffset;
                        disassembly[i].dwFields |= enum_DISASSEMBLY_STREAM_FIELDS.DSF_BYTEOFFSET;
                    }
                }
            }

            numInstructionsRead = Convert.ToUInt32(instructions.Count);
            return numInstructionsRead > 0 ? VSConstants.S_OK : VSConstants.S_FALSE;
        }

        public int Seek(enum_SEEK_START seekStart, IDebugCodeContext2 codeContext,
                        ulong codeLocationId, long numInstructions)
        {
            if (seekStart == enum_SEEK_START.SEEK_START_CODECONTEXT)
            {
                _address = codeContext.GetAddress();
            }
            else if (seekStart == enum_SEEK_START.SEEK_START_CODELOCID)
            {
                _address = codeLocationId;
            }

            SbAddress sbAddress = _target.ResolveLoadAddress(_address);
            if (sbAddress == null)
            {
                return VSConstants.E_FAIL;
            }

            if (numInstructions > 0)
            {
                // seek forward
                numInstructions++;
                List<InstructionInfo> instructions =
                    _target.ReadInstructionInfos(sbAddress, (uint)numInstructions, _flavor);
                if (instructions.Count > 0)
                {
                    numInstructions = Math.Min(numInstructions, instructions.Count);
                    _address = instructions[(int)numInstructions - 1].Address;
                }
            }
            else if (numInstructions < 0)
            {
                // TODO: Get opcode sizes from LLDB.
                // Hard-code the opcode sizes for x86_64. Currently LLDB doesn't expose this
                // information, and this is the only architecture we support.
                uint minOpcodeSize = 1;
                uint maxOpcodeSize = 15;

                // When seeking backwards we don't know the exact address since x86_64 is a variable
                // size instruction architecture. Instead we figure out the max range to fit a
                // specific number of instructions.
                uint maxRangeForInstructions = (uint)Math.Abs(numInstructions) * maxOpcodeSize;

                // Since x86_64 is a variable size instruction architecture we don't know the exact
                // number of instructions in a specific address range. Assume the smallest opcode
                // size and that will be the number of instructions we need to read.
                uint maxNumberInstructions = maxRangeForInstructions / minOpcodeSize;

                // Using the start address and the max possible range, we can determine the lower
                // bound for where we should start reading instructions from.
                ulong endAddress = _address - maxRangeForInstructions;

                // The instruction where we should start the seek.
                List<InstructionInfo> startInstructionList =
                    _target.ReadInstructionInfos(sbAddress, 1, _flavor);
                if (startInstructionList.Count == 0)
                {
                    Trace.WriteLine(
                        "Failed to seek backwards. Unable to read " +
                        $"instruction at 0x{_address:X} so we have no start point for the seek");
                    return VSConstants.E_FAIL;
                }

                InstructionInfo startInstruction = startInstructionList[0];

                // We know there is an instruction around the |endAddress| but we don't know exactly
                // where it starts (because variable size instructions). LLDB will stop reading if
                // it runs into a bad instruction. We use that to our advantage and start reading
                // instructions from the |endAddress| + offset until LLDB returns us the number of
                // instructions we requested. We can then be fairly certain |endAddress| + offset is
                // the address to a valid instruction.
                int startIndex = -1;
                List<InstructionInfo> validInstructions = null;
                for (ulong i = 0; i < maxOpcodeSize; i++)
                {
                    ulong seekAddress = endAddress + i;
                    SbAddress seekSbAddress = _target.ResolveLoadAddress(seekAddress);
                    List<InstructionInfo> instructions = _target.ReadInstructionInfos(
                        seekSbAddress, maxNumberInstructions + 1, _flavor);
                    // Shortcut: Continue if we did not get enough instructions.
                    if (instructions == null || instructions.Count < Math.Abs(numInstructions))
                    {
                        continue;
                    }
                    // Only accept the instructions if our start instruction is there.
                    startIndex = instructions.BinarySearch(startInstruction,
                                                            new InstructionComparator());
                    if (startIndex >= 0)
                    {
                        validInstructions = instructions;
                        break;
                    }
                }

                if (startIndex < 0)
                {
                    Trace.WriteLine(
                        "Failed to seek backwards. Unable to find an instruction with " +
                        $"address 0x{_address:X} so we have no start point for the seek");
                    return VSConstants.E_FAIL;
                }

                // Add the |startIndex| and the negative |numInstructions| to get the index of the
                // instruction to which we want to seek.
                int seekIndex = startIndex + (int)numInstructions;
                if (validInstructions == null || seekIndex < 0 ||
                    seekIndex >= validInstructions.Count)
                {
                    Trace.WriteLine(
                        $"Failed to seek backwards. Seek index {seekIndex} is out of range");
                    return VSConstants.E_FAIL;
                }
                _address = validInstructions[seekIndex].Address;
            }

            return VSConstants.S_OK;
        }
    }
}
