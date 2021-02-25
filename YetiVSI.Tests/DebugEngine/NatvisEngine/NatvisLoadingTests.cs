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

﻿using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using NSubstitute;
using NUnit.Framework;
using TestsCommon.TestSupport;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.Util;
using Does = YetiVSI.Test.TestSupport.NUnitExtensions.Does;

namespace YetiVSI.Test.DebugEngine.NatvisEngine
{
    [TestFixture]
    public class NatvisLoadingTests
    {
        NLogSpy _nLogSpy;
        LogSpy _traceLogSpy;

        NatvisDiagnosticLogger _natvisLogger;
        NatvisVisualizerScanner _natvisScanner;

        IWindowsRegistry _mockRegistry;

        MockFileSystem _mockFileSystem;
        NatvisLoader _natvisLoader;

        const string _userDirNatvisFilepath =
            @"C:\dummy\user\dir\path\Visualizers\file1.natvis";

        const string _systemDirNatvisFilepath =
            @"C:\dummy\system\dir\path\Packages\Debugger\Visualizers\file1.natvis";

        const string _validNatvis = @"
<AutoVisualizer xmlns = ""http://schemas.microsoft.com/vstudio/debugger/natvis/2010""/>
";

        // <AutoVisualizer> tag isn't closed.
        const string _invalidNatvis = @"
<AutoVisualizer xmlns = ""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
";

        [SetUp]
        public void SetUp()
        {
            _traceLogSpy = new LogSpy();
            _traceLogSpy.Attach();

            var compRoot = new MediumTestDebugEngineFactoryCompRoot()
            {
                WindowsRegistry = Substitute.For<IWindowsRegistry>()
            };

            _natvisScanner = compRoot.GetNatvisVisualizerScanner();
            _natvisLoader = compRoot.GetNatvisLoader();

            _nLogSpy = compRoot.GetNatvisDiagnosticLogSpy();
            _nLogSpy.Attach();

            _natvisLogger = compRoot.GetNatvisDiagnosticLogger();
            _mockRegistry = compRoot.GetWindowsRegistry();
            _mockFileSystem = (MockFileSystem) compRoot.GetFileSystem();
        }

        [TearDown]
        public void TearDown()
        {
            _traceLogSpy.Detach();
            _nLogSpy.Detach();
        }

        [Test]
        public void ValidRegistryLoad()
        {
            // User directory set up.
            _mockRegistry.GetValue(
                @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3",
                "VisualStudioLocation", null).Returns(
                @"C:\dummy\user\dir\path");

            _mockFileSystem.AddFile(_userDirNatvisFilepath, new MockFileData(_validNatvis));

            // System directory set up.

            _mockRegistry.GetValue(
                @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3_Config",
                "InstallDir", null).Returns(
                @"C:\dummy\system\dir\path\extra-sub-dir");

            _mockFileSystem.AddFile(_systemDirNatvisFilepath, new MockFileData(_validNatvis));

            // Exercise

            _natvisScanner.LoadFromRegistry(@"Microsoft\Visual Studio\1.2.3");

            Assert.That(_nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(_nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(_nLogSpy.GetOutput(), Does.Contain(_userDirNatvisFilepath));
            Assert.That(_nLogSpy.GetOutput(), Does.Contain(_systemDirNatvisFilepath));
        }

        [Test]
        public void DirectoryDoesntExistForRegstryLoading()
        {
            // User directory set up.

            _mockRegistry.GetValue(
                @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3",
                "VisualStudioLocation", null).Returns(
                @"C:\dummy\user\dir\path");

            _mockFileSystem.AddDirectory(@"C:\dummy\user\dir\path\Visualizers");

            // System directory set up.

            _mockRegistry.GetValue(
                @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3_Config",
                "InstallDir", null).Returns(
                @"C:\dummy\system\dir\path\extra-sub-dir");

            // Don't create the system directory.

            // Exercise

            _natvisScanner.LoadFromRegistry(@"Microsoft\Visual Studio\1.2.3");

            Assert.That(_nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(_nLogSpy.GetOutput(), Does.Contain("Could not find"));
            Assert.That(_nLogSpy.GetOutput(), Does.Contain(
                            @"C:\dummy\system\dir\path\Packages\Debugger\Visualizers"));

            Assert.That(_nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void UserDirectoryRegistryKeyDoesntExist()
        {
            // User directory set up.

            _mockRegistry.GetValue(
                @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3",
                "VisualStudioLocation", null).Returns(null);

            // System directory set up.

            _mockRegistry.GetValue(
                @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3_Config",
                "InstallDir", null).Returns(
                @"C:\dummy\system\dir\path\extra-sub-dir");

            _mockFileSystem.AddDirectory(@"C:\dummy\system\dir\path\Packages\Debugger\Visualizers");

            // Exercise

            _natvisScanner.LoadFromRegistry(@"Microsoft\Visual Studio\1.2.3");

            Assert.That(_nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(_nLogSpy.GetOutput(), Does.Contain(
                            @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3:VisualStudioLocation"));
            Assert.That(_nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void SystemDirectoryRegistryKeyDoesntExist()
        {
            // User directory set up.

            _mockRegistry.GetValue(
                @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3",
                "VisualStudioLocation", null).Returns(
                @"C:\dummy\user\dir\path");

            _mockFileSystem.AddDirectory(@"C:\dummy\user\dir\path\Visualizers");

            // System directory set up.

            _mockRegistry.GetValue(
                @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3_Config",
                "InstallDir", null).Returns(null);

            // Exercise

            _natvisScanner.LoadFromRegistry(@"Microsoft\Visual Studio\1.2.3");

            Assert.That(_nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(_nLogSpy.GetOutput(), Does.Not.Contain(
                            @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3:InstallDir"));
            Assert.That(_nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void FileDoesntExist()
        {
            const string filePath = @"C:\dir\file.natvis";
            Assert.That(_mockFileSystem.FileExists(filePath), Is.False);

            // Exercise
            _natvisLoader.LoadFile(filePath, new List<NatvisVisualizerScanner.FileInfo>());

            Assert.That(_nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(_nLogSpy.GetOutput(), Does.Contain(filePath));
        }

        [Test]
        public void InvalidXml()
        {
            // User directory set up.

            _mockRegistry.GetValue(
                @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3",
                "VisualStudioLocation", null).Returns(
                @"C:\dummy\user\dir\path");

            _mockFileSystem.AddDirectory(@"C:\dummy\user\dir\path\Visualizers");

            // System directory set up.

            _mockRegistry.GetValue(
                @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3_Config",
                "InstallDir", null).Returns(
                @"C:\dummy\system\dir\path\extra-sub-dir");

            _mockFileSystem.AddFile(_systemDirNatvisFilepath, new MockFileData(_invalidNatvis));

            // Exercise

            _natvisScanner.LoadFromRegistry(@"Microsoft\Visual Studio\1.2.3");

            Assert.That(_nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(_nLogSpy.GetOutput(), Does.Contain(_systemDirNatvisFilepath));
        }

        [Test]
        public void FileLoadLogsSentToTrace()
        {
            _natvisLogger.SetLogLevel(NatvisLoggingLevel.OFF);

            const string filePath = @"C:\dir\file.natvis";
            _mockFileSystem.FileExists(filePath).Returns(false);

            // Exercise
            _natvisLoader.LoadFile(filePath, new List<NatvisVisualizerScanner.FileInfo>());

            Assert.That(_traceLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(_traceLogSpy.GetOutput(), Does.Contain(filePath));
        }

        [Test]
        public void ReloadLoadsFromRegistry()
        {
            // System directory set up.

            _mockRegistry.GetValue(
                @"HKEY_CURRENT_USER\Microsoft\Visual Studio\1.2.3_Config",
                "InstallDir", null).Returns(
                @"C:\dummy\system\dir\path\extra-sub-dir");

            _mockFileSystem.AddFile(_systemDirNatvisFilepath, new MockFileData(_validNatvis));

            _natvisScanner.LoadFromRegistry(@"Microsoft\Visual Studio\1.2.3");

            _nLogSpy.Clear();

            _natvisScanner.Reload();

            Assert.That(_nLogSpy.GetOutput(), Does.Contain(_systemDirNatvisFilepath));
        }

        [Test]
        public void MultipleReloads()
        {
            _natvisScanner.Reload();
            _natvisScanner.Reload();

            // Ensures no exceptions are thrown.
        }
    }
}