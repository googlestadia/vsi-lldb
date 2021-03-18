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
using System;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;

namespace SymbolStores.Tests
{
    [TestFixture]
    class FileReferenceTests
    {
        const string SOURCE_PATH = @"C:\source\foo";
        const string DEST_PATH = @"C:\dest\foo";
        const string CONTENTS = "test";

        MockFileSystem fakeFileSystem;
        FileReference.Factory fileReferenceFactory;

        [SetUp]
        public void SetUp()
        {
            fakeFileSystem = new MockFileSystem();
            fileReferenceFactory = new FileReference.Factory(fakeFileSystem);
        }

        [Test]
        public void Create_NullFilepath()
        {
            Assert.Throws<ArgumentNullException>(() => fileReferenceFactory.Create(null));
        }

        [Test]
        public async Task CopyToAsync()
        {
            fakeFileSystem.AddFile(SOURCE_PATH, new MockFileData(CONTENTS));
            var fileReference = fileReferenceFactory.Create(SOURCE_PATH);

            await fileReference.CopyToAsync(DEST_PATH);

            Assert.AreEqual(CONTENTS, fakeFileSystem.GetFile(SOURCE_PATH).TextContents);
            Assert.AreEqual(CONTENTS, fakeFileSystem.GetFile(DEST_PATH).TextContents);
        }

        [Test]
        public async Task CopyTo_DestinationAlreadyExistsAsync()
        {
            const string DEST_CONTENTS = "dest";
            fakeFileSystem.AddFile(SOURCE_PATH, new MockFileData(CONTENTS));
            fakeFileSystem.AddFile(DEST_PATH, new MockFileData(DEST_CONTENTS));
            var fileReference = fileReferenceFactory.Create(SOURCE_PATH);

            await fileReference.CopyToAsync(DEST_PATH);
            Assert.AreEqual(CONTENTS, fakeFileSystem.GetFile(SOURCE_PATH).TextContents);
            Assert.AreEqual(CONTENTS, fakeFileSystem.GetFile(DEST_PATH).TextContents);
        }

        [Test]
        public void CopyTo_NullDestFilepath()
        {
            var fileReference = fileReferenceFactory.Create(SOURCE_PATH);

            Assert.ThrowsAsync<ArgumentNullException>(
                () => fileReference.CopyToAsync(null));
        }
    }
}
