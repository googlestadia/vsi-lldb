// Copyright 2022 Google LLC
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
using NUnit.Framework;

namespace YetiCommon.Tests
{
    [TestFixture]
    public partial class ModuleParserTests
    {
        [TestCase(new byte[] { 225, 222, 208, 173, 67, 130, 145, 107 }, "E1DED0AD-4382-916B")]
        [TestCase(new byte[] { 193, 192, 222, 117, 96, 83, 167, 177 }, "C1C0DE75-6053-A7B1")]
        [TestCase(new byte[]
        {
            96, 109, 249, 195, 85, 16, 62, 130, 20, 13, 81, 59, 199, 162,
            90, 99, 85, 145, 193, 83
        }, "606DF9C3-5510-3E82-140D-513bc7a25a63-5591C153")]
        [TestCase(new byte[] { }, "")]
        public void ParseBuildIdValue(byte[] input, string expectedHexString)
        {
            var moduleParser = new ModuleParser();
            BuildId output = moduleParser.ParseBuildIdValue(input);
            Assert.True(output.Matches(new BuildId(expectedHexString), ModuleFormat.Elf));
        }

        [TestCase(new byte[]
        {
            54, 100, 102, 57, 99, 51, 53, 53, 49, 48, 51, 101, 56, 50, 49, 52, 48, 100,
            53, 49, 51, 98, 99, 55, 97, 50, 53, 97, 54, 51, 53, 53, 57, 49, 99, 49, 53,
            51, 46, 100, 101, 98, 117, 103, 0, 0, 0, 0, 11, 140, 156, 99
        }, "6df9c355103e82140d513bc7a25a635591c153.debug")]
        [TestCase(new byte[]
        {
            53, 49, 52, 51, 101, 54, 56, 48, 102, 102, 48, 99, 100, 52, 99, 100, 53, 49,
            99, 99, 101, 49, 99, 101, 56, 99, 97, 50, 49, 54, 101, 54, 51, 53, 97, 49,
            100, 54, 46, 100, 101, 98, 117, 103, 0, 0, 0, 0, 179, 9, 173, 228
        }, "5143e680ff0cd4cd51cce1ce8ca216e635a1d6.debug")]
        [TestCase(new byte[]
        {
            52, 97, 48, 48, 54, 50, 49, 97, 52, 48, 57, 55, 51, 57, 49, 53, 98, 98, 53,
            98, 100, 99, 102, 50, 102, 48, 52, 102, 99, 48, 98, 57, 101, 57, 98, 98,
            100, 100, 46, 100, 101, 98, 117, 103, 0, 0, 0, 0, 126, 109, 209, 145
        }, "4a00621a40973915bb5bdcf2f04fc0b9e9bbdd.debug")]
        [TestCase(new byte[] { 0 }, "")]
        public void ParseStringValue(byte[] input, string expectedString)
        {
            var moduleParser = new ModuleParser();
            string output = moduleParser.ParseStringValue(input);
            Assert.AreEqual(expectedString, output);
        }

        [Test]
        public void BuildIdParametersParsingTest()
        {
            var outputLines = new List<string>
            {
                "",
                "/proc/6386/exe:     file format elf64-x86-64",
                "",
                "Contents of section .note.gnu.build-id:",
                " 0274 04000000 14000000 03000000 474e5500  ............GNU.",
                " 0284 e969e107 5118acd1 d87baccb 6f1185ed  .i..Q....{..o...",
                " 0294 c1a689e3                             ....            "
            };
            var expectedHexString =
                "040000001400000003000000474e5500e969e1075118acd1d87baccb6f1185edc1a689e3";
            var expectedModuleFormat = ModuleFormat.Elf;

            var moduleParser = new ModuleParser();

            ModuleFormat moduleFormat = moduleParser.ParseModuleFormatDump(outputLines);
            Assert.AreEqual(expectedModuleFormat, moduleFormat);

            string hexString = moduleParser.ParseHexDump(outputLines);
            Assert.AreEqual(expectedHexString, hexString);
        }

        [Test]
        public void InvalidModuleFormatTest()
        {
            var outputLines = new List<string>
            {
                "",
                "/proc/6386/exe:     file format el64-x86-64",
                "",
                "Contents of section .note.gnu.build-id:",
                " 0274 04000000 14000000 03000000 474e5500  ............GNU.",
                " 0284 e969e107 5118acd1 d87baccb 6f1185ed  .i..Q....{..o...",
                " 0294 c1a689e3                             ....            "
            };
            var moduleParser = new ModuleParser();

            Assert.Throws<FormatException>(() => moduleParser.ParseModuleFormatDump(outputLines),
                                           ErrorStrings.FailedToParseModuleFormatFromDump(
                                               outputLines));
        }
    }
}