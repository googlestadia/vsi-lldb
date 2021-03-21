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

using NUnit.Framework;
using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
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
        public async Task FindFile_ExistsAsync()
        {
            var store = await GetStoreWithFileAsync();

            var fileReference = await store.FindFileAsync(FILENAME, BUILD_ID, true, log);
            await fileReference.CopyToAsync(DEST_FILEPATH);

            Assert.AreEqual(
                BUILD_ID, await fakeBinaryFileUtil.ReadBuildIdAsync(DEST_FILEPATH));
            StringAssert.Contains(Strings.FileFound(""), log.ToString());
        }

        [Test]
        public async Task FindFile_NoLogAsync()
        {
            var store = await GetStoreWithFileAsync();

            var fileReference = await store.FindFileAsync(FILENAME, BUILD_ID);
            await fileReference.CopyToAsync(DEST_FILEPATH);

            Assert.AreEqual(
                BUILD_ID, await fakeBinaryFileUtil.ReadBuildIdAsync(DEST_FILEPATH));
        }

        [Test]
        public void FindFile_NullFilenameAsync()
        {
            var store = GetEmptyStore();

            Assert.ThrowsAsync<ArgumentException>(() => store.FindFileAsync(null, BUILD_ID));
        }

        [Test]
        public async Task FindFile_DoesNotExistAsync()
        {
            var store = GetEmptyStore();

            var fileReference = await store.FindFileAsync(FILENAME, BUILD_ID, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FileNotFound(""), log.ToString());
        }

        [Test]
        public async Task AddFile_WhenSupportedAsync()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            var fileReference = await store.AddFileAsync(
                sourceSymbolFile, FILENAME, BUILD_ID, log);

            Assert.AreEqual(
                BUILD_ID, await fakeBinaryFileUtil.ReadBuildIdAsync(fileReference.Location));
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

            Assert.ThrowsAsync<NotSupportedException>(
                () => store.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID));
        }

        [Test]
        public async Task AddFile_NoLogAsync()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            var fileReference = await store.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID);
            await fileReference.CopyToAsync(DEST_FILEPATH);

            Assert.AreEqual(
                BUILD_ID, await fakeBinaryFileUtil.ReadBuildIdAsync(DEST_FILEPATH));
        }

        [Test]
        public async Task AddFile_AlreadyExistsAsync()
        {
            var store = await GetStoreWithFileAsync();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            await store.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID);
        }

        [Test]
        public void AddFile_MissingSource()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            Assert.ThrowsAsync<SymbolStoreException>(() =>
                store.AddFileAsync(fileReferenceFactory.Create(MISSING_FILEPATH), FILENAME,
                    BUILD_ID));
        }

        [Test]
        public void AddFile_InvalidPath()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            Assert.ThrowsAsync<SymbolStoreException>(
                () => store.AddFileAsync(
                    fileReferenceFactory.Create(INVALID_PATH), FILENAME, BUILD_ID));
        }

        [Test]
        public void AddFile_NullSource()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            Assert.ThrowsAsync<ArgumentException>(
                () => store.AddFileAsync(null, FILENAME, BUILD_ID));
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

            Assert.ThrowsAsync<ArgumentException>(
                () => store.AddFileAsync(sourceSymbolFile, filename, buildId));
        }

        // Gets a valid store of the type being tested that contains no files. Does not need to be
        // unique.
        protected abstract ISymbolStore GetEmptyStore();

        // Gets a store of the type being tested that contains a file with the filename `FILENAME`
        // and build id `BUILD_ID`
        protected abstract Task<ISymbolStore> GetStoreWithFileAsync();
    }
}
