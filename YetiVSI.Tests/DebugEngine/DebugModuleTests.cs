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
using System.Collections.Generic;
using DebuggerApi;
using NUnit.Framework;
using NSubstitute;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI.DebugEngine;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using System.Linq;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugModuleTests
    {
        const uint _testLoadOrder = 123;

        CancelableTask.Factory _mockCancelableTaskFactory;
        ILldbModuleUtil _mockModuleUtil;
        IModuleFileLoader _mockModuleFileLoader;
        IModuleSearchLogHolder _mockModuleSearchLogHolder;
        SbModule _mockModule;
        ActionRecorder _mockActionRecorder;
        IDebugModule3 _debugModule;
        IDebugEngineHandler _mockEngineHandler;
        IGgpDebugProgram _mockDebugProgram;
        ISymbolSettingsProvider _mockSymbolSettingsProvider;
        IDialogUtil _dialogUtil;
        IYetiVSIService _vsiService;

        [SetUp]
        public void SetUp()
        {
            _mockCancelableTaskFactory = Substitute.For<CancelableTask.Factory>();
            _mockModuleUtil = Substitute.For<ILldbModuleUtil>();
            _mockModuleUtil.HasSymbolsLoaded(Arg.Any<SbModule>()).Returns(false);
            _mockModuleFileLoader = Substitute.For<IModuleFileLoader>();
            _mockModuleSearchLogHolder = Substitute.For<IModuleSearchLogHolder>();
            _mockModule = Substitute.For<SbModule>();
            _mockActionRecorder = Substitute.For<ActionRecorder>(null, null);
            var mockModuleFileLoadRecorderFactory =
                Substitute.For<ModuleFileLoadMetricsRecorder.Factory>();
            _mockEngineHandler = Substitute.For<IDebugEngineHandler>();
            _mockDebugProgram = Substitute.For<IGgpDebugProgram>();
            _mockSymbolSettingsProvider = Substitute.For<ISymbolSettingsProvider>();
            _dialogUtil = Substitute.For<IDialogUtil>();
            _vsiService = Substitute.For<IYetiVSIService>();
            _debugModule =
                new DebugModule
                    .Factory(_mockCancelableTaskFactory, _mockActionRecorder,
                             mockModuleFileLoadRecorderFactory, _mockModuleUtil,
                             _mockSymbolSettingsProvider, _dialogUtil, _vsiService)
                    .Create(_mockModuleFileLoader, _mockModuleSearchLogHolder, _mockModule,
                            _testLoadOrder, _mockEngineHandler, _mockDebugProgram);
        }

        [Test]
        public void GetInfo()
        {
            ulong testCodeLoadAddress = 456;
            ulong testCodeSize = 789;
            string testSymbolFile = "symbol file";
            string testSymbolDirectory = "c:\\symbol\\dir";
            string testPlatformFile = "platform file";
            string testPlatformDirectory = "/platform/dir";

            var mockPlatformFileSpec = Substitute.For<SbFileSpec>();
            mockPlatformFileSpec.GetFilename().Returns(testPlatformFile);
            mockPlatformFileSpec.GetDirectory().Returns(testPlatformDirectory);

            var mockSymbolFileSpec = Substitute.For<SbFileSpec>();
            mockSymbolFileSpec.GetFilename().Returns(testSymbolFile);
            mockSymbolFileSpec.GetDirectory().Returns(testSymbolDirectory);

            _mockModule.GetPlatformFileSpec().Returns(mockPlatformFileSpec);
            _mockModule.GetSymbolFileSpec().Returns(mockSymbolFileSpec);
            _mockModule.GetCodeLoadAddress().Returns(testCodeLoadAddress);
            _mockModule.GetCodeSize().Returns(testCodeSize);
            _mockModule.Is64Bit().Returns(true);
            _mockModuleUtil.HasSymbolsLoaded(_mockModule).Returns(true);

            var flags = enum_MODULE_INFO_FIELDS.MIF_NAME | enum_MODULE_INFO_FIELDS.MIF_URL |
                        enum_MODULE_INFO_FIELDS.MIF_URLSYMBOLLOCATION |
                        enum_MODULE_INFO_FIELDS.MIF_LOADADDRESS |
                        enum_MODULE_INFO_FIELDS.MIF_PREFFEREDADDRESS |
                        enum_MODULE_INFO_FIELDS.MIF_SIZE | enum_MODULE_INFO_FIELDS.MIF_LOADORDER |
                        enum_MODULE_INFO_FIELDS.MIF_FLAGS;
            var moduleInfo = new MODULE_INFO[1];

            Assert.Multiple(() =>
            {
                Assert.That(_debugModule.GetInfo(flags, moduleInfo), Is.EqualTo(VSConstants.S_OK));
                Assert.That(moduleInfo[0].dwValidFields, Is.EqualTo(flags));
                Assert.That(moduleInfo[0].m_bstrName, Is.EqualTo(testPlatformFile));
                Assert.That(moduleInfo[0].m_bstrUrl,
                            Is.EqualTo(testPlatformDirectory + "/" + testPlatformFile));
                Assert.That(moduleInfo[0].m_bstrUrlSymbolLocation,
                            Is.EqualTo(testSymbolDirectory + "\\" + testSymbolFile));
                Assert.That(moduleInfo[0].m_addrLoadAddress, Is.EqualTo(testCodeLoadAddress));
                Assert.That(moduleInfo[0].m_dwSize, Is.EqualTo(testCodeSize));
                Assert.That(moduleInfo[0].m_dwLoadOrder, Is.EqualTo(_testLoadOrder));
                Assert.That(moduleInfo[0].m_dwModuleFlags,
                            Is.EqualTo(enum_MODULE_FLAGS.MODULE_FLAG_64BIT |
                                       enum_MODULE_FLAGS.MODULE_FLAG_SYMBOLS));
            });
        }

        [Test]
        public void GetSymbolInfo()
        {
            string testSearchLog = @"C:\path\test.debug... File found.";
            _mockModuleSearchLogHolder.GetSearchLog(_mockModule).Returns(testSearchLog);
            var flags = enum_SYMBOL_SEARCH_INFO_FIELDS.SSIF_VERBOSE_SEARCH_INFO;
            var symbolSearchInfo = new MODULE_SYMBOL_SEARCH_INFO[1];

            Assert.Multiple(() =>
            {
                Assert.That(_debugModule.GetSymbolInfo(flags, symbolSearchInfo),
                            Is.EqualTo(VSConstants.S_OK));
                Assert.That((enum_SYMBOL_SEARCH_INFO_FIELDS) symbolSearchInfo[0].dwValidFields,
                            Is.EqualTo(flags));
                Assert.That(symbolSearchInfo[0].bstrVerboseSearchInfo, Is.EqualTo(testSearchLog));
            });
        }

        [Test]
        public void GetInfoNotifiesIfModuleIsExcludedAndNotLoaded()
        {
            var excludedModules = new List<string>() { "excludedModule" };
            bool useIncludeList = false;
            _mockSymbolSettingsProvider.GetInclusionSettings().Returns(
                new SymbolInclusionSettings(useIncludeList, excludedModules, new List<string>()));

            _mockModule.GetPlatformFileSpec().GetFilename().Returns("excludedModule");

            _mockModuleUtil.HasSymbolsLoaded(_mockModule).Returns(false);

            var flags = enum_MODULE_INFO_FIELDS.MIF_DEBUGMESSAGE;
            var moduleInfo = new MODULE_INFO[1];

            int result = _debugModule.GetInfo(flags, moduleInfo);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(VSConstants.S_OK));
                Assert.That(moduleInfo[0].m_bstrDebugMessage,
                            Is.EqualTo(SymbolInclusionSettings.ModuleExcludedMessage));
            });
        }

        [Test]
        public void GetInfoDoesNotNotifyIfModuleIsExcludedButLoaded()
        {
            var excludedModules = new List<string>() { "excludedModule" };
            bool useIncludeList = false;
            _mockSymbolSettingsProvider.GetInclusionSettings().Returns(
                new SymbolInclusionSettings(useIncludeList, excludedModules, new List<string>()));

            _mockModule.GetPlatformFileSpec().GetFilename().Returns("excludedModule");

            _mockModuleUtil.HasSymbolsLoaded(_mockModule).Returns(true);

            var flags = enum_MODULE_INFO_FIELDS.MIF_DEBUGMESSAGE;
            var moduleInfo = new MODULE_INFO[1];

            int result = _debugModule.GetInfo(flags, moduleInfo);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(VSConstants.S_OK));
                Assert.That(moduleInfo[0].m_bstrDebugMessage ?? "", Does.Not.Contain("Include"));
                Assert.That(moduleInfo[0].m_bstrDebugMessage ?? "", Does.Not.Contain("Exclude"));
            });
        }

        [Test]
        public void GetSymbolInfoNotifiesIfSymbolServerSupportIsDisabled()
        {
            _mockSymbolSettingsProvider.IsSymbolServerEnabled.Returns(false);
            _mockModuleUtil.HasSymbolsLoaded(_mockModule).Returns(false);

            var flags = enum_SYMBOL_SEARCH_INFO_FIELDS.SSIF_VERBOSE_SEARCH_INFO;
            var symbolSearchInfo = new MODULE_SYMBOL_SEARCH_INFO[1];

            int result = _debugModule.GetSymbolInfo(flags, symbolSearchInfo);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(VSConstants.S_OK));
                Assert.That(symbolSearchInfo[0].bstrVerboseSearchInfo.ToLower(),
                            Does.Contain("symbol server support"));
                Assert.That(symbolSearchInfo[0].bstrVerboseSearchInfo.ToLower(),
                            Does.Contain("disabled"));
            });
        }

        [Test]
        public void LoadSymbolsSendsEvent()
        {
            var action = Substitute.For<IAction>();
            action.Record(Arg.Any<Func<bool>>()).Returns(true);
            _mockActionRecorder.CreateToolAction(ActionType.DebugModuleLoadSymbols).Returns(action);

            var task = Substitute.For<ICancelableTask<LoadModuleFilesResult>>();
            task.Result.Returns(
                x => new LoadModuleFilesResult() { ResultCode = VSConstants.S_OK });
            _mockCancelableTaskFactory
                .Create(
                    Arg.Any<string>(),
                    Arg.Any<Func<ICancelable, LoadModuleFilesResult>>())
                .ReturnsForAnyArgs(task);

            _debugModule.LoadSymbols();

            _mockEngineHandler.Received(1).SendEvent(
                Arg.Is<DebugEvent>(e => e is IDebugSymbolSearchEvent2), _mockDebugProgram,
                (IDebugThread2)null);
        }
    }
}