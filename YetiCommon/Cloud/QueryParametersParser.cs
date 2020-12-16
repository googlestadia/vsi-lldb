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

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Web;
using GgpGrpc.Models;

namespace YetiCommon.Cloud
{
    public class QueryParametersParser
    {
        class QueryParamMapping
        {
            // Custom mapping parameters.
            public const string Cmd = "cmd";
            public const string Vars = "vars";
            public const string ColorTools = "color_tools";
            public const string VulkanDriverVariant = "vulkan_driver_variant";
            public const string EnableLlpcInAmdvlk = "enable_llpc_in_amdvlk";
            public const string EnablePipelineCacheSourceLayer =
                "enable_pipeline_cache_source_layer";

            QueryParamMapping()
            {
            }

            internal static QueryParamMapping IgnoreParam(string paramName) =>
                new QueryParamMapping
                {
                    ParamName = paramName,
                    MapPropertyName = null,
                    SetRequestParam = false,
                    PassAsUrlParam = false
                };

            internal static QueryParamMapping
                RequestOnly(string paramName, string mapPropertyName, Type mapType) =>
                new QueryParamMapping
                {
                    ParamName = paramName,
                    MapPropertyName = mapPropertyName,
                    MapType = mapType,
                    SetRequestParam = true,
                    PassAsUrlParam = false
                };

            internal static QueryParamMapping
                CustomParse(string paramName) =>
                new QueryParamMapping
                {
                    ParamName = paramName,
                    MapPropertyName = null,
                    MapType = null,
                    SetRequestParam = true,
                    PassAsUrlParam = false
                };

            internal string ParamName { get; private set; }

            internal Type MapType { get; private set; }

            internal string MapPropertyName { get; private set; }

            internal bool SetRequestParam { get; private set; }

            internal bool PassAsUrlParam { get; private set; }

            internal bool IsIgnored => !SetRequestParam && !PassAsUrlParam;
        }

        static readonly QueryParamMapping[] _queryParamsMappings =
        {
            // List of query parameters, which should be removed from
            // the query and ignored in the request.
            QueryParamMapping.IgnoreParam("account"),
            QueryParamMapping.IgnoreParam("application_id"),
            QueryParamMapping.IgnoreParam("application_version"),
            QueryParamMapping.IgnoreParam("gamelet_id"),
            QueryParamMapping.IgnoreParam("gamelet_name"),
            QueryParamMapping.IgnoreParam("package_id"),
            QueryParamMapping.IgnoreParam("sdk_version"),
            QueryParamMapping.IgnoreParam("headless"),
            QueryParamMapping.IgnoreParam("game_launch_name"),
            // Parameters, which are removed from the query string and
            // are passed as request parameters.
            QueryParamMapping.RequestOnly("test_account",
                                          nameof(ChromeClientLauncher.Params.TestAccount),
                                          typeof(ChromeClientLauncher.Params)),
            QueryParamMapping.RequestOnly("renderdoc",
                                          nameof(ChromeClientLauncher.Params.RenderDoc),
                                          typeof(ChromeClientLauncher.Params)),
            QueryParamMapping.RequestOnly("rgp", nameof(ChromeClientLauncher.Params.Rgp),
                                          typeof(ChromeClientLauncher.Params)),
            QueryParamMapping.RequestOnly("application_name",
                                          nameof(LaunchGameRequest.ApplicationName),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("igd", nameof(LaunchGameRequest.DeepLinkParamsIdGameData),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("game_state_id", nameof(LaunchGameRequest.GameStateName),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("client_resolution",
                                          nameof(LaunchGameRequest.OverrideClientResolution),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("dynamic_range_type",
                                          nameof(LaunchGameRequest.OverrideDynamicRange),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("video_codec",
                                          nameof(LaunchGameRequest.OverridePreferredCodec),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("audio_channel_mode",
                                          nameof(LaunchGameRequest.OverrideAudioChannelMode),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("debug_mode", nameof(LaunchGameRequest.Debug),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("start_forward_frame_dump",
                                          nameof(LaunchGameRequest.StartForwardFrameDump),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("streamer_fixed_resolution",
                                          nameof(LaunchGameRequest.StreamerFixedResolution),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("streamer_fixed_fps",
                                          nameof(LaunchGameRequest.StreamerFixedFps),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("streamer_maximum_bandwidth_kbps",
                                          nameof(LaunchGameRequest.StreamerMaximumBandWidthKbps),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("streamer_minimum_bandwidth_kbps",
                                          nameof(LaunchGameRequest.StreamerMinimumBandWidthKbps),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("surface_enforcement_mode",
                                          nameof(LaunchGameRequest.SurfaceEnforcementMode),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("enforce_production_ram",
                                          nameof(LaunchGameRequest.EnforceProductionRam),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("enable_realtime_priority",
                                          nameof(LaunchGameRequest.EnableGameRealtimePriority),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("mount_uploaded_pipeline_cache",
                                          nameof(LaunchGameRequest.MountUploadedPipelineCache),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("enable_pipeline_cache_source_upload",
                                          nameof(LaunchGameRequest.EnablePipelineCacheSourceUpload),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("mount_dynamic_content",
                                          nameof(LaunchGameRequest.MountDynamicContent),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("add_instance_compatibility_requirements",
                                          nameof(LaunchGameRequest
                                                     .AddInstanceCompatibilityRequirements),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("remove_instance_compatibility_requirements",
                                          nameof(LaunchGameRequest
                                                     .RemoveInstanceCompatibilityRequirements),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("package_name", nameof(LaunchGameRequest.PackageName),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("enable_retroactive_frame_dump",
                                          nameof(LaunchGameRequest.EnableRetroactiveFrameDump),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("stream_profile_preset",
                                          nameof(LaunchGameRequest.StreamQualityPreset),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.RequestOnly("pixel_density",
                                          nameof(LaunchGameRequest.OverrideDisplayPixelDensity),
                                          typeof(LaunchGameRequest)),
            QueryParamMapping.CustomParse(QueryParamMapping.Cmd),
            QueryParamMapping.CustomParse(QueryParamMapping.Vars),
            QueryParamMapping.CustomParse(QueryParamMapping.ColorTools),
            QueryParamMapping.CustomParse(QueryParamMapping.VulkanDriverVariant),
            QueryParamMapping.CustomParse(QueryParamMapping.EnableLlpcInAmdvlk),
            QueryParamMapping.CustomParse(QueryParamMapping.EnablePipelineCacheSourceLayer)
        };

        public ConfigStatus ParametersToDictionary(
            string queryString, out IDictionary<string, string> queryParams)
        {
            ConfigStatus status = ToQueryParams(queryString, out queryParams);
            return status;
        }

        public ConfigStatus ParseToParameters(ChromeClientLauncher.Params parameters,
                                              IDictionary<string, string> queryParams)
        {
            ConfigStatus status = AssignValues(parameters, queryParams);
            return status.Merge(ParseParamsCustomParameters(parameters, queryParams));
        }

        public ConfigStatus GetFinalQueryString(
            IDictionary<string, string> queryParams, out string queryString)
        {
            var status = ConfigStatus.OkStatus();
            List<string> ignoredParameters = _queryParamsMappings
                .Where(p => p.IsIgnored && queryParams.ContainsKey(p.ParamName))
                .Select(p => p.ParamName).ToList();
            if (ignoredParameters.Any())
            {
                status.AppendWarning(ErrorStrings.QueryParametersIgnored(ignoredParameters));
            }

            Dictionary<string, QueryParamMapping> queryParamsMappingsDict =
                _queryParamsMappings.ToDictionary(
                    p => p.ParamName, p => p);
            IEnumerable<KeyValuePair<string, string>> paramsToStayInQuery = queryParams.Where(
                p => !queryParamsMappingsDict.ContainsKey(p.Key) ||
                    queryParamsMappingsDict[p.Key].PassAsUrlParam);
            queryString =
                string.Join("&", paramsToStayInQuery.Select(p => $"{p.Key}={p.Value}"));
            return status;
        }

        public ConfigStatus ParseToLaunchRequest(
            IDictionary<string, string> queryParams, LaunchGameRequest launchRequest)
        {
            ConfigStatus status = AssignValues(launchRequest, queryParams);
            return status.Merge(ParseLaunchRequestCustomParameters(launchRequest, queryParams));
        }

        ConfigStatus AssignValues<T>(T assignObject,
                                     IDictionary<string, string> queryParams)
        {
            var status = ConfigStatus.OkStatus();
            Dictionary<string, QueryParamMapping> paramsToMap = _queryParamsMappings
                .Where(p => p.SetRequestParam && p.MapType == typeof(T))
                .ToDictionary(p => p.ParamName, p => p);
            foreach (KeyValuePair<string, string> queryParam in queryParams)
            {
                if (!paramsToMap.ContainsKey(queryParam.Key))
                {
                    continue;
                }

                status = status.Merge(AssignValue(assignObject, paramsToMap[queryParam.Key],
                                                  queryParam.Key, queryParam.Value));
            }

            return status;
        }

        #region ParseLaunchRequestCustomParameters

        /// <summary>
        /// Sets environment variables from query parameters.
        /// Environment variables are prioritized in the following way:
        /// <list type="number">
        /// <item>
        /// <description>
        /// Query parameter 'vars' value.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// 'Stadia Environment Variables' setting.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Other VSI settings(Rgp, RenderDoc, ...).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Other Query parameters(color_tools, enable_llpc_in_amdvlk, ...).
        /// </description>
        /// </item>
        /// </list>
        /// 3d and 4th points don't overlap, so they are actually together on the 3d place.
        /// </summary>
        /// <param name="launchRequest"></param>
        /// <param name="queryParams"></param>
        /// <returns></returns>
        ConfigStatus ParseLaunchRequestCustomParameters(LaunchGameRequest launchRequest,
                                                        IDictionary<string, string> queryParams)
        {
            ConfigStatus status = AddBoolEnvironmentVariable(
                launchRequest, queryParams, "ENABLE_VK_LAYER_VULKAN_COLOR_TOOLS",
                QueryParamMapping.ColorTools);
            status = status.Merge(
                ParseLaunchRequestVulkanDriverVariant(launchRequest, queryParams));
            status = status.Merge(AddBoolEnvironmentVariable(
                                      launchRequest, queryParams, "GGP_VK_AMDVLK_USE_LLPC",
                                      QueryParamMapping.EnableLlpcInAmdvlk));
            status = status.Merge(AddBoolEnvironmentVariable(
                                      launchRequest, queryParams,
                                      "ENABLE_GOOGLE_PIPELINE_DATA_EXPORT_LAYER",
                                      QueryParamMapping.EnablePipelineCacheSourceLayer));
            return status;
        }

        ConfigStatus AddBoolEnvironmentVariable(LaunchGameRequest launchRequest,
                                                IDictionary<string, string> queryParams,
                                                string variableName, string queryParamName)
        {
            ConfigStatus status = ConfigStatus.OkStatus();
            if (!queryParams.ContainsKey(queryParamName) ||
                launchRequest.EnvironmentVariablePairs.ContainsKey(variableName))
            {
                return status;
            }

            if (!bool.TryParse(queryParams[queryParamName], out bool isTrue))
            {
                status.AppendWarning(ErrorStrings.InvalidQueryParameterType(
                                         queryParamName, queryParams[queryParamName],
                                         typeof(bool)));
            }
            else
            {
                if (isTrue)
                {
                    launchRequest.EnvironmentVariablePairs.Add(variableName, "1");
                }
            }

            return status;
        }

        ConfigStatus ParseLaunchRequestVulkanDriverVariant(LaunchGameRequest launchRequest,
                                                           IDictionary<string, string> queryParams)
        {
            ConfigStatus status = ConfigStatus.OkStatus();
            if (queryParams.ContainsKey(QueryParamMapping.VulkanDriverVariant))
            {
                string driverVariant = "GGP_DEV_VK_DRIVER_VARIANT";
                if (!launchRequest.EnvironmentVariablePairs.ContainsKey(driverVariant))
                {
                    var allowedValues = new[] { "opt", "optprintasserts", "dbgtrapasserts" };
                    if (!allowedValues.Contains(queryParams[QueryParamMapping.VulkanDriverVariant]
                                                    .ToLower()))
                    {
                        status.AppendWarning(ErrorStrings.QueryParameterNotInAllowedRange(
                                                 QueryParamMapping.VulkanDriverVariant,
                                                 queryParams[QueryParamMapping.VulkanDriverVariant],
                                                 allowedValues));
                    }
                    else
                    {
                        launchRequest.EnvironmentVariablePairs.Add(
                            driverVariant,
                            queryParams[QueryParamMapping.VulkanDriverVariant].ToLower());
                    }
                }
            }

            return status;
        }

        #endregion

        #region ParseParamsCustomParameters

        ConfigStatus ParseParamsCustomParameters(ChromeClientLauncher.Params parameters,
                                                 IDictionary<string, string> queryParams)
        {
            ConfigStatus status = ParseParamsCmd(parameters, queryParams);
            return status.Merge(ParseParamsVars(parameters, queryParams));
        }

        ConfigStatus ParseParamsCmd(ChromeClientLauncher.Params parameters,
                                    IDictionary<string, string> queryParams)
        {
            if (!queryParams.ContainsKey(QueryParamMapping.Cmd))
            {
                return ConfigStatus.OkStatus();
            }

            string queryParamsCmd = queryParams[QueryParamMapping.Cmd];
            if (string.IsNullOrWhiteSpace(parameters.Cmd))
            {
                parameters.Cmd = queryParamsCmd;
                return ConfigStatus.OkStatus();
            }

            // The cmd is valid only if the binary name is the same as in
            // the project output.
            string settingsBinaryName =
                parameters.Cmd.Split(' ').First(s => !string.IsNullOrEmpty(s));
            string queryBinaryName =
                queryParamsCmd.Split(' ').FirstOrDefault(s => !string.IsNullOrEmpty(s));
            if (queryBinaryName != settingsBinaryName)
            {
                return ConfigStatus.ErrorStatus(
                    ErrorStrings.InvalidBinaryName(settingsBinaryName, queryBinaryName));
            }

            parameters.Cmd = queryParamsCmd;

            return ConfigStatus.OkStatus();
        }

        ConfigStatus ParseParamsVars(ChromeClientLauncher.Params parameters,
                                     IDictionary<string, string> queryParams)
        {
            if (queryParams.ContainsKey(QueryParamMapping.Vars))
            {
                parameters.GameletEnvironmentVars = queryParams[QueryParamMapping.Vars] + ";" +
                    parameters.GameletEnvironmentVars;
            }
            return ConfigStatus.OkStatus();
        }

        #endregion

        ConfigStatus AssignValue<T>(
            T assignObject, QueryParamMapping mapping, string paramName, string value)
        {
            Type objectType = assignObject.GetType();
            PropertyInfo property = objectType.GetProperty(mapping.MapPropertyName);
            if (property == null)
            {
                throw new ApplicationException(
                    $"{objectType} does not contain property '{mapping.MapPropertyName}'.");
            }

            if (property.PropertyType == typeof(string))
            {
                return SetStringProperty(assignObject, property, value);
            }

            if (property.PropertyType == typeof(int))
            {
                return SetIntProperty(assignObject, property, paramName, value);
            }

            if (Nullable.GetUnderlyingType(property.PropertyType) == typeof(ulong))
            {
                return SetUlongProperty(assignObject, property, paramName, value);
            }

            if (property.PropertyType == typeof(bool))
            {
                return SetBoolProperty(assignObject, property, paramName, value);
            }

            if (property.PropertyType.IsEnum)
            {
                return SetEnumProperty(assignObject, property, paramName, value);
            }

            if (property.PropertyType == typeof(string[]))
            {
                return SetStringArrayProperty(assignObject, property, paramName, value);
            }

            throw new ApplicationException($"Type '{property.PropertyType}' is not supported.");
        }

        ConfigStatus SetStringProperty<T>(
            T assignObject, PropertyInfo property, string value)
        {
            property.SetValue(assignObject, value);
            return ConfigStatus.OkStatus();
        }

        ConfigStatus SetIntProperty<T>(
            T assignObject, PropertyInfo property, string paramName, string value)
        {
            if (!int.TryParse(value, out int intValue))
            {
                return ConfigStatus.WarningStatus(
                    ErrorStrings.InvalidQueryParameterType(paramName, value, typeof(int)));
            }
            property.SetValue(assignObject, intValue);
            return ConfigStatus.OkStatus();
        }

        ConfigStatus SetUlongProperty<T>(
            T assignObject, PropertyInfo property, string paramName, string value)
        {
            if (!ulong.TryParse(value, out ulong ulongValue))
            {
                return ConfigStatus.WarningStatus(
                    ErrorStrings.InvalidQueryParameterType(paramName, value, typeof(ulong)));
            }
            property.SetValue(assignObject, ulongValue);
            return ConfigStatus.OkStatus();
        }

        ConfigStatus SetBoolProperty<T>(
            T assignObject, PropertyInfo property, string paramName, string value)
        {
            if (!bool.TryParse(value, out bool boolValue))
            {
                return ConfigStatus.WarningStatus(
                    ErrorStrings.InvalidQueryParameterType(paramName, value, typeof(bool)));
            }
            property.SetValue(assignObject, boolValue);
            return ConfigStatus.OkStatus();
        }

        ConfigStatus SetEnumProperty<T>(
            T assignObject, PropertyInfo property, string paramName, string value)
        {
            Type propertyType = property.PropertyType;
            if (!int.TryParse(value, out int intValue))
            {
                return ConfigStatus.WarningStatus(
                    ErrorStrings.InvalidQueryParameterType(paramName, value, propertyType));
            }
            Array enumValues = Enum.GetValues(propertyType);
            for (int i = 0; i < enumValues.Length; ++i)
            {
                object enumVal = enumValues.GetValue(i);
                if ((int)enumVal == intValue)
                {
                    property.SetValue(assignObject, enumVal);
                    return ConfigStatus.OkStatus();
                }
            }
            return ConfigStatus.WarningStatus(
                    ErrorStrings.InvalidQueryParameterType(paramName, value, propertyType));
        }

        ConfigStatus SetStringArrayProperty<T>(
            T assignObject, PropertyInfo property, string paramName, string value)
        {
            property.SetValue(assignObject, value.Split(','));
            return ConfigStatus.OkStatus();
        }

        /// <summary>
        /// Parses the query string to a dictionary.
        /// Values in the dictionary contain decoded special characters.
        /// For example: the value 'value%3D%C3%BC' in the query string will be 'value=ü'
        /// in the resulting dictionary.
        /// </summary>
        ConfigStatus ToQueryParams(string queryString,
                                   out IDictionary<string, string> queryParams)
        {
            queryParams = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(queryString))
            {
                return ConfigStatus.OkStatus();
            }

            try
            {
                NameValueCollection nameValueCollection;
                try
                {
                    // May throw an exception when maximum number of keys is exceeded.
                    nameValueCollection = HttpUtility.ParseQueryString(queryString);
                }
                catch (Exception e)
                {
                    throw new ApplicationException(e.Message);
                }

                foreach (string key in nameValueCollection.AllKeys)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        throw new ApplicationException("Parameter 'Key' can not be empty.");
                    }

                    queryParams.Add(key.ToLower(), nameValueCollection.Get(key));
                }

                LogQueryString(queryString, queryParams);
                return ConfigStatus.OkStatus();
            }
            catch (ApplicationException e)
            {
                Trace.TraceWarning($"Error happened while parsing query string. {e.Message}");
                queryParams = new Dictionary<string, string>();
                return ConfigStatus.WarningStatus(
                    ErrorStrings.QueryParametersWrongFormat);
            }
        }

        // TODO: Verify why is this logging needed and if it's still a suitable info.
        void LogQueryString(string queryString, IDictionary<string, string> queryParams)
        {
            var parsedQueryParams = queryParams.Select(p => $"({p.Key},{p.Value})")
                .Aggregate((a, b) => $"{a},{b}");
            Trace.WriteLine($"Parsed ChromeClient query string: {queryString} resulted in" +
                            $"parameter collection: '{parsedQueryParams}'");
        }
    }
}
