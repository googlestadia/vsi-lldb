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
using System.Threading.Tasks;

namespace SymbolStores.Tests
{
    [TestFixture]
    class SymbolServerTests : SymbolStoreBaseTests
    {
        const string _storeAPath = @"C:\a";
        const string _storeBPath = @"C:\b";
        const string _storeCPath = @"C:\c";

        ISymbolStore _storeA;
        ISymbolStore _storeB;
        ISymbolStore _storeC;
        ISymbolStore _invalidStore;
        SymbolServer _symbolServer;

        public override void SetUp()
        {
            base.SetUp();

            _storeA = new StructuredSymbolStore(_fakeFileSystem, _storeAPath);
            _storeB = new StructuredSymbolStore(_fakeFileSystem, _storeBPath);
            _storeC = new StructuredSymbolStore(_fakeFileSystem, _storeCPath);
            _invalidStore = new StructuredSymbolStore(_fakeFileSystem, _invalidPath);
            _symbolServer = new SymbolServer();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Constructor_IsCache(bool isCache)
        {
            var store = new SymbolServer(isCache);
            Assert.AreEqual(isCache, store.IsCache);
        }

        [Test]
        public async Task FindFile_SingleStoreAsync()
        {
            await _storeA.AddFileAsync(_sourceSymbolFile, _filename, _buildId);
            _symbolServer.AddStore(_storeA);

            var fileReference = await _symbolServer.FindFileAsync(_filename, _buildId);

            Assert.AreEqual((await _storeA.FindFileAsync(_filename, _buildId)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_FirstStoreAsync()
        {
            await _storeA.AddFileAsync(_sourceSymbolFile, _filename, _buildId);
            _symbolServer.AddStore(_storeA);
            _symbolServer.AddStore(_storeB);
            _symbolServer.AddStore(_storeC);

            var fileReference = await _symbolServer.FindFileAsync(_filename, _buildId);

            Assert.AreEqual((await _storeA.FindFileAsync(_filename, _buildId)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_CascadeAsync()
        {
            await _storeC.AddFileAsync(_sourceSymbolFile, _filename, _buildId);
            _symbolServer.AddStore(_storeA);
            _symbolServer.AddStore(_storeB);
            _symbolServer.AddStore(_storeC);

            var fileReference = await _symbolServer.FindFileAsync(_filename, _buildId);

            Assert.NotNull(await _storeA.FindFileAsync(_filename, _buildId));
            Assert.NotNull(await _storeB.FindFileAsync(_filename, _buildId));
            Assert.AreEqual((await _storeA.FindFileAsync(_filename, _buildId)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_SkipInvalidAsync()
        {
            await _storeC.AddFileAsync(_sourceSymbolFile, _filename, _buildId);
            _symbolServer.AddStore(_storeA);
            _symbolServer.AddStore(_invalidStore);
            _symbolServer.AddStore(_storeC);

            var fileReference = await _symbolServer.FindFileAsync(_filename, _buildId);

            Assert.NotNull(await _storeA.FindFileAsync(_filename, _buildId));
            Assert.AreEqual((await _storeA.FindFileAsync(_filename, _buildId)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_NoStoresAsync()
        {
            var fileReference = await _symbolServer.FindFileAsync(_filename, _buildId);

            Assert.Null(fileReference);
        }

        [Test]
        public async Task FindFile_NotFoundAsync()
        {
            _symbolServer.AddStore(_storeA);
            _symbolServer.AddStore(_storeB);
            _symbolServer.AddStore(_storeC);

            var fileReference = await _symbolServer.FindFileAsync(_filename, _buildId);

            Assert.Null(fileReference);
        }

        [Test]
        public async Task AddFile_SingleStoreAsync()
        {
            _symbolServer.AddStore(_storeA);

            var fileReference =
                await _symbolServer.AddFileAsync(_sourceSymbolFile, _filename, _buildId);

            Assert.NotNull(await _storeA.FindFileAsync(_filename, _buildId));
            Assert.AreEqual((await _storeA.FindFileAsync(_filename, _buildId)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task AddFile_MultipleStoresAsync()
        {
            _symbolServer.AddStore(_storeA);
            _symbolServer.AddStore(_storeB);
            _symbolServer.AddStore(_storeC);

            var fileReference =
                await _symbolServer.AddFileAsync(_sourceSymbolFile, _filename, _buildId);

            Assert.NotNull(await _storeA.FindFileAsync(_filename, _buildId));
            Assert.NotNull(await _storeB.FindFileAsync(_filename, _buildId));
            Assert.NotNull(await _storeC.FindFileAsync(_filename, _buildId));
            Assert.AreEqual((await _storeA.FindFileAsync(_filename, _buildId)).Location,
                            fileReference.Location);
        }

        [Test]
        public void AddFile_NoStores()
        {
            Assert.ThrowsAsync<SymbolStoreException>(
                () => _symbolServer.AddFileAsync(_sourceSymbolFile, _filename, _buildId));
        }

        [Test]
        public async Task AddFile_SkipInvalidStoresAsync()
        {
            _symbolServer.AddStore(_storeA);
            _symbolServer.AddStore(_invalidStore);
            _symbolServer.AddStore(_storeC);

            var fileReference =
                await _symbolServer.AddFileAsync(_sourceSymbolFile, _filename, _buildId);

            Assert.NotNull(await _storeA.FindFileAsync(_filename, _buildId));
            Assert.NotNull(await _storeC.FindFileAsync(_filename, _buildId));
            Assert.AreEqual((await _storeA.FindFileAsync(_filename, _buildId)).Location,
                            fileReference.Location);
        }

        [Test]
        public void GetAllStores()
        {
            _symbolServer.AddStore(_storeA);
            var subServer = new SymbolServer();
            subServer.AddStore(_storeB);
            _symbolServer.AddStore(subServer);

            CollectionAssert.AreEqual(new[] { _symbolServer, _storeA, subServer, _storeB },
                                      _symbolServer.GetAllStores());
        }

        [Test]
        public void DeepEquals_NoStores()
        {
            var serverA = new SymbolServer();
            var serverB = new SymbolServer();

            Assert.True(serverA.DeepEquals(serverB));
            Assert.True(serverB.DeepEquals(serverA));
        }

        [Test]
        public void DeepEquals_WithStores()
        {
            var serverA = new SymbolServer();
            serverA.AddStore(_storeA);
            serverA.AddStore(_storeB);
            var serverB = new SymbolServer();
            serverB.AddStore(_storeA);
            serverB.AddStore(_storeB);

            Assert.True(serverA.DeepEquals(serverB));
            Assert.True(serverB.DeepEquals(serverA));
        }

        [Test]
        public void DeepEquals_DifferentOrder()
        {
            var serverA = new SymbolServer();
            serverA.AddStore(_storeA);
            serverA.AddStore(_storeB);
            var serverB = new SymbolServer();
            serverB.AddStore(_storeB);
            serverB.AddStore(_storeA);

            Assert.False(serverA.DeepEquals(serverB));
            Assert.False(serverB.DeepEquals(serverA));
        }

        [Test]
        public void DeepEquals_StoresNotEqual()
        {
            var serverA = new SymbolServer();
            serverA.AddStore(_storeA);
            var serverB = new SymbolServer();
            serverB.AddStore(_storeB);

            Assert.False(serverA.DeepEquals(serverB));
            Assert.False(serverB.DeepEquals(serverA));
        }

        [Test]
        public void DeepEquals_IsCacheMismatch()
        {
            var serverA = new SymbolServer();
            var serverB = new SymbolServer(isCache: true);

            Assert.False(serverA.DeepEquals(serverB));
            Assert.False(serverB.DeepEquals(serverA));
        }

        [Test]
        public void DeepEquals_DifferentLengths()
        {
            var serverA = new SymbolServer();
            serverA.AddStore(_storeA);
            serverA.AddStore(_storeB);
            var serverB = new SymbolServer();
            serverB.AddStore(_storeA);

            Assert.False(serverA.DeepEquals(serverB));
            Assert.False(serverB.DeepEquals(serverA));
        }

#region SymbolStoreBaseTests functions

        protected override ISymbolStore GetEmptyStore()
        {
            _symbolServer.AddStore(_storeA);
            return _symbolServer;
        }

        protected override async Task<ISymbolStore> GetStoreWithFileAsync()
        {
            _symbolServer.AddStore(_storeA);
            await _symbolServer.AddFileAsync(_sourceSymbolFile, _filename, _buildId);
            return _symbolServer;
        }

#endregion
    }
}
