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
using TestsCommon.TestSupport;
using YetiVSI.DebugEngine;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class LldbModuleUtilTests
    {
        const ulong CODE_SECTION_FILE_OFFSET = 10;
        const ulong BASE_LOAD_ADDRESS = 2000;
        const long MODULE_SLIDE = 3000;

        RemoteTarget mockTarget;
        SbModule mockModule;
        SbFileSpec mockPlatformFileSpec;
        ILldbModuleUtil moduleUtil;
        LogSpy logSpy;

        [SetUp]
        public void SetUp()
        {
            logSpy = new LogSpy();
            logSpy.Attach();

            var noError = new SbErrorStub(true, null);
            mockTarget = Substitute.For<RemoteTarget>();
            mockTarget.SetModuleLoadAddress(Arg.Any<SbModule>(), Arg.Any<long>()).Returns(noError);
            mockModule = Substitute.For<SbModule>();
            mockModule.HasCompileUnits().Returns(false);
            mockModule.FindSection(Arg.Any<string>()).Returns((SbSection)null);
            mockPlatformFileSpec = Substitute.For<SbFileSpec>();
            moduleUtil = new LldbModuleUtil();
        }

        [TearDown]
        public void TearDown()
        {
            logSpy.Detach();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void HasSymbolsLoaded(bool hasCompileUnits)
        {
            mockModule.HasCompileUnits().Returns(hasCompileUnits);

            Assert.AreEqual(hasCompileUnits, moduleUtil.HasSymbolsLoaded(mockModule));
        }

        [Test]
        public void IsPlaceholderModule()
        {
            var placeholderModule = CreatePlaceholderModule();

            Assert.True(moduleUtil.IsPlaceholderModule(placeholderModule));
            Assert.False(moduleUtil.HasBinaryLoaded(placeholderModule));
        }

        [Test]
        public void IsPlaceholderModule_TooManySections()
        {
            var placeholderModule = CreatePlaceholderModule();
            placeholderModule.GetNumSections().Returns(2u);

            Assert.False(moduleUtil.IsPlaceholderModule(mockModule));
            Assert.True(moduleUtil.HasBinaryLoaded(mockModule));
        }

        [Test]
        public void IsPlaceholderModule_NoPlaceholderSection()
        {
            var placeholderModule = CreatePlaceholderModule();
            placeholderModule.FindSection(".module_image").Returns((SbSection)null);

            Assert.False(moduleUtil.IsPlaceholderModule(mockModule));
            Assert.True(moduleUtil.HasBinaryLoaded(mockModule));
        }

        [TestCase(0u)]
        [TestCase(2000u)]
        public void GetAndApplyPlaceholderProperties(ulong fileBaseAddress)
        {
            ulong codeSectionFileAddress = fileBaseAddress + CODE_SECTION_FILE_OFFSET;
            long slide = (long)BASE_LOAD_ADDRESS - (long)fileBaseAddress;

            SbModule placeholderModule = CreatePlaceholderModule();

            var codeSection = Substitute.For<SbSection>();
            codeSection.GetSectionType().Returns(SectionType.Code);
            codeSection.GetFileAddress().Returns(codeSectionFileAddress);
            codeSection.GetFileOffset().Returns(CODE_SECTION_FILE_OFFSET);

            var containerSection = Substitute.For<SbSection>();
            containerSection.GetSectionType().Returns(SectionType.Container);

            var otherModule = Substitute.For<SbModule>();
            otherModule.GetFirstCodeSection().Returns(codeSection);
            otherModule.SetPlatformFileSpec(Arg.Any<SbFileSpec>()).Returns(true);

            PlaceholderModuleProperties properties =
                moduleUtil.GetPlaceholderProperties(placeholderModule, mockTarget);
            Assert.IsNotNull(properties);
            Assert.AreEqual(slide + (long)fileBaseAddress, properties.Slide);
            Assert.AreEqual(mockPlatformFileSpec, properties.PlatformFileSpec);

            Assert.True(
                moduleUtil.ApplyPlaceholderProperties(otherModule, properties, mockTarget));

            otherModule.Received().SetPlatformFileSpec(mockPlatformFileSpec);
            mockTarget.Received().SetModuleLoadAddress(otherModule, slide);
        }

        [Test]
        public void GetPlaceholderProperties_NotPlaceholder()
        {
            mockModule.FindSection(".module_image").Returns((SbSection)null);

            Assert.Throws<ArgumentException>(() =>
                moduleUtil.GetPlaceholderProperties(mockModule, mockTarget));
        }

        [Test]
        public void GetPlaceholderProperties_GetLoadAddressFails()
        {
            SbModule placeholderModule = CreatePlaceholderModule();
            placeholderModule.FindSection(".module_image")
                .GetLoadAddress(mockTarget).Returns(DebuggerConstants.INVALID_ADDRESS);

            Assert.IsNull(moduleUtil.GetPlaceholderProperties(placeholderModule, mockTarget));

            var output = logSpy.GetOutput();
            Assert.That(output, Does.Contain("Failed to get load address"));
        }

        [Test]
        public void ApplyPlaceholderProperties_SetModuleLoadAddressFails()
        {
            var error = new SbErrorStub(false, "failorama");
            mockTarget.SetModuleLoadAddress(Arg.Any<SbModule>(), Arg.Any<long>()).Returns(error);

            var placeholderProperties =
                new PlaceholderModuleProperties(MODULE_SLIDE, Substitute.For<SbFileSpec>());

            Assert.IsFalse(moduleUtil.ApplyPlaceholderProperties(
                Substitute.For<SbModule>(), placeholderProperties, mockTarget));

            var output = logSpy.GetOutput();
            Assert.That(output, Does.Contain("Failed to set load address"));
            Assert.That(output, Does.Contain(error.GetCString()));
        }

        [Test]
        public void GetPlaceholderProperties_GetPlatformFileSpecFails()
        {
            SbModule placeholderModule = CreatePlaceholderModule();
            placeholderModule.GetPlatformFileSpec().Returns((SbFileSpec)null);

            Assert.IsNull(moduleUtil.GetPlaceholderProperties(placeholderModule, mockTarget));

            var output = logSpy.GetOutput();
            Assert.That(output, Does.Contain("Failed to get file spec"));
        }

        [Test]
        public void ApplyPlaceholderProperties_SetPlatformFileSpecFails()
        {
            var otherModule = Substitute.For<SbModule>();
            otherModule.SetPlatformFileSpec(Arg.Any<SbFileSpec>()).Returns(false);

            var placeholderProperties =
                new PlaceholderModuleProperties(MODULE_SLIDE, Substitute.For<SbFileSpec>());

            Assert.IsFalse(moduleUtil.ApplyPlaceholderProperties(
                otherModule, placeholderProperties, mockTarget));

            var output = logSpy.GetOutput();
            Assert.That(output, Does.Contain("Failed to set file spec"));
        }

        SbModule CreatePlaceholderModule()
        {
            var mockPlaceholderSection = Substitute.For<SbSection>();
            mockPlaceholderSection.GetLoadAddress(mockTarget).Returns(BASE_LOAD_ADDRESS);
            var mockPlaceholderModule = Substitute.For<SbModule>();
            mockPlaceholderModule.GetPlatformFileSpec().Returns(mockPlatformFileSpec);
            mockPlaceholderModule.GetNumSections().Returns(1u);
            mockPlaceholderModule.FindSection(".module_image").Returns(mockPlaceholderSection);
            return mockPlaceholderModule;
        }
    }
}
