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

using System.IO;
using System.Threading.Tasks;
using DebuggerApi;
using NSubstitute;
using NUnit.Framework;
using SymbolStores;
using YetiCommon;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    class SymbolLoaderTests
    {
        [SetUp]
        public void SetUp()
        {
            _searchLog = new StringWriter();
            _successfulCommand = Substitute.For<SbCommandReturnObject>();
            _successfulCommand.Succeeded().Returns(true);
            _failedCommand = Substitute.For<SbCommandReturnObject>();
            _failedCommand.Succeeded().Returns(false);
            _mockModuleParser = Substitute.For<IModuleParser>();
            _mockModuleFileFinder = Substitute.For<IModuleFileFinder>();
            _mockCommandInterpreter = Substitute.For<SbCommandInterpreter>();
            _symbolLoader = new SymbolLoader(
                _mockModuleParser, _mockModuleFileFinder, _mockCommandInterpreter);
        }

        [Test]
        public async Task LoadSymbols_WhenBinaryAndSymbolPathsEmpty_FailsAsync()
        {
            // module's SymbolFileSpec, FileSpec and PlatformFileSpec are empty.
            var module = Substitute.For<SbModule>();
            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, false, _forceLoad);
            string output = _searchLog.ToString().Trim();
            Assert.IsFalse(result);
            StringAssert.AreEqualIgnoringCase(ErrorStrings.SymbolFileNameUnknown, output);
        }

        [TestCase(_elfFile, _linux, ModuleFormat.Elf)]
        [TestCase(_peFile, _windows, ModuleFormat.Pe)]
        [TestCase(_pdbFile, _windows, ModuleFormat.Pdb)]
        public async Task LoadSymbols_SymbolPathInSbModuleAndValid_SucceedsAsync(
            string filename, string triple, ModuleFormat expectedFormat)
        {
            var module = Substitute.For<SbModule>();
            // SymbolFileSpec differs from the GetFileSpec result and is valid.
            // It will be used to load the symbol into LLDB.
            // BuildId is empty so the check is based on filename.
            module.GetPlatformFileSpec().GetFilename().Returns(filename);
            module.GetSymbolFileSpec().GetFilename().Returns(filename);
            module.GetSymbolFileSpec().GetDirectory().Returns(_localDir);
            module.GetTriple().Returns(triple);

            SetParseBuildId($"{_localDir}\\{filename}", expectedFormat, "");
            SetHandleCommand(filename, _successfulCommand);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, false, _forceLoad);
            string output = _searchLog.ToString();
            Assert.IsTrue(result);
            StringAssert.Contains($"Successfully loaded symbol file", output);
        }

        [TestCase(_elfFile, _linux, ModuleFormat.Elf)]
        [TestCase(_peFile, _windows, ModuleFormat.Pe)]
        [TestCase(_pdbFile, _windows, ModuleFormat.Pdb)]
        public async Task LoadSymbols_SymbolPathInSbModuleAndValid_AndBuildIdsMatch_SucceedsAsync(
            string filename, string triple, ModuleFormat expectedFormat)
        {
            var module = Substitute.For<SbModule>();
            // SymbolFileSpec differs from the GetFileSpec result and is valid.
            // It will be used to load the symbol into LLDB.
            // BuildId is not empty so the check is based on filename and BuildId.
            module.GetPlatformFileSpec().GetFilename().Returns(filename);
            module.GetSymbolFileSpec().GetFilename().Returns(filename);
            module.GetSymbolFileSpec().GetDirectory().Returns(_localDir);
            module.GetUUIDString().Returns(_validBuildId);
            module.GetTriple().Returns(triple);

            SetParseBuildId($"{_localDir}\\{filename}", expectedFormat, _validBuildId);
            SetHandleCommand(filename, _successfulCommand);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, false, _forceLoad);
            string output = _searchLog.ToString();
            Assert.IsTrue(result);
            StringAssert.Contains( $"Successfully loaded symbol file", output);
        }

        [TestCase(_elfFile, _linux, ModuleFormat.Elf)]
        [TestCase(_peFile, _windows, ModuleFormat.Pe)]
        [TestCase(_pdbFile, _windows, ModuleFormat.Pdb)]
        public async Task LoadSymbols_SymbolPathInSbModuleAndBuildIdInvalid_FailsAsync(
            string filename, string triple, ModuleFormat expectedFormat)
        {
            var module = Substitute.For<SbModule>();
            module.GetPlatformFileSpec().GetFilename().Returns(filename);
            module.GetSymbolFileSpec().GetFilename().Returns(filename);
            module.GetSymbolFileSpec().GetDirectory().Returns(_localDir);
            module.GetUUIDString().Returns(_validBuildId);
            module.GetTriple().Returns(triple);
            string errorBuildIdMessage = "BuildId error";
            // BuildId is not readable, the validation fails and symbol won't be loaded.
            SetParseBuildId($"{_localDir}\\{filename}", expectedFormat, "", errorBuildIdMessage);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, false, _forceLoad);
            var output = _searchLog.ToString();
            Assert.IsFalse(result);
            StringAssert.Contains(errorBuildIdMessage, output);
        }

        [TestCase(_elfFile, _linux, ModuleFormat.Elf)]
        [TestCase(_peFile, _windows, ModuleFormat.Pe)]
        [TestCase(_pdbFile, _windows, ModuleFormat.Pdb)]
        public async Task LoadSymbols_SymbolPathInSbModuleAndBuildIdDoesNotMatch_FailsAsync(
            string filename, string triple, ModuleFormat expectedFormat)
        {
            var module = Substitute.For<SbModule>();
            module.GetPlatformFileSpec().GetFilename().Returns(filename);
            module.GetSymbolFileSpec().GetFilename().Returns(filename);
            module.GetSymbolFileSpec().GetDirectory().Returns(_localDir);
            module.GetUUIDString().Returns(_validBuildId);
            module.GetTriple().Returns(triple);
            // BuildId doesn't match, the validation fails and symbol won't be loaded.
            SetParseBuildId($"{_localDir}\\{filename}", expectedFormat, _mismatchedBuildId);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, false, _forceLoad);
            string output = _searchLog.ToString().Trim();

            Assert.IsFalse(result);
            StringAssert.AreEqualIgnoringCase( 
                Strings.BuildIdMismatch(
                    $"{_localDir}\\{filename}",
                    new BuildId(_validBuildId, ModuleFormat.Elf),
                    new BuildId(_mismatchedBuildId, ModuleFormat.Elf)), output);
        }

        [Test]
        public async Task LoadSymbols_SymbolPathInElfBinary_SucceedsAsync(
            [Values] bool useSymbolStores)
        {
            var module = Substitute.For<SbModule>();

            module.GetPlatformFileSpec().GetFilename().Returns(_elfFile);
            module.GetFileSpec().GetFilename().Returns(_elfFile);
            module.GetFileSpec().GetDirectory().Returns(_localDir);
            // SymbolPath is encoded in the binary contents, this path
            // will be used to load symbol into LLDB.
            _mockModuleParser.ParseDebugLinkInfo($"{_localDir}\\{_elfFile}")
                .Returns(new DebugLinkLocationInfo
                {
                    Data = new SymbolFileLocation(_cacheDir, _parsedSymbolName)
                });

            SetParseBuildId($"{_cacheDir}\\{_parsedSymbolName}", ModuleFormat.Elf, "");
            SetHandleCommand(_parsedSymbolName, _successfulCommand);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, useSymbolStores, _forceLoad);
            string output = _searchLog.ToString();
            Assert.IsTrue(result);
            StringAssert.Contains(
                $"Successfully loaded symbol file '{_cacheDir}\\{_parsedSymbolName}'", output);
        }

        [Test]
        public async Task LoadSymbols_SymbolPathInElfBinaryNonReadable_FailsAsync(
            [Values] bool useSymbolStores)
        {
            var module = Substitute.For<SbModule>();
            module.GetPlatformFileSpec().GetFilename().Returns(_elfFile);
            module.GetFileSpec().GetFilename().Returns(_elfFile);
            module.GetFileSpec().GetDirectory().Returns(_localDir);

            var parsedInfo = new DebugLinkLocationInfo
            {
                Data = SymbolFileLocation.Empty
            };
            string parsingError = "Failed to parse.";
            parsedInfo.AddError(parsingError);

            _mockModuleParser.ParseDebugLinkInfo($"{_localDir}\\{_elfFile}")
                .Returns(parsedInfo);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, useSymbolStores, _forceLoad);
            string output = _searchLog.ToString();

            Assert.IsFalse(result);
            StringAssert.Contains(parsingError, output);
            StringAssert.Contains(ErrorStrings.SymbolFileNameUnknown, output);
        }

        [Test]
        public async Task LoadSymbols_ElfBinaryEmpty_FailsAsync(
            [Values] bool useSymbolStores)
        {
            var module = Substitute.For<SbModule>();

            module.GetPlatformFileSpec().GetFilename().Returns(_elfFile);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, useSymbolStores, _forceLoad);
            string output = _searchLog.ToString();

            Assert.IsFalse(result);
            StringAssert.Contains(ErrorStrings.SymbolFileNameUnknown, output);
        }

        [Test]
        public async Task LoadSymbols_ElfBinaryInvalid_FailsAsync(
            [Values] bool useSymbolStores)
        {
            var module = Substitute.For<SbModule>();

            module.GetPlatformFileSpec().GetFilename().Returns(_elfFile);
            // The path to binary is corrupted (has invalid characters).
            module.GetFileSpec().GetDirectory().Returns("<!!!>");
            module.GetFileSpec().GetFilename().Returns(_elfFile);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, useSymbolStores, _forceLoad);
            string output = _searchLog.ToString();
            Assert.IsFalse(result);
            StringAssert.Contains(
                $"Invalid binary path '<!!!>' or name '{_elfFile}'. Illegal characters in path.",
                output);
            StringAssert.Contains(ErrorStrings.SymbolFileNameUnknown, output);
        }

        [Test]
        public async Task LoadSymbols_ForPdbModule_FailsAsync(
            [Values] bool useSymbolStores)
        {
            var module = Substitute.For<SbModule>();

            module.GetPlatformFileSpec().GetFilename().Returns(_pdbFile);
            module.GetTriple().Returns(_windows);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, useSymbolStores, _forceLoad);
            string output = _searchLog.ToString();
            Assert.IsFalse(result);
            StringAssert.Contains(ErrorStrings.SymbolFileNameUnknown, output);
        }

        [Test]
        public async Task LoadSymbols_ForPeModule_SucceedsAsync(
            [Values] bool useSymbolStores)
        {
            var module = Substitute.For<SbModule>();

            module.GetPlatformFileSpec().GetFilename().Returns(_peFile);
            module.GetFileSpec().GetDirectory().Returns(_localDir);
            module.GetFileSpec().GetFilename().Returns(_peFile);
            module.GetTriple().Returns(_windows);

            // PE -> PDB if symbol file from GetSymbolFileSpec is invalid
            // (changes file extension and ModuleFormat).
            SetParseBuildId($"{_localDir}\\{_pdbFile}", ModuleFormat.Pdb, "");
            SetHandleCommand(_pdbFile, _successfulCommand);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, useSymbolStores, _forceLoad);
            string output = _searchLog.ToString();
            Assert.IsTrue(result);
            StringAssert.Contains(
                $"Successfully loaded symbol file '{_localDir}\\{_pdbFile}'", output);
        }

        [TestCase(_elfFile)]
        [TestCase(_peFile)]
        [TestCase(_pdbFile)]
        public async Task LoadSymbols_SymbolFileNameInSbModuleAndSymbolStoresDisabled_FailsAsync(
            string filename)
        {
            var module = Substitute.For<SbModule>();
            // Filename is given but the directory is not. Since symbol stores are disabled,
            // we don't know where to search for this file.
            module.GetPlatformFileSpec().GetFilename().Returns(filename);
            module.GetSymbolFileSpec().GetFilename().Returns(filename);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, false, _forceLoad);
            string output = _searchLog.ToString();
            Assert.IsFalse(result);
            Assert.IsEmpty(output);
        }

        [TestCase(_elfFile, _linux, ModuleFormat.Elf)]
        [TestCase(_peFile, _windows, ModuleFormat.Pe)]
        [TestCase(_pdbFile, _windows, ModuleFormat.Pdb)]
        public async Task LoadSymbols_SymbolFileNameInSbModuleAndSymbolStoresEnabled_SucceedsAsync(
            string filename, string triple, ModuleFormat format)
        {
            var module = Substitute.For<SbModule>();

            module.GetPlatformFileSpec().GetFilename().Returns(filename);
            module.GetSymbolFileSpec().GetFilename().Returns(filename);
            module.GetTriple().Returns(triple);
           
            SetFindFile(filename, format, $"{_cacheDir}\\{filename}");
            SetHandleCommand(filename, _successfulCommand);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, true, _forceLoad);
            string output = _searchLog.ToString();
            Assert.IsTrue(result);
            StringAssert.Contains(
                $"Successfully loaded symbol file '{_cacheDir}\\{filename}'", output);
        }

        [TestCase(_elfFile, _linux, ModuleFormat.Elf)]
        [TestCase(_peFile, _windows, ModuleFormat.Pe)]
        [TestCase(_pdbFile, _windows, ModuleFormat.Pdb)]
        public async Task LoadSymbols_LLDBCannotAddSymbol_FailsAsync(
            string filename, string triple, ModuleFormat format)
        {
            var module = Substitute.For<SbModule>();

            module.GetPlatformFileSpec().GetFilename().Returns(filename);
            module.GetSymbolFileSpec().GetFilename().Returns(filename);
            module.GetTriple().Returns(triple);

            SetFindFile(filename, format, $"{_cacheDir}\\{filename}");
            SetHandleCommand(filename, _failedCommand);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, true, _forceLoad);
            string output = _searchLog.ToString();
            Assert.IsFalse(result);
            StringAssert.Contains($"LLDB error: ", output);
        }

        [Test]
        public async Task LoadSymbols_ElfSymbolNotFoundButWillTryAgain_SucceedsAsync()
        {
            var module = Substitute.For<SbModule>();

            module.GetSymbolFileSpec().GetFilename().Returns(_symbolName);
            module.GetFileSpec().GetFilename().Returns(_binaryName);

            SetFindFile(_symbolName, ModuleFormat.Elf, null);
            var expectedOutput = $"{_cacheDir}\\{_binaryName}.debug";
            SetFindFile($"{_binaryName}.debug", ModuleFormat.Elf, expectedOutput);
            SetHandleCommand($"{_binaryName}.debug", _successfulCommand);

            bool result = await _symbolLoader.LoadSymbolsAsync(
                module, _searchLog, true, _forceLoad);
            string output = _searchLog.ToString();
            
            Assert.IsTrue(result);
            Assert.That(
                output.Contains($"Successfully loaded symbol file '{expectedOutput}'"));
        }

        void SetParseBuildId(string path, ModuleFormat format, string buildId, string error = null)
        {
            var buildIdInfo = new BuildIdInfo() { Data = new BuildId(buildId, format) };
            if (!string.IsNullOrWhiteSpace(error)) 
            {
                buildIdInfo.AddError(error);
            }
            _mockModuleParser.ParseBuildIdInfo(path, format)
                .Returns(buildIdInfo);
        }

        void SetFindFile(string file, ModuleFormat format, string foundPath)
        {
            _mockModuleFileFinder.FindFileAsync(
                Arg.Is<ModuleSearchQuery>(x => x != null 
                                          && x.Filename == file
                                          && x.RequireDebugInfo
                                          && x.ModuleFormat == format),
                Arg.Any<StringWriter>()).Returns(foundPath);
        }

        void SetHandleCommand(string file, SbCommandReturnObject toReturn)
        {
            _mockCommandInterpreter.HandleCommand(Arg.Is<string>(x => x.Contains(file)), out _)
                .Returns(x =>
                {
                    x[1] = toReturn;
                    return ReturnStatus.SuccessFinishResult;
                });
        }

        const string _binaryName = "sbModule.binary_name";
        const string _symbolName = "sbModule.symbol_name";
        const string _parsedSymbolName = "sbModule.parsed_symbol_name";
        const string _linux = "-linux-unknown";
        const string _windows = "-windows-unknown";
        const string _validBuildId = "1234-45";
        const string _mismatchedBuildId = "1234-35";
        const string _elfFile = "test.so";
        const string _peFile = "test.dll";
        const string _pdbFile = "test.pdb";
        const string _localDir = "C:\\symbols";
        const string _cacheDir = "C:\\cache";

        StringWriter _searchLog;
        IModuleParser _mockModuleParser;
        IModuleFileFinder _mockModuleFileFinder;
        SbCommandInterpreter _mockCommandInterpreter;
        SbCommandReturnObject _successfulCommand;
        SbCommandReturnObject _failedCommand;

        // The value of _forceLoad doesn't affect the SymbolLoader logic.
        // It is used to notify the symbolStores whether they should use cache.
        readonly bool _forceLoad = false;
        SymbolLoader _symbolLoader;
    }
}
