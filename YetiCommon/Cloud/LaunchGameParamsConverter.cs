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

namespace YetiCommon.Cloud
{
    public class LaunchGameParamsConverter
    {
        const string _developerLaunchGameParent = "organizations/-/players/me";

        readonly ISdkConfigFactory _sdkConfigFactory;

        readonly IQueryParametersParser _queryParametersParser;

        public LaunchGameParamsConverter(
            ISdkConfigFactory sdkConfigFactory, IQueryParametersParser queryParametersParser)
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
                _queryParametersParser.ParseToParameters(parametersDict, parameters));
            ISdkConfig sdkConfig = _sdkConfigFactory.LoadGgpSdkConfigOrDefault();
            status = status.Merge(
                EnvironmentVariables(parameters, out IDictionary<string, string> envVariables));
            status = status.Merge(CommandLineArguments(parameters, out string[] cmdArgs));

            request = new LaunchGameRequest
            {
                Parent = Parent(sdkConfig, parameters),
                GameletName = parameters.GameletName,
                ApplicationName = parameters.ApplicationName,
                ExecutablePath = ExecutablePath(parameters),
                CommandLineArguments = cmdArgs,
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

        /// <summary>
        /// Returns the full game launch name, which can be used in the GetGameLaunchState
        /// GGP api request.
        /// </summary>
        /// <param name="gameLaunchName">If empty, the current game launch will be used.</param>
        /// <param name="testAccount"></param>
        /// <returns>Full game launch name.</returns>
        public string FullGameLaunchName(string gameLaunchName, string testAccount = null)
        {
            ISdkConfig sdkConfig = _sdkConfigFactory.LoadGgpSdkConfigOrDefault();
            string actualLaunchName = string.IsNullOrWhiteSpace(gameLaunchName)
                ? "current"
                : gameLaunchName;

            return string.IsNullOrWhiteSpace(testAccount)
                ? $"{_developerLaunchGameParent}/gameLaunches/{actualLaunchName}"
                : $"organizations/{sdkConfig.OrganizationId}/projects/{sdkConfig.ProjectId}/" +
                $"testAccounts/{testAccount}/gameLaunches/{actualLaunchName}";
        }

        string Parent(ISdkConfig sdkConfig, ChromeClientLauncher.Params parameters) =>
            string.IsNullOrWhiteSpace(parameters.TestAccount)
                ? _developerLaunchGameParent
                : $"organizations/{sdkConfig.OrganizationId}/projects/{sdkConfig.ProjectId}" +
                $"/testAccounts/{parameters.TestAccount}";

        string ExecutablePath(ChromeClientLauncher.Params parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters.Cmd))
            {
                return string.Empty;
            }

            string executableName = parameters.Cmd.Split(' ').First(s => !string.IsNullOrEmpty(s));
            return executableName;
        }

        ConfigStatus CommandLineArguments(ChromeClientLauncher.Params parameters,
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

        ConfigStatus EnvironmentVariables(ChromeClientLauncher.Params parameters,
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
                                ErrorStrings.MultipleEnvironmentVariableKeys(
                                    tuple.Key, tuple.Skip(1).Select(t => t.Item2)));
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
            ChromeClientLauncher.Params parameters, IDictionary<string, string> variables)
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
