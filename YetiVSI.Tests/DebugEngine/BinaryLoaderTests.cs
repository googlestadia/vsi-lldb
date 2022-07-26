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

using System;
using System.IO;
using System.Threading.Tasks;
using DebuggerApi;
using NSubstitute;
using NUnit.Framework;
using YetiCommon;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class BinaryLoaderTests
    {
        const string _binaryFilename = "test";
        const string _pathInStore = @"C:\store\" + _binaryFilename;
        static BuildId _uuid = new BuildId("1234", ModuleFormat.Elf);
        const string _triple = "msp430--";
        readonly ModuleSearchQuery _searchQuery =
            new ModuleSearchQuery(_binaryFilename, _uuid, ModuleFormat.Elf)
        {
            RequireDebugInfo = false,
            ForceLoad = false
        };

        StringWriter _searchLog;
        RemoteTarget _mockTarget;
        EventHandler<LldbModuleReplacedEventArgs> _moduleReplacedHandler;
        SbModule _placeholderModule;
        IModuleFileFinder _mockModuleFileFinder;
        BinaryLoader _binaryLoader;

        [SetUp]
        public void SetUp()
        {
            _searchLog = new StringWriter();

            _mockTarget = Substitute.For<RemoteTarget>();
            _moduleReplacedHandler = Substitute.For<EventHandler<LldbModuleReplacedEventArgs>>();

            _mockModuleFileFinder = Substitute.For<IModuleFileFinder>();
            _mockModuleFileFinder.FindFileAsync(Arg.Any<ModuleSearchQuery>(), _searchLog)
                .Returns(Task.FromResult(_pathInStore));

            _placeholderModule = Substitute.For<SbModule>();
            _placeholderModule.GetPlatformFileSpec().GetFilename().Returns(_binaryFilename);
            _placeholderModule.GetUUIDString().Returns(_uuid.ToString());
            _placeholderModule.GetTriple().Returns(_triple);
            _placeholderModule.FindSection(".module_image").Returns(Substitute.For<SbSection>());
            _placeholderModule.GetNumSections().Returns(1ul);

            _binaryLoader = new BinaryLoader(_mockModuleFileFinder,
                _mockTarget);
            _binaryLoader.LldbModuleReplaced += _moduleReplacedHandler;
        }

        [Test]
        public async Task LoadBinary_FileNotFoundAsync()
        {
            _mockModuleFileFinder.FindFileAsync(_searchQuery, _searchLog)
                .Returns(Task.FromResult<string>(null));

            (SbModule module, bool ok) = await _binaryLoader.LoadBinaryAsync(
                _placeholderModule, _searchLog, false);
            Assert.False(ok);

            Assert.AreSame(module, _placeholderModule);
        }

        [Test]
        public async Task LoadBinary_WhenBinaryNameIsEmptyAsync()
        {
            var module = Substitute.For<SbModule>();

            (SbModule found, bool ok) = await _binaryLoader.LoadBinaryAsync(module, _searchLog,
                false);

            Assert.False(ok);
            Assert.AreSame(module, found);
            Assert.IsEmpty(_searchLog.ToString());
        }

        [Test]
        public async Task LoadBinary_WhenBinaryNotFoundByModuleFileFinderAsync()
        {
            var module = Substitute.For<SbModule>();
            string filename = "file_doesn't_exist.txt";
            module.GetPlatformFileSpec().GetFilename().Returns(filename);
            _mockModuleFileFinder
                .FindFileAsync(Arg.Is<ModuleSearchQuery>(q => q.Filename == filename), _searchLog)
                .Returns((string)null);

            (SbModule found, bool ok) = await _binaryLoader.LoadBinaryAsync(module, _searchLog,
                false);

            Assert.False(ok);
            Assert.AreSame(module, found);
            Assert.IsEmpty(_searchLog.ToString());
        }

        [Test]
        public async Task LoadBinary_FailedToAddModuleAsync()
        {
            (SbModule module, bool ok) = await _binaryLoader.LoadBinaryAsync(
                _placeholderModule, _searchLog, false);
            Assert.False(ok);

            Assert.AreSame(module, _placeholderModule);
            StringAssert.Contains(ErrorStrings.FailedToLoadBinary(_pathInStore),
                _searchLog.ToString());
        }

        [Test]
        public async Task LoadBinaryAsync()
        {
            var newModule = Substitute.For<SbModule>();

            _mockTarget.AddModule(_pathInStore, _triple, _uuid.ToString()).Returns(newModule);
            newModule.SetPlatformFileSpec(Arg.Any<SbFileSpec>()).Returns(true);

            (SbModule module, bool ok) = await _binaryLoader.LoadBinaryAsync(
                _placeholderModule, _searchLog, false);
            Assert.True(ok);

            _mockTarget.Received().RemoveModule(_placeholderModule);
            _moduleReplacedHandler.Received().Invoke(_binaryLoader,
                Arg.Is<LldbModuleReplacedEventArgs>(a =>
                    a.AddedModule == newModule && a.RemovedModule == _placeholderModule));
            Assert.AreSame(module, newModule);
            StringAssert.Contains("Successfully loaded binary 'C:\\store\\test'.",
                                  _searchLog.ToString());
        }
    }
}
