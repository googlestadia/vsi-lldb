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

        [Test, Sequential]
        public void LaunchesChromeClient(
            [Values("http://theportalurl", "")] string portalUrl,
            [Values("http://theportalurl", "https://console.ggp.google.com")]
                string expectedPortalUrl)
        {
            _sdkConfig.ChromeProfileDir = "MyProfile";
            _sdkConfig.Url = "http://theurl.com";
            _sdkConfig.PartnerPortalUrl = portalUrl;
            _sdkConfig.OrganizationId = "orgId";

            _launchParams.ApplicationName = "theAppName";
            _launchParams.ApplicationId = "theAppId";
            _launchParams.GameletName = "gameletName";
            _launchParams.Account = "test@example.com";
            _launchParams.Cmd = "TestProject launcharg=1 launcharg2=2";
            _launchParams.TestAccount = "testAccount";
            _launchParams.GameletEnvironmentVars = "var1=5";
            _launchParams.SdkVersion = "sdkVersion";
            _launchParams.Rgp = true;
            _launchParams.Dive = true;
            _launchParams.RenderDoc = true;
            _launchParams.Debug = true;
            _launchParams.SurfaceEnforcementMode = SurfaceEnforcementSetting.Warn;
            _launchParams.QueryParams = "customName=customValue";

            var workingDirectory = "the/working/directory";
            var commandRun = "";
            _backgroundProcessFactory.Create(YetiConstants.Command,
                                            Arg.Do<string>(x => commandRun = x), workingDirectory);

            var urlBuildStatus = _chromeClientsLauncher.MakeLegacyLaunchUrl(out string launchUrl);
            Assert.That(urlBuildStatus.IsOk, Is.EqualTo(true));
            _chromeClientsLauncher.LaunchGame(launchUrl, workingDirectory);

            var parser = new ChromeCommandParser(commandRun);
            Assert.Multiple(() => {
                Assert.That(
                    parser.Url,
                    Is.EqualTo(
                        $"{expectedPortalUrl}/organizations/{_sdkConfig.OrganizationId}/stream"));

                var query = parser.QueryParams;
                Assert.That(query["application_name"], Is.EqualTo(_launchParams.ApplicationName));
                Assert.That(query["gamelet_name"], Is.EqualTo(_launchParams.GameletName));
                Assert.That(query["cmd"], Is.EqualTo(_launchParams.Cmd));
                Assert.That(query["vars"], Is.EqualTo(_launchParams.GameletEnvironmentVars));
                Assert.That(query["debug_mode"], Is.EqualTo("2"));
                Assert.That(query["rgp"], Is.EqualTo("true"));
                Assert.That(query["dive"], Is.EqualTo("true"));
                Assert.That(query["renderdoc"], Is.EqualTo("true"));
                Assert.That(query["surface_enforcement_mode"], Is.EqualTo("warn"));
                Assert.That(query["sdk_version"], Is.EqualTo(_launchParams.SdkVersion));
                Assert.That(query["test_account"], Is.EqualTo(_launchParams.TestAccount));
                Assert.That(query["customName"], Is.EqualTo("customValue"));
                Assert.That(parser.Fragment, Is.EqualTo("Email=" + _launchParams.Account));
                Assert.That(parser.ProfileDir, Is.EqualTo(_sdkConfig.ChromeProfileDir));
            });
        }

        [Test]
        public void LaunchesChromeBrowserSkipsEmptyParams()
        {
            _launchParams.ApplicationName = "theAppName";
            _launchParams.GameletName = "gameletName";
            _launchParams.Debug = true;

            var workingDirectory = "the/working/directory";
            var commandRun = "";
            _backgroundProcessFactory.Create(YetiConstants.Command,
                Arg.Do<string>(x => commandRun = x), workingDirectory);

            var urlBuildStatus = _chromeClientsLauncher.MakeLegacyLaunchUrl(out string launchUrl);
            Assert.That(urlBuildStatus.IsOk, Is.EqualTo(true));
            _chromeClientsLauncher.LaunchGame(launchUrl, workingDirectory);

            var parser = new ChromeCommandParser(commandRun);
            Assert.That(parser.Url, Is.EqualTo(
                $"https://console.ggp.google.com/organizations/{_sdkConfig.OrganizationId}/stream"));

            var query = parser.QueryParams;
            Assert.That(query.Count, Is.EqualTo(7));
            Assert.That(query["application_name"], Is.EqualTo(_launchParams.ApplicationName));
            Assert.That(query["gamelet_name"], Is.EqualTo(_launchParams.GameletName));
            Assert.That(query["debug_mode"], Is.EqualTo("2"));
            Assert.That(query["rgp"], Is.EqualTo("false"));
            Assert.That(query["dive"], Is.EqualTo("false"));
            Assert.That(query["renderdoc"], Is.EqualTo("false"));
            Assert.That(query["surface_enforcement_mode"], Is.EqualTo("off"));
        }

        [Test]
        public void LaunchChromeBrowserCustomQueryParamOverridesLaunchParam(
            [Values("?", "")] string prefix)
        {
            _launchParams.ApplicationName = "theAppName";
            _launchParams.GameletName = "gameletName";
            _launchParams.Account = "test@example.com";
            _launchParams.Debug = true;
            _launchParams.QueryParams
                = $"{prefix}application_name=customAppName&gamelet_name=customGameletName";

            var workingDirectory = "the/working/directory";
            var commandRun = "";
            _backgroundProcessFactory.Create(YetiConstants.Command,
                Arg.Do<string>(x => commandRun = x), workingDirectory);

            var urlBuildStatus = _chromeClientsLauncher.MakeLegacyLaunchUrl(out string launchUrl);
            Assert.That(urlBuildStatus.IsOk, Is.EqualTo(true));
            _chromeClientsLauncher.LaunchGame(launchUrl, workingDirectory);

            var parser = new ChromeCommandParser(commandRun);
            Assert.That(parser.Url, Is.EqualTo(
                $"https://console.ggp.google.com/organizations/{_sdkConfig.OrganizationId}/stream"));

            var query = parser.QueryParams;
            Assert.That(query["application_name"], Is.EqualTo("customAppName"));
            Assert.That(query["gamelet_name"], Is.EqualTo("customGameletName"));
            Assert.That(query["test_account"], Is.EqualTo(_launchParams.TestAccount));

            Assert.That(_logSpy.GetOutput(), Does.Contain("application_name"));
            Assert.That(_logSpy.GetOutput(), Does.Contain("theAppName"));
            Assert.That(_logSpy.GetOutput(), Does.Contain("customAppName"));

            Assert.That(_logSpy.GetOutput(), Does.Contain("gamelet_name"));
            Assert.That(_logSpy.GetOutput(), Does.Contain("gameletName"));
            Assert.That(_logSpy.GetOutput(), Does.Contain("customGameletName"));
        }

        [TestCase("Plain text", TestName = "ParsePlainTextQueryParam")]
        [TestCase("=", TestName = "ParseEqualSignQueryParam")]
        [TestCase("?=val", TestName = "ParseEqualValueQueryParam")]
        [TestCase("?param=val&=val", TestName = "ParseTwoValuesQueryParam")]
        public void ParseInvalidQueryParams(string queryParam)
        {
            _launchParams.QueryParams = queryParam;
            var urlBuildStatus = _chromeClientsLauncher.MakeLegacyLaunchUrl(out string launchUrl);
            Assert.That(urlBuildStatus.IsOk, Is.EqualTo(false));
        }

        class ChromeCommandParser
        {
            readonly Match _match;

            public ChromeCommandParser(string chromeCommand)
            {
                var regex = new Regex(@"\/c ""start chrome """ +
                    @"(?<url>[^\s]+)\?(?<query>[^\s\#]+)(\#(?<fragment>[^\s]+))?""" +
                    @" --new-window --profile-directory=""(?<profileDir>[^\s]+)""""");
                this._match = regex.Match(chromeCommand);
            }

            public string Url => _match.Groups["url"].Value;

            public NameValueCollection QueryParams
                => HttpUtility.ParseQueryString(_match.Groups["query"].Value);

            public string Fragment => _match.Groups["fragment"].Value;

            public string ProfileDir => _match.Groups["profileDir"].Value;
        }
    }
}
