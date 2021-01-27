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
using System.IO.Abstractions;
using YetiCommon;

namespace YetiVSI.DebugEngine.CoreDumps
{
    public interface IDumpModulesProvider
    {
        DumpReadResult GetModules(string dumpPath);
    }

    public enum DumpReadWarning
    {
        None,
        FileDoesNotExist,
        FileIsTruncated,
        ElfHeaderIsCorrupted,
        ExecutableBuildIdMissing,
    }

    public class DumpReadResult
    {
        public IEnumerable<DumpModule> Modules { get; }
        public DumpReadWarning Warning;

        public DumpReadResult(IEnumerable<DumpModule> modules, DumpReadWarning warning)
        {
            Modules = modules;
            Warning = warning;
        }
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

        public static readonly DumpModule Empty =
            new DumpModule(string.Empty, BuildId.Empty, false);
    }

    public class DumpModulesProvider : IDumpModulesProvider
    {
        readonly IFileSystem _fileSystem;

        public DumpModulesProvider(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public DumpReadResult GetModules(string dumpPath)
        {
            if (string.IsNullOrEmpty(dumpPath) || !_fileSystem.File.Exists(dumpPath))
            {
                Trace.WriteLine($"Dump file {dumpPath} doesn't exists.");
                return new DumpReadResult(new DumpModule[0], DumpReadWarning.FileDoesNotExist);
            }

            Stream fileStream = _fileSystem.File.Open(
                dumpPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            using (var reader = new BinaryReader(fileStream))
            {
                return GetModules(reader);
            }
        }

        DumpReadResult GetModules(BinaryReader dumpReader)
        {
            ulong dumpSize = (ulong)dumpReader.BaseStream.Length;
            var moduleList = new List<DumpModule>();
            if (!ElfHeader.TryRead(dumpReader, out ElfHeader elfHeader))
            {
                return new DumpReadResult(moduleList, DumpReadWarning.ElfHeaderIsCorrupted);
            }

            var fileSections = new List<FileSection>();
            var loadSegments = new List<ProgramHeader>();
            DumpReadWarning warning = DumpReadWarning.None;

            // Go through each program header sections, look for the notes sections and the
            // loadable segments.
            for (ulong i = 0; i < elfHeader.EntriesCount; ++i)
            {
                ulong headerOffset = elfHeader.StartOffset + i * elfHeader.EntrySize;
                dumpReader.BaseStream.Seek((long)headerOffset, SeekOrigin.Begin);

                if (!ProgramHeader.TryRead(dumpReader, out ProgramHeader header))
                {
                    return new DumpReadResult(moduleList, DumpReadWarning.FileIsTruncated);
                }

                // Set the warning if the program header is outside of the file.
                if (header.OffsetInFile + header.SizeInFile > dumpSize &&
                    warning == DumpReadWarning.None)
                {
                    warning = DumpReadWarning.FileIsTruncated;
                }

                switch (header.HeaderType)
                {
                    // We found the notes section. Now we need to extract module section from
                    // the NT_FILE notes.
                    case ProgramHeader.Type.NoteSegment:
                        if (header.SizeInFile > int.MaxValue)
                        {
                            Trace.WriteLine("Can't extract note segment sections from program" +
                                            "header. Note size is more then int.Max.");
                            continue;
                        }

                        int size = (int)header.SizeInFile;
                        dumpReader.BaseStream.Seek((long)header.OffsetInFile, SeekOrigin.Begin);
                        byte[] notesBytes = dumpReader.ReadBytes(size);
                        var notesStream = new MemoryStream(notesBytes);
                        using (var notesReader = new BinaryReader(notesStream))
                        {
                            IEnumerable<FileSection> moduleSections =
                                NoteSection.ReadModuleSections(notesReader, size);
                            fileSections.AddRange(moduleSections);
                        }

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
                DumpModule module = loadableSegmentsReader.GetModule(file);
                if (module.Id == BuildId.Empty)
                {
                    Trace.WriteLine($"Can't find build id for module {module.Path}.");
                }
                else
                {
                    moduleList.Add(module);
                }
            }

            return new DumpReadResult(moduleList, warning);
        }
    }
}
