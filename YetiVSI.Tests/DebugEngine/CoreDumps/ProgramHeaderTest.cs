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
using YetiVSI.DebugEngine.CoreDumps;

namespace YetiVSI.Test.DebugEngine.CoreDumps
{
    [TestFixture]
    public class ProgramHeaderTest
    {
        readonly ulong expectedOffset = 10;
        readonly ulong expectedVaddr = 14;
        readonly ulong expectedSize = 41;

        [Test]
        public void EmptyStream()
        {
            var emptyReader = new BinaryReader(new MemoryStream());
            Assert.IsFalse(ProgramHeader.TryRead(emptyReader, out ProgramHeader h));
        }

        [Test]
        public void ShortStream()
        {
            var data = new byte[ProgramHeader.Size - 1];
            var reader = new BinaryReader(new MemoryStream(data));
            Assert.IsFalse(ProgramHeader.TryRead(reader, out ProgramHeader h));
        }

        [Test]
        public void LoadableSegment()
        {
            var data = TestData.GetProgramHeaderBytes(ProgramHeader.Type.LoadableSegment,
                                                      expectedOffset, expectedVaddr, expectedSize);

            var reader = new BinaryReader(new MemoryStream(data));
            Assert.IsTrue(ProgramHeader.TryRead(reader, out ProgramHeader h));
            Assert.AreEqual(expectedOffset, h.OffsetInDump);
            Assert.AreEqual(expectedVaddr, h.VirtualAddress);
            Assert.AreEqual(expectedSize, h.HeaderSize);
            Assert.AreEqual(ProgramHeader.Type.LoadableSegment, h.HeaderType);
        }

        [Test]
        public void NoteSegment()
        {
            var data = TestData.GetProgramHeaderBytes(ProgramHeader.Type.NoteSegment,
                                                      expectedOffset, expectedVaddr, expectedSize);

            var reader = new BinaryReader(new MemoryStream(data));
            Assert.IsTrue(ProgramHeader.TryRead(reader, out ProgramHeader h));
            Assert.AreEqual(expectedOffset, h.OffsetInDump);
            Assert.AreEqual(expectedVaddr, h.VirtualAddress);
            Assert.AreEqual(expectedSize, h.HeaderSize);
            Assert.AreEqual(ProgramHeader.Type.NoteSegment, h.HeaderType);
        }

        [Test]
        public void OtherSegment()
        {
            var data = TestData.GetProgramHeaderBytes(
                (ProgramHeader.Type) byte.MaxValue, expectedOffset, expectedVaddr, expectedSize);

            var reader = new BinaryReader(new MemoryStream(data));
            Assert.IsTrue(ProgramHeader.TryRead(reader, out ProgramHeader h));
            Assert.AreEqual(expectedOffset, h.OffsetInDump);
            Assert.AreEqual(expectedVaddr, h.VirtualAddress);
            Assert.AreEqual(expectedSize, h.HeaderSize);
            Assert.AreEqual(ProgramHeader.Type.Other, h.HeaderType);
        }
    }
}