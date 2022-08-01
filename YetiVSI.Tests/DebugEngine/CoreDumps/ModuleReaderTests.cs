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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YetiCommon;
using YetiVSI.DebugEngine.CoreDumps;

namespace YetiVSI.Test.DebugEngine.CoreDumps
{
    [TestFixture]
    public class ModuleReaderTests
    {
        [Test]
        public void EmptyStream()
        {
            var emptyReader = new BinaryReader(new MemoryStream());
            var moduleReader = new ModuleReader(new List<ProgramHeader>(), emptyReader);
            var module = moduleReader.GetModule(new FileSection("", 0, 0));
            Assert.True(BuildId.IsNullOrEmpty(module.BuildId));
        }

        [Test]
        public void CorrectBuildIdInSingleSegment()
        {
            var data = TestData.GetElfFileBytesFromBuildId(TestData.expectedId);
            var segment = new ProgramHeader(ProgramHeader.Type.LoadableSegment, 0, 0,
                (ulong) data.Length);

            var module = ReadModule(data, new[] {segment},
                                    new FileSection("", 0, (ulong) data.Length));
            Assert.AreEqual(TestData.expectedId, module.BuildId);
        }

        [Test]
        public void ElfHeaderEndsOutsideSegment()
        {
            var data = TestData.GetElfFileBytesFromBuildId(TestData.expectedId);
            var segment = new ProgramHeader(
                ProgramHeader.Type.LoadableSegment, 0, 0, ElfHeader.Size - 1);

            var module = ReadModule(data, new[] {segment},
                                    new FileSection("", 0, (ulong) data.Length));
            Assert.True(BuildId.IsNullOrEmpty(module.BuildId));
        }

        [Test]
        public void ProgramHeaderEndsOutsideSegment()
        {
            var data = TestData.GetElfFileBytesFromBuildId(TestData.expectedId);
            var segment = new ProgramHeader(ProgramHeader.Type.LoadableSegment, 0, 0,
                                            ElfHeader.Size + ProgramHeader.Size - 1);

            var module = ReadModule(data, new[] {segment},
                                    new FileSection("", 0, (ulong) data.Length));
            Assert.True(BuildId.IsNullOrEmpty(module.BuildId));
        }

        [Test]
        public void BuildIdEndsOutsideSegment()
        {
            var data = TestData.GetElfFileBytesFromBuildId(TestData.expectedId);
            var segment = new ProgramHeader(
                ProgramHeader.Type.LoadableSegment, 0, 0, (ulong)data.Length - 1);

            var module = ReadModule(data, new[] {segment},
                                    new FileSection("", 0, (ulong) data.Length));
            Assert.AreNotEqual(TestData.expectedId, module.BuildId);
        }

        [Test]
        public void FileStartBeforeSegment()
        {
            var data = TestData.GetElfFileBytesFromBuildId(TestData.expectedId);
            var segment = new ProgramHeader(ProgramHeader.Type.LoadableSegment, 0, 1,
                (ulong) data.Length);

            var module = ReadModule(data, new[] {segment},
                                    new FileSection("", 0, (ulong) data.Length));
            Assert.True(BuildId.IsNullOrEmpty(module.BuildId));
        }

        [Test]
        public void FileAtTheMiddleOfSegment()
        {
            var buildIdFileData = TestData.GetElfFileBytesFromBuildId(TestData.expectedId);
            var buildIdLength = buildIdFileData.Length;
            var data = new byte[buildIdLength].Concat(buildIdFileData).ToArray();

            var segment = new ProgramHeader(ProgramHeader.Type.LoadableSegment, 0, 0,
                (ulong) data.Length);

            var module = ReadModule(data, new[] { segment },
                new FileSection("", (ulong) buildIdLength, (ulong) buildIdLength * 2));
            Assert.AreEqual(TestData.expectedId, module.BuildId);
        }

        [Test]
        public void ExecutableFileAtTheMiddleOfSegment()
        {
            var buildIdFileData =
                TestData.GetElfFileBytesFromBuildId(TestData.expectedId, isExecutable: true);
            var buildIdLength = buildIdFileData.Length;
            var data = new byte[buildIdLength].Concat(buildIdFileData).ToArray();

            var segment = new ProgramHeader(ProgramHeader.Type.LoadableSegment, 0, 0,
                (ulong)data.Length);

            var module = ReadModule(data, new[] { segment },
                new FileSection("", (ulong)buildIdLength, (ulong)buildIdLength * 2));
            Assert.AreEqual(TestData.expectedId, module.BuildId);
            Assert.AreEqual(true, module.IsExecutable);
        }

        [Test]
        public void FileInSecondSegment()
        {
            var buildIdFileData = TestData.GetElfFileBytesFromBuildId(TestData.expectedId);
            var buildIdLength = buildIdFileData.Length;
            var data = new byte[buildIdLength].Concat(buildIdFileData).ToArray();
            var firstSegment = new ProgramHeader(ProgramHeader.Type.LoadableSegment, 0, 0,
                (ulong) buildIdLength);

            var secondSegment = new ProgramHeader(ProgramHeader.Type.LoadableSegment,
                (ulong) buildIdLength, (ulong) buildIdLength, (ulong) buildIdLength * 2);

            var module = ReadModule(data, new[] { secondSegment, firstSegment },
                            new FileSection("", (ulong)buildIdLength, (ulong) buildIdLength * 2));
            Assert.AreEqual(TestData.expectedId, module.BuildId);
        }

        [Test]
        [TestCase(ElfHeader.Size - 1, TestName = "ElfSeparated")]
        [TestCase(ElfHeader.Size + ProgramHeader.Size - 1, TestName = "ProgramHeaderSeparated")]
        [TestCase(ElfHeader.Size + ProgramHeader.Size + 10, TestName = "DataSeparated")]
        public void FileInSeveralSegments(int firstSegmentSize)
        {
            var data = TestData.GetElfFileBytesFromBuildId(TestData.expectedId);
            var secondSegmentSize = data.Length - firstSegmentSize;
            var firstSegment = new ProgramHeader(
                ProgramHeader.Type.LoadableSegment, 0, 0, (ulong) firstSegmentSize);

            var secondSegment = new ProgramHeader(ProgramHeader.Type.LoadableSegment,
                                                  (ulong) firstSegmentSize,
                                                  (ulong) firstSegmentSize,
                                                  (ulong) secondSegmentSize);

            var module = ReadModule(data, new[] {secondSegment, firstSegment},
                                    new FileSection("", 0, (ulong) data.Length));
            Assert.AreEqual(TestData.expectedId, module.BuildId);
        }

        [Test]
        public void SeveralFilesInOneSegment()
        {
            var firstFileBytes = TestData.GetElfFileBytesFromBuildId(TestData.expectedId);
            var secondFileBytes = TestData.GetElfFileBytesFromBuildId(TestData.anotherExpectedId);
            var data = firstFileBytes.Concat(secondFileBytes).ToArray();

            var firstFileSection = new FileSection("", 0, (ulong) firstFileBytes.Length);
            var secondFileSection = new FileSection("", (ulong) firstFileBytes.Length,
                                                (ulong) data.Length);

            var reader = new BinaryReader(new MemoryStream(data));

            var segment = new ProgramHeader(ProgramHeader.Type.LoadableSegment, 0, 0,
                (ulong) data.Length);

            var firstModule = ReadModule(data, new[] {segment}, firstFileSection);
            Assert.AreEqual(TestData.expectedId, firstModule.BuildId);

            var secondModule = ReadModule(data, new[] {segment}, secondFileSection);
            Assert.AreEqual(TestData.anotherExpectedId, secondModule.BuildId);
        }

        [Test]
        public void SeveralFilesInSeveralSegments()
        {
            var firstFileBytes = TestData.GetElfFileBytesFromBuildId(TestData.expectedId);
            var secondFileBytes = TestData.GetElfFileBytesFromBuildId(TestData.anotherExpectedId);
            var data = firstFileBytes.Concat(secondFileBytes).ToArray();

            var firstFileSection = new FileSection("", 0, (ulong) firstFileBytes.Length);
            var secondFileSection = new FileSection("", (ulong) firstFileBytes.Length,
                                                (ulong) data.Length);

            var reader = new BinaryReader(new MemoryStream(data));

            var firstSegmentSize = firstFileBytes.Length - 1;
            var firstSegment = new ProgramHeader(ProgramHeader.Type.LoadableSegment, 0, 0,
                                                 (ulong) firstFileBytes.Length - 1);

            var secondSegment = new ProgramHeader(ProgramHeader.Type.LoadableSegment,
                                                  (ulong) firstSegmentSize,
                                                  (ulong) firstSegmentSize,
                                                  (ulong) secondFileBytes.Length + 1);

            var firstModule = ReadModule(data, new[] { secondSegment, firstSegment },
                                         firstFileSection);
            Assert.AreEqual(TestData.expectedId, firstModule.BuildId);

            var secondModule = ReadModule(data, new[] { firstSegment, secondSegment },
                                          secondFileSection);
            Assert.AreEqual(TestData.anotherExpectedId, secondModule.BuildId);
        }

        private DumpModule ReadModule(byte[] data, ProgramHeader[] segments, FileSection index)
        {
            var reader = new BinaryReader(new MemoryStream(data));

            var moduleReader = new ModuleReader(segments.ToList(), reader);

            return moduleReader.GetModule(index);
        }
    }
}