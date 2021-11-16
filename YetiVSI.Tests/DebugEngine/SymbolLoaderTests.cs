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
using NSubstitute.ExceptionExtensions;
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
        const string BINARY_FILENAME = "test";
        const string BINARY_DIRECTORY = @"C:\path";
        const string PLATFORM_DIRECTORY = "/path/bin";
        const string SYMBOL_FILE_NAME = "test.debug";
        const string PATH_IN_STORE = @"C:\store\" + SYMBOL_FILE_NAME;
        const string ESCAPED_PATH_IN_STORE = @"C:\\store\\" + SYMBOL_FILE_NAME;
        static string COMMAND = "target symbols add \"" + ESCAPED_PATH_IN_STORE + "\"";
        static string COMMAND_WITH_MODULE_PATH = "target symbols add -s \"" + PLATFORM_DIRECTORY +
                                                 "/" + BINARY_FILENAME + "\" \"" +
                                                 ESCAPED_PATH_IN_STORE + "\"";
        const string LLDB_OUTPUT = "Test LLDB output";
        const string LLDB_ERROR = "Test LLDB error";
        static BuildId UUID = new BuildId("1234");

        StringWriter searchLog;
        IBinaryFileUtil mockBinaryFileUtil;
        IModuleFileFinder mockModuleFileFinder;
        SbCommandInterpreter mockCommandInterpreter;
        SbFileSpec mockPlatformFileSpec;
        SbFileSpec mockSymbolFileSpec;
        SbFileSpec mockBinaryFileSpec;
        SbCommandReturnObject mockSuccessCommandReturnObject;
        SymbolLoader symbolLoader;
        IFileReference symbolFileInStore;
        LogSpy logSpy;

        [SetUp]
        public void SetUp()
        {
            searchLog = new StringWriter();

            mockBinaryFileUtil = Substitute.For<IBinaryFileUtil>();

            mockSuccessCommandReturnObject = Substitute.For<SbCommandReturnObject>();
            mockSuccessCommandReturnObject.GetOutput().Returns(LLDB_OUTPUT);
            mockSuccessCommandReturnObject.GetDescription().Returns("Success: " + LLDB_OUTPUT);
            mockSuccessCommandReturnObject.Succeeded().Returns(true);

            mockCommandInterpreter = Substitute.For<SbCommandInterpreter>();
            SetHandleCommandReturnValue(mockCommandInterpreter, COMMAND_WITH_MODULE_PATH,
                                        ReturnStatus.SuccessFinishResult,
                                        mockSuccessCommandReturnObject);

            mockPlatformFileSpec = Substitute.For<SbFileSpec>();
            mockPlatformFileSpec.GetDirectory().Returns(PLATFORM_DIRECTORY);
            mockPlatformFileSpec.GetFilename().Returns(BINARY_FILENAME);

            mockSymbolFileSpec = Substitute.For<SbFileSpec>();
            mockSymbolFileSpec.GetDirectory().Returns("");
            mockSymbolFileSpec.GetFilename().Returns(SYMBOL_FILE_NAME);

            mockBinaryFileSpec = Substitute.For<SbFileSpec>();
            mockBinaryFileSpec.GetDirectory().Returns(BINARY_DIRECTORY);
            mockBinaryFileSpec.GetFilename().Returns(BINARY_FILENAME);

            mockModuleFileFinder = Substitute.For<IModuleFileFinder>();
            SetFindFileReturnValue(PATH_IN_STORE);

            symbolLoader = new SymbolLoader(mockBinaryFileUtil,
                                            mockModuleFileFinder, mockCommandInterpreter);

            symbolFileInStore = Substitute.For<IFileReference>();
            symbolFileInStore.IsFilesystemLocation.Returns(true);
            symbolFileInStore.Location.Returns(PATH_IN_STORE);

            logSpy = new LogSpy();
            logSpy.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            logSpy.Detach();
        }

        [Test]
        public async Task LoadSymbolsAsync()
        {
            var mockModule = CreateMockModule();

            Assert.IsTrue(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, true));

            StringAssert.Contains(LLDB_OUTPUT, searchLog.ToString());
            StringAssert.Contains("Symbols loaded successfully", searchLog.ToString());
            StringAssert.Contains(LLDB_OUTPUT, logSpy.GetOutput());
            StringAssert.Contains("Successfully loaded symbol file", logSpy.GetOutput());
        }

        [Test]
        public async Task LoadSymbols_FindFileFailsAsync()
        {
            var mockModule = CreateMockModule();
            SetFindFileReturnValue(null);

            Assert.IsFalse(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, true));
        }

        public async Task LoadSymbols_LLDBCommandFailsAsync()
        {
            var mockModule = CreateMockModule();
            var mockCommandReturnObject = Substitute.For<SbCommandReturnObject>();
            mockCommandReturnObject.GetError().Returns(LLDB_ERROR);
            mockCommandReturnObject.Succeeded().Returns(false);
            mockCommandReturnObject.GetDescription().Returns("Failed: " + LLDB_ERROR);
            SetHandleCommandReturnValue(mockCommandInterpreter, COMMAND_WITH_MODULE_PATH,
                                        ReturnStatus.Failed, mockCommandReturnObject);

            Assert.IsFalse(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, true));

            StringAssert.Contains(LLDB_ERROR, searchLog.ToString());
            StringAssert.Contains(LLDB_ERROR, logSpy.GetOutput());
        }

        [Test]
        public async Task LoadSymbols_AlreadyLoadedAsync()
        {
            var mockModule = Substitute.For<SbModule>();
            mockModule.HasCompileUnits().Returns(true);

            Assert.IsTrue(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, true));
            Assert.IsEmpty(searchLog.ToString());
        }

        [Test]
        public async Task LoadSymbols_NullSymbolFileSpecAsync()
        {
            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns((SbFileSpec)null);
            mockBinaryFileUtil
                .ReadSymbolFileNameAsync(Path.Combine(BINARY_DIRECTORY, BINARY_FILENAME))
                .Returns(SYMBOL_FILE_NAME);

            Assert.IsTrue(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, true));
        }

        [Test]
        public async Task LoadSymbols_SymbolFileSpecEqualsBinaryFileSpecAsync()
        {
            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns(mockBinaryFileSpec);
            mockBinaryFileUtil
                .ReadSymbolFileNameAsync(Path.Combine(BINARY_DIRECTORY, BINARY_FILENAME))
                .Returns(SYMBOL_FILE_NAME);
            Assert.IsTrue(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, true));
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
            mockModule.GetSymbolFileSpec().Returns(mockBinaryFileSpec);
            mockBinaryFileUtil
                .ReadSymbolFileNameAsync(Path.Combine(BINARY_DIRECTORY, BINARY_FILENAME))
                .Returns(customFileName);
            mockBinaryFileUtil
                .ReadSymbolFileDirAsync(Path.Combine(BINARY_DIRECTORY, BINARY_FILENAME))
                .Returns(customFileDir);
            mockBinaryFileUtil.ReadBuildIdAsync(Path.Combine(customFileDir, customFileName))
                .Returns(UUID);

            SetHandleCommandReturnValue(mockCommandInterpreter, expectedCommand,
                                        ReturnStatus.SuccessFinishResult,
                                        mockSuccessCommandReturnObject);

            Assert.IsTrue(
                await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, useSymbolStores));
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
            mockModule.GetSymbolFileSpec().Returns(mockBinaryFileSpec);
            mockBinaryFileUtil
                .ReadSymbolFileNameAsync(Path.Combine(BINARY_DIRECTORY, BINARY_FILENAME))
                .Returns(customFileName);
            mockBinaryFileUtil
                .ReadSymbolFileDirAsync(Path.Combine(BINARY_DIRECTORY, BINARY_FILENAME))
                .Returns(customFileDir);
            mockBinaryFileUtil.ReadBuildIdAsync(Path.Combine(customFileDir, customFileName))
                .Returns(new BuildId("4321"));

            mockModuleFileFinder.FindFileAsync(customFileName, UUID, true, searchLog)
                .Returns(symbolStorePath);

            SetHandleCommandReturnValue(mockCommandInterpreter, expectedCommand,
                                        ReturnStatus.SuccessFinishResult,
                                        mockSuccessCommandReturnObject);

            Assert.IsTrue(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, true));
        }

        [Test]
        public async Task LoadSymbols_SymbolFileCustomDirThrowsSymbolStoreDisabledAsync()
        {
            string customFileName = "x.debug";

            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns(mockBinaryFileSpec);
            mockBinaryFileUtil
                .ReadSymbolFileNameAsync(Path.Combine(BINARY_DIRECTORY, BINARY_FILENAME))
                .Returns(customFileName);
            mockBinaryFileUtil
                .ReadSymbolFileDirAsync(Path.Combine(BINARY_DIRECTORY, BINARY_FILENAME))
                .Throws(new BinaryFileUtilException("exception"));

            Assert.IsFalse(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, false));

            await mockModuleFileFinder.DidNotReceiveWithAnyArgs().FindFileAsync(null, UUID, true,
                                                                                null);
        }

        [Test]
        public async Task LoadSymbols_FallbackWhenReadSymbolFileDirThrowsAsync()
        {
            string customFileName = "x.debug";
            string symbolStorePath = @"C:\fallback\x.debug";
            string expectedCommand =
                "target symbols add -s \"/path/bin/test\" \"C:\\\\fallback\\\\x.debug\"";

            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns(mockBinaryFileSpec);
            mockBinaryFileUtil
                .ReadSymbolFileNameAsync(Path.Combine(BINARY_DIRECTORY, BINARY_FILENAME))
                .Returns(customFileName);
            mockBinaryFileUtil
                .ReadSymbolFileDirAsync(Path.Combine(BINARY_DIRECTORY, BINARY_FILENAME))
                .Throws(new BinaryFileUtilException("exception"));

            mockModuleFileFinder.FindFileAsync(customFileName, UUID, true, searchLog)
                .Returns(symbolStorePath);

            SetHandleCommandReturnValue(mockCommandInterpreter, expectedCommand,
                                        ReturnStatus.SuccessFinishResult,
                                        mockSuccessCommandReturnObject);

            Assert.IsTrue(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, true));
        }

        [Test]
        public async Task LoadSymbols_PlatformFileSpecNullAsync()
        {
            var mockModule = CreateMockModule();
            mockModule.GetPlatformFileSpec().Returns((SbFileSpec)null);
            SetHandleCommandReturnValue(mockCommandInterpreter, COMMAND,
                                        ReturnStatus.SuccessFinishResult,
                                        mockSuccessCommandReturnObject);

            Assert.IsTrue(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, true));
        }

        [Test]
        public async Task LoadSymbols_AllFileSpecsNullAsync()
        {
            var mockModule = CreateMockModule();
            mockModule.GetPlatformFileSpec().Returns((SbFileSpec)null);
            mockModule.GetSymbolFileSpec().Returns((SbFileSpec)null);
            mockModule.GetFileSpec().Returns((SbFileSpec)null);

            Assert.IsFalse(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, true));

            StringAssert.Contains(ErrorStrings.SymbolFileNameUnknown, searchLog.ToString());
            StringAssert.Contains(ErrorStrings.SymbolFileNameUnknown, logSpy.GetOutput());
        }

        [Test]
        public async Task LoadSymbols_ReadSymbolFileNameThrowsExceptionAsync()
        {
            const string EXCEPTION_MESSAGE = "test exception";
            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns((SbFileSpec)null);
            mockBinaryFileUtil
                .ReadSymbolFileNameAsync(Path.Combine(BINARY_DIRECTORY, BINARY_FILENAME))
                .Throws(new BinaryFileUtilException(EXCEPTION_MESSAGE));

            Assert.IsFalse(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, true));

            StringAssert.Contains(EXCEPTION_MESSAGE, searchLog.ToString());
            StringAssert.Contains(EXCEPTION_MESSAGE, logSpy.GetOutput());
        }

        [Test]
        public async Task LoadSymbols_InvalidBinaryFileSpecAsync()
        {
            var mockModule = CreateMockModule();
            mockModule.GetSymbolFileSpec().Returns((SbFileSpec)null);
            var binaryFileSpec = Substitute.For<SbFileSpec>();
            binaryFileSpec.GetDirectory().Returns(BINARY_DIRECTORY);
            binaryFileSpec.GetFilename().Returns("<invalid>");
            mockModule.GetFileSpec().Returns(binaryFileSpec);

            Assert.IsFalse(await symbolLoader.LoadSymbolsAsync(mockModule, searchLog, true));

            StringAssert.Contains("Illegal characters", searchLog.ToString());
            StringAssert.Contains("Illegal characters", logSpy.GetOutput());
        }

        [Test]
        public void LoadSymbols_NullModule()
        {
            Assert.ThrowsAsync<ArgumentNullException>(
                () => symbolLoader.LoadSymbolsAsync(null, searchLog, true));
        }

        SbModule CreateMockModule()
        {
            var mockModule = Substitute.For<SbModule>();
            mockModule.GetPlatformFileSpec().Returns(mockPlatformFileSpec);
            mockModule.GetSymbolFileSpec().Returns(mockSymbolFileSpec);
            mockModule.GetFileSpec().Returns(mockBinaryFileSpec);
            mockModule.GetUUIDString().Returns(UUID.ToString());
            return mockModule;
        }

        void SetHandleCommandReturnValue(SbCommandInterpreter mockCommandInterpreter,
                                         string command, ReturnStatus returnStatus,
                                         SbCommandReturnObject returnObject)
        {
            SbCommandReturnObject _;
            mockCommandInterpreter.HandleCommand(command, out _).Returns(x => {
                x[1] = returnObject;
                return returnStatus;
            });
        }

        void SetFindFileReturnValue(string path)
        {
            mockModuleFileFinder.FindFileAsync(SYMBOL_FILE_NAME, UUID, true, searchLog)
                .Returns(path);
        }
    }
}
