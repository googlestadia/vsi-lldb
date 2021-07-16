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
using System;
using System.Collections.Generic;
using System.Linq;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiCommon.Cloud
{
    public interface ILaunchGameParamsConverter
    {
        ConfigStatus ToLaunchGameRequest(LaunchParams parameters, out LaunchGameRequest request);

        /// <summary>
        /// Returns the full game launch name, which can be used in the GetGameLaunchState
        /// GGP api request.
        /// </summary>
        /// <param name="gameLaunchName">If empty, the current game launch will be used.</param>
        /// <param name="testAccount"></param>
        /// <returns>Full game launch name.</returns>
        string FullGameLaunchName(string gameLaunchName, string testAccount = null);
    }

    public class LaunchGameParamsConverter : ILaunchGameParamsConverter
    {
        const string _developerLaunchGameParent = "organizations/-/players/me";
        const string _externalDecoder = "ExternalDecoder";

        readonly IQueryParametersParser _queryParametersParser;

        public LaunchGameParamsConverter(IQueryParametersParser queryParametersParser)
        {
            _queryParametersParser = queryParametersParser;
        }

        public ConfigStatus ToLaunchGameRequest(
            LaunchParams parameters, out LaunchGameRequest request)
        {
            ConfigStatus status =
                _queryParametersParser.ParametersToDictionary(
                    parameters.QueryParams, out IDictionary<string, string> parametersDict);
            status = status.Merge(
                _queryParametersParser.ParseToParameters(parametersDict, parameters));
            status = status.Merge(
                EnvironmentVariables(parameters, out IDictionary<string, string> envVariables));
            status = status.Merge(CommandLineArguments(parameters, out string[] cmdArgs));

            request = new LaunchGameRequest
            {
                Parent = Parent(parameters),
                GameletName = parameters.GameletName,
                ApplicationName = parameters.ApplicationName,
                ExecutablePath = ExecutablePath(parameters),
                CommandLineArguments = cmdArgs,
                EnvironmentVariablePairs = envVariables,
                SurfaceEnforcementMode = parameters.SurfaceEnforcementMode,
                Debug = parameters.Debug,
                EnableDeveloperResumeOffer = parameters.Endpoint == StadiaEndpoint.AnyEndpoint
            };

            status = status.Merge(
                _queryParametersParser.ParseToLaunchRequest(parametersDict, request));

            status = status.Merge(
                _queryParametersParser.GetFinalQueryString(parametersDict, out string queryString));
            parameters.QueryParams = queryString;

            if ((parameters.Endpoint == StadiaEndpoint.PlayerEndpoint ||
                    parameters.Endpoint == StadiaEndpoint.AnyEndpoint) &&
                !string.IsNullOrEmpty(queryString))
            {
                status.AppendWarning(ErrorStrings.QueryParamsNotSupported(queryString));
            }

            if(parameters.Endpoint == StadiaEndpoint.TestClient)
            {
                //TODO: Use simplified overrides for TestClient as well.
                status = status.Merge(ValidateAndAmendSettings(request));
            }
            else
            {
                FillSimplifiedOverrides(request);
            }

            return status;
        }

        public string FullGameLaunchName(string gameLaunchName, string testAccount = null)
        {
            string actualLaunchName = string.IsNullOrWhiteSpace(gameLaunchName)
                ? "current"
                : gameLaunchName;

            return string.IsNullOrWhiteSpace(testAccount)
                ? $"{_developerLaunchGameParent}/gameLaunches/{actualLaunchName}"
                : $"{testAccount}/gameLaunches/{actualLaunchName}";
        }

        void FillSimplifiedOverrides(LaunchGameRequest request)
        {
            request.SimplifiedOverrideDynamicRange = request.OverrideDynamicRange;
            request.SimplifiedOverrideMaxEncodeResolution = request.OverrideClientResolution;
            request.SimplifiedOverrideCodec = request.OverridePreferredCodec;
            request.SimplifiedOverrideChannelMode = request.OverrideAudioChannelMode;
            request.SimplifiedOverridePixelDensity = request.OverrideDisplayPixelDensity;
        }

        ConfigStatus ValidateAndAmendSettings(LaunchGameRequest request)
        {
            ConfigStatus status = ConfigStatus.OkStatus();

            Action<string> verifyCodecVp9 = (string message) =>
            {
                if (request.OverridePreferredCodec == Codec.Unspecified)
                {
                    request.OverridePreferredCodec = Codec.Vp9;
                }

                if (request.OverridePreferredCodec != Codec.Vp9)
                {
                    status.AppendError(message);
                }
            };

            request.OverrideSystemDynamicRange = request.OverrideDynamicRange;

            if (request.OverrideDynamicRange == DynamicRange.Hdr10)
            {
                verifyCodecVp9(ErrorStrings.CodecDynamicRangeIncompatibleError(
                    request.OverridePreferredCodec, request.OverrideDynamicRange));
            }

            if (request.OverrideClientResolution == VideoResolution._1440P ||
                request.OverrideClientResolution == VideoResolution._4K)
            {
                verifyCodecVp9(ErrorStrings.CodecResolutionIncompatibleError(
                    request.OverridePreferredCodec, request.OverrideClientResolution));
            }

            switch (request.OverrideClientResolution)
            {
                case VideoResolution._720P:
                    request.OverrideScreenWidthPixels = 1280;
                    request.OverrideScreenHeightPixels = 720;
                    break;
                case VideoResolution._1080P:
                    request.OverrideScreenWidthPixels = 1920;
                    request.OverrideScreenHeightPixels = 1080;
                    break;
                case VideoResolution._1440P:
                    request.OverrideScreenWidthPixels = 2560;
                    request.OverrideScreenHeightPixels = 1440;
                    break;
                case VideoResolution._4K:
                    request.OverrideScreenWidthPixels = 3840;
                    request.OverrideScreenHeightPixels = 2160;
                    break;
                case VideoResolution.Unspecified:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(request.OverrideClientResolution), request.OverrideClientResolution,
                        $"Unsupported client resolution value received.");
            }

            if (request.OverridePreferredCodec != Codec.Unspecified)
            {
                // Specify the external (hardware) decoder type to avoid restrictions on the stream
                // quality. Note that this string constant is defined in the comments of the proto.
                request.OverrideSystemVideoDecoderType = _externalDecoder;
            }

            if (request.OverrideClientResolution != VideoResolution.Unspecified)
            {
                // Prevent device settings from interfering with overrides specified above.
                // This setting will not change the bandwidth all by itself.
                request.OverrideDeviceSettingsBandwidth = BandwidthPreference.BandwidthUnlimited;
            }

            if (request.OverrideDynamicRange != DynamicRange.Unspecified)
            {
                // Prevent device settings from interfering with overrides specified above.
                // This setting will not enable HDR all by itself.
                request.OverrideDeviceSettingsHdr = HdrMode.HdrOn;
            }

            if (request.OverrideAudioChannelMode != ChannelMode.Unspecified)
            {
                // Make sure the developer overrides take precedence over any device-level
                // audio settings. See (internal) for details.
                request.OverrideDeviceSettingsAudioPlaybackPreference =
                    AudioPlaybackPreference.PreferenceAutomatic;
            }

            return status;
        }

        string Parent(LaunchParams parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameters.TestAccount))
            {
                return parameters.TestAccount;
            }

            if (!string.IsNullOrWhiteSpace(parameters.ExternalAccount))
            {
                return parameters.ExternalAccount;
            }

            return _developerLaunchGameParent;
        }

        string ExecutablePath(LaunchParams parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters.Cmd))
            {
                return string.Empty;
            }

            string executableName = parameters.Cmd.Split(' ').First(s => !string.IsNullOrEmpty(s));
            return executableName;
        }

        ConfigStatus CommandLineArguments(LaunchParams parameters,
                                          out string[] args)
        {
            args = new string[0];
            if (string.IsNullOrWhiteSpace(parameters.Cmd))
            {
                return ConfigStatus.OkStatus();
            }

            args = parameters.Cmd.Trim().Split(' ').Where(s => !string.IsNullOrEmpty(s)).Skip(1)
                .ToArray();
            return ConfigStatus.OkStatus();
        }

        ConfigStatus EnvironmentVariables(LaunchParams parameters,
                                          out IDictionary<string, string> envVariables)
        {
            ConfigStatus status = ConfigStatus.OkStatus();
            string variablesString = parameters.GameletEnvironmentVars ?? string.Empty;
            envVariables = variablesString.Split(';')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v)).Select(v =>
                {
                    List<string> parts = v.Split('=').ToList();

                    if (string.IsNullOrWhiteSpace(parts[0]))
                    {
                        status.AppendWarning(ErrorStrings.InvalidEnvironmentVariable(v));
                        return Tuple.Create(string.Empty, string.Empty);
                    }

                    return Tuple.Create(parts[0], string.Join("=", parts.Skip(1)));
                }).Where(t => !string.IsNullOrEmpty(t.Item1)).GroupBy(tuple => tuple.Item1).Select(
                    tuple =>
                    {
                        if (tuple.Count() > 1)
                        {
                            status.AppendWarning(
                                ErrorStrings.MultipleEnvironmentVariableKeys(tuple.Key));
                        }

                        return tuple;
                    }).ToDictionary(t => t.Key, t => t.Last().Item2);
            status = status.Merge(AddFlagsEnvironmentVariables(parameters, envVariables));
            if (!status.IsOk)
            {
                status.AppendWarning(ErrorStrings.EditEnvironmentVariables);
            }

            return status;
        }

        ConfigStatus AddFlagsEnvironmentVariables(
            LaunchParams parameters, IDictionary<string, string> variables)
        {
            ConfigStatus status = ConfigStatus.OkStatus();
            var flagEnvironmentVariables = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(parameters.VulkanDriverVariant))
            {
                flagEnvironmentVariables.Add("GGP_DEV_VK_DRIVER_VARIANT",
                                             parameters.VulkanDriverVariant);
            }
            if (parameters.RenderDoc)
            {
                flagEnvironmentVariables.Add("ENABLE_VULKAN_RENDERDOC_CAPTURE", "1");
                flagEnvironmentVariables.Add("RENDERDOC_TEMP", "/mnt/developer/ggp");
                flagEnvironmentVariables.Add("RENDERDOC_DEBUG_LOG_FILE", "/var/game/RDDebug.log");
            }
            if (parameters.Rgp)
            {
                flagEnvironmentVariables.Add("GGP_INTERNAL_LOAD_RGP", "1");
                flagEnvironmentVariables.Add("RGP_DEBUG_LOG_FILE", "/var/game/RGPDebug.log");
            }

            foreach (string key in flagEnvironmentVariables.Keys)
            {
                if (variables.ContainsKey(key))
                {
                    status.AppendWarning(ErrorStrings.EnvironmentVariableOverride(key));
                    continue;
                }
                variables.Add(key, flagEnvironmentVariables[key]);
            }

            if (parameters.Rgp)
            {
                if (!variables.ContainsKey("LD_PRELOAD"))
                {
                    variables.Add("LD_PRELOAD", string.Empty);
                }
                variables["LD_PRELOAD"] += (string.IsNullOrEmpty(variables["LD_PRELOAD"])
                    ? string.Empty
                    : ":") + "librgpserver.so";
            }
            
            return status;
        }
    }
}
