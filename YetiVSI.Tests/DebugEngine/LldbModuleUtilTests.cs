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

            Assert.AreEqual(hasCompileUnits, mockModule.HasSymbolsLoaded());
        }

        [Test]
        public void IsPlaceholderModule()
        {
            var placeholderModule = CreatePlaceholderModule();

            Assert.True(placeholderModule.IsPlaceholderModule());
            Assert.False(placeholderModule.HasBinaryLoaded());
        }

        [Test]
        public void IsPlaceholderModule_TooManySections()
        {
            var placeholderModule = CreatePlaceholderModule();
            placeholderModule.GetNumSections().Returns(2u);

            Assert.False(placeholderModule.IsPlaceholderModule());
            Assert.True(placeholderModule.HasBinaryLoaded());
        }

        [Test]
        public void IsPlaceholderModule_NoPlaceholderSection()
        {
            var placeholderModule = CreatePlaceholderModule();
            placeholderModule.FindSection(".module_image").Returns((SbSection)null);

            Assert.False(placeholderModule.IsPlaceholderModule());
            Assert.True(placeholderModule.HasBinaryLoaded());
        }

        [TestCase(0u)]
        [TestCase(2000u)]
        public void GetAndApplyPlaceholderProperties(ulong fileBaseAddress)
        {
            long slide = (long)BASE_LOAD_ADDRESS - (long)fileBaseAddress;

            SbModule placeholderModule = CreatePlaceholderModule();

            var headerAddress = Substitute.For<SbAddress>();
            headerAddress.GetFileAddress().Returns(fileBaseAddress);

            var containerSection = Substitute.For<SbSection>();
            containerSection.GetSectionType().Returns(SectionType.Container);

            var otherModule = Substitute.For<SbModule>();
            otherModule.GetObjectFileHeaderAddress().Returns(headerAddress);
            otherModule.SetPlatformFileSpec(Arg.Any<SbFileSpec>()).Returns(true);

            PlaceholderModuleProperties properties =
                placeholderModule.GetPlaceholderProperties(mockTarget);
            Assert.IsNotNull(properties);
            Assert.AreEqual(slide + (long)fileBaseAddress, properties.Slide);
            Assert.AreEqual(mockPlatformFileSpec, properties.PlatformFileSpec);

            Assert.True(
                otherModule.ApplyPlaceholderProperties(properties, mockTarget));

            otherModule.Received().SetPlatformFileSpec(mockPlatformFileSpec);
            mockTarget.Received().SetModuleLoadAddress(otherModule, slide);
        }

        [Test]
        public void GetPlaceholderProperties_NotPlaceholder()
        {
            mockModule.FindSection(".module_image").Returns((SbSection)null);

            Assert.Throws<ArgumentException>(() =>
                mockModule.GetPlaceholderProperties(mockTarget));
        }

        [Test]
        public void GetPlaceholderProperties_GetLoadAddressFails()
        {
            SbModule placeholderModule = CreatePlaceholderModule();
            placeholderModule.FindSection(".module_image")
                .GetLoadAddress(mockTarget).Returns(DebuggerConstants.INVALID_ADDRESS);

            Assert.IsNull(placeholderModule.GetPlaceholderProperties(mockTarget));

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

            Assert.IsFalse(Substitute.For<SbModule>()
                               .ApplyPlaceholderProperties(placeholderProperties, mockTarget));

            var output = logSpy.GetOutput();
            Assert.That(output, Does.Contain("Failed to set load address"));
            Assert.That(output, Does.Contain(error.GetCString()));
        }

        [Test]
        public void GetPlaceholderProperties_GetPlatformFileSpecFails()
        {
            SbModule placeholderModule = CreatePlaceholderModule();
            placeholderModule.GetPlatformFileSpec().Returns((SbFileSpec)null);

            Assert.IsNull(placeholderModule.GetPlaceholderProperties(mockTarget));

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

            Assert.IsFalse(otherModule
                               .ApplyPlaceholderProperties(placeholderProperties, mockTarget));

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
