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
using GgpGrpc.Models;
using NUnit.Framework;
using YetiCommon.Cloud;
using static YetiCommon.Tests.Cloud.LaunchRequestParsingTestData;

namespace YetiCommon.Tests.Cloud
{
    /// <summary>
    /// Tests for both <c>LaunchGameParamsConverter</c> and <c>QueryParametersParser</c>.
    /// </summary>
    [TestFixture]
    class ToLaunchGameRequestTests
    {
        QueryParametersParser _queryParametersParser;
        LaunchGameParamsConverter _parametersConverter;

        [SetUp]
        public void Setup()
        {
            _queryParametersParser = new QueryParametersParser();
            _parametersConverter =
                new LaunchGameParamsConverter(_queryParametersParser);
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
            Assert.That(request.OverrideDisplayPixelDensity,
                Is.EqualTo(PixelDensity.PixelDensityUndefined));
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
            Assert.That(request.Parent, Is.EqualTo("some_test_account"));
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
            Assert.That(request.OverrideDisplayPixelDensity, Is.EqualTo(PixelDensity.XHigh));
            Assert.That(request.OverrideDynamicRange, Is.EqualTo(DynamicRange.Sdr));
            Assert.That(request.OverridePreferredCodec, Is.EqualTo(Codec.Vp9));
            Assert.That(request.PackageName, Is.EqualTo("params_package_name"));
            Assert.That(request.StartForwardFrameDump, Is.EqualTo(false));
            Assert.That(request.StreamerFixedFps, Is.EqualTo(765));
            Assert.That(request.StreamerFixedResolution, Is.EqualTo(VideoResolution._1440P));
            Assert.That(request.StreamerMaximumBandWidthKbps, Is.EqualTo(8764));
            Assert.That(request.StreamerMinimumBandWidthKbps, Is.EqualTo(23));
        }

        [Test]
        public void ToLaunchGameRequestExternalParams()
        {
            LaunchParams parameters = ValidExternalParams;

            ConfigStatus status =
                _parametersConverter.ToLaunchGameRequest(parameters, out LaunchGameRequest request);

            Assert.That(status.IsOk, Is.True);
            Assert.IsNotNull(request);
            Assert.That(request.Parent, Is.EqualTo(parameters.ExternalAccount));
        }
    }
}
