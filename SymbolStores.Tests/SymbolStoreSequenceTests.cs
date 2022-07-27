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
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using YetiCommon;

namespace SymbolStores.Tests
{
    [TestFixture]
    class SymbolStoreSequenceTests : SymbolStoreBaseTests
    {
        const string _cacheAPath = @"C:\cacheA";
        const string _cacheBPath = @"C:\cacheB";
        const string _storeAPath = @"C:\storeA";
        const string _storeBPath = @"C:\storeB";

        const string _flatStoreAPath = @"C:\flatStoreA";
        const string _flatStoreBPath = @"C:\flatStoreB";

        ISymbolStore _cacheA;
        ISymbolStore _cacheB;
        ISymbolStore _storeA;
        ISymbolStore _storeB;
        ISymbolStore _flatStoreA;
        ISymbolStore _flatStoreB;
        SymbolStoreSequence _storeSequence;

        public override void SetUp()
        {
            base.SetUp();

            _storeSequence = new SymbolStoreSequence(_moduleParser);

            _cacheA = new StructuredSymbolStore(_fakeFileSystem, _cacheAPath, isCache: true);
            _cacheB = new StructuredSymbolStore(_fakeFileSystem, _cacheBPath, isCache: true);
            _storeA = new StructuredSymbolStore(_fakeFileSystem, _storeAPath);
            _storeB = new StructuredSymbolStore(_fakeFileSystem, _storeBPath);

            _flatStoreA = new FlatSymbolStore(_fakeFileSystem, _flatStoreAPath);
            _flatStoreB = new FlatSymbolStore(_fakeFileSystem, _flatStoreBPath);
        }

        [Test]
        public async Task FindFile_NoCacheAsync()
        {
            await _storeA.AddFileAsync(_sourceSymbolFile, _filename, _buildId, _log);
            _storeSequence.AddStore(_storeA);

            var fileReference = await _storeSequence.FindFileAsync(_searchQuery, _log);

            Assert.AreEqual((await _storeA.FindFileAsync(_searchQuery, _log)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_WhenBuildIdUnknown_SearchesOnlyFlatStoresAsync()
        {
            _fakeFileSystem.AddFile($"{_flatStoreAPath}\\symbolA", new MockFileData(""));
            _fakeFileSystem.AddFile($"{_flatStoreBPath}\\symbolB", new MockFileData(""));
            _fakeFileSystem.AddFile($"{_storeAPath}\\symbolB", new MockFileData(""));

            _storeSequence.AddStore(_storeA);
            _storeSequence.AddStore(_flatStoreA);
            _storeSequence.AddStore(_flatStoreB);

            var query = new ModuleSearchQuery( "symbolB", BuildId.Empty);
            await _storeSequence.FindFileAsync(query, _log);
            string output = _log.ToString();
            Assert.AreEqual(output,
                            @"C:\flatStoreA\symbolB... File not found." + Environment.NewLine +
                            @"C:\flatStoreB\symbolB... File found." + Environment.NewLine);
        }


        [Test]
        public async Task FindFile_WhenBuildIdKnown_SearchesAllStoresAsync()
        {
            _fakeFileSystem.AddFile($"{_flatStoreAPath}/symbolA", new MockFileData(""));
            _fakeFileSystem.AddFile($"{_flatStoreBPath}/symbolB", new MockFileData(""));
            await _storeA.AddFileAsync(_sourceSymbolFile, _filename, _buildId, _log);

            _storeSequence.AddStore(_flatStoreA);
            _storeSequence.AddStore(_flatStoreB);
            _storeSequence.AddStore(_storeA);
            _storeSequence.AddStore(_cacheA);

            await _storeSequence.FindFileAsync(_searchQuery, _log);
            string output = _log.ToString();
            Assert.AreEqual(output,
                            @"Copied 'test.debug' to 'C:\storeA\test.debug\1234\test.debug'." +
                            Environment.NewLine +
                            @"C:\flatStoreA\test.debug... File not found." + Environment.NewLine +
                            @"C:\flatStoreB\test.debug... File not found." + Environment.NewLine +
                            @"C:\storeA\test.debug\1234\test.debug... File found." +
                            Environment.NewLine);
        }

        [Test]
        public async Task FindFile_WithCacheAsync()
        {
            await _storeA.AddFileAsync(_sourceSymbolFile, _filename, _buildId, _log);
            _storeSequence.AddStore(_cacheA);
            _storeSequence.AddStore(_storeA);

            var fileReference = await _storeSequence.FindFileAsync(_searchQuery, _log);

            Assert.NotNull(await _cacheA.FindFileAsync(_searchQuery, _log));
            Assert.AreEqual((await _cacheA.FindFileAsync(_searchQuery, _log)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_WithCacheAfterStoreAsync()
        {
            await _storeA.AddFileAsync(_sourceSymbolFile, _filename, _buildId, _log);
            _storeSequence.AddStore(_storeA);
            _storeSequence.AddStore(_cacheA);

            var fileReference = await _storeSequence.FindFileAsync(_searchQuery, _log);

            Assert.Null(await _cacheA.FindFileAsync(_searchQuery, _log));
            Assert.AreEqual((await _storeA.FindFileAsync(_searchQuery, _log)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_WithMultipleCachesAsync()
        {
            await _storeB.AddFileAsync(_sourceSymbolFile, _filename, _buildId, _log);
            _storeSequence.AddStore(_cacheA);
            _storeSequence.AddStore(_storeA);
            _storeSequence.AddStore(_cacheB);
            _storeSequence.AddStore(_storeB);

            var fileReference = await _storeSequence.FindFileAsync(_searchQuery, _log);

            Assert.NotNull(await _cacheB.FindFileAsync(_searchQuery, _log));
            Assert.Null(await _cacheA.FindFileAsync(_searchQuery, _log));
            Assert.AreEqual((await _cacheB.FindFileAsync(_searchQuery, _log)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_CachedAsync()
        {
            await _cacheA.AddFileAsync(_sourceSymbolFile, _filename, _buildId, _log);
            _storeSequence.AddStore(_cacheA);

            var fileReference = await _storeSequence.FindFileAsync(_searchQuery, _log);

            Assert.AreEqual((await _cacheA.FindFileAsync(_searchQuery, _log)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_CachedInLaterCacheAsync()
        {
            await _cacheB.AddFileAsync(_sourceSymbolFile, _filename, _buildId, _log);
            _storeSequence.AddStore(_cacheA);
            _storeSequence.AddStore(_cacheB);

            var fileReference = await _storeSequence.FindFileAsync(_searchQuery, _log);

            Assert.Null(await _cacheA.FindFileAsync(_searchQuery, _log));
            Assert.AreEqual((await _cacheB.FindFileAsync(_searchQuery, _log)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_NoStoresAsync()
        {
            var fileReference = await _storeSequence.FindFileAsync(_searchQuery, _log);

            Assert.Null(fileReference);
        }

        [Test]
        public async Task FindFile_NotFoundAsync()
        {
            _storeSequence.AddStore(_cacheA);
            _storeSequence.AddStore(_storeA);

            var fileReference = await _storeSequence.FindFileAsync(_searchQuery, _log);

            Assert.Null(fileReference);
        }

        [Test]
        public async Task FindFile_SkipUnsupportedCacheAsync()
        {
            await _storeA.AddFileAsync(_sourceSymbolFile, _filename, _buildId, _log);
            var unsupportedCache = Substitute.For<ISymbolStore>();
            var query = new ModuleSearchQuery("foo",  BuildId.Empty);
            unsupportedCache.FindFileAsync(query, _log)
                .ReturnsForAnyArgs((IFileReference)null);
            unsupportedCache.AddFileAsync(null, "", BuildId.Empty, null)
                .Throws(new NotSupportedException());
            _storeSequence.AddStore(unsupportedCache);
            _storeSequence.AddStore(_storeA);

            var fileReference = await _storeSequence.FindFileAsync(_searchQuery, _log);

            Assert.AreEqual((await _storeA.FindFileAsync(_searchQuery, _log)).Location,
                            fileReference.Location);
        }

        [Test]
        public async Task FindFile_InvalidSymbolFileWithCacheAsync()
        {
            string storeAPath = Path.Combine(_storeAPath, _filename, _buildId.ToPathName(),
                                             _filename);

            _fakeBuildIdWriter.WriteBuildId(storeAPath, _buildId);
            _sourceSymbolFile = new FileReference(_fakeFileSystem, storeAPath);
            _moduleParser
                .IsValidElf(Arg.Is<string>
                                (x => x.EndsWith(_filename)), Arg.Any<bool>(), out string _)
                .Returns(x =>
                {
                    x[2] = "Symbol verification error";
                    return false;
                });

            _storeSequence.AddStore(_cacheA);
            _storeSequence.AddStore(_storeA);

            var fileReference = await _storeSequence.FindFileAsync(_searchQuery, _log);

            Assert.That(fileReference, Is.Null);
            Assert.That(_log.ToString().Contains("Symbol verification error"));
        }

        [Test]
        public async Task FindFile_InvalidSymbolFileInCacheAsync()
        {
            // Test the scenario where the symbol store cache contains an invalid symbol file,
            // but the store contains a correct file. In that case, we should overwrite the cache
            // with the correct file and return a reference to it.
            string storeAPath = Path.Combine(_storeAPath, _filename, _buildId.ToPathName(),
                                             _filename);
            _fakeBuildIdWriter.WriteBuildId(storeAPath, _buildId);
            _sourceSymbolFile = new FileReference(_fakeFileSystem, storeAPath);

            BuildId badBuildId = new BuildId("BAAD", ModuleFormat.Elf);
            string cacheAPath = Path.Combine(_cacheAPath, _filename, _buildId.ToPathName(),
                                             _filename);
            _fakeBuildIdWriter.WriteBuildId(cacheAPath, badBuildId);
            _sourceSymbolFile = new FileReference(_fakeFileSystem, cacheAPath);
            string customErrorMessage = "Symbol verification error";
            _moduleParser
                .IsValidElf(
                cacheAPath,
                Arg.Any<bool>(),
                out string _)
                .Returns(x =>
                {
                    var buildId = new BuildId(
                        _fakeFileSystem.File.ReadAllText(x[0].ToString()), ModuleFormat.Elf);
                    if (buildId == badBuildId)
                    {
                        x[2] = customErrorMessage;
                        return false;
                    }

                    x[2] = "";
                    return true;
                });

            _storeSequence.AddStore(_cacheA);
            _storeSequence.AddStore(_storeA);

            IFileReference fileReference =
                await _storeSequence.FindFileAsync(_searchQuery, _log);

            Assert.That(fileReference.Location,
                        Is.EqualTo((await _cacheA.FindFileAsync(_searchQuery, _log)).Location));
            Assert.That(_log.ToString().Contains(customErrorMessage));
        }

        [Test]
        public void GetAllStores()
        {
            _storeSequence.AddStore(_storeA);
            _storeSequence.AddStore(_cacheA);
            var subStoreSequence = new SymbolStoreSequence(_moduleParser);
            subStoreSequence.AddStore(_storeB);
            _storeSequence.AddStore(subStoreSequence);

            CollectionAssert.AreEqual(
                new[] { _storeSequence, _storeA, _cacheA, subStoreSequence, _storeB },
                _storeSequence.GetAllStores());
        }

        [Test]
        public void DeepEquals_NoStores()
        {
            var sequenceA = new SymbolStoreSequence(_moduleParser);
            var sequenceB = new SymbolStoreSequence(_moduleParser);

            Assert.True(sequenceA.DeepEquals(sequenceB));
            Assert.True(sequenceB.DeepEquals(sequenceA));
        }

        [Test]
        public void DeepEquals_WithStores()
        {
            var sequenceA = new SymbolStoreSequence(_moduleParser);
            sequenceA.AddStore(_storeA);
            sequenceA.AddStore(_storeB);
            var sequenceB = new SymbolStoreSequence(_moduleParser);
            sequenceB.AddStore(_storeA);
            sequenceB.AddStore(_storeB);

            Assert.True(sequenceA.DeepEquals(sequenceB));
            Assert.True(sequenceB.DeepEquals(sequenceA));
        }

        [Test]
        public void DeepEquals_StoresNotEquals()
        {
            var sequenceA = new SymbolStoreSequence(_moduleParser);
            sequenceA.AddStore(_storeA);
            var sequenceB = new SymbolStoreSequence(_moduleParser);
            sequenceB.AddStore(_storeB);

            Assert.False(sequenceA.DeepEquals(sequenceB));
            Assert.False(sequenceB.DeepEquals(sequenceA));
        }

        [Test]
        public void DeepEquals_DifferentLengths()
        {
            var sequenceA = new SymbolStoreSequence(_moduleParser);
            sequenceA.AddStore(_storeA);
            sequenceA.AddStore(_storeB);
            var sequenceB = new SymbolStoreSequence(_moduleParser);
            sequenceB.AddStore(_storeA);

            Assert.False(sequenceA.DeepEquals(sequenceB));
            Assert.False(sequenceB.DeepEquals(sequenceA));
        }

        protected override ISymbolStore GetEmptyStore()
        {
            _storeSequence.AddStore(_storeA);
            return _storeSequence;
        }

        protected override async Task<ISymbolStore> GetStoreWithFileAsync()
        {
            await _storeA.AddFileAsync(_sourceSymbolFile, _filename, _buildId, _log);
            _storeSequence.AddStore(_storeA);
            return _storeSequence;
        }
    }
}
