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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace YetiVSI.DebugEngine.CoreDumps
{
    /// <summary>
    /// Responsible for reading elf header information.
    /// </summary>
    /// <remarks>
    /// Elf header structure can be found here:
    /// https://en.wikipedia.org/wiki/Executable_and_Linkable_Format#File_header
    /// </remarks>
    public class ElfHeader
    {
        public const int Size = 0x40;
        public const int ElfMagicNumber = 0x464c457f;
        public const byte CurrentElfVersion = 1;
        public const int PaddingSize = 7;

        public readonly ulong StartOffset;
        public readonly ushort EntrySize;
        public readonly ushort EntriesCount;
        public readonly bool IsExecutable;

        static readonly ElfHeader EmptyHeader = new ElfHeader(0, 0, 0, false);

        public ElfHeader(ulong startOffset, ushort entrySize, ushort entriesCount,
                         bool isExecutable)
        {
            StartOffset = startOffset;
            EntrySize = entrySize;
            EntriesCount = entriesCount;
            IsExecutable = isExecutable;
        }

        /// <summary>
        /// Try to read elf header.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the stream was disposed.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown in case of any problems during reading.
        /// </exception>
        /// <param name="reader">Input stream reader.</param>
        /// <param name="result">Read header or empty header.</param>
        /// <returns>
        /// True if x64 elf header with little endianness and correct elf version was successfully
        /// read and false otherwise.
        /// </returns>
        public static bool TryRead(BinaryReader reader, out ElfHeader result)
        {
            result = EmptyHeader;
            if (!HeadersReadingUtils.IsEnoughBytes(reader, Size))
            {
                return false;
            }

            var ei_magic = reader.ReadInt32();
            var ei_bitness = reader.ReadByte();
            var ei_endianness = reader.ReadByte();
            var ei_version = reader.ReadByte();
            var ei_abi = reader.ReadByte();
            var ei_abi_version = reader.ReadByte();
            var ei_padding = reader.ReadBytes(PaddingSize);
            var e_type = reader.ReadInt16();
            var e_machine = reader.ReadInt16();
            var e_version = reader.ReadInt32();
            var e_entry = reader.ReadInt64();

            // Points to the start of the program header table.
            var e_phoff = reader.ReadUInt64();
            var e_shoff = reader.ReadUInt64();
            var e_flags = reader.ReadInt32();
            var e_ehsize = reader.ReadUInt16();

            // Contains the size of a program header table entry.
            var e_phentsize = reader.ReadUInt16();

            // Contains the number of entries in the program header table.
            var e_phnum = reader.ReadUInt16();
            var e_shentsize = reader.ReadUInt16();
            var e_shnum = reader.ReadUInt16();
            var e_shstrndx = reader.ReadUInt16();

            if (ei_magic != ElfMagicNumber)
            {
                Trace.WriteLine($"Invalid ELF magic {ElfMagicNumber} expected {ei_magic}.");
                return false;
            }

            if (ei_bitness != (byte)Bitness.x64)
            {
                Trace.WriteLine("Only 64-bit elf supported.");
                return false;
            }

            if (ei_endianness != (byte)Endianness.Little)
            {
                Trace.WriteLine("Only little endian supported.");
                return false;
            }

            if (ei_version != CurrentElfVersion)
            {
                Trace.WriteLine($"Invalid elf version: {ei_version} expected: " +
                                $"{CurrentElfVersion}.");
                return false;
            }

            result = new ElfHeader(e_phoff, e_phentsize, e_phnum, e_type == (byte)Type.Executable);
            return true;
        }

        public enum Bitness
        {
            x86 = 1,
            x64 = 2
        }

        public enum Endianness
        {
            Little = 1,
            Big = 2
        }

        public enum Type
        {
            Executable = 2
        }
    }
}
