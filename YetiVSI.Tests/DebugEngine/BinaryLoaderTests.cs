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
using NSubstitute;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading.Tasks;
using YetiCommon;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class BinaryLoaderTests
    {
        const string BINARY_FILENAME = "test";
        const string PATH_IN_STORE = @"C:\store\" + BINARY_FILENAME;
        static BuildId UUID = new BuildId("1234");
        const long MODULE_SLIDE = 2000;

        PlaceholderModuleProperties placeholderProperties;
        StringWriter searchLog;
        RemoteTarget mockTarget;
        EventHandler<LldbModuleReplacedEventArgs> moduleReplacedHandler;
        SbModule placeholderModule;
        IModuleFileFinder mockModuleFileFinder;
        ILldbModuleUtil mockModuleUtil;
        BinaryLoader binaryLoader;

        [SetUp]
        public void SetUp()
        {
            searchLog = new StringWriter();

            mockTarget = Substitute.For<RemoteTarget>();
            moduleReplacedHandler = Substitute.For<EventHandler<LldbModuleReplacedEventArgs>>();

            mockModuleFileFinder = Substitute.For<IModuleFileFinder>();
            mockModuleFileFinder.FindFileAsync(BINARY_FILENAME, UUID, false, searchLog)
                .Returns(Task.FromResult(PATH_IN_STORE));

            placeholderModule = Substitute.For<SbModule>();
            placeholderModule.GetPlatformFileSpec().GetFilename().Returns(BINARY_FILENAME);
            placeholderModule.GetUUIDString().Returns(UUID.ToString());

            placeholderProperties =
                new PlaceholderModuleProperties(MODULE_SLIDE, Substitute.For<SbFileSpec>());

            mockModuleUtil = Substitute.For<ILldbModuleUtil>();
            mockModuleUtil.IsPlaceholderModule(placeholderModule).Returns(true);
            mockModuleUtil.GetPlaceholderProperties(Arg.Any<SbModule>(), Arg.Any<RemoteTarget>())
                .ReturnsForAnyArgs(placeholderProperties);
            mockModuleUtil.ApplyPlaceholderProperties(
                Arg.Any<SbModule>(), Arg.Any<PlaceholderModuleProperties>(),
                Arg.Any<RemoteTarget>())
                    .ReturnsForAnyArgs(true);


            binaryLoader = new BinaryLoader(mockModuleUtil, mockModuleFileFinder,
                mockTarget);
            binaryLoader.LldbModuleReplaced += moduleReplacedHandler;
        }

        [Test]
        public void LoadBinary_NullModule()
        {
            SbModule module = null;
            Assert.ThrowsAsync<ArgumentNullException>(
                () => binaryLoader.LoadBinaryAsync(module, searchLog));
        }

        [Test]
        public async Task LoadBinary_AlreadyLoadedAsync()
        {
            var loadedModule = Substitute.For<SbModule>();
            mockModuleUtil.IsPlaceholderModule(loadedModule).Returns(false);

            var module = loadedModule;
            bool ok;
            (module, ok) = await binaryLoader.LoadBinaryAsync(module, searchLog);
            Assert.True(ok);

            Assert.AreSame(loadedModule, module);
        }

        [Test]
        public async Task LoadBinary_NoBinaryNameAsync()
        {
            placeholderModule.GetPlatformFileSpec().Returns((SbFileSpec)null);

            var module = placeholderModule;
            bool ok;
            (module, ok) = await binaryLoader.LoadBinaryAsync(module, searchLog);
            Assert.False(ok);

            Assert.AreSame(module, placeholderModule);
            StringAssert.Contains(ErrorStrings.BinaryFileNameUnknown, searchLog.ToString());
        }

        [Test]
        public async Task LoadBinary_FileNotFoundAsync()
        {
            mockModuleFileFinder.FindFileAsync(BINARY_FILENAME, UUID, false, searchLog)
                .Returns(Task.FromResult<string>(null));

            var module = placeholderModule;
            bool ok;
            (module, ok) = await binaryLoader.LoadBinaryAsync(module, searchLog);
            Assert.False(ok);

            Assert.AreSame(module, placeholderModule);
        }

        [Test]
        public async Task LoadBinary_FailedToAddModuleAsync()
        {
            mockTarget.AddModule(PATH_IN_STORE, null, UUID.ToString()).Returns((SbModule)null);

            var module = placeholderModule;
            bool ok;
            (module, ok) = await binaryLoader.LoadBinaryAsync(module, searchLog);
            Assert.False(ok);

            Assert.AreSame(module, placeholderModule);
            StringAssert.Contains(ErrorStrings.FailedToLoadBinary(PATH_IN_STORE),
                searchLog.ToString());
        }

        [Test]
        public async Task LoadBinaryAsync()
        {
            var newModule = Substitute.For<SbModule>();
            mockTarget.AddModule(PATH_IN_STORE, null, UUID.ToString()).Returns(newModule);

            var module = placeholderModule;
            bool ok;
            (module, ok) = await binaryLoader.LoadBinaryAsync(module, searchLog);
            Assert.True(ok);

            mockModuleUtil.Received().GetPlaceholderProperties(placeholderModule, mockTarget);
            mockModuleUtil.Received().ApplyPlaceholderProperties(newModule,
                placeholderProperties, mockTarget);
            mockTarget.Received().AddModule(PATH_IN_STORE, null, UUID.ToString());
            mockTarget.Received().RemoveModule(placeholderModule);
            moduleReplacedHandler.Received().Invoke(binaryLoader,
                Arg.Is<LldbModuleReplacedEventArgs>(a =>
                    a.AddedModule == newModule && a.RemovedModule == placeholderModule));
            Assert.AreSame(module, newModule);
            StringAssert.Contains("Binary loaded successfully.", searchLog.ToString());
        }
    }
}
