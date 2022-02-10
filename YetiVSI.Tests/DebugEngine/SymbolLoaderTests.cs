// Copyright 2021 Google LLC
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

using DebuggerApi;
using NSubstitute;
using NUnit.Framework;
using SymbolStores;
using System;
using System.IO;
using System.Threading.Tasks;
using TestsCommon.TestSupport;
using YetiCommon;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class SymbolLoaderTests
    {
        const string _binaryFilename = "test";
        const string _binaryDirectory = @"C:\path";
        static readonly string _binaryFullPath = Path.Combine(_binaryDirectory, _binaryFilename);
        const string _platformDirectory = "/path/bin";
        const string _symbolFileName = "test.debug";
        const string _pathInStore = @"C:\store\" + _symbolFileName;
        const string _escapedPathInStore = @"C:\\store\\" + _symbolFileName;
        static string _command = "target symbols add \"" + _escapedPathInStore + "\"";
        static string _commandWithModulePath = "target symbols add -s \"" + _platformDirectory +
                                                 "/" + _binaryFilename + "\" \"" +
                                                 _escapedPathInStore + "\"";
        const string _lldbOutput = "Test LLDB output";
        const string _lldbError = "Test LLDB error";
        static BuildId _uuid = new BuildId("1234");

        StringWriter _searchLog;
        IModuleParser _mockModuleParser;
        IModuleFileFinder _mockModuleFileFinder;
        SbCommandInterpreter _mockCommandInterpreter;
        SbFileSpec _mockPlatformFileSpec;
        SbFileSpec _mockSymbolFileSpec;
        SbFileSpec _mockBinaryFileSpec;
        SbCommandReturnObject _mockSuccessCommandReturnObject;
        SymbolLoader _symbolLoader;
        IFileReference _symbolFileInStore;
        LogSpy _logSpy;

        [SetUp]
        public void SetUp()
        {
            _searchLog = new StringWriter();

            _mockSuccessCommandReturnObject = Substitute.For<SbCommandReturnObject>();
            _mockSuccessCommandReturnObject.GetOutput().Returns(_lldbOutput);
            _mockSuccessCommandReturnObject.GetDescription().Returns("Success: " + _lldbOutput);
            _mockSuccessCommandReturnObject.Succeeded().Returns(true);

            _mockCommandInterpreter = Substitute.For<SbCommandInterpreter>();
            SetHandleCommandReturnValue(_mockCommandInterpreter, _commandWithModulePath,
                                        ReturnStatus.SuccessFinishResult,
                                        _mockSuccessCommandReturnObject);

            _mockPlatformFileSpec = Substitute.For<SbFileSpec>();
            _mockPlatformFileSpec.GetDirectory().Returns(_platformDirectory);
            _mockPlatformFileSpec.GetFilename().Returns(_binaryFilename);

            _mockSymbolFileSpec = Substitute.For<SbFileSpec>();
            _mockSymbolFileSpec.GetDirectory().Returns("");
            _mockSymbolFileSpec.GetFilename().Returns(_symbolFileName);

            _mockBinaryFileSpec = Substitute.For<SbFileSpec>();
            _mockBinaryFileSpec.GetDirectory().Returns(_binaryDirectory);
            _mockBinaryFileSpec.GetFilename().Returns(_binaryFilename);

            _mockModuleFileFinder = Substitute.For<IModuleFileFinder>();
            SetFindFileReturnValue(_pathInStore);
            _mockModuleParser = Substitute.For<IModuleParser>();
            _symbolLoader = new SymbolLoader(_mockModuleParser,
                                             _mockModuleFileFinder, _mockCommandInterpreter);

            _symbolFileInStore = Substitute.For<IFileReference>();
            _symbolFileInStore.IsFilesystemLocation.Returns(true);
            _symbolFileInStore.Location.Returns(_pathInStore);

            _logSpy = new LogSpy();
            _logSpy.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            _logSpy.Detach();
        }

        [Test]
        public async Task LoadSymbolsAsync()
        {
            var mockModule = CreateMockModule();

            Assert.IsTrue(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                               true, false));

            StringAssert.Contains(_lldbOutput, _searchLog.ToString());
            StringAssert.Contains("Successfully loaded symbol file", _searchLog.ToString());
            StringAssert.Contains(_lldbOutput, _logSpy.GetOutput());
            StringAssert.Contains("Successfully loaded symbol file", _logSpy.GetOutput());
        }

        [Test]
        public async Task LoadSymbols_FindFileFailsAsync()
        {
            var mockModule = CreateMockModule();
            SetFindFileReturnValue(null);

            Assert.IsFalse(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                                true, false));
        }

        [Test]
        public async Task LoadSymbols_LLDBCommandFailsAsync()
        {
            var mockModule = CreateMockModule();
            var mockCommandReturnObject = Substitute.For<SbCommandReturnObject>();
            mockCommandReturnObject.GetError().Returns(_lldbError);
            mockCommandReturnObject.Succeeded().Returns(false);
            mockCommandReturnObject.GetDescription().Returns("Failed: " + _lldbError);
            SetHandleCommandReturnValue(_mockCommandInterpreter, _commandWithModulePath,
                                        ReturnStatus.Failed, mockCommandReturnObject);

            Assert.IsFalse(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                                true, false));

            StringAssert.Contains(_lldbError, _searchLog.ToString());
            StringAssert.Contains(_lldbError, _logSpy.GetOutput());
        }

        [Test]
        public async Task LoadSymbols_PEModulesAreSkippedAsync()
        {
            var mockModule = Substitute.For<SbModule>();
            mockModule.GetTriple().Returns("x86_64-pc-windows-msvc");

            Assert.IsFalse(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                               true, false));
            Assert.IsEmpty(_searchLog.ToString());
        }

        [Test]
        public async Task LoadSymbols_NullSymbolFileSpecAsync()
        {
            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns((SbFileSpec)null);
            _mockModuleParser.ParseDebugLinkInfo(_binaryFullPath)
                .Returns(new DebugLinkLocationInfo()
                { Data = new DebugLinkLocation() { Filename = _symbolFileName } });
            
            Assert.IsTrue(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                               true, false));
        }

        [Test]
        public async Task LoadSymbols_SymbolFileSpecEqualsBinaryFileSpecAsync()
        {
            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns(_mockBinaryFileSpec);
            _mockModuleParser.ParseDebugLinkInfo(_binaryFullPath)
                .Returns(new DebugLinkLocationInfo()
                { Data = new DebugLinkLocation() { Filename = _symbolFileName } });

            Assert.IsTrue(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                               true, false));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task LoadSymbols_SymbolFileCustomDirSuccessAsync(bool useSymbolStores)
        {
            string customFileName = "x.debug";
            string customFileDir = @"C:\custom";
            string expectedCommand =
                "target symbols add -s \"/path/bin/test\" \"C:\\\\custom\\\\x.debug\"";

            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns(_mockBinaryFileSpec);

            _mockModuleParser.ParseDebugLinkInfo(_binaryFullPath)
                .Returns(new DebugLinkLocationInfo()
                {
                    Data = new DebugLinkLocation()
                    {
                        Filename = customFileName,
                        Directory = customFileDir
                    }
                });

            _mockModuleParser.ParseBuildIdInfo(Path.Combine(customFileDir, customFileName), true)
                .Returns(new BuildIdInfo() { Data = _uuid });

            SetHandleCommandReturnValue(_mockCommandInterpreter, expectedCommand,
                                        ReturnStatus.SuccessFinishResult,
                                        _mockSuccessCommandReturnObject);

            Assert.IsTrue(
                await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                     useSymbolStores, false));
        }

        [Test]
        public async Task LoadSymbols_SymbolFileCustomDirSuccessFallbackOnBuildIdMismatchAsync()
        {
            string customFileName = "x.debug";
            string customFileDir = @"C:\symbols";
            string symbolStorePath = @"C:\fallback\x.debug";
            string expectedCommand =
                "target symbols add -s \"/path/bin/test\" \"C:\\\\fallback\\\\x.debug\"";

            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns(_mockBinaryFileSpec);

            _mockModuleParser.ParseDebugLinkInfo(_binaryFullPath)
                .Returns(new DebugLinkLocationInfo()
                {
                    Data = new DebugLinkLocation()
                    {
                        Filename = customFileName,
                        Directory = customFileDir
                    }
                });

            _mockModuleParser.ParseBuildIdInfo(Path.Combine(customFileDir, customFileName), true)
                .Returns(new BuildIdInfo() { Data = new BuildId("4321") });

            _mockModuleFileFinder.FindFileAsync(customFileName, _uuid, true, _searchLog,
                                                false)
                .Returns(symbolStorePath);

            SetHandleCommandReturnValue(_mockCommandInterpreter, expectedCommand,
                                        ReturnStatus.SuccessFinishResult,
                                        _mockSuccessCommandReturnObject);

            Assert.IsTrue(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                               true, false));
        }

        [Test]
        public async Task LoadSymbols_SymbolFileCustomDirThrowsSymbolStoreDisabledAsync()
        {
            string customFileName = "x.debug";

            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns(_mockBinaryFileSpec);
            _mockModuleParser.ParseDebugLinkInfo(_binaryFullPath)
                .Returns(new DebugLinkLocationInfo()
                {
                    Data = new DebugLinkLocation()
                    {
                        Filename = customFileName,
                    }
                });

            Assert.IsFalse(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                                false, false));

            await _mockModuleFileFinder.DidNotReceiveWithAnyArgs().FindFileAsync(null, _uuid, true,
                                                                                null, false);
        }

        [Test]
        public async Task LoadSymbols_FallbackWhenReadSymbolFileDirThrowsAsync()
        {
            string customFileName = "x.debug";
            string symbolStorePath = @"C:\fallback\x.debug";
            string expectedCommand =
                "target symbols add -s \"/path/bin/test\" \"C:\\\\fallback\\\\x.debug\"";

            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns(_mockBinaryFileSpec);
            _mockModuleParser.ParseDebugLinkInfo(_binaryFullPath)
                .Returns(new DebugLinkLocationInfo()
                {
                    Data = new DebugLinkLocation()
                    {
                        Filename = customFileName,
                    }
                });

            _mockModuleFileFinder.FindFileAsync(customFileName, _uuid, true, _searchLog, false)
                .Returns(symbolStorePath);

            SetHandleCommandReturnValue(_mockCommandInterpreter, expectedCommand,
                                        ReturnStatus.SuccessFinishResult,
                                        _mockSuccessCommandReturnObject);

            Assert.IsTrue(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                               true, false));
        }

        [Test]
        public async Task LoadSymbols_PlatformFileSpecNullAsync()
        {
            var mockModule = CreateMockModule();
            mockModule.GetPlatformFileSpec().Returns((SbFileSpec)null);
            SetHandleCommandReturnValue(_mockCommandInterpreter, _command,
                                        ReturnStatus.SuccessFinishResult,
                                        _mockSuccessCommandReturnObject);

            Assert.IsTrue(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                               true, false));
        }

        [Test]
        public async Task LoadSymbols_AllFileSpecsNullAsync()
        {
            var mockModule = CreateMockModule();
            mockModule.GetPlatformFileSpec().Returns((SbFileSpec)null);
            mockModule.GetSymbolFileSpec().Returns((SbFileSpec)null);
            mockModule.GetFileSpec().Returns((SbFileSpec)null);

            Assert.IsFalse(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                                true, false));

            StringAssert.Contains(ErrorStrings.SymbolFileNameUnknown, _searchLog.ToString());
            StringAssert.Contains(ErrorStrings.SymbolFileNameUnknown, _logSpy.GetOutput());
        }

        [Test]
        public async Task LoadSymbols_ReadSymbolFileNameThrowsExceptionAsync()
        {
            const string exceptionMessage = "test exception";
            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns((SbFileSpec)null);
            var toReturn = new DebugLinkLocationInfo();
            toReturn.AddError(exceptionMessage);


            _mockModuleParser.ParseDebugLinkInfo(_binaryFullPath)
                .Returns(toReturn);

            Assert.IsFalse(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog,
                                                                true, false));

            StringAssert.Contains(exceptionMessage, _searchLog.ToString());
            StringAssert.Contains(exceptionMessage, _logSpy.GetOutput());
        }

        [Test]
        public async Task LoadSymbols_InvalidBinaryFileSpecAsync()
        {
            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns((SbFileSpec)null);
            var binaryFileSpec = Substitute.For<SbFileSpec>();
            binaryFileSpec.GetDirectory().Returns(_binaryDirectory);
            binaryFileSpec.GetFilename().Returns("<invalid>");
            mockModule.GetFileSpec().Returns(binaryFileSpec);

            Assert.IsFalse(await _symbolLoader.LoadSymbolsAsync(mockModule, _searchLog, true,
                                                                false));

            StringAssert.Contains("Illegal characters", _searchLog.ToString());
            StringAssert.Contains("Illegal characters", _logSpy.GetOutput());
        }

        SbModule CreateMockModule()
        {
            var mockModule = Substitute.For<SbModule>();
            mockModule.GetPlatformFileSpec().Returns(_mockPlatformFileSpec);
            mockModule.GetSymbolFileSpec().Returns(_mockSymbolFileSpec);
            mockModule.GetFileSpec().Returns(_mockBinaryFileSpec);
            mockModule.GetUUIDString().Returns(_uuid.ToString());
            return mockModule;
        }

        void SetHandleCommandReturnValue(SbCommandInterpreter mockCommandInterpreter,
                                         string command, ReturnStatus returnStatus,
                                         SbCommandReturnObject returnObject)
        {
            SbCommandReturnObject _;
            mockCommandInterpreter.HandleCommand(command, out _).Returns(x =>
            {
                x[1] = returnObject;
                return returnStatus;
            });
        }

        void SetFindFileReturnValue(string path)
        {
            _mockModuleFileFinder.FindFileAsync(_symbolFileName, _uuid, true, _searchLog, false)
                .Returns(path);
        }
    }
}
