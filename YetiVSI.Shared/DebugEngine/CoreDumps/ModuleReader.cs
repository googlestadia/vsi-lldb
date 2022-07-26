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
using System.Diagnostics;
using System.IO;
using YetiCommon;

namespace YetiVSI.DebugEngine.CoreDumps
{
    /// <summary>
    /// Looking for module information in loadable segments.
    /// </summary>
    public class ModuleReader
    {
        readonly List<ProgramHeader> _sortedSegments;
        readonly BinaryReader _dumpReader;

        public ModuleReader(List<ProgramHeader> loadableSegments, BinaryReader dumpReader)
        {
            _sortedSegments = loadableSegments;
            _sortedSegments.Sort((x, y) => x.VirtualAddress.CompareTo(y.VirtualAddress));
            _dumpReader = dumpReader;
        }

        /// <summary>
        /// Looks for build id for specified module index in core dump.
        /// Method also returns information about the module: is it executable and path.
        /// </summary>
        /// <returns>Dump module or DumpModule.Empty.</returns>
        public DumpModule GetModule(FileSection file)
        {
            byte[] elfHeaderBytes = ReadBlockByAddress(file.StartAddress, ElfHeader.Size);
            using (var elfHeaderReader = new BinaryReader(new MemoryStream(elfHeaderBytes)))
            {
                if (!ElfHeader.TryRead(elfHeaderReader, out ElfHeader moduleHeader))
                {
                    Trace.WriteLine($"Failed to read elf header for module {file.Path}.");
                    return DumpModule.Empty;
                }

                int headersSize = moduleHeader.EntriesCount * moduleHeader.EntrySize;
                byte[] headerBytes =
                    ReadBlockByAddress(file.StartAddress + moduleHeader.StartOffset, headersSize);

                using (var headerReader = new BinaryReader(new MemoryStream(headerBytes)))
                {
                    // Iterate through the program headers, until we find the note with the build
                    // id.
                    for (ulong i = 0; i < moduleHeader.EntriesCount; i++)
                    {
                        ulong offset = i * moduleHeader.EntrySize;
                        headerReader.BaseStream.Seek((long)offset, SeekOrigin.Begin);
                        if (!ProgramHeader.TryRead(headerReader, out ProgramHeader header))
                        {
                            Trace.WriteLine(
                                $"Failed to read program header with offset: {offset} " +
                                $"from module {file.Path}.");
                            continue;
                        }

                        if (header.HeaderType != ProgramHeader.Type.NoteSegment)
                        {
                            continue;
                        }

                        ulong fileSize = file.EndAddress - file.StartAddress;
                        ulong headerEnd = header.OffsetInFile + header.SizeInFile;
                        if (headerEnd > fileSize)
                        {
                            Trace.WriteLine("Can't extract note sections from program header. " +
                                            "Note section is outside of the first mapped location.");
                            continue;
                        }

                        if (header.SizeInFile > int.MaxValue)
                        {
                            Trace.WriteLine("Can't extract note sections from program header. " +
                                            "Note size is more then int.Max.");
                            continue;
                        }

                        int size = (int)header.SizeInFile;
                        byte[] noteSegmentBytes =
                            ReadBlockByAddress(file.StartAddress + header.OffsetInFile, size);
                        if (noteSegmentBytes.Length < size)
                        {
                            Trace.WriteLine("Can't extract build ids from note section. " +
                                            "Note is not fully in load segments.");
                            continue;
                        }

                        var notesStream = new MemoryStream(noteSegmentBytes);
                        using (var notesReader = new BinaryReader(notesStream))
                        {
                            BuildId buildId =
                                NoteSection.ReadBuildId(notesReader, size, ModuleFormat.Elf);
                            if (buildId != BuildId.Empty)
                            {
                                return new DumpModule(file.Path, buildId,
                                                      moduleHeader.IsExecutable);
                            }
                        }
                    }
                }
            }

            return DumpModule.Empty;
        }

        /// <summary>
        /// Reads block of bytes from loadable segments.
        /// Looks for the first loadable segment which contains the start address and reads from
        /// necessary count of segments.
        /// </summary>
        /// <param name="startAddress">Start address.</param>
        /// <param name="size">Size of block.</param>
        /// <returns></returns>
        byte[] ReadBlockByAddress(ulong startAddress, int size)
        {
            int firstSegmentIndex = FindSegmentContainingAddress(startAddress);
            if (firstSegmentIndex < 0)
            {
                return Array.Empty<byte>();
            }

            var result = new byte[size];
            int remainSize = size;
            for (int i = firstSegmentIndex; i < _sortedSegments.Count && remainSize > 0; i++)
            {
                ProgramHeader segment = _sortedSegments[i];
                if (startAddress < segment.VirtualAddress)
                {
                    break;
                }

                ulong offsetInSegment = startAddress - segment.VirtualAddress;
                long offsetInDump = (long)(segment.OffsetInFile + offsetInSegment);
                int blockSize = Math.Min((int)(segment.SizeInFile - offsetInSegment), remainSize);

                _dumpReader.BaseStream.Seek(offsetInDump, SeekOrigin.Begin);

                byte[] blockBytes = _dumpReader.ReadBytes(blockSize);
                Array.Copy(blockBytes, 0, result, size - remainSize, blockBytes.Length);
                remainSize -= blockBytes.Length;
                startAddress += (ulong)blockBytes.Length;
            }

            if (remainSize > 0)
            {
                byte[] shrunkResult = new byte[size - remainSize];
                Array.Copy(result, shrunkResult, shrunkResult.Length);
                result = shrunkResult;
            }

            return result;
        }

        /// <summary>
        /// Looks for the first loadable segment which contains the address. Uses binary search.
        /// </summary>
        /// <param name="address">The address which should be inside segment.</param>
        /// <returns>Index of the loadable segment if it was found and -1 otherwise.</returns>
        int FindSegmentContainingAddress(ulong address)
        {
            int min = 0;
            int max = _sortedSegments.Count - 1;

            while (min <= max)
            {
                int mid = (min + max) / 2;
                ProgramHeader segment = _sortedSegments[mid];
                ulong end = segment.VirtualAddress + segment.SizeInFile;
                if (address < segment.VirtualAddress)
                {
                    max = mid - 1;
                    continue;
                }

                if (address >= end)
                {
                    min = mid + 1;
                    continue;
                }

                return mid;
            }

            return -1;
        }
    }
}