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
using YetiVSI.DebugEngine.CoreDumps;

namespace YetiVSI.Test.DebugEngine.CoreDumps
{
    [TestFixture]
    public class ElfHeaderTest
    {
        byte[] elfData;

        readonly ulong expectedStartOffset = 42;
        readonly ushort expectedEntrySize = 10;
        readonly ushort expectedEntriesCount = 3;

        [SetUp]
        public void SetUp()
        {
            elfData = TestData.GetElfBytes(expectedStartOffset, expectedEntrySize,
                                           expectedEntriesCount, false);
        }

        [Test]
        public void EmptyStream()
        {
            var emptyReader = new BinaryReader(new MemoryStream());
            Assert.IsFalse(ElfHeader.TryRead(emptyReader, out ElfHeader h));
        }

        [Test]
        public void ShortStream()
        {
            var shortData = elfData.Take(ElfHeader.Size - 1).ToArray();
            var emptyReader = new BinaryReader(new MemoryStream(shortData));
            Assert.IsFalse(ElfHeader.TryRead(emptyReader, out ElfHeader h));
        }

        [Test]
        public void WrongMagicNumber()
        {
            elfData[0] = 0;
            var dataReader = new BinaryReader(new MemoryStream(elfData));
            Assert.IsFalse(ElfHeader.TryRead(dataReader, out ElfHeader h));
        }

        [Test]
        public void WrongBitness()
        {
            elfData[TestData.bitnessIndex] = (byte) ElfHeader.Bitness.x86;
            var dataReader = new BinaryReader(new MemoryStream(elfData));
            Assert.IsFalse(ElfHeader.TryRead(dataReader, out ElfHeader h));
        }

        [Test]
        public void WrongEndianness()
        {
            elfData[TestData.endiannessIndex] = (byte) ElfHeader.Endianness.Big;
            var dataReader = new BinaryReader(new MemoryStream(elfData));
            Assert.IsFalse(ElfHeader.TryRead(dataReader, out ElfHeader h));
        }

        [Test]
        public void WrongVersion()
        {
            elfData[TestData.versionIndex] = ElfHeader.CurrentElfVersion - 1;
            var dataReader = new BinaryReader(new MemoryStream(elfData));
            Assert.IsFalse(ElfHeader.TryRead(dataReader, out ElfHeader h));
        }

        [Test]
        public void CorrectEntry()
        {
            var dataReader = new BinaryReader(new MemoryStream(elfData));
            var isSuccess = ElfHeader.TryRead(dataReader, out ElfHeader h);
            var offsets = h.GetAbsoluteProgramHeaderOffsets().ToArray();
            var relativeOffsets = h.GetRelativeProgramHeaderOffsets().ToArray();

            Assert.IsTrue(isSuccess);
            Assert.AreEqual(expectedStartOffset, h.StartOffset);
            Assert.AreEqual(expectedEntriesCount, h.EntriesCount);
            Assert.AreEqual(expectedEntrySize, h.EntrySize);
            Assert.AreEqual(expectedEntriesCount, offsets.Length);
            Assert.AreEqual(expectedEntriesCount, relativeOffsets.Length);

            for (ulong i = 0; i < expectedEntriesCount; i++)
            {
                var offset = offsets[i];
                var relativeOffset = relativeOffsets[i];

                Assert.AreEqual(expectedStartOffset + i * expectedEntrySize, offset);
                Assert.AreEqual(i * expectedEntrySize, relativeOffset);
            }
        }
    }
}