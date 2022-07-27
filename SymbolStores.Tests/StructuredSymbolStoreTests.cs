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
using NUnit.Framework;
using YetiCommon;

namespace SymbolStores.Tests
{
    [TestFixture]
    class StructuredSymbolStoreTests : SymbolStoreBaseTests
    {
        const string _storePath = @"C:\store";
        const string _storePathB = @"C:\storeB";

        [Test]
        public void Constructor_EmptyPath()
        {
            Assert.IsFalse(StructuredSymbolStore.IsStructuredStore(_fakeFileSystem, ""));
            Assert.Throws<ArgumentException>(() => new StructuredSymbolStore(_fakeFileSystem, ""));
        }

        [Test]
        public void Constructor_InvalidStore()
        {
            Assert.IsFalse(StructuredSymbolStore.IsStructuredStore(_fakeFileSystem, _invalidPath));
            Assert.Throws<ArgumentException>(
                () => new StructuredSymbolStore(_fakeFileSystem, _invalidPath));
        }

        [Test]
        public async Task AddFile_VerifyPathStructureAsync()
        {
            var store = GetEmptyStore();

            var fileReference = await store.AddFileAsync(_sourceSymbolFile, _filename,
                                                         _buildId, _log);

            Assert.AreEqual(
                Path.Combine(_storePath, _filename, _buildId.ToPathName(), _filename),
                fileReference.Location);
        }

        [Test]
        public void AddFile_EmptyBuildId()
        {
            var store = GetEmptyStore();

            var exception = Assert.ThrowsAsync<ArgumentException>(
                () => store.AddFileAsync(_sourceSymbolFile, _filename, BuildId.Empty, _nullLog));

            StringAssert.Contains(
                Strings.FailedToCopyToStructuredStore(_storePath, _filename, Strings.EmptyBuildId),
                exception.Message);
        }

        [Test]
        public void DeepEquals()
        {
            var storeA = new StructuredSymbolStore(_fakeFileSystem, _storePath);
            var storeB = new StructuredSymbolStore(_fakeFileSystem, _storePath);

            Assert.True(storeA.DeepEquals(storeB));
            Assert.True(storeB.DeepEquals(storeA));
        }

        [Test]
        public void DeepEquals_NotEqual()
        {
            var storeA = new StructuredSymbolStore(_fakeFileSystem, _storePath);
            var storeB = new StructuredSymbolStore(_fakeFileSystem, _storePathB);

            Assert.False(storeA.DeepEquals(storeB));
            Assert.False(storeB.DeepEquals(storeA));
        }

#region SymbolStoreBaseTests functions

        protected override ISymbolStore GetEmptyStore()
        {
            return new StructuredSymbolStore(_fakeFileSystem, _storePath);
        }

        protected override async Task<ISymbolStore> GetStoreWithFileAsync()
        {
            var store = new StructuredSymbolStore(_fakeFileSystem, _storePath);
            await store.AddFileAsync(_sourceSymbolFile, _filename, _buildId, _log);
            return store;
        }

#endregion
    }
}
