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
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using YetiCommon;

namespace SymbolStores.Tests
{
    [TestFixture]
    abstract class SymbolStoreBaseTests
    {
        protected const string FILENAME = "test.debug";
        protected const string SOURCE_FILEPATH = @"C:\source\" + FILENAME;
        protected const string MISSING_FILEPATH = @"C:\missing\" + FILENAME;
        protected const string DEST_FILEPATH = @"C:\dest\" + FILENAME;
        protected const string INVALID_PATH = @"C:\invalid|";
        protected static readonly BuildId BUILD_ID = new BuildId("1234");

        protected MockFileSystem fakeFileSystem;
        protected FakeBinaryFileUtil fakeBinaryFileUtil;
        protected FakeBuildIdWriter fakeBuildIdWriter;
        protected FileReference.Factory fileReferenceFactory;
        protected FileReference sourceSymbolFile;
        protected StringWriter log;

        [SetUp]
        public virtual void SetUp()
        {
            fakeFileSystem = new MockFileSystem();
            fakeBinaryFileUtil = new FakeBinaryFileUtil(fakeFileSystem);
            fakeBuildIdWriter = new FakeBuildIdWriter(fakeFileSystem);
            fileReferenceFactory = new FileReference.Factory(fakeFileSystem);
            fakeBuildIdWriter.WriteBuildId(SOURCE_FILEPATH, BUILD_ID);
            sourceSymbolFile = fileReferenceFactory.Create(SOURCE_FILEPATH);
            log = new StringWriter();
        }

        [Test]
        public void FindFile_Exists()
        {
            var store = GetStoreWithFile();

            var fileReference = store.FindFile(FILENAME, BUILD_ID, true, log);
            fileReference.CopyTo(DEST_FILEPATH);

            Assert.AreEqual(BUILD_ID, fakeBinaryFileUtil.ReadBuildId(DEST_FILEPATH));
            StringAssert.Contains(Strings.FileFound(""), log.ToString());
        }

        [Test]
        public void FindFile_NoLog()
        {
            var store = GetStoreWithFile();

            var fileReference = store.FindFile(FILENAME, BUILD_ID);
            fileReference.CopyTo(DEST_FILEPATH);

            Assert.AreEqual(BUILD_ID, fakeBinaryFileUtil.ReadBuildId(DEST_FILEPATH));
        }

        [Test]
        public void FindFile_NullFilename()
        {
            var store = GetEmptyStore();

            Assert.Throws<ArgumentException>(() => store.FindFile(null, BUILD_ID));
        }

        [Test]
        public void FindFile_DoesNotExist()
        {
            var store = GetEmptyStore();

            var fileReference = store.FindFile(FILENAME, BUILD_ID, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FileNotFound(""), log.ToString());
        }

        [Test]
        public void AddFile_WhenSupported()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            var fileReference = store.AddFile(sourceSymbolFile, FILENAME, BUILD_ID, log);

            Assert.AreEqual(BUILD_ID, fakeBinaryFileUtil.ReadBuildId(fileReference.Location));
            StringAssert.Contains(Strings.CopiedFile(FILENAME, fileReference.Location),
                log.ToString());
        }

        [Test]
        public void AddFile_WhenNotSupported()
        {
            var store = GetEmptyStore();
            if (store.SupportsAddingFiles)
            {
                return;
            }

            Assert.Throws<NotSupportedException>(
                () => store.AddFile(sourceSymbolFile, FILENAME, BUILD_ID));
        }

        [Test]
        public void AddFile_NoLog()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            var fileReference = store.AddFile(sourceSymbolFile, FILENAME, BUILD_ID);
            fileReference.CopyTo(DEST_FILEPATH);

            Assert.AreEqual(BUILD_ID, fakeBinaryFileUtil.ReadBuildId(DEST_FILEPATH));
        }

        [Test]
        public void AddFile_AlreadyExists()
        {
            var store = GetStoreWithFile();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            store.AddFile(sourceSymbolFile, FILENAME, BUILD_ID);
        }

        [Test]
        public void AddFile_MissingSource()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            Assert.Throws<SymbolStoreException>(() =>
            {
                store.AddFile(fileReferenceFactory.Create(MISSING_FILEPATH), FILENAME,
                    BUILD_ID);
            });
        }

        [Test]
        public void AddFile_InvalidPath()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            Assert.Throws<SymbolStoreException>(
                () => store.AddFile(fileReferenceFactory.Create(INVALID_PATH), FILENAME, BUILD_ID));
        }

        [Test]
        public void AddFile_NullSource()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            Assert.Throws<ArgumentException>(() => store.AddFile(null, FILENAME, BUILD_ID));
        }

        [TestCase(null, "1234")]
        [TestCase(FILENAME, "")]
        public void AddFile_InvalidArgument(string filename, string buildIdStr)
        {
            var buildId = new BuildId(buildIdStr);
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            Assert.Throws<ArgumentException>(
                () => store.AddFile(sourceSymbolFile, filename, buildId));
        }

        // Gets a valid store of the type being tested that contains no files. Does not need to be
        // unique.
        protected abstract ISymbolStore GetEmptyStore();

        // Gets a store of the type being tested that contains a file with the filename `FILENAME`
        // and build id `BUILD_ID`
        protected abstract ISymbolStore GetStoreWithFile();
    }
}
