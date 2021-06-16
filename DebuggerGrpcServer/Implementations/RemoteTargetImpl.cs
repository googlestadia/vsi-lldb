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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DebuggerGrpcServer
{
    public class RemoteTargetFactory
    {
        readonly RemoteBreakpointFactory _breakpointFactory;

        public RemoteTargetFactory(RemoteBreakpointFactory breakpointFactory)
        {
            _breakpointFactory = breakpointFactory;
        }

        public RemoteTarget Create(SbTarget sbTarget) =>
            sbTarget != null ? new RemoteTargetImpl(sbTarget, _breakpointFactory) : null;
    }

    class RemoteTargetImpl : RemoteTarget
    {
        const ulong _pageSize = 4096;
        const ulong _maxOpcodeSize = 15;
        readonly SbTarget _sbTarget;
        readonly RemoteBreakpointFactory _breakpointFactory;

        internal RemoteTargetImpl(SbTarget sbTarget, RemoteBreakpointFactory breakpointFactory)
        {
            _sbTarget = sbTarget;
            _breakpointFactory = breakpointFactory;
        }

#region RemoteTarget

        public SbProcess AttachToProcessWithID(SbListener listener, ulong pid, out SbError error) =>
            _sbTarget.AttachToProcessWithID(listener, pid, out error);

        public RemoteBreakpoint BreakpointCreateByLocation(string file, uint line) =>
            _breakpointFactory.Create(_sbTarget.BreakpointCreateByLocation(file, line));

        public RemoteBreakpoint BreakpointCreateByName(string symbolName) =>
            _breakpointFactory.Create(_sbTarget.BreakpointCreateByName(symbolName));

        public RemoteBreakpoint BreakpointCreateByAddress(ulong address) =>
            _breakpointFactory.Create(_sbTarget.BreakpointCreateByAddress(address));

        public RemoteBreakpoint FindBreakpointById(int id) =>
            _breakpointFactory.Create(_sbTarget.FindBreakpointById(id));

        public bool BreakpointDelete(int breakpointId) => _sbTarget.BreakpointDelete(breakpointId);

        public int GetNumModules() => _sbTarget.GetNumModules();

        public SbModule GetModuleAtIndex(int index) => _sbTarget.GetModuleAtIndex(index);

        public long GetId() => _sbTarget.GetId();

        public SbWatchpoint WatchAddress(long address, ulong size, bool read, bool write,
                                         out SbError error) => _sbTarget.WatchAddress(address, size,
                                                                                      read, write,
                                                                                      out error);

        public bool DeleteWatchpoint(int watchId) => _sbTarget.DeleteWatchpoint(watchId);

        public SbAddress ResolveLoadAddress(ulong address) => _sbTarget.ResolveLoadAddress(address);

        public List<SbInstruction> ReadInstructions(SbAddress baseAddress, uint count,
                                                    string flavor) =>
            _sbTarget.ReadInstructions(baseAddress, count, flavor);

        public SbProcess LoadCore(string coreFile) => _sbTarget.LoadCore(coreFile);

        public SbModule AddModule(string path, string triple,
                                  string uuid) => _sbTarget.AddModule(path, triple, uuid);

        public bool RemoveModule(SbModule module) => _sbTarget.RemoveModule(module);

        public SbError SetModuleLoadAddress(SbModule module, long sectionsOffset) =>
            _sbTarget.SetModuleLoadAddress(module, sectionsOffset);

        public SbTarget GetSbTarget() => _sbTarget;

        public List<InstructionInfo> ReadInstructionInfos(SbAddress address, uint count,
                                                          string flavor)
        {
            SbProcess process = _sbTarget.GetProcess();
            var instructions = new List<InstructionInfo>();
            long lastPageChecked = -1;
            while (instructions.Count < count)
            {
                ulong currentAddress = address.GetLoadAddress(_sbTarget);
                ulong currentPage = currentAddress / _pageSize;

                if (lastPageChecked != (long)currentPage)
                {
                    SbError error = process.GetMemoryRegionInfo(
                        currentAddress, out SbMemoryRegionInfo memoryRegion);

                    if (error.Fail())
                    {
                        Trace.WriteLine("Unable to retrieve memory region info.");
                        return new List<InstructionInfo>();
                    }

                    lastPageChecked = (long)currentPage;

                    // If the address we are given is not mapped we should not try to disassemble
                    // it.
                    if (!memoryRegion.IsMapped())
                    {
                        uint instructionsLeft = count - (uint)instructions.Count;

                        ulong nextAddress = AddUnmappedInstructions(address, instructionsLeft,
                                                                    memoryRegion, instructions);

                        address = _sbTarget.ResolveLoadAddress(nextAddress);

                        // Continue in case we still need more instructions
                        continue;
                    }
                }

                ulong nextPage = currentPage + 1;
                ulong endPageAddress = nextPage * _pageSize - 1;

                ulong size = endPageAddress - currentAddress + 1;
                var buffer = new byte[size];

                ulong readSize = _sbTarget.GetProcess().ReadMemory(currentAddress, buffer, size,
                                                                   out SbError readMemoryError);

                if (readMemoryError.Fail() && readSize == 0)
                {
                    Trace.WriteLine("Unable to read mapped region");
                    return new List<InstructionInfo>();
                }

                List<SbInstruction> sbInstructions =
                    _sbTarget.GetInstructionsWithFlavor(address, buffer, readSize, flavor);

                int missingInstructions = (int)count - instructions.Count;
                foreach (SbInstruction sbInstruction in sbInstructions.Take(missingInstructions))
                {
                    var instruction = PrepareInstruction(sbInstruction);
                    // Unexpected error occured, PrepareInstruction should write a log.
                    if (instruction == null)
                    {
                        return  new List<InstructionInfo>();
                    }

                    instructions.Add(instruction);
                }

                // Let's check if there is an instruction that is split between two pages.
                if (instructions.Count < count)
                {
                    bool hasBoundaryInstruction = ReadInstructionOnPageBound(
                        address, sbInstructions, instructions, endPageAddress, flavor,
                        out InstructionInfo instruction, out ulong nextInstruction);
                    if (hasBoundaryInstruction)
                    {
                        if (instruction == null)
                        {
                            return new List<InstructionInfo>();
                        }

                        instructions.Add(instruction);

                        address = _sbTarget.ResolveLoadAddress(nextInstruction);
                        if (instructions.Count < count)
                        {
                            continue;
                        }
                    }

                }

                if (instructions.Count < count)
                {
                    ulong nextAddress = AddInvalidInstruction(address, sbInstructions, instructions,
                                                              endPageAddress + 1);

                    // Set the address to the next address after the invalid instruction
                    address = _sbTarget.ResolveLoadAddress(nextAddress);
                }
            }
            return instructions;
        }

        public BreakpointErrorPair CreateFunctionOffsetBreakpoint(string symbolName, uint offset)
        {
            RemoteBreakpoint functionBreakpoint = BreakpointCreateByName(symbolName);

            if (functionBreakpoint.GetNumLocations() < 1)
            {
                return new BreakpointErrorPair(null, BreakpointError.NoFunctionLocation);
            }

            // At the moment if there are two functions with the same offset will be applied only
            // for one of them. In base VS 2017 the breakpoint is created only for one
            // function if there are overloaded functions.
            SbFunction function =
                functionBreakpoint.GetLocationAtIndex(0).GetAddress().GetFunction();

            // Delete the breakpoint as we don't need it anymore and don't want it to linger
            _sbTarget.BreakpointDelete(functionBreakpoint.GetId());

            if (function == null)
            {
                return new BreakpointErrorPair(null, BreakpointError.NoFunctionFound);
            }

            SbLineEntry startLineEntry = function.GetStartAddress().GetLineEntry();

            uint startLine = startLineEntry.GetLine();

            // EndAddress points to the first address after the function so we
            // subtract one from that address and get the corresponding lineEntry.
            SbAddress endAddress = function.GetEndAddress();
            ulong address = endAddress.GetLoadAddress(_sbTarget);
            endAddress = ResolveLoadAddress(address - 1);
            uint endLine = endAddress.GetLineEntry().GetLine();

            uint newPosition = startLine + offset + 1;

            if (newPosition > endLine)
            {
                return new BreakpointErrorPair(null, BreakpointError.PositionNotAvailable);
            }

            string fileName = startLineEntry.GetFileName();
            string directory = startLineEntry.GetDirectory();
            string path = Path.Combine(directory, fileName);

            RemoteBreakpoint breakpoint = BreakpointCreateByLocation(path, newPosition);

            return new BreakpointErrorPair(breakpoint, BreakpointError.Success);
        }

        public SbBroadcaster GetBroadcaster() => _sbTarget.GetBroadcaster();

        #endregion

        #region RemoteTarget Helpers

        /// <summary>
        /// This method tries to figure out whether we have an instruction on the page boundary.
        /// It tries to read one instruction right after the last used address if it is on the
        /// current page. If it reads the instruction and its start and end addresses are on
        /// different pages, this method will return true and initialize newInstruction and
        /// nextInstruction parameters. Otherwise, the method returns false.
        /// </summary>
        bool ReadInstructionOnPageBound(SbAddress address, List<SbInstruction> sbInstructions,
                                        List<InstructionInfo> instructions, ulong endPageAddress,
                                        string flavor, out InstructionInfo newInstruction,
                                        out ulong nextInstruction)
        {
            ulong nextInstructionAddress =
                GetNextInstructionAddress(address, sbInstructions, instructions);

            // Let's check if there is a possibility that the instruction is split between pages.
            // Its address should still be on the current page, but it's end address, which we
            // estimate using max possible instruction size, should be on the next page.
            if (nextInstructionAddress <= endPageAddress &&
                nextInstructionAddress + _maxOpcodeSize > endPageAddress)
            {
                var boundaryInstruction = _sbTarget.ReadInstructions(
                    _sbTarget.ResolveLoadAddress(nextInstructionAddress), 1, flavor);
                if (boundaryInstruction.Count == 1)
                {
                    ulong startAddress = boundaryInstruction[0].GetAddress()
                        .GetLoadAddress(_sbTarget);
                    ulong endAddress = startAddress + boundaryInstruction[0].GetByteSize() - 1;
                    if (startAddress / _pageSize != endAddress / _pageSize)
                    {
                        newInstruction = PrepareInstruction(boundaryInstruction[0]);
                        nextInstruction = endAddress + 1;
                        return true;
                    }
                }
            }

            nextInstruction = 0;
            newInstruction = null;
            return false;
        }

        InstructionInfo PrepareInstruction(SbInstruction sbInstruction)
        {
            SbAddress sbAddress = sbInstruction.GetAddress();
            if (sbAddress == null)
            {
                // It should never happen that we cannot get an address for an instruction
                Trace.WriteLine("Unable to retrieve address.");
                return null;
            }

            ulong instructionAddress = sbAddress.GetLoadAddress(_sbTarget);

            SbSymbol symbol = sbAddress.GetSymbol();

            string symbolName = null;
            // Only set symbolName if it is the start of a function
            SbAddress startAddress = symbol?.GetStartAddress();
            if (startAddress != null &&
                startAddress.GetLoadAddress(_sbTarget) == instructionAddress)
            {
                symbolName = symbol.GetName();
            }

            SbLineEntry lineEntry = sbAddress.GetLineEntry();
            LineEntryInfo lineEntryInfo = null;
            if (lineEntry != null)
            {
                lineEntryInfo = new LineEntryInfo
                {
                    FileName = lineEntry.GetFileName(),
                    Directory = lineEntry.GetDirectory(),
                    Line = lineEntry.GetLine(),
                    Column = lineEntry.GetColumn(),
                };
            }

            return new InstructionInfo
            {
                Address = instructionAddress,
                Operands = sbInstruction.GetOperands(_sbTarget),
                Comment = sbInstruction.GetComment(_sbTarget),
                Mnemonic = sbInstruction.GetMnemonic(_sbTarget),
                SymbolName = symbolName,
                LineEntry = lineEntryInfo,
            };
        }

        /// <summary>
        /// Creates unknown/invalid instructions for an unmapped region.
        /// </summary>
        /// <returns> The address of the next instruction. </returns>
        ulong AddUnmappedInstructions(SbAddress address, uint instructionsLeft,
                                      SbMemoryRegionInfo memoryRegion,
                                      List<InstructionInfo> instructions)
        {
            // SbMemoryRegionInfo holds information about the end address even for unmapped
            // memory regions so we can use that to determine how far we can pad with
            // invalid/unknown instructions before checking again.
            ulong memoryRegionEndAddress = memoryRegion.GetRegionEnd();

            ulong startAddress = address.GetLoadAddress(_sbTarget);
            ulong endAddress = Math.Min(memoryRegionEndAddress, startAddress + instructionsLeft);

            for (ulong currentAddress = startAddress; currentAddress < endAddress; currentAddress++)
            {
                instructions.Add(new InstructionInfo
                {
                    Address = currentAddress, Operands = "??",
                                                       Mnemonic = "??" });
            }

            return endAddress;
        }

        /// <summary>
        /// Figures out the address of the next instruction and adds it as an invalid instruction
        /// to |instructions| if the last instruction is not on the memory region boundary.
        /// </summary>
        /// <returns> The address after the invalid instruction. </returns>
        ulong AddInvalidInstruction(SbAddress address, List<SbInstruction> sbInstructions,
                                    List<InstructionInfo> instructions, ulong nextPage)
        {
            ulong instructionAddress =
                GetNextInstructionAddress(address, sbInstructions, instructions);

            // Don't add invalid instruction if we stopped adding instructions because we
            // reached the end of the page.
            if (instructionAddress == nextPage)
            {
                return nextPage;
            }

            // Add the invalid instruction, we represent them with setting both the
            // operands and mnemonic to question marks
            instructions.Add(new InstructionInfo
            {
                Address = instructionAddress, Operands = "??",
                Mnemonic = "??"
            });

            return instructionAddress + 1;
        }

        ulong GetNextInstructionAddress(SbAddress address, List<SbInstruction> sbInstructions,
                                        List<InstructionInfo> instructions)
        {
            if (sbInstructions.Count > 0)
            {
                // The address of the next instruction should be the address
                // of the last instruction + its byte size.
                SbInstruction lastInstruction = sbInstructions[sbInstructions.Count - 1];
                return lastInstruction.GetAddress().GetLoadAddress(_sbTarget) +
                    lastInstruction.GetByteSize();
            }

            // If we haven't got any instructions yet we use the starting address
            if (instructions.Count == 0)
            {
                return address.GetLoadAddress(_sbTarget);
            }
            // If we got no new instructions and we already have some instructions,
            // use the address of the last instruction + 1 since the last instruction
            // must have been invalid and therefore the byte size is one.

            return instructions[instructions.Count - 1].Address + 1;
        }

        #endregion
    }
}