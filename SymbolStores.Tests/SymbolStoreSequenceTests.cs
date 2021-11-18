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
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading.Tasks;
using YetiCommon;

namespace SymbolStores.Tests
{
    [TestFixture]
    class SymbolStoreSequenceTests : SymbolStoreBaseTests
    {
        const string CACHE_A_PATH = @"C:\cacheA";
        const string CACHE_B_PATH = @"C:\cacheB";
        const string STORE_A_PATH = @"C:\storeA";
        const string STORE_B_PATH = @"C:\storeB";

        ISymbolStore cacheA;
        ISymbolStore cacheB;
        ISymbolStore storeA;
        ISymbolStore storeB;
        SymbolStoreSequence storeSequence;

        public override void SetUp()
        {
            base.SetUp();

            storeSequence = new SymbolStoreSequence(fakeBinaryFileUtil);

            cacheA = new StructuredSymbolStore(fakeFileSystem, CACHE_A_PATH, isCache: true);
            cacheB = new StructuredSymbolStore(fakeFileSystem, CACHE_B_PATH, isCache: true);
            storeA = new StructuredSymbolStore(fakeFileSystem, STORE_A_PATH);
            storeB = new StructuredSymbolStore(fakeFileSystem, STORE_B_PATH);
        }

        [Test]
        public async Task FindFile_NoCacheAsync()
        {
            await storeA.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(storeA);

            var fileReference = await storeSequence.FindFileAsync(FILENAME, BUILD_ID);

            Assert.AreEqual((await storeA.FindFileAsync(FILENAME, BUILD_ID)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_WithCacheAsync()
        {
            await storeA.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(cacheA);
            storeSequence.AddStore(storeA);

            var fileReference = await storeSequence.FindFileAsync(FILENAME, BUILD_ID);

            Assert.NotNull(await cacheA.FindFileAsync(FILENAME, BUILD_ID));
            Assert.AreEqual((await cacheA.FindFileAsync(FILENAME, BUILD_ID)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_WithCacheAfterStoreAsync()
        {
            await storeA.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(storeA);
            storeSequence.AddStore(cacheA);

            var fileReference = await storeSequence.FindFileAsync(FILENAME, BUILD_ID);

            Assert.Null(await cacheA.FindFileAsync(FILENAME, BUILD_ID));
            Assert.AreEqual((await storeA.FindFileAsync(FILENAME, BUILD_ID)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_WithMultipeCachesAsync()
        {
            await storeB.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(cacheA);
            storeSequence.AddStore(storeA);
            storeSequence.AddStore(cacheB);
            storeSequence.AddStore(storeB);

            var fileReference = await storeSequence.FindFileAsync(FILENAME, BUILD_ID);

            Assert.NotNull(await cacheB.FindFileAsync(FILENAME, BUILD_ID));
            Assert.Null(await cacheA.FindFileAsync(FILENAME, BUILD_ID));
            Assert.AreEqual((await cacheB.FindFileAsync(FILENAME, BUILD_ID)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_CachedAsync()
        {
            await cacheA.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(cacheA);

            var fileReference = await storeSequence.FindFileAsync(FILENAME, BUILD_ID);

            Assert.AreEqual((await cacheA.FindFileAsync(FILENAME, BUILD_ID)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_CachedInLaterCacheAsync()
        {
            await cacheB.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(cacheA);
            storeSequence.AddStore(cacheB);

            var fileReference = await storeSequence.FindFileAsync(FILENAME, BUILD_ID);

            Assert.Null(await cacheA.FindFileAsync(FILENAME, BUILD_ID));
            Assert.AreEqual((await cacheB.FindFileAsync(FILENAME, BUILD_ID)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_NoStoresAsync()
        {
            var fileReference = await storeSequence.FindFileAsync(FILENAME, BUILD_ID);

            Assert.Null(fileReference);
        }

        [Test]
        public async Task FindFile_NotFoundAsync()
        {
            storeSequence.AddStore(cacheA);
            storeSequence.AddStore(storeA);

            var fileReference = await storeSequence.FindFileAsync(FILENAME, BUILD_ID);

            Assert.Null(fileReference);
        }

        [Test]
        public async Task FindFile_SkipUnsupportedCacheAsync()
        {
            await storeA.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID);
            var unsupportedCache = Substitute.For<ISymbolStore>();
            unsupportedCache.FindFileAsync("", BuildId.Empty, true, null, false)
                .ReturnsForAnyArgs((IFileReference)null);
            unsupportedCache.AddFileAsync(null, "", BuildId.Empty, null)
                .Throws(new NotSupportedException());
            storeSequence.AddStore(unsupportedCache);
            storeSequence.AddStore(storeA);

            var fileReference = await storeSequence.FindFileAsync(FILENAME, BUILD_ID);

            Assert.AreEqual((await storeA.FindFileAsync(FILENAME, BUILD_ID)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_InvalidSymbolFileWithCacheAsync()
        {
            string storeAPath = Path.Combine(STORE_A_PATH, FILENAME, BUILD_ID.ToString(), FILENAME);

            fakeBuildIdWriter.WriteBuildId(storeAPath, BUILD_ID);
            sourceSymbolFile = new FileReference(fakeFileSystem, storeAPath);
            fakeBinaryFileUtil.AddVerificationFailureFor(BUILD_ID, "Symbol verification error");

            storeSequence.AddStore(cacheA);
            storeSequence.AddStore(storeA);

            var logWriter = new StringWriter();

            var fileReference =
                await storeSequence.FindFileAsync(FILENAME, BUILD_ID, true, logWriter, false);

            Assert.That(fileReference, Is.Null);
        }

        [Test]
        public async Task FindFile_InvalidSymbolFileInCacheAsync()
        {
            // Test the scenario where the symbol store cache contains an invalid symbol file,
            // but the store contains a correct file. In that case, we should overwrite the cache
            // with the correct file and return a reference to it.
            string storeAPath = Path.Combine(STORE_A_PATH, FILENAME, BUILD_ID.ToString(), FILENAME);
            fakeBuildIdWriter.WriteBuildId(storeAPath, BUILD_ID);
            sourceSymbolFile = new FileReference(fakeFileSystem, storeAPath);

            BuildId badBuildId = new BuildId("BAAD");
            string cacheAPath = Path.Combine(CACHE_A_PATH, FILENAME, BUILD_ID.ToString(), FILENAME);
            fakeBuildIdWriter.WriteBuildId(cacheAPath, badBuildId);
            sourceSymbolFile = new FileReference(fakeFileSystem, cacheAPath);

            fakeBinaryFileUtil.AddVerificationFailureFor(badBuildId, "Symbol verification error");

            storeSequence.AddStore(cacheA);
            storeSequence.AddStore(storeA);

            var logWriter = new StringWriter();

            var fileReference =
                await storeSequence.FindFileAsync(FILENAME, BUILD_ID, true, logWriter, false);

            Assert.That(fileReference.Location,
                        Is.EqualTo((await cacheA.FindFileAsync(FILENAME, BUILD_ID)).Location));

            Assert.That(await fakeBinaryFileUtil.ReadBuildIdAsync(
                            (await storeA.FindFileAsync(FILENAME, BUILD_ID)).Location),
                        Is.EqualTo(BUILD_ID));
            Assert.That(await fakeBinaryFileUtil.ReadBuildIdAsync(
                            (await cacheA.FindFileAsync(FILENAME, BUILD_ID)).Location),
                        Is.EqualTo(BUILD_ID));
        }

        [Test]
        public void GetAllStores()
        {
            storeSequence.AddStore(storeA);
            storeSequence.AddStore(cacheA);
            var subStoreSequence = new SymbolStoreSequence(fakeBinaryFileUtil);
            subStoreSequence.AddStore(storeB);
            storeSequence.AddStore(subStoreSequence);

            CollectionAssert.AreEqual(
                new[] { storeSequence, storeA, cacheA, subStoreSequence, storeB },
                storeSequence.GetAllStores());
        }

        [Test]
        public void DeepEquals_NoStores()
        {
            var sequenceA = new SymbolStoreSequence(fakeBinaryFileUtil);
            var sequenceB = new SymbolStoreSequence(fakeBinaryFileUtil);

            Assert.True(sequenceA.DeepEquals(sequenceB));
            Assert.True(sequenceB.DeepEquals(sequenceA));
        }

        [Test]
        public void DeepEquals_WithStores()
        {
            var sequenceA = new SymbolStoreSequence(fakeBinaryFileUtil);
            sequenceA.AddStore(storeA);
            sequenceA.AddStore(storeB);
            var sequenceB = new SymbolStoreSequence(fakeBinaryFileUtil);
            sequenceB.AddStore(storeA);
            sequenceB.AddStore(storeB);

            Assert.True(sequenceA.DeepEquals(sequenceB));
            Assert.True(sequenceB.DeepEquals(sequenceA));
        }

        [Test]
        public void DeepEquals_StoresNotEquals()
        {
            var sequenceA = new SymbolStoreSequence(fakeBinaryFileUtil);
            sequenceA.AddStore(storeA);
            var sequenceB = new SymbolStoreSequence(fakeBinaryFileUtil);
            sequenceB.AddStore(storeB);

            Assert.False(sequenceA.DeepEquals(sequenceB));
            Assert.False(sequenceB.DeepEquals(sequenceA));
        }

        [Test]
        public void DeepEquals_DifferentLengths()
        {
            var sequenceA = new SymbolStoreSequence(fakeBinaryFileUtil);
            sequenceA.AddStore(storeA);
            sequenceA.AddStore(storeB);
            var sequenceB = new SymbolStoreSequence(fakeBinaryFileUtil);
            sequenceB.AddStore(storeA);

            Assert.False(sequenceA.DeepEquals(sequenceB));
            Assert.False(sequenceB.DeepEquals(sequenceA));
        }

#region SymbolStoreBaseTests functions

        protected override ISymbolStore GetEmptyStore()
        {
            storeSequence.AddStore(storeA);
            return storeSequence;
        }

        protected override async Task<ISymbolStore> GetStoreWithFileAsync()
        {
            await storeA.AddFileAsync(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(storeA);
            return storeSequence;
        }

#endregion
    }
}
