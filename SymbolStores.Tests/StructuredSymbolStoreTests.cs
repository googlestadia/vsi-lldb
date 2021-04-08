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
using System.Threading.Tasks;
using YetiCommon;

namespace SymbolStores.Tests
{
    [TestFixture]
    class StructuredSymbolStoreTests : SymbolStoreBaseTests
    {
        const string STORE_PATH = @"C:\store";
        const string STORE_PATH_B = @"C:\storeB";

        StructuredSymbolStore.Factory structuredStoreFactory;

        public override void SetUp()
        {
            base.SetUp();

            structuredStoreFactory = new StructuredSymbolStore.Factory(fakeFileSystem,
                fileReferenceFactory);
        }

        [Test]
        public void Constructor_EmptyPath()
        {
            Assert.Throws<ArgumentException>(() => structuredStoreFactory.Create(""));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Constructor_IsCache(bool isCache)
        {
            var store = structuredStoreFactory.Create(STORE_PATH, isCache);
            Assert.AreEqual(isCache, store.IsCache);
        }

        [Test]
        public async Task FindFile_EmptyBuildIdAsync()
        {
            var store = GetEmptyStore();

            var fileReference = await store.FindFileAsync(FILENAME, BuildId.Empty, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchStructuredStore(STORE_PATH, FILENAME,
                Strings.EmptyBuildId), log.ToString());
        }

        [Test]
        public async Task FindFile_InvalidStoreAsync()
        {
            var store = structuredStoreFactory.Create(INVALID_PATH);

            var fileReference = await store.FindFileAsync(FILENAME, BUILD_ID, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchStructuredStore(INVALID_PATH, FILENAME,
                ""), log.ToString());
        }

        [Test]
        public async Task AddFile_VerifyPathStructureAsync()
        {
            var store = GetEmptyStore();

            var fileReference = await store.AddFileAsync(
                sourceSymbolFile, FILENAME, BUILD_ID, log);

            Assert.AreEqual(Path.Combine(STORE_PATH, FILENAME, BUILD_ID.ToString(), FILENAME),
                fileReference.Location);
        }

        [Test]
        public void AddFile_EmptyBuildId()
        {
            var store = GetEmptyStore();

            var exception = Assert.ThrowsAsync<ArgumentException>(
                () => store.AddFileAsync(sourceSymbolFile, FILENAME, BuildId.Empty));

            StringAssert.Contains(Strings.FailedToCopyToStructuredStore(STORE_PATH, FILENAME,
                Strings.EmptyBuildId), exception.Message);
        }

        [Test]
        public void DeepEquals()
        {
            var storeA = structuredStoreFactory.Create(STORE_PATH);
            var storeB = structuredStoreFactory.Create(STORE_PATH);

            Assert.True(storeA.DeepEquals(storeB));
            Assert.True(storeB.DeepEquals(storeA));
        }

        [Test]
        public void DeepEquals_NotEqual()
        {
            var storeA = structuredStoreFactory.Create(STORE_PATH);
            var storeB = structuredStoreFactory.Create(STORE_PATH_B);

            Assert.False(storeA.DeepEquals(storeB));
            Assert.False(storeB.DeepEquals(storeA));
        }

        [Test]
        public void DeepEquals_IsCacheMismatch()
        {
            var storeA = structuredStoreFactory.Create(STORE_PATH, false);
            var storeB = structuredStoreFactory.Create(STORE_PATH, true);

            Assert.False(storeA.DeepEquals(storeB));
            Assert.False(storeB.DeepEquals(storeA));
        }

        #region SymbolStoreBaseTests functions

        protected override ISymbolStore GetEmptyStore()
        {
            return structuredStoreFactory.Create(STORE_PATH);
        }

        protected override async Task<ISymbolStore> GetStoreWithFileAsync()
        {
            var store = structuredStoreFactory.Create(STORE_PATH);
            await store.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID);
            return store;
        }

        #endregion
    }
}
