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

﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using YetiCommon;

namespace YetiVSI.DebugEngine.CoreDumps
{
    public interface IDumpModulesProvider
    {
        IEnumerable<DumpModule> GetModules(string dumpPath);
    }

    public class DumpModule
    {
        public string Path { get; }

        public BuildId Id { get; }

        public bool IsExecutable { get; }

        public DumpModule(string path, BuildId id, bool isExecutable)
        {
            Path = path;
            Id = id;
            IsExecutable = isExecutable;
        }

        public static DumpModule Empty = new DumpModule(string.Empty, BuildId.Empty, false);
    }

    public class DumpModulesProvider : IDumpModulesProvider
    {
        readonly IFileSystem _fileSystem;

        public DumpModulesProvider(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public IEnumerable<DumpModule> GetModules(string dumpPath)
        {
            if (string.IsNullOrEmpty(dumpPath) || !_fileSystem.File.Exists(dumpPath))
            {
                Trace.WriteLine($"Dump file {dumpPath} doesn't exists.");
                return new DumpModule[0];
            }

            using (var reader = new BinaryReader(_fileSystem.File.Open(dumpPath, FileMode.Open)))
            {
                return GetModules(reader);
            }
        }

        IEnumerable<DumpModule> GetModules(BinaryReader dumpReader)
        {
            var moduleList = new List<DumpModule>();
            if (!ElfHeader.TryRead(dumpReader, out ElfHeader elfHeader))
            {
                return moduleList;
            }

            var fileSections = new List<FileSection>();
            var loadSegments = new List<ProgramHeader>();

            // Go through each program header sections, look for the notes sections and the
            // loadable segments.
            foreach (ulong headerOffset in elfHeader.GetAbsoluteProgramHeaderOffsets())
            {
                dumpReader.BaseStream.Seek((long) headerOffset, SeekOrigin.Begin);

                if (!ProgramHeader.TryRead(dumpReader, out ProgramHeader header))
                {
                    continue;
                }

                switch (header.HeaderType)
                {
                    // We found the notes section. Now we need to extract module section from
                    // the NT_FILE notes.
                    case ProgramHeader.Type.NoteSegment:
                        if (header.HeaderSize > int.MaxValue)
                        {
                            Trace.WriteLine("Can't extract note segment sections from program" +
                                            "header. Note size is more then int.Max.");
                            continue;
                        }

                        int size = (int)header.HeaderSize;
                        dumpReader.BaseStream.Seek((long) header.OffsetInDump, SeekOrigin.Begin);
                        byte[] notesBytes = dumpReader.ReadBytes(size);
                        var notesStream = new MemoryStream(notesBytes);
                        var notesReader = new BinaryReader(notesStream);
                        fileSections.AddRange(
                            NoteSection.ReadModuleSections(notesReader, size));
                        break;

                    // Collect memory mappings for the core files.
                    case ProgramHeader.Type.LoadableSegment:
                        loadSegments.Add(header);
                        break;
                }
            }

            var loadableSegmentsReader = new ModuleReader(loadSegments, dumpReader);

            // Go through each module and try to find build id in the mapped regions.
            foreach (FileSection file in fileSections)
            {
                var module = loadableSegmentsReader.GetModule(file);
                if (module.Id == BuildId.Empty)
                {
                    Trace.WriteLine($"Can't find build id for module {module.Path}.");
                }
                else
                {
                    moduleList.Add(module);
                }
            }

            return moduleList;
        }
    }
}