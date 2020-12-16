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

﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YetiCommon;

namespace YetiCommon.Tests
{
    [TestFixture]
    class VersionsTests
    {
        [Test]
        public void ParseReleaseSdkVersion()
        {
            var sdkVersion = Versions.SdkVersion.Create("7489.1.25.4");
            Assert.AreEqual("1", sdkVersion.major);
            Assert.AreEqual("25", sdkVersion.minor);
            Assert.AreEqual("4", sdkVersion.patch);
            Assert.AreEqual("7489", sdkVersion.build);
            Assert.AreEqual("1.25.4.7489", sdkVersion.ToString());
        }

        [Test]
        public void ParseMasterSdkVersion()
        {
            var sdkVersion = Versions.SdkVersion.Create("7489.master");
            Assert.AreEqual("master", sdkVersion.major);
            Assert.AreEqual("", sdkVersion.minor);
            Assert.AreEqual("", sdkVersion.patch);
            Assert.AreEqual("7489", sdkVersion.build);
            Assert.AreEqual("master.7489", sdkVersion.ToString());
        }

        [TestCase("7489.3.4")]
        [TestCase("")]
        [TestCase("invalid")]
        public void ParseInvalidSdkVersion(string version)
        {
            var sdkVersion = Versions.SdkVersion.Create(version);
            Assert.AreEqual(null, sdkVersion);
        }

        [Test]
        public void GetExtensionVersion()
        {
            string vsixManifestPath =
                Path.Combine(YetiConstants.RootDir, @"TestData\source.extension.vsixmanifest");
            Assert.That(File.Exists(vsixManifestPath),
                        () => $"Manifest file {vsixManifestPath} not found");
            string extensionVersion = Versions.GetExtensionVersion(vsixManifestPath);
            Assert.That(extensionVersion, Is.Not.Null);

            string[] parts = extensionVersion.Split('.');
            Assert.That(parts.Length, Is.EqualTo(3));

            int major, minor, patch;
            Assert.True(int.TryParse(parts[0], out major));
            Assert.True(int.TryParse(parts[1], out minor));
            Assert.True(int.TryParse(parts[2], out patch));
            Assert.That(major, Is.GreaterThanOrEqualTo(1));
            Assert.That(minor, Is.GreaterThanOrEqualTo(0));
            Assert.That(patch, Is.GreaterThanOrEqualTo(0));
        }
    }
}
