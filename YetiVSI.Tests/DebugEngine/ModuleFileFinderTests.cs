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
using SymbolStores;
using System;
using System.IO;
using System.Threading.Tasks;
using YetiCommon;
using YetiVSI.DebugEngine;
using static Metrics.Shared.DeveloperLogEvent.Types;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class ModuleFileFinderTests
    {
        const string _searchPaths = @"path";
        const string _filename = "foo";
        static BuildId _uuid = new BuildId("0123");
        const string _pathInStore = @"path\foo";

        readonly ModuleSearchQuery _searchQuery = new ModuleSearchQuery(_filename, _uuid)
        {
            RequireDebugInfo = true
        };

        StringWriter _searchLog;
        IFileReference _fileReference;
        ISymbolStore _mockSymbolStore;
        SymbolPathParser _mockSymbolPathParser;
        ModuleFileFinder _moduleFileFinder;

        [SetUp]
        public void SetUp()
        {
            _searchLog = new StringWriter();

            _fileReference = Substitute.For<IFileReference>();
            _fileReference.IsFilesystemLocation.Returns(true);
            _fileReference.Location.Returns(_pathInStore);

            _mockSymbolStore = Substitute.For<ISymbolStore>();
            _mockSymbolStore.FindFileAsync(_searchQuery, Arg.Any<TextWriter>())
                .Returns(Task.FromResult(_fileReference));

            _mockSymbolPathParser = Substitute.For<SymbolPathParser>();
            _mockSymbolPathParser.Parse(_searchPaths).Returns(_mockSymbolStore);
            _moduleFileFinder = new ModuleFileFinder(_mockSymbolPathParser);
        }

        [Test]
        public async Task FindFileAsync()
        {
            _moduleFileFinder.SetSearchPaths(_searchPaths);
            Assert.AreEqual(
                _pathInStore,
                await _moduleFileFinder.FindFileAsync(_searchQuery, _searchLog));

            StringAssert.Contains("Searching for", _searchLog.ToString());
        }

        [Test]
        public void FindFile_NullFilename()
        {
            _moduleFileFinder.SetSearchPaths(_searchPaths);
            var query = new ModuleSearchQuery(null, BuildId.Empty);
            Assert.ThrowsAsync<ArgumentNullException>(
                () => _moduleFileFinder.FindFileAsync(query, _searchLog));
        }

        [Test]
        public async Task FindFile_EmptyBuildIdAsync()
        {
            var query = new ModuleSearchQuery(_filename, BuildId.Empty)
            {
                RequireDebugInfo = true
            };
            _mockSymbolStore.FindFileAsync(query, Arg.Any<TextWriter>())
                .Returns(Task.FromResult(_fileReference));

            _moduleFileFinder.SetSearchPaths(_searchPaths);
            Assert.AreEqual(
                _pathInStore,
                await _moduleFileFinder.FindFileAsync(query, _searchLog));

            StringAssert.Contains(ErrorStrings.ModuleBuildIdUnknown(_filename),
                                  _searchLog.ToString());
        }

        [Test]
        public async Task FindFile_SearchPathsNotSetAsync()
        {
            // The search paths default to an empty placeholder if not set. This helps avoid making
            // unnecessary assumptions about the order visual studio calls SetSearchPaths and
            // LoadSymbols.

            Assert.IsNull(await _moduleFileFinder.FindFileAsync(_searchQuery, _searchLog));

            StringAssert.Contains("Failed to find file", _searchLog.ToString());
        }

        [Test]
        public async Task FindFile_SearchFailedAsync()
        {
            _mockSymbolStore.FindFileAsync(_searchQuery, _searchLog)
                .Returns(Task.FromResult((IFileReference)null));

            _moduleFileFinder.SetSearchPaths(_searchPaths);
            Assert.IsNull(await _moduleFileFinder.FindFileAsync(_searchQuery, _searchLog));

            StringAssert.Contains("Failed to find file", _searchLog.ToString());
        }

        [Test]
        public async Task FindFile_NotFilesystemLocationAsync()
        {
            _fileReference.IsFilesystemLocation.Returns(false);

            _moduleFileFinder.SetSearchPaths(_searchPaths);
            Assert.IsNull(
                await _moduleFileFinder.FindFileAsync(_searchQuery, _searchLog));

            StringAssert.Contains("Unable to load file", _searchLog.ToString());
        }

        [Test]
        public void RecordMetrics()
        {
            var sequence = new SymbolStoreSequence(Substitute.For<IModuleParser>());
            for (int i = 0; i < 4; ++i)
            {
                sequence.AddStore(Substitute.For<IFlatSymbolStore>());
            }

            for (int i = 0; i < 3; ++i)
            {
                sequence.AddStore(Substitute.For<IStructuredSymbolStore>());
            }

            for (int i = 0; i < 2; ++i)
            {
                sequence.AddStore(Substitute.For<IHttpSymbolStore>());
            }

            sequence.AddStore(Substitute.For<IStadiaSymbolStore>());

            _mockSymbolPathParser = Substitute.For<SymbolPathParser>();
            _mockSymbolPathParser.Parse(_searchPaths).Returns(sequence);
            _moduleFileFinder = new ModuleFileFinder(_mockSymbolPathParser);

            var data = new LoadSymbolData();

            _moduleFileFinder.SetSearchPaths(_searchPaths);
            _moduleFileFinder.RecordMetrics(data);

            Assert.AreEqual(4, data.FlatSymbolStoresCount);
            Assert.AreEqual(3, data.StructuredSymbolStoresCount);
            Assert.AreEqual(2, data.HttpSymbolStoresCount);
            Assert.AreEqual(1, data.StadiaSymbolStoresCount);
        }
    }
}
