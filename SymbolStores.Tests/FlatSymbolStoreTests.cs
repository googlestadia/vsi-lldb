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
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
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
            Assert.IsFalse(FlatSymbolStore.IsFlatStore(_fakeFileSystem, _invalidPath));
            Assert.Throws<ArgumentException>(
                () => new FlatSymbolStore(_fakeFileSystem, _moduleParser, ""));
        }

        [Test]
        public void Constructor_InvalidStore()
        {
            Assert.IsFalse(FlatSymbolStore.IsFlatStore(_fakeFileSystem, _invalidPath));
            Assert.Throws<ArgumentException>(
                () => new FlatSymbolStore(_fakeFileSystem, _moduleParser, _invalidPath));
        }

        [Test]
        public async Task FindFile_Exists_EmptyBuildIdAsync()
        {
            var store = await GetStoreWithFileAsync();

            var fileReference = await store.FindFileAsync(_searchQuery, _log);

            Assert.AreEqual(Path.Combine(_storePath, _filename), fileReference.Location);
            StringAssert.Contains(Strings.FileFound(Path.Combine(_storePath, _filename)),
                                  _log.ToString());
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
