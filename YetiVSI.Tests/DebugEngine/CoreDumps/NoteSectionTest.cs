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
using System.IO;
using System.Linq;
using YetiCommon;
using YetiVSI.DebugEngine.CoreDumps;

namespace YetiVSI.Test.DebugEngine.CoreDumps
{
    [TestFixture]
    public class NoteSectionTest
    {
        static string expectedName = "123";
        static ulong expectedStartAddress = 1;
        static ulong expectedEndAddress = 2;
        static ulong expectedOffset = 0;

        static string anotherExpectedName = "1234";
        static ulong anotherExpectedStartAddress = 2;
        static ulong anotherExpectedEndAddress = 3;

        [Test]
        public void EmptyStreamCoreFiles()
        {
            var emptyReader = new BinaryReader(new MemoryStream());
            var sections = NoteSection.ReadModuleSections(emptyReader, 1).ToArray();
            Assert.AreEqual(0, sections.Length);
        }

        [Test]
        public void ShortNoteStream()
        {
            var minNote = TestData.GetNoteSectionBytes("", 0, new byte[0]);
            var notEnoughBytes = minNote.Skip(1).ToArray();
            var emptyReader = new BinaryReader(new MemoryStream(notEnoughBytes));
            var sections = NoteSection.ReadModuleSections(emptyReader, minNote.Length).ToArray();
            Assert.AreEqual(0, sections.Length);
        }

        [Test]
        public void CorrectNtFileNote()
        {
            var ntFile = GetExpectedNtFileBytes();
            var noteBytes =
                TestData.GetNoteSectionBytes(NoteSection.CoreName, NoteSection.NtFileType, ntFile);

            var fileReader = new BinaryReader(new MemoryStream(noteBytes));
            var sections = NoteSection.ReadModuleSections(fileReader, ntFile.Length).ToArray();
            Assert.AreEqual(1, sections.Length);

            var section = sections.First();
            Assert.AreEqual(expectedEndAddress, section.EndAddress);
            Assert.AreEqual(expectedStartAddress, section.StartAddress);
            Assert.AreEqual(expectedName, section.Path);
        }

        [Test]
        public void WrongNtFileName()
        {
            var ntFile = GetExpectedNtFileBytes();
            var noteBytes = TestData.GetNoteSectionBytes("123", NoteSection.NtFileType, ntFile);

            var fileReader = new BinaryReader(new MemoryStream(noteBytes));
            var sections = NoteSection.ReadModuleSections(fileReader, ntFile.Length).ToArray();
            Assert.AreEqual(0, sections.Length);
        }

        [Test]
        public void WrongNtFileType()
        {
            var ntFile = GetExpectedNtFileBytes();
            var noteBytes = TestData.GetNoteSectionBytes(NoteSection.CoreName, 0, ntFile);

            var fileReader = new BinaryReader(new MemoryStream(noteBytes));
            var sections = NoteSection.ReadModuleSections(fileReader, ntFile.Length).ToArray();
            Assert.AreEqual(0, sections.Length);
        }

        [Test]
        public void NtFileWithSeveralLocations()
        {
            var StartAddresss = new ulong[] {expectedStartAddress + 1, expectedStartAddress};
            var offsets = new ulong[] {expectedOffset + 1, expectedOffset};
            var EndAddresss = new ulong[] {expectedEndAddress + 1, expectedEndAddress};
            var names = new string[] {expectedName, expectedName};

            var ntFile = TestData.GetNtFileSectionsBytes(StartAddresss, EndAddresss, offsets,
                names);
            var noteBytes =
                TestData.GetNoteSectionBytes(NoteSection.CoreName, NoteSection.NtFileType, ntFile);

            var fileReader = new BinaryReader(new MemoryStream(noteBytes));
            var sections = NoteSection.ReadModuleSections(fileReader, ntFile.Length).ToArray();
            Assert.AreEqual(1, sections.Length);

            var section = sections.First();
            Assert.AreEqual(expectedEndAddress, section.EndAddress);
            Assert.AreEqual(expectedStartAddress, section.StartAddress);
            Assert.AreEqual(expectedName, section.Path);
        }

        [Test]
        public void ShortNtFile()
        {
            var ntFile = GetExpectedNtFileBytes().Skip(1).ToArray();
            var noteBytes =
                TestData.GetNoteSectionBytes(NoteSection.CoreName, NoteSection.NtFileType, ntFile);

            var fileReader = new BinaryReader(new MemoryStream(noteBytes));
            var sections = NoteSection.ReadModuleSections(fileReader, ntFile.Length + 1).ToArray();
            Assert.AreEqual(0, sections.Length);
        }

        [Test]
        public void SeveralNtFiles()
        {
            var StartAddresss = new ulong[] {expectedStartAddress, anotherExpectedStartAddress};
            var offsets = new ulong[] {expectedOffset, expectedOffset};
            var EndAddresss = new ulong[] {expectedEndAddress, anotherExpectedEndAddress};
            var names = new string[] {expectedName, anotherExpectedName};

            var ntFiles = TestData.GetNtFileSectionsBytes(StartAddresss, EndAddresss, offsets, names);
            var noteBytes =
                TestData.GetNoteSectionBytes(NoteSection.CoreName, NoteSection.NtFileType, ntFiles);

            var fileReader = new BinaryReader(new MemoryStream(noteBytes));
            var sections = NoteSection.ReadModuleSections(fileReader, ntFiles.Length).ToArray();
            Assert.AreEqual(2, sections.Length);

            var first = sections.First();
            Assert.AreEqual(expectedEndAddress, first.EndAddress);
            Assert.AreEqual(expectedStartAddress, first.StartAddress);
            Assert.AreEqual(expectedName, first.Path);

            var last = sections.Last();
            Assert.AreEqual(anotherExpectedEndAddress, last.EndAddress);
            Assert.AreEqual(anotherExpectedStartAddress, last.StartAddress);
            Assert.AreEqual(anotherExpectedName, last.Path);
        }

        [Test]
        public void SeveralNtFilesNotes()
        {
            var firstNtFilesSection = GetExpectedNtFileBytes();
            var firstNoteBytes = TestData.GetNoteSectionBytes(
                NoteSection.CoreName, NoteSection.NtFileType, firstNtFilesSection);

            var wrongNtFilesSection = GetExpectedNtFileBytes();
            var wrongNoteBytes = TestData.GetNoteSectionBytes(
                NoteSection.GnuName, NoteSection.NtFileType, wrongNtFilesSection);

            var secondNtFileSection = TestData.GetNtFileSectionBytes(
                anotherExpectedStartAddress, anotherExpectedEndAddress, expectedOffset,
                anotherExpectedName);
            var secondNoteBytes = TestData.GetNoteSectionBytes(
                NoteSection.CoreName, NoteSection.NtFileType, secondNtFileSection);

            var fullNoteBytes = firstNoteBytes.Concat(wrongNoteBytes).Concat(secondNoteBytes)
                .ToArray();

            var fileReader = new BinaryReader(new MemoryStream(fullNoteBytes));
            var sections = NoteSection.ReadModuleSections(fileReader, fullNoteBytes.Length).ToArray();
            Assert.AreEqual(2, sections.Length);

            var first = sections.First();
            Assert.AreEqual(expectedEndAddress, first.EndAddress);
            Assert.AreEqual(expectedStartAddress, first.StartAddress);
            Assert.AreEqual(expectedName, first.Path);

            var last = sections.Last();
            Assert.AreEqual(anotherExpectedEndAddress, last.EndAddress);
            Assert.AreEqual(anotherExpectedStartAddress, last.StartAddress);
            Assert.AreEqual(anotherExpectedName, last.Path);
        }

        [Test]
        public void CorrectBuildIdNote()
        {
            var noteBytes =
                TestData.GetNoteSectionBytes(NoteSection.GnuName, NoteSection.NtGnuBuildIdType,
                                             TestData.expectedId.Bytes.ToArray());

            var reader = new BinaryReader(new MemoryStream(noteBytes));
            var buildId = NoteSection.ReadBuildId(reader, noteBytes.Length, ModuleFormat.Elf);
            Assert.AreEqual(TestData.expectedId, buildId);
        }

        [Test]
        public void WrongIdNoteType()
        {
            var noteBytes = TestData.GetNoteSectionBytes(
                NoteSection.GnuName, NoteSection.NtFileType, TestData.expectedId.Bytes.ToArray());

            var reader = new BinaryReader(new MemoryStream(noteBytes));
            var buildId = NoteSection.ReadBuildId(reader, noteBytes.Length, ModuleFormat.Elf);

            Assert.AreEqual(BuildId.Empty, buildId);
        }

        [Test]
        public void WrongIdNameType()
        {
            var noteBytes =
                TestData.GetNoteSectionBytes(NoteSection.CoreName, NoteSection.NtGnuBuildIdType,
                                             TestData.expectedId.Bytes.ToArray());

            var reader = new BinaryReader(new MemoryStream(noteBytes));
            var buildId = NoteSection.ReadBuildId(reader, noteBytes.Length, ModuleFormat.Elf);

            Assert.AreEqual(BuildId.Empty, buildId);
        }

        byte[] GetExpectedNtFileBytes()
        {
            return TestData.GetNtFileSectionBytes(expectedStartAddress, expectedEndAddress,
                                                  expectedOffset, expectedName);
        }
    }
}