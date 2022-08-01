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

﻿using NUnit.Framework;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using YetiCommon;
using YetiVSI.DebugEngine.CoreDumps;

namespace YetiVSI.Test.DebugEngine.CoreDumps
{
    [TestFixture]
    public class DumpModulesProviderTest
    {
        const string dumpPath = "dump";
        const string expectedFileName = "file";
        const string anotherExpectedFileName = "File";

        DumpModulesProvider _provider;
        MockFileSystem _fileSystem;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = new MockFileSystem();
            _provider = new DumpModulesProvider(_fileSystem);
        }

        [Test]
        public void DumpNotExist()
        {
            Assert.AreEqual(0, _provider.GetModules("").Modules.Count());
            Assert.AreEqual(DumpReadWarning.FileDoesNotExist, _provider.GetModules("").Warning);
            Assert.AreEqual(0, _provider.GetModules("C:\\1.txt").Modules.Count());
            Assert.AreEqual(DumpReadWarning.FileDoesNotExist,
                            _provider.GetModules("C:\\1.txt").Warning);
        }

        [Test]
        public void ProgramHeaderOutsideDump()
        {
            var elfHeaderBytes = TestData.GetElfBytes(ElfHeader.Size + 1, ProgramHeader.Size, 1,
                                                      false);

            _fileSystem.AddFile(dumpPath, new MockFileData(elfHeaderBytes));
            DumpReadResult dump = _provider.GetModules(dumpPath);
            Assert.AreEqual(0, dump.Modules.Count());
            Assert.AreEqual(DumpReadWarning.FileIsTruncated, dump.Warning);
        }

        [Test]
        public void NoteSectionOutsideDump()
        {
            var elfHeaderBytes = TestData.GetElfBytes(ElfHeader.Size, ProgramHeader.Size, 1,
                                                      false);

            var positionOutsideFile = 1000ul;
            var programHeader = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.NoteSegment, positionOutsideFile, positionOutsideFile, 10);

            var data = elfHeaderBytes.Concat(programHeader).ToArray();
            _fileSystem.AddFile(dumpPath, new MockFileData(data));
            DumpReadResult dump = _provider.GetModules(dumpPath);
            Assert.AreEqual(0, dump.Modules.Count());
            Assert.AreEqual(DumpReadWarning.FileIsTruncated, dump.Warning);
        }

        // This test is checking a dump with the following structure:
        // Start position    Section name              Section size in bytes
        //              0    Dump elf header                   64
        //             64    First file header                 56
        //            120    First file index header           56
        //            176    Second file index header          56
        //            232    Second file header                56
        //            288    First file with build id          156
        //            444    First file index                  68
        //            512    Second file with build id         68
        //            580    Second file index                 156
        [Test]
        public void CorrectModules()
        {
            ushort programHeadersCount = 4;
            var elfHeaderBytes = TestData.GetElfBytes(ElfHeader.Size, ProgramHeader.Size,
                                                      programHeadersCount, false);

            ulong firstFileOffset = ElfHeader.Size +
                (ulong) programHeadersCount * ProgramHeader.Size;

            var firstFileBytes = TestData.GetElfFileBytesFromBuildId(TestData.expectedId);
            var secondFileBytes = TestData.GetElfFileBytesFromBuildId(TestData.anotherExpectedId);

            var firstFileHeaderBytes = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.LoadableSegment, firstFileOffset, firstFileOffset,
                (ulong) firstFileBytes.Length);

            var firstFileIndexOffset = firstFileOffset + (ulong) firstFileBytes.Length;
            var fileIndexBytes = TestData.GetNtFileSectionBytes(
                firstFileOffset, firstFileIndexOffset, 0ul, expectedFileName);

            var firstFileIndexNoteBytes = TestData.GetNoteSectionBytes(
                NoteSection.CoreName, NoteSection.NtFileType, fileIndexBytes);

            var firstFileIndexHeaderBytes = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.NoteSegment, firstFileIndexOffset, firstFileIndexOffset,
                (ulong) firstFileBytes.Length);

            ulong secondFileIndexOffset = firstFileIndexOffset +
                (ulong) firstFileIndexNoteBytes.Length;
            ulong secondFileOffset = secondFileIndexOffset + (ulong) firstFileIndexNoteBytes.Length;
            ulong secondFileDataEnd = secondFileOffset + (ulong) secondFileBytes.Length;

            var secondIndexBytes = TestData.GetNtFileSectionBytes(
                secondFileOffset, secondFileDataEnd, 0ul, anotherExpectedFileName);

            var secondFileIndexHeaderBytes = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.NoteSegment, secondFileIndexOffset, secondFileIndexOffset,
                (ulong) secondIndexBytes.Length);

            var secondFileIndexNoteBytes = TestData.GetNoteSectionBytes(
                NoteSection.CoreName, NoteSection.NtFileType, secondIndexBytes);

            var secondFileHeaderBytes = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.LoadableSegment, secondFileOffset, secondFileOffset,
                (ulong) secondFileBytes.Length);

            var dumpBytes = elfHeaderBytes.Concat(firstFileHeaderBytes)
                .Concat(firstFileIndexHeaderBytes).Concat(secondFileIndexHeaderBytes)
                .Concat(secondFileHeaderBytes).Concat(firstFileBytes)
                .Concat(firstFileIndexNoteBytes).Concat(secondFileIndexNoteBytes)
                .Concat(secondFileBytes).ToArray();

            _fileSystem.AddFile(dumpPath, new MockFileData(dumpBytes));

            DumpReadResult dump = _provider.GetModules(dumpPath);
            Assert.AreEqual(2, dump.Modules.Count());

            var firstModule = dump.Modules.First();
            Assert.AreEqual(expectedFileName, firstModule.Path);
            Assert.AreEqual(TestData.expectedId, firstModule.BuildId);

            var lastModule = dump.Modules.Last();
            Assert.AreEqual(anotherExpectedFileName, lastModule.Path);
            Assert.AreEqual(TestData.anotherExpectedId, lastModule.BuildId);
        }

        struct TestDumpFile
        {
            public ulong NoteNtFileEnd;
            public byte[] Bytes;
        }

        // This test is checking a dump with the following structure:
        // Start position    Section description                Section size in bytes
        //              0    Dump elf header                      64
        //             64    Program headers                      --
        //             64      File  index note program header    56
        //            120      File1 header                       56
        //            176      File2 header                       56
        //            232    File1 contents                       156
        //            388    File index note                      104
        //            484    File2 contents                       156
        TestDumpFile CreateValidDumpFile()
        {
            ushort programHeadersCount = 3;
            byte[] elfHeaderBytes = TestData.GetElfBytes(ElfHeader.Size, ProgramHeader.Size,
                                                         programHeadersCount, false);

            byte[] file1Bytes = TestData.GetElfFileBytesFromBuildId(TestData.expectedId);
            ulong file1Offset =
                (ulong)elfHeaderBytes.Length + programHeadersCount * (ulong)ProgramHeader.Size;
            ulong file1Size = (ulong)file1Bytes.Length;
            ulong file1Address = 1000;

            byte[] file2Bytes = TestData.GetElfFileBytesFromBuildId(TestData.anotherExpectedId);
            ulong file2Offset = 484;
            ulong file2Size = (ulong)file2Bytes.Length;
            ulong file2Address = 2000;

            ulong fileIndexOffset = file1Offset + file1Size;
            byte[] fileIndexBytes = TestData.GetNtFileSectionsBytes(
                new[] { file1Address, file2Address },
                new[] { file1Address + file1Size, file2Address + file2Size }, new[] { 0ul, 0ul },
                new[] { expectedFileName, anotherExpectedFileName });
            byte[] fileIndexNoteBytes = TestData.GetNoteSectionBytes(
                NoteSection.CoreName, NoteSection.NtFileType, fileIndexBytes);

            byte[] fileIndexHeaderBytes = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.NoteSegment, fileIndexOffset, 0ul /* address */,
                (ulong)(fileIndexNoteBytes.Length));
            byte[] file1HeaderBytes = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.LoadableSegment, file1Offset, file1Address, file1Size);
            byte[] file2HeaderBytes = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.LoadableSegment, file2Offset, file2Address, file2Size);
            byte[] headers = elfHeaderBytes.Concat(fileIndexHeaderBytes)
                                 .Concat(file1HeaderBytes)
                                 .Concat(file2HeaderBytes)
                                 .ToArray();

            Assert.That(file2Offset,
                        Is.EqualTo(fileIndexOffset + (ulong)fileIndexNoteBytes.Length));

            return new TestDumpFile {
                NoteNtFileEnd = file2Offset,
                Bytes = headers.Concat(file1Bytes)
                            .Concat(fileIndexNoteBytes)
                            .Concat(file2Bytes)
                            .ToArray(),
            };
        }

        [Test]
        public void TwoModulesInIndexCorrect()
        {
            TestDumpFile dumpFile = CreateValidDumpFile();
            _fileSystem.AddFile(dumpPath, new MockFileData(dumpFile.Bytes));

            DumpReadResult dump = _provider.GetModules(dumpPath);

            Assert.That(dump.Warning, Is.EqualTo(DumpReadWarning.None));
            Assert.That(dump.Modules.Count(), Is.EqualTo(2));
            Assert.That(dump.Modules.ElementAt(0).Path, Is.EqualTo(expectedFileName));
            Assert.That(dump.Modules.ElementAt(0).BuildId, Is.EqualTo(TestData.expectedId));
            Assert.That(dump.Modules.ElementAt(1).Path, Is.EqualTo(anotherExpectedFileName));
            Assert.That(dump.Modules.ElementAt(1).BuildId, Is.EqualTo(TestData.anotherExpectedId));
        }

        [Test]
        public void TruncatedFileIndexReturnsEmptyModules()
        {
            TestDumpFile dumpFile = CreateValidDumpFile();

            // If the file is truncated before the end of the NtFile note segment, we should only
            // get empty module list back.
            for (ulong i = 0; i < dumpFile.NoteNtFileEnd; i += 10)
            {
                byte[] truncatedDumpBytes = dumpFile.Bytes.Take((int)i).ToArray();
                _fileSystem.AddFile(dumpPath, new MockFileData(truncatedDumpBytes));
                DumpReadResult dump = _provider.GetModules(dumpPath);
                Assert.That(dump.Modules.Count(), Is.EqualTo(0));
                Assert.That(dump.Warning, Is.AnyOf(DumpReadWarning.FileIsTruncated,
                                                   DumpReadWarning.ElfHeaderIsCorrupted));
            }
        }

        [Test]
        public void TruncatedFileNotInModules()
        {
            TestDumpFile dumpFile = CreateValidDumpFile();

            // If the embedded module file is truncated after the NtFile note but before the end of
            // the build ID of the second file, we should only get back the non-truncated module.
            for (int i = (int)dumpFile.NoteNtFileEnd; i < dumpFile.Bytes.Length; i += 10)
            {
                byte[] truncatedDumpBytes = dumpFile.Bytes.Take(i).ToArray();
                _fileSystem.AddFile(dumpPath, new MockFileData(truncatedDumpBytes));
                DumpReadResult dump = _provider.GetModules(dumpPath);
                Assert.That(dump.Modules.Count(), Is.EqualTo(1));
                Assert.That(dump.Modules.ElementAt(0).Path, Is.EqualTo(expectedFileName));
                Assert.That(dump.Modules.ElementAt(0).BuildId, Is.EqualTo(TestData.expectedId));
                Assert.That(dump.Warning, Is.EqualTo(DumpReadWarning.FileIsTruncated));
            }
        }

        // This creates an ELF file with a hundred dummy note sections preceding the build ID
        // section.
        public static byte[] GetElfFileBytesWithManySections(BuildId id)
        {
            byte[] idNote = TestData.GetNoteSectionBytes(
                NoteSection.GnuName, NoteSection.NtGnuBuildIdType, id.Bytes.ToArray());
            byte[] dummyNote = TestData.GetNoteSectionBytes("dummy", 1234, new byte[1]);
            ushort dummySectionCount = 100;
            ushort sectionCount = (ushort)(dummySectionCount + 1);

            var contents = new System.Collections.Generic.List<byte>();

            // Add the elf header.
            contents.AddRange(
                TestData.GetElfBytes(ElfHeader.Size, ProgramHeader.Size, sectionCount, false));

            // Add program headers.
            for (ulong i = 0; i < dummySectionCount; i++)
            {
                ulong dummyNoteOffset = ElfHeader.Size + sectionCount * (ulong)ProgramHeader.Size +
                                        i * (ulong)dummyNote.Length;
                contents.AddRange(TestData.GetProgramHeaderBytes(
                    ProgramHeader.Type.NoteSegment, dummyNoteOffset, 0, (ulong)dummyNote.Length));
            }

            var noteOffset = ElfHeader.Size + sectionCount * (ulong)ProgramHeader.Size +
                             dummySectionCount * (ulong)dummyNote.Length;
            contents.AddRange(TestData.GetProgramHeaderBytes(ProgramHeader.Type.NoteSegment,
                                                             noteOffset, 0, (ulong)idNote.Length));

            // Add the sections.
            for (ulong i = 0; i < dummySectionCount; i++)
            {
                contents.AddRange(dummyNote);
            }
            contents.AddRange(idNote);

            return contents.ToArray();
        }

        [Test]
        public void ManySectionsInBinaryFile()
        {
            // This test is checking a dump with the structure below. The elf module file in the
            // dump contains many sections to test that the code that looks for build ID can
            // find build IDs far in the file.
            //
            // Start position    Section name                       Section size in bytes
            //              0    Dump elf header                      64
            //             64    Program headers                      --
            //             64      File index note program header     56
            //            120      File header                        56
            //            176    File contents                      8156
            //           8332    File index                           68
            ushort programHeadersCount = 2;
            byte[] elfHeaderBytes = TestData.GetElfBytes(ElfHeader.Size, ProgramHeader.Size,
                                                         programHeadersCount, false);

            byte[] fileBytes = GetElfFileBytesWithManySections(TestData.expectedId);
            ulong fileOffset = (ulong)elfHeaderBytes.Length +
                programHeadersCount * (ulong)ProgramHeader.Size;
            ulong fileSize = (ulong)fileBytes.Length;
            ulong fileAddress = 1000;

            ulong fileIndexOffset = fileOffset + fileSize;
            byte[] fileIndexBytes = TestData.GetNtFileSectionBytes(
                fileAddress, fileAddress + fileSize, 0ul, expectedFileName);
            byte[] fileIndexNoteBytes = TestData.GetNoteSectionBytes(
                NoteSection.CoreName, NoteSection.NtFileType, fileIndexBytes);

            byte[] fileIndexHeaderBytes = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.NoteSegment, fileIndexOffset, 0ul /* address */,
                (ulong)(fileIndexNoteBytes.Length));
            byte[] fileHeaderBytes = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.LoadableSegment, fileOffset, fileAddress, fileSize);
            byte[] headers = elfHeaderBytes.Concat(fileIndexHeaderBytes).Concat(fileHeaderBytes)
                .ToArray();

            byte[] dumpFileBytes = headers.Concat(fileBytes).Concat(fileIndexNoteBytes).ToArray();
            _fileSystem.AddFile(dumpPath, new MockFileData(dumpFileBytes));

            DumpReadResult dump = _provider.GetModules(dumpPath);

            Assert.That(dump.Warning, Is.EqualTo(DumpReadWarning.None));
            Assert.That(dump.Modules.Count(), Is.EqualTo(1));
            Assert.That(dump.Modules.ElementAt(0).Path, Is.EqualTo(expectedFileName));
            Assert.That(dump.Modules.ElementAt(0).BuildId, Is.EqualTo(TestData.expectedId));
        }
    }
}