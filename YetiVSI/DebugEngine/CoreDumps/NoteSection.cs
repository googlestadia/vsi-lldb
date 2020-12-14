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
using System.IO;
using System.Linq;
using System.Text;
using YetiCommon;

namespace YetiVSI.DebugEngine.CoreDumps
{
    /// <summary>
    /// Responsible for reading file indexes or build ids from note section.
    /// </summary>
    /// <remarks>
    /// Note section structure can be found here:
    /// https://refspecs.linuxfoundation.org/elf/elf.pdf
    /// </remarks>
    public class NoteSection
    {
        public const int NtFileType = 0x46494c45;
        public const int NtGnuBuildIdType = 3;

        public const string GnuName = "GNU";
        public const string CoreName = "CORE";
        public const int MinNoteSize = 12;
        public const int MinNtFileIndexSize = 16;
        public const int FileLocationSize = 24;

        readonly Type _type;
        readonly Name _name;
        readonly byte[] _data;

        NoteSection(Type type, Name name, byte[] data)
        {
            _type = type;
            _name = name;
            _data = data;
        }

        /// <summary>
        /// Read all note sections until the end of stream and parse file indexes from them.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the stream was disposed.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown in case of any problems during reading.
        /// </exception>
        /// <param name="reader">Input stream reader.</param>
        /// <param name="size">Expected note section size from program header.</param>
        /// <returns>Core module files sections.</returns>
        public static IEnumerable<FileSection> ReadModuleSections(BinaryReader reader, int size)
        {
            var result = new List<FileSection>();
            foreach (NoteSection note in ReadNotes(reader, size))
            {
                if (note._name != Name.Core || note._type != Type.File)
                {
                    continue;
                }

                var noteStream = new MemoryStream(note._data);
                using (var noteReader = new BinaryReader(noteStream))
                {
                    if (!HeadersReadingUtils.IsEnoughBytes(noteReader, MinNtFileIndexSize))
                    {
                        continue;
                    }

                    int count = (int)noteReader.ReadUInt64();
                    ulong pageSize = noteReader.ReadUInt64();

                    int locationsSize = FileLocationSize * count;
                    if (!HeadersReadingUtils.IsEnoughBytes(noteReader, locationsSize))
                    {
                        continue;
                    }

                    var starts = new ulong[count];
                    var ends = new ulong[count];
                    var offsets = new ulong[count];
                    for (int i = 0; i < count; i++)
                    {
                        starts[i] = noteReader.ReadUInt64();
                        ends[i] = noteReader.ReadUInt64();
                        offsets[i] = noteReader.ReadUInt64();
                    }

                    for (int i = 0; i < count; i++)
                    {
                        string path = ReadUtf8String(noteReader);

                        // We assume the build id lives in the first location.
                        if (offsets[i] == 0)
                        {
                            result.Add(new FileSection(path, starts[i], ends[i]));
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Read all note sections until the end of stream and parse build id from them.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the stream was disposed.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown in case of any problems during reading.
        /// </exception>
        /// <param name="reader">Input stream reader.</param>
        /// <param name="size">Expected note section size from program header.</param>
        /// <returns>Build id or BuildId.Empty.</returns>
        public static BuildId ReadBuildId(BinaryReader reader, int size)
        {
            NoteSection buildIdNote =
                ReadNotes(reader, size)
                    .FirstOrDefault(note => note._name == Name.Gnu && note._type == Type.BuildId);

            return buildIdNote == null ? BuildId.Empty : new BuildId(buildIdNote._data);
        }

        static string ReadUtf8String(BinaryReader reader)
        {
            byte b;
            var bytes = new List<byte>();
            while ((b = (byte)reader.BaseStream.ReadByte()) > 0)
            {
                bytes.Add(b);
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        static List<NoteSection> ReadNotes(BinaryReader reader, int size)
        {
            var notes = new List<NoteSection>();
            if (!HeadersReadingUtils.IsEnoughBytes(reader, Math.Max(size, MinNoteSize)))
            {
                return notes;
            }

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                if (!HeadersReadingUtils.IsEnoughBytes(reader, MinNoteSize))
                {
                    break;
                }

                int nameSize = reader.ReadInt32();
                int descriptionSize = reader.ReadInt32();
                uint typeValue = reader.ReadUInt32();

                int namePadding = HeadersReadingUtils.GetInt32ValuePadding(nameSize);
                int dataPadding = HeadersReadingUtils.GetInt32ValuePadding(descriptionSize);
                int noteSize = nameSize + namePadding + descriptionSize + dataPadding;
                if (!HeadersReadingUtils.IsEnoughBytes(reader, noteSize))
                {
                    break;
                }

                byte[] nameBytes = reader.ReadBytes(nameSize);
                reader.ReadBytes(namePadding);

                byte[] desc = reader.ReadBytes(descriptionSize);

                reader.ReadBytes(dataPadding);

                string nameString = Encoding.UTF8.GetString(nameBytes, 0, nameBytes.Length - 1);

                Type type;
                switch (typeValue)
                {
                    case NtFileType:
                        type = Type.File;
                        break;
                    case NtGnuBuildIdType:
                        type = Type.BuildId;
                        break;
                    default:
                        type = Type.Other;
                        break;
                }

                Name name;
                switch (nameString)
                {
                    case GnuName:
                        name = Name.Gnu;
                        break;
                    case CoreName:
                        name = Name.Core;
                        break;
                    default:
                        name = Name.Other;
                        break;
                }

                notes.Add(new NoteSection(type, name, desc));
            }

            return notes;
        }

        public enum Type
        {
            File,
            BuildId,
            Other
        }

        public enum Name
        {
            Core,
            Gnu,
            Other
        }
    }
}
