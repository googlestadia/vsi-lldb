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
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web;
using YetiCommon.Cloud;

namespace YetiCommon
{
    public class ChromeClientLauncher : ChromeLauncher
    {
        public class Factory
        {
            readonly BackgroundProcess.Factory backgroundProcessFactory;
            readonly ChromeClientLaunchCommandFormatter launchCommandFormatter;
            readonly SdkConfig.Factory sdkConfigFactory;

            public Factory(BackgroundProcess.Factory backgroundProcessFactory,
                ChromeClientLaunchCommandFormatter launchCommandFormatter,
                SdkConfig.Factory sdkConfigFactory)
            {
                this.backgroundProcessFactory = backgroundProcessFactory;
                this.launchCommandFormatter = launchCommandFormatter;
                this.sdkConfigFactory = sdkConfigFactory;
            }

            public ChromeClientLauncher Create() => new ChromeClientLauncher(
                backgroundProcessFactory, sdkConfigFactory,
                new Params());

            /// <summary>
            /// Create a chrome client launcher given base64 encoded arguments.
            /// </summary>
            /// <exception cref="SerializationException">Thrown if the arguments can't
            /// be deserialized.</exception>
            public ChromeClientLauncher Create(string args) =>
                new ChromeClientLauncher(backgroundProcessFactory, sdkConfigFactory,
                    launchCommandFormatter.DecodeLaunchParams(args));
        }

        public class Params
        {
            public string ApplicationName { get; set; }

            public string GameletName { get; set; }

            public string PoolId { get; set; }

            public string Account { get; set; }

            public string TestAccount { get; set; }

            public string GameletEnvironmentVars { get; set; }

            public string Cmd { get; set; }

            public bool Debug { get; set; }

            public string SdkVersion { get; set; }

            public bool RenderDoc { get; set; }

            public bool Rgp { get; set; }

            public string VulkanDriverVariant { get; set; }

            public SurfaceEnforcementSetting SurfaceEnforcementMode { get; set; } =
                SurfaceEnforcementSetting.Off;

            public string QueryParams { get; set; }
        }

        const int Port = 44741;
        const string DebugModeValue = "2";

        readonly string LocalChromeClientUrl = $"http://localhost:{Port}/chromeclient/index.html";

        readonly Params launchParams;

        public ChromeClientLauncher(BackgroundProcess.Factory backgroundProcessFactory,
            SdkConfig.Factory sdkConfigFactory,
            Params launchParams) : base(backgroundProcessFactory, sdkConfigFactory)
        {
            this.launchParams = launchParams;
        }

        public Params LaunchParams => launchParams;

        public string BuildLaunchUrlWithLaunchName(string launchName)
        {
            string portalUrl = SdkConfig.PortalUrlOrDefault;
            string chromeClientUrl = $"{portalUrl}/organizations/{SdkConfig.OrganizationId}/stream";
            string fragmentString = string.IsNullOrEmpty(launchParams.Account)
                ? ""
                : $"#Email={launchParams.Account}";

            var additionalUrlParams = new List<QueryParam>
            {
                QueryParam.Create("sdk_version", Uri.EscapeDataString(launchParams.SdkVersion)),
                QueryParam.Create("game_launch_name",
                                  Uri.EscapeDataString(launchName ?? string.Empty))
            };
            string queryString =
                additionalUrlParams.Where(p => p != null).Select(p => p.ToString())
                    .Aggregate((a, b) => $"{a}&{b}") +
                (string.IsNullOrWhiteSpace(LaunchParams.QueryParams) ? "" : "&") +
                LaunchParams.QueryParams;
            return $"{chromeClientUrl}?{queryString}{fragmentString}";
        }

        //TODO: remove the legacy launch flow.
        public ConfigStatus BuildLaunchUrl(out string url)
        {
            var portalUrl = SdkConfig.PortalUrlOrDefault;
            var chromeClientUrl =
                $"{portalUrl}/organizations/{SdkConfig.OrganizationId}/stream";

            var queryParams = new List<QueryParam>
            {
                QueryParam.Create("cmd", WebUtility.UrlEncode(launchParams.Cmd)),
                QueryParam.Create("application_name",
                                  Uri.EscapeDataString(
                                      launchParams.ApplicationName ?? string.Empty)),
                QueryParam.Create("gamelet_name",
                                  Uri.EscapeDataString(launchParams.GameletName ?? string.Empty)),
                QueryParam.Create("test_account", launchParams.TestAccount),
                QueryParam.Create("vars",
                                  WebUtility.UrlEncode(launchParams.GameletEnvironmentVars)),
                QueryParam.Create("renderdoc", launchParams.RenderDoc.ToString().ToLower()),
                QueryParam.Create("rgp", launchParams.Rgp.ToString().ToLower()),
                QueryParam.Create("sdk_version", WebUtility.UrlEncode(launchParams.SdkVersion)),
                QueryParam.Create("vulkan_driver_variant", launchParams.VulkanDriverVariant),
                QueryParam.Create("surface_enforcement_mode",
                                  launchParams.SurfaceEnforcementMode.ToString().ToLower()),
                QueryParam.Create("debug_mode", GetDebugMode())
            };

            var fragment = string.IsNullOrEmpty(launchParams.Account) ? "" :
                $"Email={launchParams.Account}";

            var status =
                TryMergeCustomQueryString(queryParams,
                                          out IEnumerable<QueryParam> mergedQueryParams);
            var queryString = mergedQueryParams
                .Select(p => p.ToString())
                .Aggregate((a, b) => $"{a}&{b}");
            var fragmentString = string.IsNullOrEmpty(fragment) ? "" : "#" + fragment;

            url = $"{chromeClientUrl}?{queryString}{fragmentString}";
            return status;
        }

        /// <summary>
        /// Merge the custom query string into the given set of parameters. If there is a parameter
        /// collision, the custom parameter will be selected.
        /// </summary>
        ConfigStatus TryMergeCustomQueryString(IEnumerable<QueryParam> queryParams,
                                               out IEnumerable<QueryParam> outParams)
        {
            var paramsByName = queryParams.Where(p => p != null).ToDictionary(p => p.Name);
            var status = TryParseQueryString(launchParams.QueryParams,
                                             out IEnumerable<QueryParam> customQueryParams);
            if (status.IsOk)
            {
                foreach (var customParam in customQueryParams)
                {
                    if (paramsByName.ContainsKey(customParam.Name))
                    {
                        Trace.WriteLine("Warning: Custom query parameter is replacing previous " +
                                        $"value. Param: {customParam.Name}, " +
                                        $"Previous value: {paramsByName[customParam.Name]}, " +
                                        $"New Value: {customParam.Value}");
                        paramsByName[customParam.Name] = customParam;
                    }
                    else
                    {
                        paramsByName.Add(customParam.Name, customParam);
                    }
                }
            }

            outParams = paramsByName.Values;
            return status;
        }

        ConfigStatus TryParseQueryString(string queryString, out IEnumerable<QueryParam> outParams)
        {
            if (string.IsNullOrEmpty(queryString))
            {
                outParams = Enumerable.Empty<QueryParam>();
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
                var queryParams = new List<QueryParam>();
                foreach (var key in nameValueCollection.AllKeys)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        throw new ApplicationException("Parameter 'Key' can not be empty.");
                    }

                    queryParams.Add(new QueryParam()
                    {
                        Name = key,
                        Value = nameValueCollection.Get(key)
                    });
                }

                LogQueryString(queryString, queryParams);
                outParams = queryParams;
                return ConfigStatus.OkStatus();
            }
            catch (ApplicationException e)
            {
                Trace.TraceWarning($"Error happened while parsing query string. {e.Message}");
                outParams = null;
                return ConfigStatus.WarningStatus(ErrorStrings.QueryParametersWrongFormat);
            }
        }

        string GetDebugMode() => launchParams.Debug ? DebugModeValue : "";

        void LogQueryString(string queryString, List<QueryParam> queryParams)
        {
            var parsedQueryParams = queryParams
                .Select(p => $"({p.Name},{p.Value})")
                .Aggregate((a, b) => $"{a},{b}");
            Trace.WriteLine($"Parsed ChromeClient query string: {queryString} resulted in" +
                $"parameter collection: '{parsedQueryParams}'");
        }

        class QueryParam
        {
            public string Name { get; set; }
            public string Value { get; set; }

            public override string ToString() => $"{Name}={Value}";

            /// <summary>
            /// Create a QueryParam. If the value is null or an empty string, return null,
            /// indicating that the query param isn't required.
            /// </summary>
            public static QueryParam Create(string name, string value)
                => string.IsNullOrEmpty(value) ? null :
                    new QueryParam() { Name = name, Value = value };
        }

    }
}
