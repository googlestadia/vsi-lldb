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
            Assert.AreEqual(0, _provider.GetModules("").ToArray().Length);
            Assert.AreEqual(0, _provider.GetModules("C:\\1.txt").ToArray().Length);
        }

        [Test]
        public void ProgramHeaderOutsideDump()
        {
            var elfHeaderBytes = TestData.GetElfBytes(ElfHeader.Size + 1, ProgramHeader.Size, 1,
                                                      false);

            _fileSystem.AddFile(dumpPath, new MockFileData(elfHeaderBytes));
            var dumpModules = _provider.GetModules(dumpPath).ToArray();
            Assert.AreEqual(0, dumpModules.Length);
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
            var dumpModules = _provider.GetModules(dumpPath).ToArray();
            Assert.AreEqual(0, dumpModules.Length);
        }

        // This test is checking dump with followed structure:
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

            var firstFileBytes = TestData.GetNtFileBytes(TestData.expectedId);
            var secondFileBytes = TestData.GetNtFileBytes(TestData.anotherExpectedId);

            var firstFileHeaderBytes = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.LoadableSegment, firstFileOffset, firstFileOffset,
                (ulong) firstFileBytes.Length);

            var firstFileIndexOffset = firstFileOffset + (ulong) firstFileBytes.Length;
            var fileIndexBytes = TestData.GetFileSectionBytes(
                firstFileOffset, firstFileIndexOffset, 0ul, expectedFileName);

            var firstFileIndexNoteBytes = TestData.GetNoteSectionData(
                NoteSection.CoreName, NoteSection.NtFileType, fileIndexBytes);

            var firstFileIndexHeaderBytes = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.NoteSegment, firstFileIndexOffset, firstFileIndexOffset,
                (ulong) firstFileBytes.Length);

            ulong secondFileIndexOffset = firstFileIndexOffset +
                (ulong) firstFileIndexNoteBytes.Length;
            ulong secondFileOffset = secondFileIndexOffset + (ulong) firstFileIndexNoteBytes.Length;
            ulong secondFileDataEnd = secondFileOffset + (ulong) secondFileBytes.Length;

            var secondIndexBytes = TestData.GetFileSectionBytes(
                secondFileOffset, secondFileDataEnd, 0ul, anotherExpectedFileName);

            var secondFileIndexHeaderBytes = TestData.GetProgramHeaderBytes(
                ProgramHeader.Type.NoteSegment, secondFileIndexOffset, secondFileIndexOffset,
                (ulong) secondIndexBytes.Length);

            var secondFileIndexNoteBytes = TestData.GetNoteSectionData(
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

            var dumpModules = _provider.GetModules(dumpPath).ToArray();
            Assert.AreEqual(2, dumpModules.Length);

            var firstModule = dumpModules.First();
            Assert.AreEqual(expectedFileName, firstModule.Path);
            Assert.AreEqual(TestData.expectedId, firstModule.Id);

            var lastModule = dumpModules.Last();
            Assert.AreEqual(anotherExpectedFileName, lastModule.Path);
            Assert.AreEqual(TestData.anotherExpectedId, lastModule.Id);
        }
    }
}