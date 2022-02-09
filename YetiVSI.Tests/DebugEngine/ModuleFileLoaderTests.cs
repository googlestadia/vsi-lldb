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

﻿using DebuggerApi;
using Microsoft.VisualStudio;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class ModuleFileLoaderTests
    {
        const string BINARY_FILENAME = "test";
        const string PLATFORM_DIRECTORY = "/path/bin";
        const string LOAD_OUTPUT = "Load output.";
        readonly static string[] IMPORTANT_MODULES = new[]
        {
            "libc-2.24.so", "libc.so", "libc.so.6", "libc-2.24.so.6", "libBrokenLocale.so.1",
            "libutil.so", "libc++.so", "libc++abi.so", "libggp.so", "libvulkan.so",
            "libpulsecommon-12.0.so", "amdvlk64.so", "libdrm.so", "libdrm_amdgpu.so",
            "libidn.so", "libnettle.so",
        };

        ICancelable mockTask;
        SbFileSpec mockPlatformFileSpec;
        FakeModuleFileLoadRecorder fakeModuleFileLoadRecorder;
        ISymbolLoader mockSymbolLoader;
        IBinaryLoader mockBinaryLoader;
        ModuleFileLoader moduleFileLoader;
        IModuleSearchLogHolder mockModuleSearchLogHolder;

        [SetUp]
        public void SetUp()
        {
            mockTask = Substitute.For<ICancelable>();

            mockPlatformFileSpec = Substitute.For<SbFileSpec>();
            mockPlatformFileSpec.GetDirectory().Returns(PLATFORM_DIRECTORY);
            mockPlatformFileSpec.GetFilename().Returns(BINARY_FILENAME);

            fakeModuleFileLoadRecorder = new FakeModuleFileLoadRecorder();

            mockSymbolLoader = Substitute.For<ISymbolLoader>();
            mockSymbolLoader
                .LoadSymbolsAsync(Arg.Any<SbModule>(), Arg.Any<TextWriter>(),
                                  Arg.Any<bool>(), Arg.Any<bool>())
                .Returns(Task.FromResult(false));
            mockBinaryLoader = Substitute.For<IBinaryLoader>();
            var anyModule = Arg.Any<SbModule>();
            mockBinaryLoader
                .LoadBinaryAsync(anyModule, Arg.Any<TextWriter>())
                .Returns((anyModule, false));

            mockModuleSearchLogHolder = new ModuleSearchLogHolder();
            moduleFileLoader = new ModuleFileLoader(mockSymbolLoader, mockBinaryLoader,
                                                    false, mockModuleSearchLogHolder);
        }

        // Need to assign test name since otherwise the autogenerated names would clash, e.g.
        // "LoadModuleFiles(0,System.Boolean[])" for the first two.
        [TestCase(VSConstants.S_OK, new bool[] {},
            TestName = "LoadModuleFilesSucceeds_NoModules")]
        [TestCase(VSConstants.S_OK, new[] { true },
            TestName = "LoadModuleFilesSucceeds_SymbolLoadSucceeds")]
        [TestCase(VSConstants.E_FAIL, new[] { false },
            TestName = "LoadModuleFilesFails_SymbolLoadFails")]
        [TestCase(VSConstants.S_OK, new[] { true, true, true },
            TestName = "LoadModuleFilesSucceeds_SymbolLoadSucceeds3Times")]
        [TestCase(VSConstants.E_FAIL, new[] { true, false, true },
            TestName = "LoadModuleFilesFails_SymbolLoadSucceedsFailsSucceeds")]
        [TestCase(VSConstants.E_FAIL, new[] { false, false, false },
            TestName = "LoadModuleFilesFails_SymbolLoadFails3Times")]
        public async Task LoadModuleFilesAsync(int expectedReturnCode, bool[] loadSymbolsSuccessValues)
        {
            var modules = loadSymbolsSuccessValues
                              .Select(loadSymbolsSuccessValue => CreateMockModule(
                                          loadBinarySuccess: true,
                                          loadSymbolsSuccess: loadSymbolsSuccessValue))
                              .ToList();

            Assert.AreEqual(
                expectedReturnCode,
                (await moduleFileLoader.LoadModuleFilesAsync(
                    modules, mockTask, fakeModuleFileLoadRecorder)).ResultCode);

            foreach (var module in modules)
            {
                await AssertLoadBinaryReceivedAsync(module);
                await AssertLoadSymbolsReceivedAsync(module);
            }

            Assert.AreEqual(fakeModuleFileLoadRecorder.ModulesRecordedBeforeLoad, modules);
            Assert.AreEqual(fakeModuleFileLoadRecorder.ModulesRecordedAfterLoad, modules);
        }

        [Test]
        public async Task LoadModuleFilesWithInclusionSettingsAsync()
        {
            SbModule includedModule = CreateMockModule(true, true, "included");
            SbModule excludedModule = CreateMockModule(true, true, "excluded");
            var modules = new List<SbModule>() { includedModule, excludedModule };

            bool useIncludeList = true;
            var includeList = new List<string>() { "included" };
            var settings =
                new SymbolInclusionSettings(useIncludeList, new List<string>(), includeList);

            Assert.That(
                (await moduleFileLoader.LoadModuleFilesAsync(
                    modules, settings, true, true, mockTask, fakeModuleFileLoadRecorder))
                    .ResultCode,
                Is.EqualTo(VSConstants.S_OK));

            await AssertLoadBinaryReceivedAsync(includedModule);
            await AssertLoadSymbolsReceivedAsync(includedModule);
            await AssertLoadBinaryNotReceivedAsync(excludedModule);
            await AssertLoadSymbolsNotReceivedAsync(excludedModule);
        }

        [Test]
        public async Task LoadModuleFilesWithExclusionSettingsAsync()
        {
            SbModule includedModule = CreateMockModule(true, true, "included");
            SbModule excludedModule = CreateMockModule(true, true, "excluded");
            var modules = new List<SbModule>() { includedModule, excludedModule };

            bool useIncludeList = false;
            var excludeList = new List<string>() { "excluded" };
            var settings =
                new SymbolInclusionSettings(useIncludeList, excludeList, new List<string>());

            Assert.That(
                (await moduleFileLoader.LoadModuleFilesAsync(
                    modules, settings, true, true, mockTask, fakeModuleFileLoadRecorder))
                    .ResultCode,
                Is.EqualTo(VSConstants.S_OK));

            await AssertLoadBinaryReceivedAsync(includedModule);
            await AssertLoadSymbolsReceivedAsync(includedModule);
            await AssertLoadBinaryNotReceivedAsync(excludedModule);
            await AssertLoadSymbolsNotReceivedAsync(excludedModule);
        }

        [Test]
        public async Task LoadModuleFiles_AlreadyLoadedAsync()
        {
            var module = CreateMockModule(loadBinarySuccess: true, loadSymbolsSuccess: true);

            Assert.AreEqual(VSConstants.S_OK, (await moduleFileLoader.LoadModuleFilesAsync(
                new[] { module }, mockTask, fakeModuleFileLoadRecorder)).ResultCode);
        }

        [Test]
        public async Task LoadModuleFiles_CanceledAsync()
        {
            var modules = new[] {
                CreateMockModule(loadBinarySuccess: true, loadSymbolsSuccess: false),
                CreateMockModule(loadBinarySuccess: true, loadSymbolsSuccess: false),
            }.ToList();
            mockTask.When(x => x.ThrowIfCancellationRequested())
                .Do(Callback.First(x => { }).Then(x => { })
                    .ThenThrow(new OperationCanceledException()));

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                moduleFileLoader.LoadModuleFilesAsync(modules, mockTask, fakeModuleFileLoadRecorder));

            await mockSymbolLoader.Received().LoadSymbolsAsync(
                modules[0], Arg.Any<TextWriter>(), Arg.Any<bool>(), Arg.Any<bool>());
            await mockSymbolLoader.DidNotReceive().LoadSymbolsAsync(
                modules[1], Arg.Any<TextWriter>(), Arg.Any<bool>(), Arg.Any<bool>());
            Assert.AreEqual(fakeModuleFileLoadRecorder.ModulesRecordedBeforeLoad, modules);
        }

        [Test]
        public async Task LoadModuleFiles_LoadBinariesFailsAsync()
        {
            var module = CreateMockModule(loadBinarySuccess: false, loadSymbolsSuccess: false);

            Assert.AreEqual(VSConstants.E_FAIL, (await moduleFileLoader.LoadModuleFilesAsync(
                new[] { module }, mockTask, fakeModuleFileLoadRecorder)).ResultCode);

            await AssertLoadBinaryReceivedAsync(module);
            await mockSymbolLoader.DidNotReceiveWithAnyArgs()
                .LoadSymbolsAsync(module, null,true, false);
        }

        [Test]
        public async Task LoadModuleFiles_ReplacedPlaceholderModuleAsync()
        {
            var placeholderModule = Substitute.For<SbModule>();
            var newModule = Substitute.For<SbModule>();
            mockBinaryLoader.LoadBinaryAsync(placeholderModule, Arg.Any<TextWriter>()).Returns(x =>
            {
                return (newModule, true);
            });
            mockSymbolLoader.LoadSymbolsAsync(newModule, Arg.Any<TextWriter>(),
                                              Arg.Any<bool>(), Arg.Any<bool>())
                .Returns(Task.FromResult(true));
            var modules = new[] { placeholderModule };

            Assert.AreEqual(VSConstants.S_OK, (await moduleFileLoader.LoadModuleFilesAsync(
                modules, mockTask, fakeModuleFileLoadRecorder)).ResultCode);

            await AssertLoadBinaryReceivedAsync(placeholderModule);
            await AssertLoadSymbolsReceivedAsync(newModule);
            Assert.AreSame(newModule, modules[0]);
            Assert.AreEqual(fakeModuleFileLoadRecorder.ModulesRecordedBeforeLoad,
                new[] { placeholderModule });
            Assert.AreEqual(fakeModuleFileLoadRecorder.ModulesRecordedAfterLoad,
                new[] { newModule });
        }

        [Test]
        public void LoadModuleFiles_NullModules()
        {
            Assert.ThrowsAsync<ArgumentNullException>(
                () => moduleFileLoader.LoadModuleFilesAsync(null, mockTask, fakeModuleFileLoadRecorder));
        }

        [Test]
        public async Task LoadModuleFiles_UnableToLoadImportantModuleForGameAttachAsync()
        {
            var module = CreateMockModule(
                loadBinarySuccess: false, loadSymbolsSuccess: true, IMPORTANT_MODULES.First());
            var result = await moduleFileLoader.LoadModuleFilesAsync(
                new[] { module }, null, true, true, mockTask, fakeModuleFileLoadRecorder);

            Assert.AreEqual(VSConstants.E_FAIL, result.ResultCode);
            Assert.AreEqual(false, result.SuggestToEnableSymbolStore);
        }

        [Test]
        public async Task LoadModuleFiles_UnableToLoadImportantModuleForCrashDumpAsync()
        {
            foreach (var moduleName in IMPORTANT_MODULES)
            {
                var module = CreateMockModule(
                    loadBinarySuccess: false, loadSymbolsSuccess: true, moduleName);

                var coredumpModuleLoader = new ModuleFileLoader(mockSymbolLoader, mockBinaryLoader,
                                                        true,
                                                        mockModuleSearchLogHolder);
                var result = await coredumpModuleLoader.LoadModuleFilesAsync(
                    new[] { module }, null, false, false, mockTask, fakeModuleFileLoadRecorder);

                Assert.AreEqual(VSConstants.E_FAIL, result.ResultCode, moduleName);
                Assert.AreEqual(true, result.SuggestToEnableSymbolStore, moduleName);
            }
        }

        [Test]
        public async Task LoadModuleFiles_UnableToLoadModuleForCrashDumpAsync()
        {
            var module = CreateMockModule(loadBinarySuccess: false, loadSymbolsSuccess: true);

            var coredumpModuleLoader = new ModuleFileLoader(mockSymbolLoader, mockBinaryLoader,
                                                            true, mockModuleSearchLogHolder);
            var result = await coredumpModuleLoader.LoadModuleFilesAsync(
                new[] { module }, null, false, false, mockTask, fakeModuleFileLoadRecorder);

            Assert.AreEqual(VSConstants.E_FAIL, result.ResultCode);
            Assert.AreEqual(false, result.SuggestToEnableSymbolStore);
        }

        [Test]
        public async Task LoadModuleFiles_DoNotShowSuggestionIfSymbolStoreEnabledAsync()
        {
            var module = CreateMockModule(loadBinarySuccess: false, loadSymbolsSuccess: true);

            var coredumpModuleLoader = new ModuleFileLoader(mockSymbolLoader, mockBinaryLoader,
                                                    true,
                                                    mockModuleSearchLogHolder);
            var result = await coredumpModuleLoader.LoadModuleFilesAsync(
                new[] { module }, null, true, true, mockTask, fakeModuleFileLoadRecorder);

            Assert.AreEqual(VSConstants.E_FAIL, result.ResultCode);
            Assert.AreEqual(false, result.SuggestToEnableSymbolStore);
        }

        [Test]
        public async Task GetSearchLogAsync()
        {
            var module = Substitute.For<SbModule>();
            module.GetPlatformFileSpec().Returns(mockPlatformFileSpec);
            mockBinaryLoader.LoadBinaryAsync(module, Arg.Any<TextWriter>()).Returns(x =>
            {
                x.Arg<TextWriter>().WriteLine(LOAD_OUTPUT);
                return (module, false);
            });
            await moduleFileLoader.LoadModuleFilesAsync(new[] { module }, mockTask,
                fakeModuleFileLoadRecorder);

            StringAssert.Contains(LOAD_OUTPUT, mockModuleSearchLogHolder.GetSearchLog(module));
        }

        [Test]
        public void GetSearchLog_NoPlatformFileSpec()
        {
            var mockModule = Substitute.For<SbModule>();
            mockModule.GetPlatformFileSpec().Returns((SbFileSpec)null);

            Assert.AreEqual("", mockModuleSearchLogHolder.GetSearchLog(mockModule));
        }

        [Test]
        public void GetSearchLog_NoResult()
        {
            var mockModule = Substitute.For<SbModule>();
            mockModule.GetPlatformFileSpec().Returns(mockPlatformFileSpec);

            Assert.AreEqual("", mockModuleSearchLogHolder.GetSearchLog(mockModule));
        }

        /// <summary>
        /// Creates a new mock module, and configures mockBinaryLoader and mockSymbolLoader to
        /// return appropriate values for said module in the context of a call to LoadModuleFiles.
        /// The success values of LoadBinaries and LoadSymbols are directly determined by the values
        /// of |loadBinarySuccess| and |loadSymbolsSuccess|.
        /// </summary>
        SbModule CreateMockModule(bool loadBinarySuccess, bool loadSymbolsSuccess, string name = "")
        {
            var module = Substitute.For<SbModule>();
            module.GetPlatformFileSpec().GetFilename().Returns(name);
            mockBinaryLoader.LoadBinaryAsync(module, Arg.Any<TextWriter>())
                .Returns((module, loadBinarySuccess));
            mockSymbolLoader.LoadSymbolsAsync(module, Arg.Any<TextWriter>(),
                                              Arg.Any<bool>(), Arg.Any<bool>())
                .Returns(Task.FromResult(loadSymbolsSuccess));
            return module;
        }

        async Task AssertLoadBinaryReceivedAsync(SbModule module)
        {
            await mockBinaryLoader.Received().LoadBinaryAsync(module, Arg.Any<TextWriter>());
        }

        async Task AssertLoadSymbolsReceivedAsync(SbModule module)
        {
            await mockSymbolLoader.Received().LoadSymbolsAsync(
                module, Arg.Any<TextWriter>(), Arg.Any<bool>(), Arg.Any<bool>());
        }

        async Task AssertLoadBinaryNotReceivedAsync(SbModule module)
        {
            await mockBinaryLoader.DidNotReceive().LoadBinaryAsync(module, Arg.Any<TextWriter>());
        }

        async Task AssertLoadSymbolsNotReceivedAsync(SbModule module)
        {
            await mockSymbolLoader.DidNotReceive().LoadSymbolsAsync(
                module, Arg.Any<TextWriter>(), Arg.Any<bool>(), Arg.Any<bool>());
        }

        class FakeModuleFileLoadRecorder : IModuleFileLoadMetricsRecorder
        {
            public IList<SbModule> ModulesRecordedBeforeLoad { get; private set; }
            public IList<SbModule> ModulesRecordedAfterLoad { get; private set; }

            public void RecordBeforeLoad(IList<SbModule> modules)
            {
                ModulesRecordedBeforeLoad = modules.ToList();
            }

            public void RecordAfterLoad(IList<SbModule> modules)
            {
                ModulesRecordedAfterLoad = modules.ToList();
            }

            public void RecordFeatureDisabled() { }
        }
    }
}
