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

using System.IO;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using SymbolStores;
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
        static readonly BuildId _uuid = new BuildId("0123", ModuleFormat.Elf);
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
        public async Task FindFile_EmptyBuildIdAsync()
        {
            var query = new ModuleSearchQuery(_filename, null)
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

        const string _path1 = @"c:\path\file1";
        const string _path2 = @"c:\path\file2";

        [Test]
        public async Task FindFile_NotFilesystemLocationAsync()
        {
            // Symbol store sequence contains a single symbol store, that returns non-filesystem
            // location `_fileReference`. It will be validated directly in the
            // symbolStoreSequence.FindFileAsync call, corresponding error message gets written to
            // the log and null is returned into ModuleFileFinder.FindFileAsync (no additional
            // validation after that).
            var file = Substitute.For<IFileReference>();
            file.IsFilesystemLocation.Returns(false);
            file.Location.Returns(_path1);
            var sequence = new SymbolStoreSequence(Substitute.For<IModuleParser>());
            var flatStore = Substitute.For<ISymbolStore>(); 
            flatStore.FindFileAsync(Arg.Any<ModuleSearchQuery>(), _searchLog)
                .Returns(x => file);
            sequence.AddStore(flatStore);
            _mockSymbolPathParser = Substitute.For<SymbolPathParser>();
            _mockSymbolPathParser.Parse(_searchPaths).Returns(sequence);
            _moduleFileFinder = new ModuleFileFinder(_mockSymbolPathParser);
            // Populate SymbolStoreSequence.
            _moduleFileFinder.SetSearchPaths(_searchPaths);

            // Search for a file.
            string filename = await _moduleFileFinder.FindFileAsync(_searchQuery, _searchLog);

            // Validate the result.
            Assert.IsNull(filename);
            StringAssert.Contains(YetiCommon.ErrorStrings
                                      .FileNotOnFilesystem(_path1),
                                  _searchLog.ToString());
        }


        [Test]
        public async Task FindFile_NotFilesystemLocation_SearchContinues_Async()
        {
            // Symbol store sequence contains 2 symbol stores, first one returns
            // non-fileSystem location reference, second one is able to locate the
            // file locally. The FindFileAsync should succeed.
            var firstFile = Substitute.For<IFileReference>();
            firstFile.IsFilesystemLocation.Returns(false);
            firstFile.Location.Returns(_path1);
            var secondFile = Substitute.For<IFileReference>();
            secondFile.IsFilesystemLocation.Returns(true);
            secondFile.Location.Returns(_path2);
            var moduleParser = Substitute.For<IModuleParser>();
            moduleParser.IsValidElf(_path2, Arg.Any<bool>(), out _)
                .Returns(true);
            moduleParser.ParseBuildIdInfo(_path2, ModuleFormat.Elf)
                .Returns(new BuildIdInfo() {Data = _uuid});
            var sequence = new SymbolStoreSequence(moduleParser);
            var flatStore1 = Substitute.For<ISymbolStore>();
            flatStore1.FindFileAsync(Arg.Any<ModuleSearchQuery>(), _searchLog)
                .Returns(x => firstFile);
            var flatStore2 = Substitute.For<ISymbolStore>();
            flatStore2.FindFileAsync(Arg.Any<ModuleSearchQuery>(), _searchLog)
                .Returns(x => secondFile);
            sequence.AddStore(flatStore1);
            sequence.AddStore(flatStore2);

            _mockSymbolPathParser = Substitute.For<SymbolPathParser>();
            _mockSymbolPathParser.Parse(_searchPaths).Returns(sequence);
            _moduleFileFinder = new ModuleFileFinder(_mockSymbolPathParser);
            // Populate SymbolStoreSequence.
            _moduleFileFinder.SetSearchPaths(_searchPaths);

            // Search for a file.
            string file = await _moduleFileFinder.FindFileAsync(_searchQuery, _searchLog);

            // Validate the result.
            Assert.That(file.Equals(_path2));
            StringAssert.Contains(YetiCommon.ErrorStrings
                                      .FileNotOnFilesystem(_path1),
                                  _searchLog.ToString());
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
