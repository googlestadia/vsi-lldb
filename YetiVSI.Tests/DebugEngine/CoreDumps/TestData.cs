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
using System.Linq;
using System.Text;
using YetiCommon;
using YetiVSI.DebugEngine.CoreDumps;

namespace YetiVSI.Test.DebugEngine.CoreDumps
{
    public static class TestData
    {
        public static readonly byte[] magicNumberBytes =
            BitConverter.GetBytes(ElfHeader.ElfMagicNumber);


        public static BuildId expectedId = new BuildId(
            "61A1A0AE-FAA0-04E4-228E-D5E8D37C7C1F-5C6EEB3D", ModuleFormat.Elf);

        public static BuildId anotherExpectedId = new BuildId(
            "61A1A0AE-FAA0-04E4-228E-D5E8D37C7C1F-5C6EEBED", ModuleFormat.Elf);

        public static readonly int bitnessIndex = magicNumberBytes.Length;
        public static readonly int endiannessIndex = bitnessIndex + 1;
        public static readonly int versionIndex = endiannessIndex + 1;
        public static readonly int typeIndex = versionIndex + 3 + ElfHeader.PaddingSize;

        public static readonly int elfOffsetIndex = 0x20;
        public static readonly int elfEntrySizeIndex = 0x36;
        public static readonly int elfEntriesCountIndex = 0x38;

        static readonly int phTypeIndex = 0x00;
        static readonly int phOffsetIndex = 0x08;
        static readonly int phVaddrIndex = 0x10;
        static readonly int phSizeIndex = 0x20;

        static readonly int noteNameSizeIndex = 0x00;
        static readonly int noteDescSizeIndex = 0x04;
        static readonly int noteTypeValueIndex = 0x08;
        static readonly int noteNameStartIndex = 0xC;

        public static byte[] GetElfBytes(ulong offset, ushort size, ushort count,
                                         bool isExecutable)
        {
            var elfData = new byte[ElfHeader.Size];
            for (int i = 0; i < magicNumberBytes.Length; i++)
            {
                elfData[i] = magicNumberBytes[i];
            }

            elfData[bitnessIndex] = (byte) ElfHeader.Bitness.x64;
            elfData[endiannessIndex] = (byte) ElfHeader.Endianness.Little;
            elfData[versionIndex] = ElfHeader.CurrentElfVersion;

            if (isExecutable)
            {
                elfData[typeIndex] = (byte) ElfHeader.Type.Executable;
            }

            FillUInt64(elfData, elfOffsetIndex, offset);
            FillShort(elfData, elfEntrySizeIndex, size);
            FillShort(elfData, elfEntriesCountIndex, count);

            return elfData;
        }

        public static byte[] GetProgramHeaderBytes(ProgramHeader.Type type, ulong offset,
            ulong vaddr, ulong size)
        {
            var phData = new byte[ProgramHeader.Size];
            phData[phTypeIndex] = (byte) type;

            FillUInt64(phData, phOffsetIndex, offset);
            FillUInt64(phData, phVaddrIndex, vaddr);
            FillUInt64(phData, phSizeIndex, size);

            return phData;
        }

        public static byte[] GetNoteSectionBytes(string name, int type, byte[] data)
        {
            var nameBytes = GetStringBytes(name);
            var nameLength = nameBytes.Length;
            var dataLength = data.Length;

            var namePaddingSize = HeadersReadingUtils.GetInt32ValuePadding(nameLength);
            var descPaddingSize = HeadersReadingUtils.GetInt32ValuePadding(dataLength);
            var namePadding = new byte[namePaddingSize];
            var descPadding = new byte[descPaddingSize];
            var noteSize = nameLength + dataLength + namePaddingSize + descPaddingSize +
                NoteSection.MinNoteSize;
            var noteData = new byte[noteSize];

            FillInt32(noteData, noteNameSizeIndex, nameLength);
            FillInt32(noteData, noteDescSizeIndex, dataLength);
            FillInt32(noteData, noteTypeValueIndex, type);

            var index = noteNameStartIndex;
            Array.Copy(nameBytes, 0, noteData, index, nameLength);
            index += nameLength;
            Array.Copy(namePadding, 0, noteData, index, namePaddingSize);
            index += namePaddingSize;
            Array.Copy(data, 0, noteData, index, dataLength);
            index += dataLength;
            Array.Copy(descPadding, 0, noteData, index, descPaddingSize);

            return noteData;
        }

        public static byte[] GetNtFileSectionBytes(ulong startAddress, ulong endAddress,
                                                   ulong offset, string name)
        {
            return GetNtFileSectionsBytes(new[] { startAddress }, new[] { endAddress },
                                          new[] { offset }, new[] { name });
        }

        public static byte[] GetNtFileSectionsBytes(ulong[] starts, ulong[] ends, ulong[] offsets,
                                                   string[] names)
        {
            var nameBytes = names.SelectMany(n => GetStringBytes(n)).ToArray();
            var ntFilesSize = NoteSection.FileLocationSize * starts.Length + nameBytes.Length +
                NoteSection.MinNtFileIndexSize;

            var ntFilesData = new byte[ntFilesSize];
            int index = 0;
            FillUInt64(ntFilesData, index, (ulong) starts.Length);
            index += 8;

            // Fill page size.
            FillUInt64(ntFilesData, index, 0);
            index += 8;
            for (var i = 0; i < starts.Length; i++)
            {
                FillUInt64(ntFilesData, index, starts[i]);
                index += 8;
                FillUInt64(ntFilesData, index, ends[i]);
                index += 8;
                FillUInt64(ntFilesData, index, offsets[i]);
                index += 8;
            }

            Array.Copy(nameBytes, 0, ntFilesData, index, nameBytes.Length);
            return ntFilesData;
        }

        public static byte[] GetElfFileBytesFromBuildId(BuildId id, bool isExecutable = false)
        {
            var idNote = GetNoteSectionBytes(NoteSection.GnuName, NoteSection.NtGnuBuildIdType,
                                             id.Bytes.ToArray());

            var noteOffset = ProgramHeader.Size + ElfHeader.Size;
            var programHeader = GetProgramHeaderBytes(
                ProgramHeader.Type.NoteSegment, (ulong) noteOffset, 0, (ulong) idNote.Length);

            var elfHeader = GetElfBytes(ElfHeader.Size, ProgramHeader.Size, 1, isExecutable);

            return elfHeader.Concat(programHeader).Concat(idNote).ToArray();
        }

        static byte[] GetStringBytes(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value).ToList();
            bytes.Add(0);
            return bytes.ToArray();
        }

        static void FillShort(byte[] data, int offset, ushort value)
        {
            var valueBytes = BitConverter.GetBytes(value);
            for (int i = 0; i < valueBytes.Length; i++)
            {
                data[offset + i] = valueBytes[i];
            }
        }

        static void FillInt32(byte[] data, int offset, int value)
        {
            var valueBytes = BitConverter.GetBytes(value);
            for (int i = 0; i < valueBytes.Length; i++)
            {
                data[offset + i] = valueBytes[i];
            }
        }

        static void FillUInt64(byte[] data, int offset, ulong value)
        {
            var valueBytes = BitConverter.GetBytes(value);
            for (int i = 0; i < valueBytes.Length; i++)
            {
                data[offset + i] = valueBytes[i];
            }
        }
    }
}