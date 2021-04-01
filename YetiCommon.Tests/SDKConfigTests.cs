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

using NUnit.Framework;
using System.IO;
using System.IO.Abstractions.TestingHelpers;

namespace YetiCommon.Tests
{
    [TestFixture]
    class SdkConfigTests
    {
        MockFileSystem _filesystem;
        JsonUtil _jsonUtil;
        SdkConfig.Factory _configFactory;

        [SetUp]
        public void SetUp()
        {
            _filesystem = new MockFileSystem();
            _filesystem.AddDirectory(SDKUtil.GetUserConfigPath());

            _jsonUtil = new JsonUtil(_filesystem);
            _configFactory = new SdkConfig.Factory(_jsonUtil);
        }

        [Test]
        public void Load()
        {
            _filesystem.AddFile(
                Path.Combine(SDKUtil.GetUserConfigPath(), SdkConfig.SdkConfigFilename),
                new MockFileData(
                    string.Join("", new string[] {
                        @"{",
                        @"  ""url"":""https://test.com/foo"",", // URL fields
                        @"  ""portalUrl"":""https://test.com/bar"",",
                        @"  ""playerPortalUrl"":""https://test.com/baz"",",
                        @"  ""organizationId"":""MyOrg"",", // string field
                        @"  ""poolId"":"""",",               // unused field
                        @"  ""disableMetrics"":true",       // boolean field
                        @"}",
                    })));

            var config = _configFactory.LoadOrDefault();
            Assert.That(config.Url, Is.EqualTo("https://test.com/foo"));
            Assert.That(config.PartnerPortalUrl, Is.EqualTo("https://test.com/bar"));
            Assert.That(config.PlayerPortalUrl, Is.EqualTo("https://test.com/baz"));
            Assert.That(config.OrganizationId, Is.EqualTo("MyOrg"));
            Assert.That(config.DisableMetrics, Is.EqualTo(true));

            // This field is not present - default value should be used.
            Assert.That(config.ChromeProfileDir, Is.Null);

            // Check URL defaults.
            Assert.That(config.UrlOrDefault, Is.EqualTo("https://test.com/foo"));
            Assert.That(config.PartnerPortalUrlOrDefault, Is.EqualTo("https://test.com/bar"));
            Assert.That(config.PlayerPortalUrlOrDefault, Is.EqualTo("https://test.com/baz"));
        }

        [Test]
        public void Default()
        {
            var config = _configFactory.LoadOrDefault();

            Assert.That(config.Url, Is.Null);
            Assert.That(config.PartnerPortalUrl, Is.Null);
            Assert.That(config.PlayerPortalUrl, Is.Null);

            Assert.That(config.UrlOrDefault, Is.Not.Empty);
            Assert.That(config.PartnerPortalUrlOrDefault, Is.Not.Empty);
            Assert.That(config.PlayerPortalUrlOrDefault, Is.Not.Empty);
        }
    }
}
