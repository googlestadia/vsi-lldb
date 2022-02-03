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

using NSubstitute;
using NUnit.Framework;
using TestsCommon.TestSupport;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiCommon.Tests
{
    [TestFixture]
    class ChromeClientsLauncherTests
    {
        ChromeClientsLauncher _chromeClientsLauncher;
        IChromeLauncher _chromeLauncher;
        LaunchParams _launchParams;
        SdkConfig _sdkConfig;
        LogSpy _logSpy;

        [SetUp]
        public void SetUp()
        {
            _launchParams = new LaunchParams();
            _sdkConfig = new SdkConfig();
            var sdkConfigFactory = Substitute.For<SdkConfig.Factory>();
            sdkConfigFactory.LoadOrDefault().Returns(c => _sdkConfig);
            _chromeLauncher = Substitute.For<IChromeLauncher>();
            _chromeClientsLauncher =
                new ChromeClientsLauncher(sdkConfigFactory, _launchParams, _chromeLauncher);
            _logSpy = new LogSpy();
            _logSpy.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            _logSpy.Detach();
        }

        [Test]
        public void TestStartChromeForTestClient()
        {
            _sdkConfig.OrganizationId = "orgId";
            _sdkConfig.PartnerPortalUrl = "https://partner-portal.com";
            _sdkConfig.ChromeProfileDir = "profileDir";
            _launchParams.Account = "theAccount";
            _launchParams.SdkVersion = "1.2.3";
            _launchParams.Endpoint = StadiaEndpoint.TestClient;
            var launchName = "test_launch_name";
            var launchId = "test_launch_id";
            var workingDirectory = "working_dir";

            var testClientUrl = "https://partner-portal.com/organizations/orgId/stream?" +
                "sdk_version=1.2.3&game_launch_name=test_launch_name#Email=theAccount";

            _chromeClientsLauncher.MaybeLaunchChrome(launchName, launchId, workingDirectory);
            _chromeLauncher.Received()
                .StartChrome(testClientUrl, workingDirectory, _sdkConfig.ChromeProfileDir);
        }

        [Test]
        public void TestStartChromeForPlayerEndpoint()
        {
            _sdkConfig.PlayerPortalUrl = "https://player-portal.com";
            _launchParams.ApplicationId = "appId";
            _launchParams.Endpoint = StadiaEndpoint.PlayerEndpoint;
            var launchName = "test_launch_name";
            var launchId = "test_launch_id";
            var workingDirectory = "working_dir";

            string playerPortalUrl = "https://player-portal.com/player/appId?bypass_pts=true" +
                "&launch_id=test_launch_id";

            _chromeClientsLauncher.MaybeLaunchChrome(launchName, launchId, workingDirectory);
            _chromeLauncher.Received()
                .StartChrome(playerPortalUrl, workingDirectory, _sdkConfig.ChromeProfileDir);
        }

        [Test]
        public void TestStartChromeForAnyEndpoint()
        {
            _launchParams.Endpoint = StadiaEndpoint.AnyEndpoint;
            var launchName = "test_launch_name";
            var launchId = "test_launch_id";
            var workingDirectory = "working_dir";

            _chromeClientsLauncher.MaybeLaunchChrome(launchName, launchId, workingDirectory);
            _chromeLauncher.DidNotReceive()
                .StartChrome(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        }
    }
}