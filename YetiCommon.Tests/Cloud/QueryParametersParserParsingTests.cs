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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using YetiCommon.Cloud;
using static YetiCommon.Tests.Cloud.LaunchRequestParsingTestData;

namespace YetiCommon.Tests.Cloud
{
    partial class QueryParametersParserTests
    {
        [Test]
        public void ParseToParametersAllQueryParameters()
        {
            LaunchParams parameters = ShallowCopy(ValidParams);
            Dictionary<string, string> queryParams = AllValidQueryParams;
            int expectedOutQueryParametersCount = queryParams.Count;

            ConfigStatus status = _target.ParseToParameters(queryParams, parameters);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.That(queryParams.Count, Is.EqualTo(expectedOutQueryParametersCount));
            Assert.That(parameters.Account, Is.EqualTo("some_account"));
            Assert.That(parameters.ApplicationName, Is.EqualTo("test/app"));
            Assert.That(parameters.Cmd, Is.EqualTo("  some_bin arg2"));
            Assert.That(parameters.Debug, Is.EqualTo(true));
            Assert.That(parameters.GameletEnvironmentVars,
                        Is.EqualTo("Var1=1;vAR2=3;ParamsVar=val"));
            Assert.That(parameters.GameletName, Is.EqualTo("test/gamelet"));
            Assert.That(parameters.PoolId, Is.EqualTo("test_pool"));
            Assert.That(parameters.RenderDoc, Is.EqualTo(false));
            Assert.That(parameters.Rgp, Is.EqualTo(true));
            Assert.That(parameters.SdkVersion, Is.EqualTo("1"));
            Assert.That(parameters.VulkanDriverVariant, Is.EqualTo("test_variant"));
            Assert.That(parameters.SurfaceEnforcementMode,
                        Is.EqualTo(SurfaceEnforcementSetting.Warn));
            Assert.That(parameters.TestAccount,
                        Is.EqualTo("organizations/organization_id/" +
                                   "projects/project_id/testAccounts/gamer#1234"));
            Assert.That(parameters.QueryParams, Is.EqualTo(""));
        }

        [Test]
        public void ParseToRequestAllQueryParameters()
        {
            LaunchGameRequest launchRequest = ShallowCopy(ValidRequest);
            Dictionary<string, string> queryParams = AllValidQueryParams;
            int expectedOutQueryParametersCount = queryParams.Count;

            ConfigStatus status = _target.ParseToLaunchRequest(queryParams, launchRequest);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.That(queryParams.Count, Is.EqualTo(expectedOutQueryParametersCount));
            Assert.That(launchRequest.Parent, Is.EqualTo("Request_parent"));
            Assert.That(launchRequest.GameletName, Is.EqualTo("Request_gamelet_name"));
            Assert.That(launchRequest.ApplicationName, Is.EqualTo("params_app_name"));
            Assert.That(launchRequest.ReleaseName, Is.EqualTo(null));
            Assert.That(launchRequest.EnablePipelineCacheSourceUpload, Is.EqualTo(false));
            Assert.That(launchRequest.EnableGameRealtimePriority, Is.EqualTo(true));
            Assert.That(launchRequest.EnforceProductionRam, Is.EqualTo(false));
            Assert.That(launchRequest.ExecutablePath, Is.EqualTo("Request_bin"));
            Assert.That(launchRequest.CommandLineArguments,
                        Is.EqualTo(new[] { "Request_arg1", "--arg456" }));
            Assert.That(launchRequest.EnvironmentVariablePairs,
                        Is.EqualTo(new Dictionary<string, string>
                                       { { "Request_VAR1", "Some value" } }));
            Assert.That(launchRequest.SurfaceEnforcementMode,
                        Is.EqualTo(SurfaceEnforcementSetting.Warn));
            Assert.That(launchRequest.Debug, Is.EqualTo(true));
            Assert.That(launchRequest.MountUploadedPipelineCache, Is.EqualTo(true));
            Assert.That(launchRequest.MountDynamicContent, Is.EqualTo(true));
            Assert.That(launchRequest.GameStateName, Is.EqualTo("params_game_state_name"));
            Assert.That(launchRequest.DeepLinkParamsIdGameData, Is.EqualTo(23475456543));
            Assert.That(launchRequest.PackageName, Is.EqualTo("params_package_name"));
            Assert.That(launchRequest.StreamerMinimumBandWidthKbps, Is.EqualTo(23));
            Assert.That(launchRequest.StreamerMaximumBandWidthKbps, Is.EqualTo(8764));
            Assert.That(launchRequest.StreamerFixedFps, Is.EqualTo(765));
            Assert.That(launchRequest.OverrideClientResolution, Is.EqualTo(VideoResolution._720P));
            Assert.That(launchRequest.OverrideDynamicRange, Is.EqualTo(DynamicRange.Sdr));
            Assert.That(launchRequest.OverrideAudioChannelMode,
                        Is.EqualTo(ChannelMode.Surround51True));
            Assert.That(launchRequest.StreamerFixedResolution, Is.EqualTo(VideoResolution._1440P));
            Assert.That(launchRequest.OverrideDisplayPixelDensity, Is.EqualTo(9876));
            Assert.That(launchRequest.StartForwardFrameDump, Is.EqualTo(false));
            Assert.That(launchRequest.AddInstanceCompatibilityRequirements,
                        Is.EqualTo(new[] { "r1", "other" }));
            Assert.That(launchRequest.RemoveInstanceCompatibilityRequirements,
                        Is.EqualTo(new[] { "5", "7", " 8" }));
            Assert.That(launchRequest.OverridePreferredCodec, Is.EqualTo(Codec.Vp9));
            Assert.That(launchRequest.EnableRetroactiveFrameDump, Is.EqualTo(true));
            Assert.That(launchRequest.StreamQualityPreset,
                        Is.EqualTo(StreamQualityPreset.HighVisualQuality));
            Assert.That(launchRequest.NetworkModel, Is.EqualTo("test - model"));
        }

        #region CustomParse

        [TestCase("some_bin", TestName = "NoArgs")]
        [TestCase("some_bin --arg1 'other Arg'", TestName = "OverrideArgs")]
        [TestCase("  some_bin  \" Arg \"  ", TestName = "ExtraSpaces")]
        public void ParseParamsCmdSuccess(string cmdValue)
        {
            LaunchParams parameters = ShallowCopy(ValidParams);
            var queryParams = new Dictionary<string, string>
            {
                {"cmd", cmdValue}
            };

            ConfigStatus status = _target.ParseToParameters(queryParams, parameters);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.That(parameters.Cmd, Is.EqualTo(cmdValue));
        }

        [TestCase("some_bin.exe", TestName = "AddExtension")]
        [TestCase("Some_bin --arg1 'other Arg'", TestName = "Uppercase")]
        [TestCase(" some bin some_bin", TestName = "NameDiffers")]
        [TestCase("", TestName = "Empty")]
        public void ParseParamsCmdFail(string cmdValue)
        {
            LaunchParams parameters = ShallowCopy(ValidParams);
            var queryParams = new Dictionary<string, string>
            {
                {"cmd", cmdValue}
            };

            ConfigStatus status = _target.ParseToParameters(queryParams, parameters);

            Assert.That(status.IsErrorLevel, Is.EqualTo(true));
            Assert.That(status.ErrorMessages.Count, Is.EqualTo(1));
            Assert.That(status.AllMessages.Count, Is.EqualTo(1));
            Assert.That(status.ErrorMessages[0].Contains("invalid binary name"));
            Assert.That(status.ErrorMessages[0].Contains("Expected: 'some_bin'"));
            Assert.That(parameters.Cmd, Is.EqualTo("some_bin arg1"));
        }

        [TestCase(" Param1Var=1;OtherVAr=sd5;", TestName = "NewVariables")]
        [TestCase(" ", TestName = "Empty")]
        public void ParseParamsVars(string varsValue)
        {
            LaunchParams parameters = ShallowCopy(ValidParams);
            string previousVars = parameters.GameletEnvironmentVars;
            var queryParams = new Dictionary<string, string>
            {
                {"vars", varsValue}
            };

            ConfigStatus status = _target.ParseToParameters(queryParams, parameters);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.That(parameters.GameletEnvironmentVars.Contains(previousVars));
            Assert.That(parameters.GameletEnvironmentVars.Contains(varsValue));
        }

        [TestCase("color_tools", "ENABLE_VK_LAYER_VULKAN_COLOR_TOOLS", TestName = "ColorTools")]
        [TestCase("enable_llpc_in_amdvlk", "GGP_VK_AMDVLK_USE_LLPC", TestName = "UseLlpc")]
        [TestCase("enable_pipeline_cache_source_layer", "ENABLE_GOOGLE_PIPELINE_DATA_EXPORT_LAYER",
                  TestName = "PipelineCacheSourceLayer")]
        public void ParseParamsSetBoolEnvironmentVariables(string paramName, string envVarName)
        {
            var validEnvVarsValues = new Dictionary<string, string>
            {
                { "True", "1" },
                { "1", "1" },
                { " false", null },
                { "0", null }
            };

            var invalidEnvVarsValues = new[]
            {
                "ngg", "-6", "1.1"
            };

            CheckVariablesDontOverride(paramName, envVarName,
                                       validEnvVarsValues.Keys.Concat(invalidEnvVarsValues));
            CheckValidEnvVars(paramName, envVarName, validEnvVarsValues);
            CheckInvalidEnvVars(paramName, invalidEnvVarsValues, new[]
            {
                $"Can't convert query parameter's '{paramName}' value '{{0}}' to {typeof(bool)}."
            });
        }

        [Test]
        public void ParseParamsSetVulcanDriverEnvVar()
        {
            string paramName = "vulkan_driver_variant";
            string envVarName = "GGP_DEV_VK_DRIVER_VARIANT";
            var validEnvVarsValues = new Dictionary<string, string>
            {
                { "opt", "opt" },
                { "OPT", "opt" },
                { "optprintasserts", "optprintasserts" },
                { "dbgtrapasserts", "dbgtrapasserts" },
                { "DbgtraPAsserts", "dbgtrapasserts" },
            };

            var invalidEnvVarsValues = new[]
            {
                "opt.", "opt printasserts", "dbg_trapasserts", ""
            };

            CheckVariablesDontOverride(paramName, envVarName,
                                       validEnvVarsValues.Keys.Concat(invalidEnvVarsValues));
            CheckValidEnvVars(paramName, envVarName, validEnvVarsValues);
            CheckInvalidEnvVars(paramName, invalidEnvVarsValues,
                                new[]
                                {
                                    $"The parameter '{paramName}' has an invalid value: '{{0}}'",
                                    "'opt', 'optprintasserts', 'dbgtrapasserts'"
                                });
        }

        void CheckVariablesDontOverride(string paramName, string envVarName,
                                        IEnumerable<string> paramValues)
        {
            foreach (string paramVal in paramValues)
            {
                LaunchGameRequest launchRequest = ShallowCopy(ValidRequest);
                var queryParams = new Dictionary<string, string>
                {
                    { paramName, paramVal }
                };
                var envVars = new Dictionary<string, string>
                {
                    { envVarName, "DefaultValue" }
                };
                launchRequest.EnvironmentVariablePairs =
                    envVars.ToDictionary(v => v.Key, v => v.Value);

                ConfigStatus status = _target.ParseToLaunchRequest(queryParams, launchRequest);

                Assert.That(status.IsOk, Is.EqualTo(true));
                Assert.That(launchRequest.EnvironmentVariablePairs, Is.EqualTo(envVars));
            }
        }

        void CheckValidEnvVars(string paramName, string envVarName,
                               Dictionary<string, string> validEnvVarsValues)
        {
            foreach (KeyValuePair<string, string> validEnvVarsValue in validEnvVarsValues)
            {
                LaunchGameRequest launchRequest = ShallowCopy(ValidRequest);
                var queryParams = new Dictionary<string, string>
                {
                    { paramName, validEnvVarsValue.Key }
                };
                launchRequest.EnvironmentVariablePairs = new Dictionary<string, string>();

                ConfigStatus status = _target.ParseToLaunchRequest(queryParams, launchRequest);

                Assert.That(status.IsOk, Is.EqualTo(true));
                if (validEnvVarsValue.Value == null)
                {
                    Assert.That(!launchRequest.EnvironmentVariablePairs.Any());
                }
                else
                {
                    Assert.That(launchRequest.EnvironmentVariablePairs, Is.EqualTo(
                                    new Dictionary<string, string>
                                    {
                                        { envVarName, validEnvVarsValue.Value }
                                    }));
                }
            }
        }

        void CheckInvalidEnvVars(string paramName, string[] invalidEnvVarsValues,
                                 string[] messageParts)
        {
            foreach (string paramVal in invalidEnvVarsValues)
            {
                LaunchGameRequest launchRequest = ShallowCopy(ValidRequest);
                var queryParams = new Dictionary<string, string>
                {
                    { paramName, paramVal }
                };
                launchRequest.EnvironmentVariablePairs = new Dictionary<string, string>();

                ConfigStatus status = _target.ParseToLaunchRequest(queryParams, launchRequest);

                Assert.That(status.IsWarningLevel, Is.EqualTo(true));
                Assert.That(status.WarningMessages.Count, Is.EqualTo(1));
                Assert.That(status.AllMessages.Count, Is.EqualTo(1));
                foreach (string messagePart in messageParts)
                {
                    Assert.That(
                        status.AllMessages[0].Contains(string.Format(messagePart, paramVal)));
                }

                Assert.That(!launchRequest.EnvironmentVariablePairs.Any());
            }
        }

        #endregion

        [TestCase("renderdoc", false, nameof(LaunchParams.RenderDoc),
                  TestName = "RenderDoc")]
        [TestCase("rgp", false, nameof(LaunchParams.Rgp), TestName = "Rgp")]
        [TestCase("debug_mode", true, nameof(LaunchGameRequest.Debug), TestName = "Debug")]
        [TestCase("start_forward_frame_dump", true, nameof(LaunchGameRequest.StartForwardFrameDump),
                  TestName = "StartForwardFrameDump")]
        [TestCase("enforce_production_ram", true, nameof(LaunchGameRequest.EnforceProductionRam),
                  TestName = "EnforceProductionRam")]
        [TestCase("enable_realtime_priority", true,
                  nameof(LaunchGameRequest.EnableGameRealtimePriority),
                  TestName = "EnableGameRealtimePriority")]
        [TestCase("mount_uploaded_pipeline_cache", true,
                  nameof(LaunchGameRequest.MountUploadedPipelineCache),
                  TestName = "MountUploadedPipelineCache")]
        [TestCase("enable_pipeline_cache_source_upload", true,
                  nameof(LaunchGameRequest.EnablePipelineCacheSourceUpload),
                  TestName = "EnablePipelineCacheSourceUpload")]
        [TestCase("mount_dynamic_content", true, nameof(LaunchGameRequest.MountDynamicContent),
                  TestName = "MountDynamicContent")]
        [TestCase("enable_retroactive_frame_dump", true,
                  nameof(LaunchGameRequest.EnableRetroactiveFrameDump),
                  TestName = "EnableRetroactiveFrameDump")]
        public void ParseBoolean(string paramName, bool launchRequest, string propertyName)
        {
            var validBooleanValues = new Dictionary<string, bool>
            {
                { "true", true },
                { "TrUe", true },
                { "1", true },
                { "false", false },
                { "FaLSe", false },
                { "0", false }
            };
            ParseValueSuccess(paramName, launchRequest, propertyName, validBooleanValues);
            string errorTemplate = $"Can't convert query parameter's '{paramName}' value '{{0}}' " +
                $"to {typeof(bool)}.";
            Dictionary<string, Tuple<string, ConfigStatus.ErrorLevel>> invalidBooleanValues =
                new Dictionary<string, ConfigStatus.ErrorLevel>
                {
                    { "some string", ConfigStatus.ErrorLevel.Warning },
                    { " true _", ConfigStatus.ErrorLevel.Warning },
                    { ".false", ConfigStatus.ErrorLevel.Warning },
                    { "2", ConfigStatus.ErrorLevel.Warning },
                    { "-1", ConfigStatus.ErrorLevel.Warning },
                }.ToDictionary(p => p.Key,
                               p => Tuple.Create(string.Format(errorTemplate, p.Key), p.Value));
            ParseValueFailure(paramName, launchRequest, invalidBooleanValues);
        }

        [TestCase("test_account", false, nameof(LaunchParams.TestAccount),
                  TestName = "TestAccount")]
        [TestCase("application_name", true, nameof(LaunchGameRequest.ApplicationName),
                  TestName = "ApplicationName")]
        [TestCase("game_state_id", true, nameof(LaunchGameRequest.GameStateName),
                  TestName = "GameStateName")]
        [TestCase("package_name", true, nameof(LaunchGameRequest.PackageName),
                  TestName = "PackageName")]
        public void ParseString(string paramName, bool launchRequest, string propertyName)
        {
            var validStringValues = new List<string>
            {
                "  string  with spaces ", "UppER anD LoweR CAse",
                "Special characters %='\"`+-|\\ Üöїя", "  ", string.Empty
            };
            ParseValueSuccess(paramName, launchRequest, propertyName,
                              validStringValues.ToDictionary(s => s, s => s));
        }

        [TestCase("igd", true, nameof(LaunchGameRequest.DeepLinkParamsIdGameData),
                  TestName = "DeepLinkParamsIdGameData")]
        public void ParseUlong(string paramName, bool launchRequest, string propertyName)
        {
            var validUlongValues = new Dictionary<string, ulong>
            {
                { "1", 1 },
                { "0", 0 },
                { "007470", 7470 },
                { ulong.MaxValue.ToString(), ulong.MaxValue }
            };
            ParseValueSuccess(paramName, launchRequest, propertyName, validUlongValues);
            string errorTemplate = $"Can't convert query parameter's '{paramName}' value '{{0}}' " +
                $"to {typeof(ulong)}.";
            Dictionary<string, Tuple<string, ConfigStatus.ErrorLevel>> invalidUlongValues =
                new Dictionary<string, ConfigStatus.ErrorLevel>
                {
                    { "some string", ConfigStatus.ErrorLevel.Warning },
                    { "12.453.2.1", ConfigStatus.ErrorLevel.Warning },
                    { ulong.MaxValue + "1", ConfigStatus.ErrorLevel.Warning },
                    { "-2", ConfigStatus.ErrorLevel.Warning },
                }.ToDictionary(p => p.Key,
                               p => Tuple.Create(string.Format(errorTemplate, p.Key), p.Value));
            ParseValueFailure(paramName, launchRequest, invalidUlongValues);
        }

        [TestCase("streamer_fixed_fps", true, nameof(LaunchGameRequest.StreamerFixedFps),
                  TestName = "StreamerFixedFps")]
        [TestCase("streamer_maximum_bandwidth_kbps", true,
                  nameof(LaunchGameRequest.StreamerMaximumBandWidthKbps),
                  TestName = "StreamerMaximumBandWidthKbps")]
        [TestCase("streamer_minimum_bandwidth_kbps", true,
                  nameof(LaunchGameRequest.StreamerMinimumBandWidthKbps),
                  TestName = "StreamerMinimumBandWidthKbps")]
        [TestCase("pixel_density", true, nameof(LaunchGameRequest.OverrideDisplayPixelDensity),
                  TestName = "OverrideDisplayPixelDensity")]
        public void ParseInt(string paramName, bool launchRequest, string propertyName)
        {
            var validIntValues = new Dictionary<string, int>
            {
                { "1", 1 },
                { "0", 0 },
                { "007470", 7470 },
                { "-0470", -470 },
                { int.MaxValue.ToString(), int.MaxValue },
                { int.MinValue.ToString(), int.MinValue }
            };
            ParseValueSuccess(paramName, launchRequest, propertyName, validIntValues);
            string errorTemplate = $"Can't convert query parameter's '{paramName}' value '{{0}}' " +
                $"to {typeof(int)}.";
            Dictionary<string, Tuple<string, ConfigStatus.ErrorLevel>> invalidIntValues =
                new Dictionary<string, ConfigStatus.ErrorLevel>
                {
                    { "some string", ConfigStatus.ErrorLevel.Warning },
                    { "12.453", ConfigStatus.ErrorLevel.Warning },
                    { int.MaxValue + "1", ConfigStatus.ErrorLevel.Warning },
                    { int.MinValue + "1", ConfigStatus.ErrorLevel.Warning }
                }.ToDictionary(p => p.Key,
                               p => Tuple.Create(string.Format(errorTemplate, p.Key), p.Value));
            ParseValueFailure(paramName, launchRequest, invalidIntValues);
        }

        [TestCase("add_instance_compatibility_requirements", true,
                  nameof(LaunchGameRequest.AddInstanceCompatibilityRequirements),
                  TestName = "AddInstanceCompatibilityRequirements")]
        [TestCase("remove_instance_compatibility_requirements", true,
                  nameof(LaunchGameRequest.RemoveInstanceCompatibilityRequirements),
                  TestName = "RemoveInstanceCompatibilityRequirements")]
        public void ParseStringArray(string paramName, bool launchRequest, string propertyName)
        {
            var validArrayValues = new Dictionary<string, string[]>
            {
                { " values, with  , spaces", new[] { " values", " with  ", " spaces" } },
                { "1,  ,45,,,6,7, ", new[] { "1", "45", "6", "7" } }
            };
            ParseValueSuccess(paramName, launchRequest, propertyName, validArrayValues);
        }

        #region EnumsParsing

        [TestCase("client_resolution", true, nameof(LaunchGameRequest.OverrideClientResolution),
                  TestName = "OverrideClientResolution")]
        [TestCase("streamer_fixed_resolution", true,
                  nameof(LaunchGameRequest.StreamerFixedResolution),
                  TestName = "StreamerFixedResolution")]
        public void ParseVideoResolution(string paramName, bool launchRequest, string propertyName)
        {
            var validEnumValues = new Dictionary<string, VideoResolution>
            {
                { "4k", VideoResolution._4K },
                { "1440p", VideoResolution._1440P },
                { "1440P", VideoResolution._1440P },
                { "1080p", VideoResolution._1080P },
                { "720p", VideoResolution._720P },
                { "none", VideoResolution.Unspecified },
                { "NoNe", VideoResolution.Unspecified }
            };
            ParseValueSuccess(paramName, launchRequest, propertyName, validEnumValues);
            string errorTemplate = $"The parameter '{paramName}' has an invalid value: '{{0}}'. " +
                "Valid values are: 'none', '720p', '1080p', '1440p', '4k'.";
            Dictionary<string, Tuple<string, ConfigStatus.ErrorLevel>> invalidEnumValues =
                new Dictionary<string, ConfigStatus.ErrorLevel>
                {
                    { "4kK", ConfigStatus.ErrorLevel.Warning },
                    { "1080p ", ConfigStatus.ErrorLevel.Warning },
                    { string.Empty, ConfigStatus.ErrorLevel.Warning }
                }.ToDictionary(p => p.Key,
                               p => Tuple.Create(string.Format(errorTemplate, p.Key), p.Value));
            ParseValueFailure(paramName, launchRequest, invalidEnumValues);
        }

        [TestCase("dynamic_range_type", true, nameof(LaunchGameRequest.OverrideDynamicRange),
                  TestName = "OverrideDynamicRange")]
        public void ParseDynamicRange(string paramName, bool launchRequest, string propertyName)
        {
            var validEnumValues = new Dictionary<string, DynamicRange>
            {
                { "SDR", DynamicRange.Sdr },
                { "HDR10", DynamicRange.Hdr10 }
            };
            ParseValueSuccess(paramName, launchRequest, propertyName, validEnumValues);
            string errorTemplate = $"The parameter '{paramName}' has an invalid value: '{{0}}'. " +
                "Valid values are: 'SDR', 'HDR10'.";
            Dictionary<string, Tuple<string, ConfigStatus.ErrorLevel>> invalidEnumValues =
                new Dictionary<string, ConfigStatus.ErrorLevel>
                {
                    { "Unspecified", ConfigStatus.ErrorLevel.Warning },
                    { " HDR10", ConfigStatus.ErrorLevel.Warning },
                    { string.Empty, ConfigStatus.ErrorLevel.Warning }
                }.ToDictionary(p => p.Key,
                               p => Tuple.Create(string.Format(errorTemplate, p.Key), p.Value));
            ParseValueFailure(paramName, launchRequest, invalidEnumValues);
        }

        [TestCase("video_codec", true, nameof(LaunchGameRequest.OverridePreferredCodec),
                  TestName = "OverridePreferredCodec")]
        public void ParseCodec(string paramName, bool launchRequest, string propertyName)
        {
            var validEnumValues = new Dictionary<string, Codec>
            {
                { "H264", Codec.H264 },
                { "VP9", Codec.Vp9 },
                { "vp9", Codec.Vp9 }
            };
            ParseValueSuccess(paramName, launchRequest, propertyName, validEnumValues);
            string errorTemplate = $"The parameter '{paramName}' has an invalid value: '{{0}}'. " +
                "Valid values are: 'H264', 'VP9'.";
            Dictionary<string, Tuple<string, ConfigStatus.ErrorLevel>> invalidEnumValues =
                new Dictionary<string, ConfigStatus.ErrorLevel>
                {
                    { "Unspecified", ConfigStatus.ErrorLevel.Warning },
                    { "  ", ConfigStatus.ErrorLevel.Warning },
                    { "Av1", ConfigStatus.ErrorLevel.Warning },
                    { "Vp9Profile2", ConfigStatus.ErrorLevel.Warning },
                    { string.Empty, ConfigStatus.ErrorLevel.Warning }
                }.ToDictionary(p => p.Key,
                               p => Tuple.Create(string.Format(errorTemplate, p.Key), p.Value));
            ParseValueFailure(paramName, launchRequest, invalidEnumValues);
        }

        [TestCase("audio_channel_mode", true, nameof(LaunchGameRequest.OverrideAudioChannelMode),
                  TestName = "OverrideAudioChannelMode")]
        public void ParseChannelMode(string paramName, bool launchRequest, string propertyName)
        {
            var validEnumValues = new Dictionary<string, ChannelMode>
            {
                { "STEREO", ChannelMode.Stereo },
                { "stereo", ChannelMode.Stereo },
                { "SURROUND51", ChannelMode.Surround51True }
            };
            ParseValueSuccess(paramName, launchRequest, propertyName, validEnumValues);
            string errorTemplate = $"The parameter '{paramName}' has an invalid value: '{{0}}'. " +
                "Valid values are: 'STEREO', 'SURROUND51'.";
            Dictionary<string, Tuple<string, ConfigStatus.ErrorLevel>> invalidEnumValues =
                new Dictionary<string, ConfigStatus.ErrorLevel>
                {
                    { "Unspecified", ConfigStatus.ErrorLevel.Warning },
                    { "Surround51True", ConfigStatus.ErrorLevel.Warning },
                    { "Mono", ConfigStatus.ErrorLevel.Warning },
                    { string.Empty, ConfigStatus.ErrorLevel.Warning }
                }.ToDictionary(p => p.Key,
                               p => Tuple.Create(string.Format(errorTemplate, p.Key), p.Value));
            ParseValueFailure(paramName, launchRequest, invalidEnumValues);
        }

        [TestCase("surface_enforcement_mode", true,
                  nameof(LaunchGameRequest.SurfaceEnforcementMode),
                  TestName = "SurfaceEnforcementMode")]
        public void ParseSurfaceEnforcement(string paramName, bool launchRequest,
                                            string propertyName)
        {
            var validEnumValues = new Dictionary<string, SurfaceEnforcementSetting>
            {
                { "UNKNOWN", SurfaceEnforcementSetting.Unknown },
                { "OFF", SurfaceEnforcementSetting.Off },
                { "WARN", SurfaceEnforcementSetting.Warn },
                { "warn", SurfaceEnforcementSetting.Warn },
                { "BLOCK", SurfaceEnforcementSetting.Block }
            };
            ParseValueSuccess(paramName, launchRequest, propertyName, validEnumValues);
            string errorTemplate = $"The parameter '{paramName}' has an invalid value: '{{0}}'. " +
                "Valid values are: 'UNKNOWN', 'OFF', 'WARN', 'BLOCK'.";
            Dictionary<string, Tuple<string, ConfigStatus.ErrorLevel>> invalidEnumValues =
                new Dictionary<string, ConfigStatus.ErrorLevel>
                {
                    { "Unspecified", ConfigStatus.ErrorLevel.Warning },
                    { "none", ConfigStatus.ErrorLevel.Warning },
                    { "OFF.", ConfigStatus.ErrorLevel.Warning },
                    { string.Empty, ConfigStatus.ErrorLevel.Warning }
                }.ToDictionary(p => p.Key,
                               p => Tuple.Create(string.Format(errorTemplate, p.Key), p.Value));
            ParseValueFailure(paramName, launchRequest, invalidEnumValues);
        }

        [TestCase("stream_profile_preset", true, nameof(LaunchGameRequest.StreamQualityPreset),
                  TestName = "StreamQualityPreset")]
        public void ParseStreamQualityPreset(string paramName, bool launchRequest,
                                             string propertyName)
        {
            var validEnumValues = new Dictionary<string, StreamQualityPreset>
            {
                { "", StreamQualityPreset.Undefined },
                { "LOW_LATENCY", StreamQualityPreset.LowLatency },
                { "Low_latency", StreamQualityPreset.LowLatency },
                { "BALANCED", StreamQualityPreset.Balanced },
                { "HIGH_VISUAL_QUALITY", StreamQualityPreset.HighVisualQuality }
            };
            ParseValueSuccess(paramName, launchRequest, propertyName, validEnumValues);
            string errorTemplate = $"The parameter '{paramName}' has an invalid value: '{{0}}'. " +
                "Valid values are: '', 'LOW_LATENCY', 'BALANCED', 'HIGH_VISUAL_QUALITY'.";
            Dictionary<string, Tuple<string, ConfigStatus.ErrorLevel>> invalidEnumValues =
                new Dictionary<string, ConfigStatus.ErrorLevel>
                {
                    { "  ", ConfigStatus.ErrorLevel.Warning },
                    { "HighVisualQuality", ConfigStatus.ErrorLevel.Warning },
                    { "LOW_LATENCY.", ConfigStatus.ErrorLevel.Warning }
                }.ToDictionary(p => p.Key,
                               p => Tuple.Create(string.Format(errorTemplate, p.Key), p.Value));
            ParseValueFailure(paramName, launchRequest, invalidEnumValues);
        }

        #endregion

        void ParseValueSuccess<T>(string paramName, bool launchRequest, string propertyName,
                                  Dictionary<string, T> validValues)
        {
            foreach (KeyValuePair<string, T> valuePair in validValues)
            {
                var queryParams = new Dictionary<string, string>
                {
                    { paramName, valuePair.Key }
                };
                LaunchGameRequest request = ShallowCopy(ValidRequest);
                LaunchParams parameters = ShallowCopy(ValidParams);
                ConfigStatus status = launchRequest
                    ? _target.ParseToLaunchRequest(queryParams, request)
                    : _target.ParseToParameters(queryParams, parameters);
                // Verify that value is valid.
                Assert.That(status.IsOk, Is.EqualTo(true));
                object obj = launchRequest ? (object)request : parameters;
                Type type = launchRequest ? typeof(LaunchGameRequest)
                    : typeof(LaunchParams);
                PropertyInfo property = type.GetProperty(propertyName);
                var value = (T)property.GetValue(obj);
                // Corresponding property is populated properly.
                Assert.That(value, Is.EqualTo(valuePair.Value));
                object originObject = launchRequest ? (object)ValidRequest : ValidParams;
                // Assert that all other properties are unchanged.
                foreach (PropertyInfo propertyInfo in type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetSetMethod() != null))
                {
                    if (propertyInfo != property)
                    {
                        Assert.That(propertyInfo.GetValue(obj),
                                    Is.EqualTo(propertyInfo.GetValue(originObject)));
                    }
                }
            }
        }

        void ParseValueFailure(string paramName, bool launchRequest,
                               Dictionary<string, Tuple<string, ConfigStatus.ErrorLevel>>
                                   invalidValues)
        {
            foreach (KeyValuePair<string, Tuple<string, ConfigStatus.ErrorLevel>> valuePair in
                invalidValues)
            {
                string paramValue = valuePair.Key;
                string messagePart = valuePair.Value.Item1;
                ConfigStatus.ErrorLevel expectedErrorLevel = valuePair.Value.Item2;
                var queryParams = new Dictionary<string, string>
                {
                    { paramName, paramValue }
                };
                LaunchGameRequest request = ShallowCopy(ValidRequest);
                LaunchParams parameters = ShallowCopy(ValidParams);
                ConfigStatus status = launchRequest
                    ? _target.ParseToLaunchRequest(queryParams, request)
                    : _target.ParseToParameters(queryParams, parameters);
                // Verify that corresponding message is populated.
                Assert.That(status.SeverityLevel, Is.EqualTo(expectedErrorLevel));
                Assert.That(status.MessagesByErrorLevel(expectedErrorLevel).Count, Is.EqualTo(1));
                Assert.That(status.AllMessages.Count, Is.EqualTo(1));
                Assert.That(status.AllMessages[0].Contains(messagePart));

                object obj = launchRequest ? (object)request : parameters;
                Type type = launchRequest
                    ? typeof(LaunchGameRequest)
                    : typeof(LaunchParams);
                object originObject = launchRequest ? (object)ValidRequest : ValidParams;
                // Assert that all properties are unchanged.
                AssertObjectPropertiesEqual(type, obj, originObject);
            }
        }

        void AssertObjectPropertiesEqual(Type type, object actual, object expected)
        {
            foreach (PropertyInfo propertyInfo in type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetSetMethod() != null))
            {
                Assert.That(propertyInfo.GetValue(actual),
                            Is.EqualTo(propertyInfo.GetValue(expected)));
            }
        }

        T ShallowCopy<T>(T obj)
        {
            var newObj = Activator.CreateInstance<T>();
            IEnumerable<PropertyInfo> properties = newObj.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetSetMethod() != null);
            foreach (PropertyInfo prop in properties)
            {
                object value = prop.GetValue(obj);
                prop.SetValue(newObj, value);
            }

            return newObj;
        }
    }
}
