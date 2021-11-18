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

        [Test]
        public void Constructor_EmptyPath()
        {
            Assert.Throws<ArgumentException>(() => new StructuredSymbolStore(fakeFileSystem, ""));
        }

        [Test]
        public async Task FindFile_EmptyBuildIdAsync()
        {
            var store = GetEmptyStore();

            var fileReference = await store.FindFileAsync(FILENAME, BuildId.Empty, true,
                                                          log, _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(
                Strings.FailedToSearchStructuredStore(STORE_PATH, FILENAME, Strings.EmptyBuildId),
                log.ToString());
        }

        [Test]
        public async Task FindFile_InvalidStoreAsync()
        {
            var store = new StructuredSymbolStore(fakeFileSystem, INVALID_PATH);

            var fileReference = await store.FindFileAsync(FILENAME, BUILD_ID, true,
                                                          log, _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchStructuredStore(INVALID_PATH, FILENAME, ""),
                                  log.ToString());
        }

        [Test]
        public async Task AddFile_VerifyPathStructureAsync()
        {
            var store = GetEmptyStore();

            var fileReference = await store.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID, log);

            Assert.AreEqual(Path.Combine(STORE_PATH, FILENAME, BUILD_ID.ToString(), FILENAME),
                            fileReference.Location);
        }

        [Test]
        public void AddFile_EmptyBuildId()
        {
            var store = GetEmptyStore();

            var exception = Assert.ThrowsAsync<ArgumentException>(
                () => store.AddFileAsync(sourceSymbolFile, FILENAME, BuildId.Empty));

            StringAssert.Contains(
                Strings.FailedToCopyToStructuredStore(STORE_PATH, FILENAME, Strings.EmptyBuildId),
                exception.Message);
        }

        [Test]
        public void DeepEquals()
        {
            var storeA = new StructuredSymbolStore(fakeFileSystem, STORE_PATH);
            var storeB = new StructuredSymbolStore(fakeFileSystem, STORE_PATH);

            Assert.True(storeA.DeepEquals(storeB));
            Assert.True(storeB.DeepEquals(storeA));
        }

        [Test]
        public void DeepEquals_NotEqual()
        {
            var storeA = new StructuredSymbolStore(fakeFileSystem, STORE_PATH);
            var storeB = new StructuredSymbolStore(fakeFileSystem, STORE_PATH_B);

            Assert.False(storeA.DeepEquals(storeB));
            Assert.False(storeB.DeepEquals(storeA));
        }

#region SymbolStoreBaseTests functions

        protected override ISymbolStore GetEmptyStore()
        {
            return new StructuredSymbolStore(fakeFileSystem, STORE_PATH);
        }

        protected override async Task<ISymbolStore> GetStoreWithFileAsync()
        {
            var store = new StructuredSymbolStore(fakeFileSystem, STORE_PATH);
            await store.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID);
            return store;
        }

#endregion
    }
}
