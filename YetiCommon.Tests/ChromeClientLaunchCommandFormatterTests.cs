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
using NUnit.Framework;

namespace YetiCommon.Tests
{
    [TestFixture]
    class ChromeClientLaunchCommandFormatterTests
    {
        const string LauncherDir = "the/launcherDir";

        [Test]
        public void TestCreateAndParse()
        {
            var launchCommand = new ChromeClientLaunchCommandFormatter(new JsonUtil(), LauncherDir);

            var launchParams = new LaunchParams()
            {
                ApplicationName = "theAppName",
                ApplicationId = "theAppId",
                GameletName= "gameletName",
                Account = "test@example.com",
                Cmd = "TestProject",
                TestAccount = "testAccount",
                GameletEnvironmentVars = "var1=5",
                SdkVersion = "sdkVersion",
                SurfaceEnforcementMode = SurfaceEnforcementSetting.Block,
                Rgp = true,
                RenderDoc = true,
                Debug = true,
            };

            var command = launchCommand.CreateFromParams(launchParams);
            launchCommand.Parse(command, out LaunchParams parsedLaunchParams,
                                out _);
            Assert.Multiple(() =>
            {
                Assert.That(parsedLaunchParams.ApplicationName,
                            Is.EqualTo(launchParams.ApplicationName));
                Assert.That(parsedLaunchParams.ApplicationId,
                            Is.EqualTo(launchParams.ApplicationId));
                Assert.That(parsedLaunchParams.GameletName, Is.EqualTo(launchParams.GameletName));
                Assert.That(parsedLaunchParams.Account, Is.EqualTo(launchParams.Account));
                Assert.That(parsedLaunchParams.Cmd, Is.EqualTo(launchParams.Cmd));
                Assert.That(parsedLaunchParams.TestAccount, Is.EqualTo(launchParams.TestAccount));
                Assert.That(parsedLaunchParams.GameletEnvironmentVars,
                            Is.EqualTo(launchParams.GameletEnvironmentVars));
                Assert.That(parsedLaunchParams.SdkVersion, Is.EqualTo(launchParams.SdkVersion));
                Assert.That(parsedLaunchParams.SurfaceEnforcementMode,
                            Is.EqualTo(launchParams.SurfaceEnforcementMode));
                Assert.That(parsedLaunchParams.Rgp, Is.EqualTo(launchParams.Rgp));
                Assert.That(parsedLaunchParams.RenderDoc, Is.EqualTo(launchParams.RenderDoc));
                Assert.That(parsedLaunchParams.Debug, Is.EqualTo(launchParams.Debug));
            });
        }

        [Test]
        public void TestCreateAndParseWithLaunchName()
        {
            var launchCommand = new ChromeClientLaunchCommandFormatter(new JsonUtil(), LauncherDir);
            string launchName = "launchName";
            var launchParams = new LaunchParams()
            {
                Account = "test@example.com",
                SdkVersion = "sdkVersion",
            };

            var command = launchCommand.CreateWithLaunchName(launchParams, launchName);
            launchCommand.Parse(command, out LaunchParams parsedLaunchParams,
                                out string parsedLaunchName);
            Assert.Multiple(() =>
            {
                Assert.That(parsedLaunchParams.Account, Is.EqualTo(launchParams.Account));
                Assert.That(parsedLaunchParams.SdkVersion, Is.EqualTo(launchParams.SdkVersion));
                Assert.That(parsedLaunchName, Is.EqualTo(launchName));
            });
        }
    }
}
