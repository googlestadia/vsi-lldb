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
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YetiCommon.Tests
{
    [TestFixture]
    class SdkConfigTests
    {
        MockFileSystem filesystem;
        JsonUtil jsonUtil;
        SdkConfig.Factory configFactory;

        [SetUp]
        public void SetUp()
        {
            filesystem = new MockFileSystem();
            filesystem.AddDirectory(SDKUtil.GetUserConfigPath());

            jsonUtil = new JsonUtil(filesystem);
            configFactory = new SdkConfig.Factory(jsonUtil);
        }

        [Test]
        public void Load()
        {
            filesystem.AddFile(
                Path.Combine(SDKUtil.GetUserConfigPath(), SdkConfig.SdkConfigFilename),
                new MockFileData(
                    string.Join("", new string[] {
                        @"{",
                        @"  ""url"":""https://test.com/foo"",", // URL fields
                        @"  ""portalUrl"":""https://test.com/bar"",",
                        @"  ""organizationId"":""MyOrg"",", // string field
                        @"  ""poolId"":"""",",               // unused field
                        @"  ""disableMetrics"":true",       // boolean field
                        @"}",
                    })));

            var config = configFactory.LoadOrDefault();
            Assert.That(config.Url, Is.EqualTo("https://test.com/foo"));
            Assert.That(config.PortalUrl, Is.EqualTo("https://test.com/bar"));
            Assert.That(config.OrganizationId, Is.EqualTo("MyOrg"));
            Assert.That(config.DisableMetrics, Is.EqualTo(true));

            // This field is not present - default value should be used.
            Assert.That(config.ChromeProfileDir, Is.Null);

            // Check URL defaults.
            Assert.That(config.UrlOrDefault, Is.EqualTo("https://test.com/foo"));
            Assert.That(config.PortalUrlOrDefault, Is.EqualTo("https://test.com/bar"));
        }

        [Test]
        public void Default()
        {
            var config = configFactory.LoadOrDefault();

            Assert.That(config.Url, Is.Null);
            Assert.That(config.PortalUrl, Is.Null);

            Assert.That(config.UrlOrDefault, Is.Not.Empty);
            Assert.That(config.PortalUrlOrDefault, Is.Not.Empty);
        }
    }
}
