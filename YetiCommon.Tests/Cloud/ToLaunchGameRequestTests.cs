// Copyright 2021 Google LLC
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
using System.Linq;
using GgpGrpc;
using GgpGrpc.Models;
using NSubstitute;
using NUnit.Framework;
using YetiCommon.Cloud;
using YetiVSI.ProjectSystem.Abstractions;
using static YetiCommon.Tests.Cloud.LaunchRequestParsingTestData;

namespace YetiCommon.Tests.Cloud
{
    /// <summary>
    /// Tests for both <c>LaunchGameParamsConverter</c> and <c>QueryParametersParser</c>.
    /// </summary>
    [TestFixture]
    class ToLaunchGameRequestTests
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
        QueryParametersParser _queryParametersParser;
        LaunchGameParamsConverter _parametersConverter;

        [SetUp]
        public void Setup()
        {
            _sdkConfigFactory = Substitute.For<ISdkConfigFactory>();
            _sdkConfigFactory.LoadGgpSdkConfigOrDefault().Returns(_sdkConfig);
            _queryParametersParser = new QueryParametersParser();
            _parametersConverter =
                new LaunchGameParamsConverter(_sdkConfigFactory, _queryParametersParser);
        }

        [Test]
        public void ToLaunchGameRequestNoQueryParams()
        {
            LaunchParams parameters = ValidParams;
            parameters.RenderDoc = false;
            parameters.Rgp = false;
            parameters.VulkanDriverVariant = string.Empty;

            ConfigStatus status =
                _parametersConverter.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.IsNotNull(request);
            Assert.That(request.Parent, Is.EqualTo(parameters.TestAccount));
            Assert.That(request.GameletName, Is.EqualTo(parameters.GameletName));
            Assert.That(request.ApplicationName, Is.EqualTo(parameters.ApplicationName));
            Assert.That(request.ExecutablePath, Is.EqualTo("some_bin"));
            Assert.That(request.CommandLineArguments, Is.EqualTo(new[] { "arg1" }));
            Assert.That(request.EnvironmentVariablePairs, Is.EqualTo(new Dictionary<string, string>
            {
                { "Var1", "1" },
                { "vAR2", "3" }
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
        public void ToLaunchGameRequestEnvironmentVariablesOverlap()
        {
            LaunchParams parameters = ValidParams;
            parameters.RenderDoc = true;
            parameters.Rgp = true;
            parameters.GameletEnvironmentVars =
                "RENDERDOC_TEMP=chrome/params/temp;RENDERDOC_DEBUG_LOG_FILE=chrome/params.log;" +
                "GGP_DEV_VK_DRIVER_VARIANT=opt;GGP_VK_AMDVLK_USE_LLPC=0;Some_Var=12;other=9" +
                ";ENABLE_VK_LAYER_VULKAN_COLOR_TOOLS=0";
            parameters.QueryParams = "color_tools=1&enable_llpc_in_amdvlk=True&" +
                "enable_pipeline_cache_source_layer=true&vulkan_driver_variant=optprintasserts" +
                "&vars=GGP_DEV_VK_DRIVER_VARIANT=dbgtrapasserts;Other=567;OTHER_var=67;" +
                "RENDERDOC_TEMP=query/params/temp1;ENABLE_VK_LAYER_VULKAN_COLOR_TOOLS=false";

            ConfigStatus status =
                _parametersConverter.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsWarningLevel, Is.EqualTo(true));
            Assert.That(status.AllMessages.Count, Is.EqualTo(7));
            Assert.That(status.WarningMessages.Count(m => m.Contains("is set multiple times.")),
                        Is.EqualTo(3));
            Assert.That(
                status.WarningMessages.Count(m => m.Contains("overrides the setting variable")),
                Is.EqualTo(3));
            Assert.That(status.WarningMessages.Count(m => m.Contains("edit the setting")),
                        Is.EqualTo(1));
            Assert.That(request.EnvironmentVariablePairs,
                        Is.EqualTo(new Dictionary<string, string> {
                            { "GGP_DEV_VK_DRIVER_VARIANT", "dbgtrapasserts" },
                            { "ENABLE_VULKAN_RENDERDOC_CAPTURE", "1" },
                            { "RENDERDOC_TEMP", "query/params/temp1" },
                            { "RENDERDOC_DEBUG_LOG_FILE", "chrome/params.log" },
                            { "GGP_INTERNAL_LOAD_RGP", "1" },
                            { "RGP_DEBUG_LOG_FILE", "/var/game/RGPDebug.log" },
                            { "LD_PRELOAD", "librgpserver.so" },
                            { "ENABLE_VK_LAYER_VULKAN_COLOR_TOOLS", "false" },
                            { "GGP_VK_AMDVLK_USE_LLPC", "0" },
                            { "ENABLE_GOOGLE_PIPELINE_DATA_EXPORT_LAYER", "1" },
                            { "Some_Var", "12" },
                            { "other", "9" },
                            { "Other", "567" },
                            { "OTHER_var", "67" }
                        }));
        }

        [Test]
        public void ToLaunchGameRequestParametersOverlap()
        {
            LaunchParams parameters = ValidParams;
            parameters.SurfaceEnforcementMode = SurfaceEnforcementSetting.Block;
            parameters.Debug = false;
            parameters.QueryParams = string.Join(
                "&", AllValidQueryParams.Select(
                         p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

            ConfigStatus status =
                _parametersConverter.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsWarningLevel, Is.EqualTo(true));
            Assert.That(status.AllMessages.Count, Is.EqualTo(1));
            Assert.That(
                status.WarningMessage.Contains("The following query parameters will be ignored"));
            Assert.IsNotNull(request);
            Assert.That(request.Parent,
                        Is.EqualTo("organizations/organization_id/projects/project_id/" +
                                   "testAccounts/gamer#1234"));
            Assert.That(request.GameletName, Is.EqualTo(parameters.GameletName));
            Assert.That(request.ApplicationName, Is.EqualTo("params_app_name"));
            Assert.That(request.ExecutablePath, Is.EqualTo("some_bin"));
            Assert.That(request.CommandLineArguments, Is.EqualTo(new[] { "arg2" }));
            Assert.That(request.EnvironmentVariablePairs,
                        Is.EqualTo(new Dictionary<string, string> {
                            { "ParamsVar", "val" },
                            { "Var1", "1" },
                            { "vAR2", "3" },
                            { "GGP_DEV_VK_DRIVER_VARIANT", "test_variant" },
                            { "GGP_INTERNAL_LOAD_RGP", "1" },
                            { "RGP_DEBUG_LOG_FILE", "/var/game/RGPDebug.log" },
                            { "LD_PRELOAD", "librgpserver.so" }
                        }));
            Assert.That(request.SurfaceEnforcementMode, Is.EqualTo(SurfaceEnforcementSetting.Warn));
            Assert.That(request.Debug, Is.EqualTo(true));
            Assert.That(request.EnablePipelineCacheSourceUpload, Is.EqualTo(false));
            Assert.That(request.EnableRetroactiveFrameDump, Is.EqualTo(true));
            Assert.That(request.StreamQualityPreset,
                        Is.EqualTo(StreamQualityPreset.HighVisualQuality));
            Assert.That(request.AddInstanceCompatibilityRequirements,
                        Is.EqualTo(new[] { "r1", "other" }));
            Assert.That(request.RemoveInstanceCompatibilityRequirements,
                        Is.EqualTo(new[] { "5", "7", " 8" }));
            Assert.That(request.DeepLinkParamsIdGameData, Is.EqualTo(23475456543));
            Assert.That(request.EnableGameRealtimePriority, Is.EqualTo(true));
            Assert.That(request.EnforceProductionRam, Is.EqualTo(false));
            Assert.That(request.GameStateName, Is.EqualTo("params_game_state_name"));
            Assert.That(request.ReleaseName, Is.EqualTo(null));
            Assert.That(request.MountDynamicContent, Is.EqualTo(true));
            Assert.That(request.MountUploadedPipelineCache, Is.EqualTo(true));
            Assert.That(request.OverrideAudioChannelMode, Is.EqualTo(ChannelMode.Surround51True));
            Assert.That(request.OverrideClientResolution, Is.EqualTo(VideoResolution._720P));
            Assert.That(request.OverrideDisplayPixelDensity, Is.EqualTo(9876));
            Assert.That(request.OverrideDynamicRange, Is.EqualTo(DynamicRange.Sdr));
            Assert.That(request.OverridePreferredCodec, Is.EqualTo(Codec.Vp9));
            Assert.That(request.PackageName, Is.EqualTo("params_package_name"));
            Assert.That(request.StartForwardFrameDump, Is.EqualTo(false));
            Assert.That(request.StreamerFixedFps, Is.EqualTo(765));
            Assert.That(request.StreamerFixedResolution, Is.EqualTo(VideoResolution._1440P));
            Assert.That(request.StreamerMaximumBandWidthKbps, Is.EqualTo(8764));
            Assert.That(request.StreamerMinimumBandWidthKbps, Is.EqualTo(23));
        }

        [TestCase(StadiaEndpoint.PlayerEndpoint, TestName = "PlayerEndpoint")]
        [TestCase(StadiaEndpoint.AnyEndpoint, TestName = "AnyEndpoint")]
        public void ToLaunchGameRequestNotSupportedQueryParams(StadiaEndpoint endpoint)
        {
            LaunchParams parameters = ValidParams;
            parameters.Endpoint = endpoint;
            parameters.QueryParams = "a=42&vars=valid=1";

            ConfigStatus status =
                _parametersConverter.ToLaunchGameRequest(parameters, out LaunchGameRequest _);

            Assert.That(status.IsWarningLevel, Is.EqualTo(true));
            Assert.That(status.WarningMessage,
                        Does.Contain("are not supported by any player endpoint"));
            Assert.That(status.WarningMessage, Does.Contain("a=42"));
            Assert.That(status.WarningMessage, Does.Not.Contain("vars=valid=1"));
        }

        [TestCase(StadiaEndpoint.PlayerEndpoint, TestName = "PlayerEndpoint")]
        [TestCase(StadiaEndpoint.AnyEndpoint, TestName = "AnyEndpoint")]
        public void ToLaunchGameRequestNotSupportedTestAccounts(StadiaEndpoint endpoint)
        {
            LaunchParams parameters = ValidParams;
            parameters.Endpoint = endpoint;
            parameters.TestAccount = "a/b/c";
            parameters.TestAccountGamerName = "gamer#1234";

            ConfigStatus status =
                _parametersConverter.ToLaunchGameRequest(parameters, out LaunchGameRequest _);

            Assert.That(status.IsWarningLevel, Is.EqualTo(true));
            Assert.That(status.WarningMessage,
                        Does.Contain("Test accounts are not supported by any player endpoint"));
            Assert.That(status.WarningMessage, Does.Contain("gamer#1234"));
        }
    }
}
