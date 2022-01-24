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
        protected const string _filename = "test.debug";
        protected const string _sourceFilepath = @"C:\source\" + _filename;
        protected const string _missingFilepath = @"C:\missing\" + _filename;
        protected const string _destFilepath = @"C:\dest\" + _filename;
        protected const string _invalidPath = @"C:\invalid|";
        protected static readonly BuildId _buildId = new BuildId("1234");

        protected MockFileSystem _fakeFileSystem;
        protected FakeBinaryFileUtil _fakeBinaryFileUtil;
        protected FakeBuildIdWriter _fakeBuildIdWriter;
        protected FileReference _sourceSymbolFile;
        protected StringWriter _log;
        protected bool _forceLoad = true;

        [SetUp]
        public virtual void SetUp()
        {
            _fakeFileSystem = new MockFileSystem();
            _fakeBinaryFileUtil = new FakeBinaryFileUtil(_fakeFileSystem);
            _fakeBuildIdWriter = new FakeBuildIdWriter(_fakeFileSystem);
            _fakeBuildIdWriter.WriteBuildId(_sourceFilepath, _buildId);
            _sourceSymbolFile = new FileReference(_fakeFileSystem, _sourceFilepath);
            _log = new StringWriter();
        }

        [Test]
        public async Task FindFile_ExistsAsync()
        {
            var store = await GetStoreWithFileAsync();

            var fileReference = await store.FindFileAsync(_filename, _buildId, true,
                                                          _log, _forceLoad);
            await fileReference.CopyToAsync(_destFilepath);

            Assert.AreEqual(_buildId, await _fakeBinaryFileUtil.ReadBuildIdAsync(_destFilepath));
            StringAssert.Contains(Strings.FileFound(""), _log.ToString());
        }

        [Test]
        public async Task FindFile_NoLogAsync()
        {
            var store = await GetStoreWithFileAsync();

            var fileReference = await store.FindFileAsync(_filename, _buildId);
            await fileReference.CopyToAsync(_destFilepath);

            Assert.AreEqual(_buildId, await _fakeBinaryFileUtil.ReadBuildIdAsync(_destFilepath));
        }

        [Test]
        public void FindFile_NullFilename()
        {
            var store = GetEmptyStore();

            Assert.ThrowsAsync<ArgumentException>(() => store.FindFileAsync(null, _buildId));
        }

        [Test]
        public async Task FindFile_DoesNotExistAsync()
        {
            var store = GetEmptyStore();

            var fileReference = await store.FindFileAsync(_filename, _buildId, true,
                                                          _log, _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FileNotFound(""), _log.ToString());
        }

        [Test]
        public async Task AddFile_WhenSupportedAsync()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            var fileReference = await store.AddFileAsync(_sourceSymbolFile, _filename,
                                                         _buildId, _log);

            Assert.AreEqual(_buildId,
                            await _fakeBinaryFileUtil.ReadBuildIdAsync(fileReference.Location));
            StringAssert.Contains(Strings.CopiedFile(_filename, fileReference.Location),
                                  _log.ToString());
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
                () => store.AddFileAsync(_sourceSymbolFile, _filename, _buildId));
        }

        [Test]
        public async Task AddFile_NoLogAsync()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            var fileReference = await store.AddFileAsync(_sourceSymbolFile, _filename, _buildId);
            await fileReference.CopyToAsync(_destFilepath);

            Assert.AreEqual(_buildId, await _fakeBinaryFileUtil.ReadBuildIdAsync(_destFilepath));
        }

        [Test]
        public async Task AddFile_AlreadyExistsAsync()
        {
            var store = await GetStoreWithFileAsync();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            await store.AddFileAsync(_sourceSymbolFile, _filename, _buildId);
        }

        [Test]
        public void AddFile_MissingSource()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            Assert.ThrowsAsync<SymbolStoreException>(
                () => store.AddFileAsync(new FileReference(_fakeFileSystem, _missingFilepath),
                                         _filename, _buildId));
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
                () => store.AddFileAsync(new FileReference(_fakeFileSystem, _invalidPath),
                                         _filename,
                                         _buildId));
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
                () => store.AddFileAsync(null, _filename, _buildId));
        }

        [TestCase(null, "1234")]
        [TestCase(_filename, "")]
        public void AddFile_InvalidArgument(string filename, string buildIdStr)
        {
            var buildId = new BuildId(buildIdStr);
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            Assert.ThrowsAsync<ArgumentException>(
                () => store.AddFileAsync(_sourceSymbolFile, filename, buildId));
        }

        /// <summary>
        /// Gets a valid store of the type being tested that contains no files. Does not need to be
        /// unique.
        /// </summary>
        protected abstract ISymbolStore GetEmptyStore();

        /// <summary>
        /// Gets a store of the type being tested that contains a file with the filename `FILENAME`
        /// and build id `BUILD_ID`
        /// </summary>
        protected abstract Task<ISymbolStore> GetStoreWithFileAsync();
    }
}
