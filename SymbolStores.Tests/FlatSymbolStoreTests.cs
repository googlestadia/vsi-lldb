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

using NSubstitute;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading.Tasks;
using YetiCommon;

namespace SymbolStores.Tests
{
    [TestFixture]
    class FlatSymbolStoreTests : SymbolStoreBaseTests
    {
        const string _storePath = @"C:\flatStore";
        const string _storePathB = @"C:\flatStoreB";

        [Test]
        public void Constructor_EmptyPath()
        {
            Assert.Throws<ArgumentException>(
                () => new FlatSymbolStore(_fakeFileSystem, _moduleParser, ""));
        }

        [Test]
        public async Task FindFile_Exists_EmptyBuildIdAsync()
        {
            var store = await GetStoreWithFileAsync();

            var fileReference = await store.FindFileAsync(_filename, BuildId.Empty, true,
                                                          _log, _forceLoad);

            Assert.AreEqual(Path.Combine(_storePath, _filename), fileReference.Location);
            StringAssert.Contains(Strings.FileFound(Path.Combine(_storePath, _filename)),
                                  _log.ToString());
        }

        [Test]
        public async Task FindFile_BuildIdMismatchAsync()
        {
            var mismatchedBuildId = new BuildId("4321");
            
            var store = await GetStoreWithFileAsync();
            string pathInStore = Path.Combine(_storePath, _filename);
            _moduleParser.ParseBuildIdInfo(pathInStore, true)
                .Returns(new BuildIdInfo() { Data = _buildId });

            var fileReference = await store.FindFileAsync(_filename, mismatchedBuildId, true,
                                                          _log, _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.BuildIdMismatch(pathInStore,
                                                          mismatchedBuildId, _buildId),
                                  _log.ToString());
        }

        [Test]
        public async Task FindFile_InvalidStoreAsync()
        {
            var store = new FlatSymbolStore(_fakeFileSystem, _moduleParser, _invalidPath);

            var fileReference = await store.FindFileAsync(_filename, _buildId, true,
                                                          _log, _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchFlatStore(_invalidPath, _filename, ""),
                                  _log.ToString());
        }

        [Test]
        public async Task FindFile_ReadBuildIdFailureAsync()
        {
            var errorMessage = "test exception"; 
            BuildIdInfo failedBuildId = new BuildIdInfo();
            failedBuildId.AddError(errorMessage);
            
            var moduleParser = Substitute.For<IModuleParser>();
            moduleParser.ParseBuildIdInfo(Arg.Any<string>(), true).Returns(failedBuildId);
            var store = new FlatSymbolStore(_fakeFileSystem, moduleParser, _storePath);
            _fakeBuildIdWriter.WriteBuildId(Path.Combine(_storePath, _filename), _buildId);

            var fileReference = await store.FindFileAsync(_filename, _buildId, true,
                                                          _log, _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(errorMessage, _log.ToString());
        }

        [Test]
        public void DeepEquals()
        {
            var storeA = new FlatSymbolStore(_fakeFileSystem, _moduleParser, _storePath);
            var storeB = new FlatSymbolStore(_fakeFileSystem, _moduleParser, _storePath);

            Assert.True(storeA.DeepEquals(storeB));
            Assert.True(storeB.DeepEquals(storeA));
        }

        [Test]
        public void DeepEquals_NotEqual()
        {
            var storeA = new FlatSymbolStore(_fakeFileSystem, _moduleParser, _storePath);
            var storeB = new FlatSymbolStore(_fakeFileSystem, _moduleParser, _storePathB);

            Assert.False(storeA.DeepEquals(storeB));
            Assert.False(storeB.DeepEquals(storeA));
        }

        protected override ISymbolStore GetEmptyStore()
        {
            return new FlatSymbolStore(_fakeFileSystem, _moduleParser, _storePath);
        }

        protected override Task<ISymbolStore> GetStoreWithFileAsync()
        {
            _fakeBuildIdWriter.WriteBuildId(Path.Combine(_storePath, _filename), _buildId);
            return Task.FromResult<ISymbolStore>(
                new FlatSymbolStore(_fakeFileSystem, _moduleParser, _storePath));
        }
    }
}
