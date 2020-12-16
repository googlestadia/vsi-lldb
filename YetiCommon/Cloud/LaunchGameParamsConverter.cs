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
using System.Linq;
using GgpGrpc;
using GgpGrpc.Models;

namespace YetiCommon.Cloud
{
    public class LaunchGameParamsConverter
    {
        const string _developerLaunchGameParent = "organizations/-/players/me";

        readonly SdkConfig.Factory _sdkConfigFactory;

        readonly QueryParametersParser _queryParametersParser;

        public LaunchGameParamsConverter(
            SdkConfig.Factory sdkConfigFactory, QueryParametersParser queryParametersParser)
        {
            _sdkConfigFactory = sdkConfigFactory;
            _queryParametersParser = queryParametersParser;
        }

        public ConfigStatus ToLaunchGameRequest(
            ChromeClientLauncher.Params parameters, out LaunchGameRequest request)
        {
            ConfigStatus status =
                _queryParametersParser.ParametersToDictionary(
                    parameters.QueryParams, out IDictionary<string, string> parametersDict);
            status = status.Merge(
                _queryParametersParser.ParseToParameters(parameters, parametersDict));
            ISdkConfig sdkConfig = _sdkConfigFactory.LoadGgpSdkConfigOrDefault();
            status = status.Merge(
                EnvironmentVariables(parameters, out IDictionary<string, string> envVariables));

            request = new LaunchGameRequest
            {
                Parent = Parent(sdkConfig, parameters),
                GameletName = FullGameletName(sdkConfig, parameters.PoolId, parameters.GameletId),
                ApplicationName = FullApplicationName(sdkConfig, parameters),
                ExecutablePath = ExecutablePath(parameters),
                CommandLineArguments = CommandLineArguments(parameters),
                EnvironmentVariablePairs = envVariables,
                SurfaceEnforcementMode = parameters.SurfaceEnforcementMode,
                Debug = parameters.Debug
            };

            status = status.Merge(
                _queryParametersParser.ParseToLaunchRequest(parametersDict, request));

            status = status.Merge(
                _queryParametersParser.GetFinalQueryString(parametersDict, out string queryString));
            parameters.QueryParams = queryString;

            return status;
        }

        public string GetFullGameletName(string poolId, string gameletId)
        {
            ISdkConfig sdkConfig = _sdkConfigFactory.LoadGgpSdkConfigOrDefault();
            return FullGameletName(sdkConfig, poolId, gameletId);
        }

        public string FullGameLaunchName(string gameLaunchName, string testAccount = null)
        {
            ISdkConfig sdkConfig = _sdkConfigFactory.LoadGgpSdkConfigOrDefault();
            string fullLaunchName = string.IsNullOrWhiteSpace(gameLaunchName)
                ? "current"
                : gameLaunchName;

            return string.IsNullOrWhiteSpace(testAccount)
                ? $"organizations/-/players/me/gameLaunches/{fullLaunchName}"
                : $"organizations/{sdkConfig.OrganizationId}/projects/{sdkConfig.ProjectId}/" +
                $"testAccounts/{testAccount}/gameLaunches/{fullLaunchName}";
        }

        string Parent(ISdkConfig sdkConfig, ChromeClientLauncher.Params parameters) =>
            string.IsNullOrWhiteSpace(parameters.TestAccount)
                ? _developerLaunchGameParent
                : $"organizations/{sdkConfig.OrganizationId}/projects/{sdkConfig.ProjectId}" +
                "/testAccounts/{parameters.TestAccount}";

        string FullGameletName(ISdkConfig sdkConfig, string poolId, string gameletId) =>
            $"organizations/{sdkConfig.OrganizationId}/projects/{sdkConfig.ProjectId}/" +
            $"pools/{poolId}/gamelets/{gameletId}";

        string FullApplicationName(ISdkConfig sdkConfig, ChromeClientLauncher.Params parameters) =>
            $"organizations/{sdkConfig.OrganizationId}/applications/{parameters.ApplicationId}";

        string ExecutablePath(ChromeClientLauncher.Params parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters.Cmd))
            {
                return string.Empty;
            }

            string executableName = parameters.Cmd.Split(' ').First(s => !string.IsNullOrEmpty(s));
            return executableName;
        }

        string[] CommandLineArguments(ChromeClientLauncher.Params parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters.Cmd))
            {
                return new string[0];
            }

            return parameters.Cmd.Split(' ').Where(s => !string.IsNullOrEmpty(s)).Skip(1).ToArray();
        }

        ConfigStatus EnvironmentVariables(ChromeClientLauncher.Params parameters,
                                          out IDictionary<string, string> envVariables)
        {
            var status = ConfigStatus.OkStatus();
            envVariables = parameters.GameletEnvironmentVars.Split(';')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v)).Select(v =>
                {
                    List<string> parts = v.Split('=')
                        .Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

                    if (parts.Count > 2)
                    {
                        status.AppendWarning(ErrorStrings.InvalidEnvironmentVariable(v));
                        return Tuple.Create(string.Empty, string.Empty);
                    }

                    return Tuple.Create(parts[0].ToUpper(), parts.Count > 1
                                            ? parts[1]
                                            : string.Empty);
                }).Where(t => !string.IsNullOrEmpty(t.Item1)).GroupBy(tuple => tuple.Item1).Select(
                    tuple =>
                    {
                        if (tuple.Count() > 1)
                        {
                            status.AppendWarning(
                                ErrorStrings.MultipleEnvironmentVariableKeys(
                                    tuple.Key, tuple.Skip(1).Select(t => t.Item2)));
                        }

                        return tuple;
                    }).ToDictionary(t => t.Key, t => t.First().Item2);
            status = status.Merge(AddFlagsEnvironmentVariables(parameters, envVariables));
            if (!status.IsOk)
            {
                status.AppendWarning(ErrorStrings.EditEnvironmentVariables);
            }

            return status;
        }

        ConfigStatus AddFlagsEnvironmentVariables(
            ChromeClientLauncher.Params parameters, IDictionary<string, string> variables)
        {
            ConfigStatus status = ConfigStatus.OkStatus();
            var flagEnvironmentVariables = new Dictionary<string, string>
            {
                {"GGP_DEV_VK_DRIVER_VARIANT", parameters.VulkanDriverVariant},
                {"ENABLE_VULKAN_RENDERDOC_CAPTURE", parameters.RenderDoc ? "1" : "0"},
                {"RENDERDOC_TEMP", "/mnt/developer/ggp"},
                {"RENDERDOC_DEBUG_LOG_FILE", "/var/game/RDDebug.log"},
                {"GGP_INTERNAL_LOAD_RGP", parameters.Rgp ? "1" : "0"},
                {"RGP_DEBUG_LOG_FILE", "/var/game/RGPDebug.log"}
            };
            foreach (string key in flagEnvironmentVariables.Keys)
            {
                if (variables.ContainsKey(key))
                {
                    status.AppendWarning(ErrorStrings.EnvironmentVariableOverride(key));
                    continue;
                }
                variables.Add(key, flagEnvironmentVariables[key]);
            }
            if (!variables.ContainsKey("LD_PRELOAD"))
            {
                variables.Add("LD_PRELOAD", string.Empty);
            }

            variables["LD_PRELOAD"] += (string.IsNullOrEmpty(variables["LD_PRELOAD"])
                ? string.Empty
                : ":") + "librgpserver.so";
            return status;
        }
    }
}
