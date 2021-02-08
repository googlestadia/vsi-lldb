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

using GgpGrpc;
using GgpGrpc.Models;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using YetiCommon.Cloud;
using static YetiCommon.Tests.Cloud.LaunchRequestParsingTestData;

namespace YetiCommon.Tests.Cloud
{
    [TestFixture]
    public class LaunchGameParamsConverterTests
    {
        class SdkConfigMock : ISdkConfig
        {
            public string ProjectId => "test_project_id";
            public string OrganizationId => "test_organization_id";
            public string UrlOrDefault => "http://test.url";

            public string OrganizationProject =>
                $"organizations/{OrganizationId}/projects/{ProjectId}/";
        }

        readonly SdkConfigMock _sdkConfig = new SdkConfigMock();
        ISdkConfigFactory _sdkConfigFactory;
        IQueryParametersParser _queryParametersParser;
        LaunchGameParamsConverter _target;

        [SetUp]
        public void Setup()
        {
            _sdkConfigFactory = Substitute.For<ISdkConfigFactory>();
            _sdkConfigFactory.LoadGgpSdkConfigOrDefault().Returns(_sdkConfig);
            _queryParametersParser = Substitute.For<IQueryParametersParser>();
            IDictionary<string, string> dict = new Dictionary<string, string>();
            _queryParametersParser
                .GetFinalQueryString(dict, out string _)
                .Returns(x =>
                {
                    x[1] = string.Empty;
                    return ConfigStatus.OkStatus();
                });
            _queryParametersParser
                .ParametersToDictionary(Arg.Any<string>(), out IDictionary<string, string> _)
                .Returns(x =>
                {
                    x[1] = dict;
                    return ConfigStatus.OkStatus();
                });
            _queryParametersParser.ParseToLaunchRequest(dict, Arg.Any<LaunchGameRequest>())
                .Returns(ConfigStatus.OkStatus());
            _queryParametersParser.ParseToParameters(dict, Arg.Any<ChromeClientLauncher.Params>())
                .Returns(ConfigStatus.OkStatus());
            _target = new LaunchGameParamsConverter(_sdkConfigFactory, _queryParametersParser);
        }

        [Test]
        public void FullGameLaunchNameWithGameLaunchNameWithTestAccount()
        {
            string gameLaunchName = "test_game_launch_name";
            string testAccount = $"{_sdkConfig.OrganizationProject}testAccounts/gamer#1234";
            string result = _target.FullGameLaunchName(gameLaunchName, testAccount);
            Assert.That(result, Is.EqualTo($"{testAccount}/gameLaunches/{gameLaunchName}"));
        }

        [Test]
        public void FullGameLaunchNameWithGameLaunchNameNoTestAccount()
        {
            string gameLaunchName = "test_game_launch_name";
            string result = _target.FullGameLaunchName(gameLaunchName);
            Assert.That(
                result,
                Is.EqualTo($"organizations/-/players/me/gameLaunches/{gameLaunchName}"));
        }

        [Test]
        public void FullGameLaunchNameNoGameLaunchNameWithTestAccount()
        {
            string testAccount = $"{_sdkConfig.OrganizationProject}testAccounts/gamer#1234";
            string result = _target.FullGameLaunchName(" ", testAccount);
            Assert.That(result, Is.EqualTo($"{testAccount}/gameLaunches/current"));
        }

        [Test]
        public void FullGameLaunchNameNoGameLaunchNameNoTestAccount()
        {
            string result = _target.FullGameLaunchName(null);
            Assert.That(
                result,
                Is.EqualTo("organizations/-/players/me/gameLaunches/current"));
        }

        [Test]
        public void ToLaunchGameRequestValidParams()
        {
            ChromeClientLauncher.Params parameters = ValidParams;
            parameters.GameletEnvironmentVars =
                "  Var1=test_V=a==l;  vaR2=47  ;  var3 =  ;  var1  =";
            parameters.RenderDoc = false;
            parameters.Rgp = false;
            parameters.VulkanDriverVariant = string.Empty;

            ConfigStatus status =
                _target.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.IsNotNull(request);
            Assert.That(request.Parent, Is.EqualTo(parameters.TestAccount));
            Assert.That(request.GameletName, Is.EqualTo(parameters.GameletName));
            Assert.That(request.ApplicationName, Is.EqualTo(parameters.ApplicationName));
            Assert.That(request.ExecutablePath, Is.EqualTo("some_bin"));
            Assert.That(request.CommandLineArguments, Is.EqualTo(new [] { "arg1" }));
            Assert.That(request.EnvironmentVariablePairs, Is.EqualTo(new Dictionary<string, string>
            {
                { "Var1", "test_V=a==l" },
                { "vaR2", "47" },
                { "var3 ", string.Empty },
                { "var1  ", string.Empty }
            }));
            Assert.That(request.SurfaceEnforcementMode,
                        Is.EqualTo(parameters.SurfaceEnforcementMode));
            Assert.That(request.Debug, Is.EqualTo(parameters.Debug));
            // Assert another parameters values are default.
            Assert.That(request.EnablePipelineCacheSourceUpload, Is.EqualTo(null));
            Assert.That(request.EnableRetroactiveFrameDump, Is.EqualTo(null));
            Assert.That(request.StreamQualityPreset, Is.EqualTo(StreamQualityPreset.Undefined));
            Assert.That(request.AddInstanceCompatibilityRequirements.Length, Is.EqualTo(0));
            Assert.That(request.RemoveInstanceCompatibilityRequirements.Length, Is.EqualTo(0));
            Assert.That(request.DeepLinkParamsIdGameData, Is.EqualTo(null));
            Assert.That(request.EnableGameRealtimePriority, Is.EqualTo(null));
            Assert.That(request.EnforceProductionRam, Is.EqualTo(null));
            Assert.That(request.GameStateName, Is.EqualTo(null));
            Assert.That(request.ReleaseName, Is.EqualTo(null));
            Assert.That(request.MountDynamicContent, Is.EqualTo(null));
            Assert.That(request.MountUploadedPipelineCache, Is.EqualTo(null));
            Assert.That(request.OverrideAudioChannelMode, Is.EqualTo(ChannelMode.Unspecified));
            Assert.That(request.OverrideClientResolution, Is.EqualTo(VideoResolution.Unspecified));
            Assert.That(request.OverrideDisplayPixelDensity, Is.EqualTo(null));
            Assert.That(request.OverrideDynamicRange, Is.EqualTo(DynamicRange.Unspecified));
            Assert.That(request.OverridePreferredCodec, Is.EqualTo(Codec.Unspecified));
            Assert.That(request.PackageName, Is.EqualTo(null));
            Assert.That(request.StartForwardFrameDump, Is.EqualTo(null));
            Assert.That(request.StreamerFixedFps, Is.EqualTo(null));
            Assert.That(request.StreamerFixedResolution, Is.EqualTo(VideoResolution.Unspecified));
            Assert.That(request.StreamerMaximumBandWidthKbps, Is.EqualTo(null));
            Assert.That(request.StreamerMinimumBandWidthKbps, Is.EqualTo(null));
        }

        [Test]
        public void ToLaunchGameRequestRgpVariables()
        {
            ChromeClientLauncher.Params parameters = ValidParams;
            parameters.RenderDoc = false;
            parameters.VulkanDriverVariant = string.Empty;
            parameters.Rgp = true;
            parameters.GameletEnvironmentVars = "LD_PRELOAD=mylib.so";

            ConfigStatus status =
                _target.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.IsNotNull(request);
            Assert.That(request.EnvironmentVariablePairs, Is.EqualTo(new Dictionary<string, string>
            {
                { "GGP_INTERNAL_LOAD_RGP", "1" },
                { "RGP_DEBUG_LOG_FILE", "/var/game/RGPDebug.log" },
                { "LD_PRELOAD", "mylib.so:librgpserver.so" }
            }));
        }

        [Test]
        public void ToLaunchGameRequestRenderDocVariables()
        {
            ChromeClientLauncher.Params parameters = ValidParams;
            parameters.GameletEnvironmentVars = string.Empty;
            parameters.Rgp = false;
            parameters.VulkanDriverVariant = string.Empty;
            parameters.RenderDoc = true;

            ConfigStatus status =
                _target.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.IsNotNull(request);
            Assert.That(request.EnvironmentVariablePairs, Is.EqualTo(new Dictionary<string, string>
            {
                { "ENABLE_VULKAN_RENDERDOC_CAPTURE", "1" },
                { "RENDERDOC_TEMP", "/mnt/developer/ggp" },
                { "RENDERDOC_DEBUG_LOG_FILE", "/var/game/RDDebug.log" }
            }));
        }

        [Test]
        public void ToLaunchGameRequestVulkanDriverVariantVariables()
        {
            ChromeClientLauncher.Params parameters = ValidParams;
            parameters.GameletEnvironmentVars = string.Empty;
            parameters.RenderDoc = false;
            parameters.Rgp = false;
            parameters.VulkanDriverVariant = "test_variant";

            ConfigStatus status =
                _target.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.IsNotNull(request);
            Assert.That(request.EnvironmentVariablePairs, Is.EqualTo(new Dictionary<string, string>
            {
                { "GGP_DEV_VK_DRIVER_VARIANT", parameters.VulkanDriverVariant }
            }));
        }

        [Test]
        public void ToLaunchGameRequestEnvironmentVariablesOverride()
        {
            ChromeClientLauncher.Params parameters = ValidParams;
            parameters.RenderDoc = false;
            parameters.VulkanDriverVariant = "test_variant";
            parameters.Rgp = true;
            parameters.GameletEnvironmentVars =
                "GGP_DEV_VK_DRIVER_VARIANT=otherVariant;RGP_DEBUG_LOG_FILE=my/path.log";

            ConfigStatus status =
                _target.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsWarningLevel, Is.EqualTo(true));
            Assert.That(status.WarningMessages.Count, Is.EqualTo(3));
            Assert.That(status.AllMessages.Count, Is.EqualTo(3));
            Assert.That(status.WarningMessages[0].Contains("overrides the setting variable"));
            Assert.That(status.WarningMessages[1].Contains("overrides the setting variable"));
            Assert.That(status.WarningMessages[2].Contains("edit the setting"));
            Assert.IsNotNull(request);
            Assert.That(request.EnvironmentVariablePairs, Is.EqualTo(new Dictionary<string, string>
            {
                { "GGP_DEV_VK_DRIVER_VARIANT", "otherVariant" },
                { "GGP_INTERNAL_LOAD_RGP", "1" },
                { "RGP_DEBUG_LOG_FILE", "my/path.log" },
                { "LD_PRELOAD", "librgpserver.so" },
            }));
        }

        [Test]
        public void ToLaunchGameRequestEnvironmentVariablesInvalid()
        {
            ChromeClientLauncher.Params parameters = ValidParams;
            parameters.GameletEnvironmentVars = "  =asd ;=;v=a ;v=;2V==;v= ==bb ; ;  ";
            parameters.Rgp = false;
            parameters.RenderDoc = false;
            parameters.VulkanDriverVariant = string.Empty;

            ConfigStatus status =
                _target.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsWarningLevel, Is.EqualTo(true));
            Assert.That(status.WarningMessages.Count, Is.EqualTo(4));
            Assert.That(status.AllMessages.Count, Is.EqualTo(4));
            Assert.That(
                status.WarningMessages.Count(
                    m => m.Contains("Invalid format of environment variable")), Is.EqualTo(2));
            Assert.That(status.WarningMessages.Count(m => m.Contains("is assigned more than once")),
                        Is.EqualTo(1));
            Assert.That(status.WarningMessages.Count(m => m.Contains("edit the setting")),
                        Is.EqualTo(1));
            Assert.That(request.EnvironmentVariablePairs, Is.EqualTo(new Dictionary<string, string>
            {
                { "v", " ==bb" },
                { "2V", "=" }
            }));
        }

        [Test]
        public void ToLaunchGameRequestEmptyExecutable()
        {
            ChromeClientLauncher.Params parameters = ValidParams;
            parameters.Cmd = "  ";

            ConfigStatus status =
                _target.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.IsNotNull(request);
            Assert.That(request.ExecutablePath, Is.EqualTo(string.Empty));
            Assert.That(request.CommandLineArguments.Length, Is.EqualTo(0));
        }

        [Test]
        public void ToLaunchGameRequestParentNoTestAccount()
        {
            ChromeClientLauncher.Params parameters = ValidParams;
            parameters.TestAccount = "   ";

            ConfigStatus status =
                _target.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.IsNotNull(request);
            Assert.That(request.Parent, Is.EqualTo("organizations/-/players/me"));
        }

        [TestCase("  bin_name  --my_arg some_pretty_val  --other-arg other_VAlue ",
                  new[] { "--my_arg", "some_pretty_val", "--other-arg", "other_VAlue" },
                  TestName = "Uppercase")]
        [TestCase("bin_name", new string[] { }, TestName = "NoArguments")]
        [TestCase("    ", new string[] { }, TestName = "NoExecutable")]
        public void ToLaunchGameRequestCommandArgumentsValid(string cmd, string[] expectedOutput)
        {
            ChromeClientLauncher.Params parameters = ValidParams;
            parameters.Cmd = cmd;

            ConfigStatus status =
                _target.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.IsNotNull(request);
            Assert.That(request.CommandLineArguments, Is.EqualTo(expectedOutput));
        }
    }
}
