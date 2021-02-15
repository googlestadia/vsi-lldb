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
    class ChromeTestClientLauncherTests
    {
        ChromeTestClientLauncher _chromeTestClient;
        ChromeTestClientLauncher.Params launchParams;
        BackgroundProcess.Factory backgroundProcessFactory;
        SdkConfig sdkConfig;
        LogSpy logSpy;

        [SetUp]
        public void SetUp()
        {
            backgroundProcessFactory = Substitute.For<BackgroundProcess.Factory>();
            launchParams = new ChromeTestClientLauncher.Params();
            sdkConfig = new SdkConfig();
            var sdkConfigFactory = Substitute.For<SdkConfig.Factory>();
            sdkConfigFactory.LoadOrDefault().Returns(c => sdkConfig);
            var chromeLauncher = new ChromeLauncher(backgroundProcessFactory);
            _chromeTestClient =
                new ChromeTestClientLauncher(sdkConfigFactory, launchParams, chromeLauncher);
            logSpy = new LogSpy();
            logSpy.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            logSpy.Detach();
        }

        [Test, Sequential]
        public void LaunchesChromeClient(
            [Values("http://theportalurl", "")] string portalUrl,
            [Values("http://theportalurl", "https://console.ggp.google.com")]
                string expectedPortalUrl)
        {
            sdkConfig.ChromeProfileDir = "MyProfile";
            sdkConfig.Url = "http://theurl.com";
            sdkConfig.PortalUrl = portalUrl;
            sdkConfig.OrganizationId = "orgId";
            
            launchParams.ApplicationName = "theAppName";
            launchParams.GameletName = "gameletName";
            launchParams.Account = "test@example.com";
            launchParams.Cmd = "TestProject launcharg=1 launcharg2=2";
            launchParams.TestAccount = "testAccount";
            launchParams.GameletEnvironmentVars = "var1=5";
            launchParams.SdkVersion = "sdkVersion";
            launchParams.Rgp = true;
            launchParams.RenderDoc = true;
            launchParams.Debug = true;
            launchParams.SurfaceEnforcementMode = SurfaceEnforcementSetting.Warn;
            launchParams.QueryParams = "customName=customValue";

            var workingDirectory = "the/working/directory";
            var commandRun = "";
            backgroundProcessFactory.Create(YetiConstants.Command,
                                            Arg.Do<string>(x => commandRun = x), workingDirectory);

            var urlBuildStatus = _chromeTestClient.BuildLaunchUrl(out string launchUrl);
            Assert.That(urlBuildStatus.IsOk, Is.EqualTo(true));
            _chromeTestClient.LaunchGame(launchUrl, workingDirectory);

            var parser = new ChromeCommandParser(commandRun);
            Assert.Multiple(() => {
                Assert.That(
                    parser.Url,
                    Is.EqualTo(
                        $"{expectedPortalUrl}/organizations/{sdkConfig.OrganizationId}/stream"));

                var query = parser.QueryParams;
                Assert.That(query["application_name"], Is.EqualTo(launchParams.ApplicationName));
                Assert.That(query["gamelet_name"], Is.EqualTo(launchParams.GameletName));
                Assert.That(query["cmd"], Is.EqualTo(launchParams.Cmd));
                Assert.That(query["vars"], Is.EqualTo(launchParams.GameletEnvironmentVars));
                Assert.That(query["debug_mode"], Is.EqualTo("2"));
                Assert.That(query["rgp"], Is.EqualTo("true"));
                Assert.That(query["renderdoc"], Is.EqualTo("true"));
                Assert.That(query["surface_enforcement_mode"], Is.EqualTo("warn"));
                Assert.That(query["sdk_version"], Is.EqualTo(launchParams.SdkVersion));
                Assert.That(query["test_account"], Is.EqualTo(launchParams.TestAccount));
                Assert.That(query["customName"], Is.EqualTo("customValue"));
                Assert.That(parser.Fragment, Is.EqualTo("Email=" + launchParams.Account));
                Assert.That(parser.ProfileDir, Is.EqualTo(sdkConfig.ChromeProfileDir));
            });
        }

        [Test]
        public void LaunchesChromeBrowserSkipsEmptyParams()
        {
            launchParams.ApplicationName = "theAppName";
            launchParams.GameletName = "gameletName";
            launchParams.Debug = true;

            var workingDirectory = "the/working/directory";
            var commandRun = "";
            backgroundProcessFactory.Create(YetiConstants.Command,
                Arg.Do<string>(x => commandRun = x), workingDirectory);

            var urlBuildStatus = _chromeTestClient.BuildLaunchUrl(out string launchUrl);
            Assert.That(urlBuildStatus.IsOk, Is.EqualTo(true));
            _chromeTestClient.LaunchGame(launchUrl, workingDirectory);

            var parser = new ChromeCommandParser(commandRun);
            Assert.That(parser.Url, Is.EqualTo(
                $"https://console.ggp.google.com/organizations/{sdkConfig.OrganizationId}/stream"));

            var query = parser.QueryParams;
            Assert.That(query.Count, Is.EqualTo(6));
            Assert.That(query["application_name"], Is.EqualTo(launchParams.ApplicationName));
            Assert.That(query["gamelet_name"], Is.EqualTo(launchParams.GameletName));
            Assert.That(query["debug_mode"], Is.EqualTo("2"));
            Assert.That(query["rgp"], Is.EqualTo("false"));
            Assert.That(query["renderdoc"], Is.EqualTo("false"));
            Assert.That(query["surface_enforcement_mode"], Is.EqualTo("off"));
        }

        [Test]
        public void LaunchChromeBrowserCustomQueryParamOverridesLaunchParam(
            [Values("?", "")] string prefix)
        {
            launchParams.ApplicationName = "theAppName";
            launchParams.GameletName = "gameletName";
            launchParams.Account = "test@example.com";
            launchParams.Debug = true;
            launchParams.QueryParams
                = $"{prefix}application_name=customAppName&gamelet_name=customGameletName";

            var workingDirectory = "the/working/directory";
            var commandRun = "";
            backgroundProcessFactory.Create(YetiConstants.Command,
                Arg.Do<string>(x => commandRun = x), workingDirectory);

            var urlBuildStatus = _chromeTestClient.BuildLaunchUrl(out string launchUrl);
            Assert.That(urlBuildStatus.IsOk, Is.EqualTo(true));
            _chromeTestClient.LaunchGame(launchUrl, workingDirectory);

            var parser = new ChromeCommandParser(commandRun);
            Assert.That(parser.Url, Is.EqualTo(
                $"https://console.ggp.google.com/organizations/{sdkConfig.OrganizationId}/stream"));

            var query = parser.QueryParams;
            Assert.That(query["application_name"], Is.EqualTo("customAppName"));
            Assert.That(query["gamelet_name"], Is.EqualTo("customGameletName"));
            Assert.That(query["test_account"], Is.EqualTo(launchParams.TestAccount));

            Assert.That(logSpy.GetOutput(), Does.Contain("application_name"));
            Assert.That(logSpy.GetOutput(), Does.Contain("theAppName"));
            Assert.That(logSpy.GetOutput(), Does.Contain("customAppName"));

            Assert.That(logSpy.GetOutput(), Does.Contain("gamelet_name"));
            Assert.That(logSpy.GetOutput(), Does.Contain("gameletName"));
            Assert.That(logSpy.GetOutput(), Does.Contain("customGameletName"));
        }

        [TestCase("Plain text", TestName = "ParsePlainTextQueryParam")]
        [TestCase("=", TestName = "ParseEqualSignQueryParam")]
        [TestCase("?=val", TestName = "ParseEqualValueQueryParam")]
        [TestCase("?param=val&=val", TestName = "ParseTwoValuesQueryParam")]
        public void ParseInvalidQueryParams(string queryParam)
        {
            launchParams.QueryParams = queryParam;
            var urlBuildStatus = _chromeTestClient.BuildLaunchUrl(out string launchUrl);
            Assert.That(urlBuildStatus.IsOk, Is.EqualTo(false));
        }

        class ChromeCommandParser
        {
            readonly Match match;

            public ChromeCommandParser(string chromeCommand)
            {
                var regex = new Regex(@"\/c ""start chrome """ +
                    @"(?<url>[^\s]+)\?(?<query>[^\s\#]+)(\#(?<fragment>[^\s]+))?""" +
                    @" --new-window --profile-directory=""(?<profileDir>[^\s]+)""""");
                this.match = regex.Match(chromeCommand);
            }

            public string Url => match.Groups["url"].Value;

            public NameValueCollection QueryParams
                => HttpUtility.ParseQueryString(match.Groups["query"].Value);

            public string Fragment => match.Groups["fragment"].Value;

            public string ProfileDir => match.Groups["profileDir"].Value;
        }
    }
}
