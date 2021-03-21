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
        const string STORE_PATH = @"C:\flatStore";
        const string STORE_PATH_B = @"C:\flatStoreB";

        FlatSymbolStore.Factory flatStoreFactory;

        public override void SetUp()
        {
            base.SetUp();

            flatStoreFactory = new FlatSymbolStore.Factory(fakeFileSystem, fakeBinaryFileUtil,
                fileReferenceFactory);
        }

        [Test]
        public void Constructor_EmptyPath()
        {
            Assert.Throws<ArgumentException>(() => flatStoreFactory.Create(""));
        }

        [Test]
        public async Task FindFile_Exists_EmptyBuildIdAsync()
        {
            var store = await GetStoreWithFileAsync();

            var fileReference = await store.FindFileAsync(FILENAME, BuildId.Empty, true, log);

            Assert.AreEqual(Path.Combine(STORE_PATH, FILENAME), fileReference.Location);
            StringAssert.Contains(Strings.FileFound(Path.Combine(STORE_PATH, FILENAME)),
                log.ToString());
        }

        [Test]
        public async Task FindFile_BuildIdMismatchAsync()
        {
            var mismatchedBuildId = new BuildId("4321");

            var store = await GetStoreWithFileAsync();
            var fileReference = await store.FindFileAsync(FILENAME, mismatchedBuildId, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.BuildIdMismatch(Path.Combine(STORE_PATH, FILENAME),
                mismatchedBuildId, BUILD_ID), log.ToString());
        }

        [Test]
        public async Task FindFile_InvalidStoreAsync()
        {
            var store = flatStoreFactory.Create(INVALID_PATH);

            var fileReference = await store.FindFileAsync(FILENAME, BUILD_ID, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchFlatStore(INVALID_PATH, FILENAME,
                ""), log.ToString());
        }

        [Test]
        public async Task FindFile_ReadBuildIdFailureAsync()
        {
            var mockBinaryFileUtil = Substitute.For<IBinaryFileUtil>();
            mockBinaryFileUtil.ReadBuildIdAsync(Arg.Any<string>()).Returns<BuildId>(x =>
            {
                throw new BinaryFileUtilException("test exception");
            });
            var store = new FlatSymbolStore.Factory(fakeFileSystem, mockBinaryFileUtil,
                fileReferenceFactory).Create(STORE_PATH);
            fakeBuildIdWriter.WriteBuildId(Path.Combine(STORE_PATH, FILENAME), BUILD_ID);

            var fileReference = await store.FindFileAsync(FILENAME, BUILD_ID, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains("test exception", log.ToString());
        }

        [Test]
        public void DeepEquals()
        {
            var storeA = flatStoreFactory.Create(STORE_PATH);
            var storeB = flatStoreFactory.Create(STORE_PATH);

            Assert.True(storeA.DeepEquals(storeB));
            Assert.True(storeB.DeepEquals(storeA));
        }

        [Test]
        public void DeepEquals_NotEqual()
        {
            var storeA = flatStoreFactory.Create(STORE_PATH);
            var storeB = flatStoreFactory.Create(STORE_PATH_B);

            Assert.False(storeA.DeepEquals(storeB));
            Assert.False(storeB.DeepEquals(storeA));
        }

        #region SymbolsStoreBaseTests functions

        protected override ISymbolStore GetEmptyStore()
        {
            return flatStoreFactory.Create(STORE_PATH);
        }

        protected override Task<ISymbolStore> GetStoreWithFileAsync()
        {
            fakeBuildIdWriter.WriteBuildId(Path.Combine(STORE_PATH, FILENAME), BUILD_ID);
            return Task.FromResult<ISymbolStore>(flatStoreFactory.Create(STORE_PATH));
        }

        #endregion
    }
}
