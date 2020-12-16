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

﻿using System;
using System.Diagnostics;
using System.IO;

namespace YetiVSI.DebugEngine.CoreDumps
{
    /// <summary>
    /// Responsible for reading program header information.
    /// </summary>
    /// <remarks>
    /// Program header structure can be found here:
    /// https://en.wikipedia.org/wiki/Executable_and_Linkable_Format#Program_header
    /// </remarks>
    public class ProgramHeader
    {
        public const int Size = 0x38;

        public readonly Type HeaderType;
        public readonly ulong OffsetInDump;
        public readonly ulong VirtualAddress;
        public readonly ulong HeaderSize;

        static readonly ProgramHeader EmptyHeader = new ProgramHeader(Type.Other, 0, 0, 0);

        public ProgramHeader(Type type, ulong offsetInDump, ulong virtualAddress, ulong size)
        {
            HeaderType = type;
            OffsetInDump = offsetInDump;
            VirtualAddress = virtualAddress;
            HeaderSize = size;
        }

        /// <summary>
        /// Try to read program header.
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
        /// True if program header was successfully read and false otherwise.
        /// </returns>
        public static bool TryRead(BinaryReader reader, out ProgramHeader result)
        {
            result = EmptyHeader;
            if (!HeadersReadingUtils.IsEnoughBytes(reader, Size))
            {
                return false;
            }

            var typeValue = reader.ReadUInt32();
            var flags = reader.ReadUInt32();
            var offset = reader.ReadUInt64();
            var vaddr = reader.ReadUInt64();
            var paddr = reader.ReadUInt64();
            var filesz = reader.ReadUInt64();
            var memsz = reader.ReadUInt64();
            var align = reader.ReadUInt64();

            if (!Enum.TryParse(typeValue.ToString(), out Type type)
                || !Enum.IsDefined(typeof(Type), type))
            {
                type = Type.Other;
            }

            result = new ProgramHeader(type, offset, vaddr, filesz);
            return true;
        }

        public enum Type
        {
            Other = 0,
            LoadableSegment = 1,
            NoteSegment = 4
        }
    }
}