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
using SymbolStores;
using System;
using System.IO;
using System.Threading.Tasks;
using YetiCommon;
using YetiVSI.DebugEngine;
using static YetiVSI.Shared.Metrics.DeveloperLogEvent.Types;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class ModuleFileFinderTests
    {
        const string SEARCH_PATHS = @"path";
        const string FILENAME = "foo";
        static BuildId UUID = new BuildId("0123");
        const string PATH_IN_STORE = @"path\foo";

        StringWriter searchLog;
        IFileReference fileReference;
        ISymbolStore mockSymbolStore;
        SymbolPathParser mockSymbolPathParser;
        ModuleFileFinder moduleFileFinder;

        [SetUp]
        public void SetUp()
        {
            searchLog = new StringWriter();

            fileReference = Substitute.For<IFileReference>();
            fileReference.IsFilesystemLocation.Returns(true);
            fileReference.Location.Returns(PATH_IN_STORE);

            mockSymbolStore = Substitute.For<ISymbolStore>();
            mockSymbolStore.FindFileAsync(FILENAME, UUID, true, Arg.Any<TextWriter>())
                .Returns(Task.FromResult(fileReference));

            mockSymbolPathParser = Substitute.For<SymbolPathParser>();
            mockSymbolPathParser.Parse(SEARCH_PATHS).Returns(mockSymbolStore);
            moduleFileFinder = new ModuleFileFinder(mockSymbolPathParser);
        }

        [Test]
        public async Task FindFileAsync()
        {
            moduleFileFinder.SetSearchPaths(SEARCH_PATHS);
            Assert.AreEqual(
                PATH_IN_STORE,
                await moduleFileFinder.FindFileAsync(FILENAME, UUID, true, searchLog));

            StringAssert.Contains("Searching for", searchLog.ToString());
        }

        [Test]
        public void FindFile_NullFilename()
        {
            moduleFileFinder.SetSearchPaths(SEARCH_PATHS);
            Assert.ThrowsAsync<ArgumentNullException>(
                () => moduleFileFinder.FindFileAsync(null, UUID, true, searchLog));
        }

        [Test]
        public async Task FindFile_EmptyBuildIdAsync()
        {
            mockSymbolStore.FindFileAsync(FILENAME, BuildId.Empty, true, Arg.Any<TextWriter>())
                .Returns(Task.FromResult(fileReference));

            moduleFileFinder.SetSearchPaths(SEARCH_PATHS);
            Assert.AreEqual(
                PATH_IN_STORE,
                await moduleFileFinder.FindFileAsync(FILENAME, BuildId.Empty, true, searchLog));

            StringAssert.Contains(ErrorStrings.ModuleBuildIdUnknown(FILENAME),
                                  searchLog.ToString());
        }

        [Test]
        public async Task FindFile_SearchPathsNotSetAsync()
        {
            // The search paths default to an empty placeholder if not set. This helps avoid making
            // unnecessary assumptions about the order visual studio calls SetSearchPaths and
            // LoadSymbols.

            Assert.IsNull(await moduleFileFinder.FindFileAsync(FILENAME, UUID, true, searchLog));

            StringAssert.Contains("Failed to find file", searchLog.ToString());
        }

        [Test]
        public async Task FindFile_SearchFailedAsync()
        {
            mockSymbolStore.FindFileAsync(FILENAME, UUID, true, Arg.Any<TextWriter>())
                .Returns(Task.FromResult((IFileReference)null));

            moduleFileFinder.SetSearchPaths(SEARCH_PATHS);
            Assert.IsNull(await moduleFileFinder.FindFileAsync(FILENAME, UUID, true, searchLog));

            StringAssert.Contains("Failed to find file", searchLog.ToString());
        }

        [Test]
        public async Task FindFile_NotFilesystemLocationAsync()
        {
            fileReference.IsFilesystemLocation.Returns(false);

            moduleFileFinder.SetSearchPaths(SEARCH_PATHS);
            Assert.IsNull(
                await moduleFileFinder.FindFileAsync(FILENAME, UUID, true, searchLog));

            StringAssert.Contains("Unable to load file", searchLog.ToString());
        }

        [Test]
        public void RecordMetrics()
        {
            var sequence = new SymbolStoreSequence(Substitute.For<IBinaryFileUtil>());
            for (int i = 0; i < 4; ++i) sequence.AddStore(Substitute.For<IFlatSymbolStore>());
            for (int i = 0; i < 3; ++i) sequence.AddStore(Substitute.For<IStructuredSymbolStore>());
            for (int i = 0; i < 2; ++i) sequence.AddStore(Substitute.For<IHttpSymbolStore>());
            sequence.AddStore(Substitute.For<IStadiaSymbolStore>());

            mockSymbolPathParser = Substitute.For<SymbolPathParser>();
            mockSymbolPathParser.Parse(SEARCH_PATHS).Returns(sequence);
            moduleFileFinder = new ModuleFileFinder(mockSymbolPathParser);

            var data = new LoadSymbolData();

            moduleFileFinder.SetSearchPaths(SEARCH_PATHS);
            moduleFileFinder.RecordMetrics(data);

            Assert.AreEqual(4, data.FlatSymbolStoresCount);
            Assert.AreEqual(3, data.StructuredSymbolStoresCount);
            Assert.AreEqual(2, data.HttpSymbolStoresCount);
            Assert.AreEqual(1, data.StadiaSymbolStoresCount);
        }
    }
}
