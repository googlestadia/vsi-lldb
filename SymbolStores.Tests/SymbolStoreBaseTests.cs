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

using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
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
        protected static BuildId _buildId = new BuildId("1234");
        protected const ModuleFormat _elfFormat = ModuleFormat.Elf;

        protected MockFileSystem _fakeFileSystem;
        protected IModuleParser _moduleParser;
        protected FakeBuildIdWriter _fakeBuildIdWriter;
        protected FileReference _sourceSymbolFile;
        protected StringWriter _log;
        protected TextWriter _nullLog = TextWriter.Null;

        protected ModuleSearchQuery _searchQuery =
            new ModuleSearchQuery(_filename, _buildId, _elfFormat)
        {
            ForceLoad = true,
            RequireDebugInfo = true
        };

        [SetUp]
        public virtual void SetUp()
        {
            _fakeFileSystem = new MockFileSystem();
            _moduleParser = Substitute.For<IModuleParser>();
            _moduleParser.ParseBuildIdInfo(Arg.Is<string>(x => x.EndsWith(_filename)), _elfFormat)
                .Returns(new BuildIdInfo() { Data = _buildId });
            _moduleParser.IsValidElf(Arg.Any<string>(), Arg.Any<bool>(), out string _)
                .Returns(true);

            _fakeBuildIdWriter = new FakeBuildIdWriter(_fakeFileSystem);
            _fakeBuildIdWriter.WriteBuildId(_sourceFilepath, _buildId);
            _sourceSymbolFile = new FileReference(_fakeFileSystem, _sourceFilepath);
            _log = new StringWriter();
        }

        [Test]
        public async Task FindFile_ExistsAsync()
        {
            ISymbolStore store = await GetStoreWithFileAsync();
            IFileReference fileReference = await store.FindFileAsync(_searchQuery, _log);
            await fileReference.CopyToAsync(_destFilepath);

            StringAssert.Contains(Strings.FileFound(""), _log.ToString());
        }

        [Test]
        public async Task FindFile_DoesNotExistAsync()
        {
            ISymbolStore store = GetEmptyStore();
            IFileReference fileReference = await store.FindFileAsync(_searchQuery, _log);

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
                () => store.AddFileAsync(_sourceSymbolFile, _filename, _buildId, _log));
        }

        [Test]
        public async Task AddFile_NoLogAsync()
        {
            var store = GetEmptyStore();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            var fileReference = await store.AddFileAsync(_sourceSymbolFile, _filename,
                                                         _buildId, _log);
            await fileReference.CopyToAsync(_destFilepath);
        }

        [Test]
        public async Task AddFile_AlreadyExistsAsync()
        {
            var store = await GetStoreWithFileAsync();
            if (!store.SupportsAddingFiles)
            {
                return;
            }

            await store.AddFileAsync(_sourceSymbolFile, _filename, _buildId, _log);
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
                                         _filename, _buildId, _log));
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
                                         _buildId, _log));
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
                () => store.AddFileAsync(null, _filename, _buildId, _log));
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
                () => store.AddFileAsync(_sourceSymbolFile, filename, buildId, _log));
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
