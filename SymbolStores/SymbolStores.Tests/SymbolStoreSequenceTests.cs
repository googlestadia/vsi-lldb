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

﻿using NSubstitute;
using NUnit.Framework;
using System;
using System.IO;
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
        StructuredSymbolStore.Factory structuredStoreFactory;
        SymbolStoreSequence.Factory storeSequenceFactory;

        public override void SetUp()
        {
            base.SetUp();

            structuredStoreFactory = new StructuredSymbolStore.Factory(fakeFileSystem,
                fileReferenceFactory);
            storeSequenceFactory = new SymbolStoreSequence.Factory(fakeBinaryFileUtil);
            storeSequence = storeSequenceFactory.Create();

            cacheA = structuredStoreFactory.Create(CACHE_A_PATH, true);
            cacheB = structuredStoreFactory.Create(CACHE_B_PATH, true);
            storeA = structuredStoreFactory.Create(STORE_A_PATH);
            storeB = structuredStoreFactory.Create(STORE_B_PATH);
        }

        [Test]
        public void FindFile_NoCache()
        {
            storeA.AddFile(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(storeA);

            var fileReference = storeSequence.FindFile(FILENAME, BUILD_ID);

            Assert.AreEqual(storeA.FindFile(FILENAME, BUILD_ID).Location, fileReference.Location);
        }

        [Test]
        public void FindFile_WithCache()
        {
            storeA.AddFile(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(cacheA);
            storeSequence.AddStore(storeA);

            var fileReference = storeSequence.FindFile(FILENAME, BUILD_ID);

            Assert.NotNull(cacheA.FindFile(FILENAME, BUILD_ID));
            Assert.AreEqual(cacheA.FindFile(FILENAME, BUILD_ID).Location, fileReference.Location);
        }

        [Test]
        public void FindFile_WithCacheAfterStore()
        {
            storeA.AddFile(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(storeA);
            storeSequence.AddStore(cacheA);

            var fileReference = storeSequence.FindFile(FILENAME, BUILD_ID);

            Assert.Null(cacheA.FindFile(FILENAME, BUILD_ID));
            Assert.AreEqual(storeA.FindFile(FILENAME, BUILD_ID).Location, fileReference.Location);
        }

        [Test]
        public void FindFile_WithMultipeCaches()
        {
            storeB.AddFile(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(cacheA);
            storeSequence.AddStore(storeA);
            storeSequence.AddStore(cacheB);
            storeSequence.AddStore(storeB);

            var fileReference = storeSequence.FindFile(FILENAME, BUILD_ID);

            Assert.NotNull(cacheB.FindFile(FILENAME, BUILD_ID));
            Assert.Null(cacheA.FindFile(FILENAME, BUILD_ID));
            Assert.AreEqual(cacheB.FindFile(FILENAME, BUILD_ID).Location, fileReference.Location);
        }

        [Test]
        public void FindFile_Cached()
        {
            cacheA.AddFile(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(cacheA);

            var fileReference = storeSequence.FindFile(FILENAME, BUILD_ID);

            Assert.AreEqual(cacheA.FindFile(FILENAME, BUILD_ID).Location, fileReference.Location);
        }

        [Test]
        public void FindFile_CachedInLaterCache()
        {
            cacheB.AddFile(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(cacheA);
            storeSequence.AddStore(cacheB);

            var fileReference = storeSequence.FindFile(FILENAME, BUILD_ID);

            Assert.Null(cacheA.FindFile(FILENAME, BUILD_ID));
            Assert.AreEqual(cacheB.FindFile(FILENAME, BUILD_ID).Location, fileReference.Location);
        }

        [Test]
        public void FindFile_NoStores()
        {
            var fileReference = storeSequence.FindFile(FILENAME, BUILD_ID);

            Assert.Null(fileReference);
        }

        [Test]
        public void FindFile_NotFound()
        {
            storeSequence.AddStore(cacheA);
            storeSequence.AddStore(storeA);

            var fileReference = storeSequence.FindFile(FILENAME, BUILD_ID);

            Assert.Null(fileReference);
        }

        [Test]
        public void FindFile_SkipUnsupportedCache()
        {
            storeA.AddFile(sourceSymbolFile, FILENAME, BUILD_ID);
            var unsupportedCache = Substitute.For<SymbolStoreBase>(false, true);
            unsupportedCache.FindFile("", BuildId.Empty, true, null)
                .ReturnsForAnyArgs((IFileReference)null);
            unsupportedCache.AddFile(null, "", BuildId.Empty, null).ReturnsForAnyArgs(x =>
            {
                throw new NotSupportedException();
            });
            storeSequence.AddStore(unsupportedCache);
            storeSequence.AddStore(storeA);

            var fileReference = storeSequence.FindFile(FILENAME, BUILD_ID);

            Assert.AreEqual(storeA.FindFile(FILENAME, BUILD_ID).Location, fileReference.Location);
        }

        [Test]
        public void FindFile_InvalidSymbolFileWithCache()
        {
            string storeAPath = Path.Combine(STORE_A_PATH, FILENAME, BUILD_ID.ToString(), FILENAME);

            fakeBuildIdWriter.WriteBuildId(storeAPath, BUILD_ID);
            sourceSymbolFile = fileReferenceFactory.Create(storeAPath);
            fakeBinaryFileUtil.AddVerificationFailureFor(BUILD_ID, "Symbol verification error");

            storeSequence.AddStore(cacheA);
            storeSequence.AddStore(storeA);

            var logWriter = new StringWriter();

            var fileReference = storeSequence.FindFile(FILENAME, BUILD_ID, true, logWriter);

            Assert.That(fileReference, Is.Null);
        }

        [Test]
        public void FindFile_InvalidSymbolFileInCache()
        {
            // Test the scenario where the symbol store cache contains an invalid symbol file,
            // but the store contains a correct file. In that case, we should overwrite the cache
            // with the correct file and return a reference to it.
            string storeAPath = Path.Combine(STORE_A_PATH, FILENAME, BUILD_ID.ToString(), FILENAME);
            fakeBuildIdWriter.WriteBuildId(storeAPath, BUILD_ID);
            sourceSymbolFile = fileReferenceFactory.Create(storeAPath);

            BuildId badBuildId = new BuildId("BAAD");
            string cacheAPath = Path.Combine(CACHE_A_PATH, FILENAME, BUILD_ID.ToString(), FILENAME);
            fakeBuildIdWriter.WriteBuildId(cacheAPath, badBuildId);
            sourceSymbolFile = fileReferenceFactory.Create(cacheAPath);

            fakeBinaryFileUtil.AddVerificationFailureFor(badBuildId, "Symbol verification error");

            storeSequence.AddStore(cacheA);
            storeSequence.AddStore(storeA);

            var logWriter = new StringWriter();

            var fileReference = storeSequence.FindFile(FILENAME, BUILD_ID, true, logWriter);

            Assert.That(fileReference.Location,
                        Is.EqualTo(cacheA.FindFile(FILENAME, BUILD_ID).Location));
            Assert.That(
                fakeBinaryFileUtil.ReadBuildId(storeA.FindFile(FILENAME, BUILD_ID).Location),
                Is.EqualTo(BUILD_ID));
            Assert.That(
                fakeBinaryFileUtil.ReadBuildId(cacheA.FindFile(FILENAME, BUILD_ID).Location),
                Is.EqualTo(BUILD_ID));
        }

        [Test]
        public void GetAllStores()
        {
            storeSequence.AddStore(storeA);
            storeSequence.AddStore(cacheA);
            var subStoreSequence = storeSequenceFactory.Create();
            subStoreSequence.AddStore(storeB);
            storeSequence.AddStore(subStoreSequence);

            CollectionAssert.AreEqual(
                new[] { storeSequence, storeA, cacheA, subStoreSequence, storeB },
                storeSequence.GetAllStores());
        }

        [Test]
        public void DeepEquals_NoStores()
        {
            var sequenceA = storeSequenceFactory.Create();
            var sequenceB = storeSequenceFactory.Create();

            Assert.True(sequenceA.DeepEquals(sequenceB));
            Assert.True(sequenceB.DeepEquals(sequenceA));
        }

        [Test]
        public void DeepEquals_WithStores()
        {
            var sequenceA = storeSequenceFactory.Create();
            sequenceA.AddStore(storeA);
            sequenceA.AddStore(storeB);
            var sequenceB = storeSequenceFactory.Create();
            sequenceB.AddStore(storeA);
            sequenceB.AddStore(storeB);

            Assert.True(sequenceA.DeepEquals(sequenceB));
            Assert.True(sequenceB.DeepEquals(sequenceA));
        }

        [Test]
        public void DeepEquals_StoresNotEquals()
        {
            var sequenceA = storeSequenceFactory.Create();
            sequenceA.AddStore(storeA);
            var sequenceB = storeSequenceFactory.Create();
            sequenceB.AddStore(storeB);

            Assert.False(sequenceA.DeepEquals(sequenceB));
            Assert.False(sequenceB.DeepEquals(sequenceA));
        }

        [Test]
        public void DeepEquals_DifferentLengths()
        {
            var sequenceA = storeSequenceFactory.Create();
            sequenceA.AddStore(storeA);
            sequenceA.AddStore(storeB);
            var sequenceB = storeSequenceFactory.Create();
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

        protected override ISymbolStore GetStoreWithFile()
        {
            storeA.AddFile(sourceSymbolFile, FILENAME, BUILD_ID);
            storeSequence.AddStore(storeA);
            return storeSequence;
        }

        #endregion
    }
}
