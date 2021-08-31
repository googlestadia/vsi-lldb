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

using GgpGrpc.Models;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Web;
using TestsCommon.TestSupport;

namespace YetiCommon.Tests
{
    [TestFixture]
    class ChromeBasedClientLauncherTests
    {
        const string _launch_id = "launch/id";

        ChromeClientsLauncher _chromeClientsLauncher;
        LaunchParams _launchParams;
        BackgroundProcess.Factory _backgroundProcessFactory;
        SdkConfig _sdkConfig;
        LogSpy _logSpy;

        [SetUp]
        public void SetUp()
        {
            _backgroundProcessFactory = Substitute.For<BackgroundProcess.Factory>();
            _launchParams = new LaunchParams();
            _sdkConfig = new SdkConfig();
            var sdkConfigFactory = Substitute.For<SdkConfig.Factory>();
            sdkConfigFactory.LoadOrDefault().Returns(c => _sdkConfig);
            var chromeLauncher = new ChromeLauncher(_backgroundProcessFactory);
            _chromeClientsLauncher =
                new ChromeClientsLauncher(sdkConfigFactory, _launchParams, chromeLauncher);
            _logSpy = new LogSpy();
            _logSpy.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            _logSpy.Detach();
        }

        [Test]
        public void TestClientUrlIsValid()
        {
            _sdkConfig.OrganizationId = "orgId";
            _sdkConfig.PartnerPortalUrl = "https://partner-portal.com";
            _launchParams.Account = "theAccount";
            _launchParams.SdkVersion = "1.2.3";

            string testClientUrl = _chromeClientsLauncher.MakeTestClientUrl("launch_name");
            Assert.That(testClientUrl,
                        Is.EqualTo("https://partner-portal.com/organizations/orgId/stream?" +
                                   "sdk_version=1.2.3&game_launch_name=launch_name" +
                                   "#Email=theAccount"));
        }

        [Test]
        public void PlayerClientUrlIsValid()
        {
            _sdkConfig.PlayerPortalUrl = "https://player-portal.com";
            _launchParams.ApplicationId = "appId";

            string playerPortalUrl = _chromeClientsLauncher.MakePlayerClientUrl("launch_name");
            Assert.That(playerPortalUrl,
                        Is.EqualTo("https://player-portal.com/player/appId?bypass_pts=true" +
                                   "&launch_id=launch_name"));
        }
    }
}
