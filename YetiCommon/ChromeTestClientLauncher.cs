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
    public interface IChromeTestClientLauncher
    {
        ChromeLaunchParams LaunchParams { get; }

        void LaunchGame(string url, string workingDirectory);

        string BuildLaunchUrlWithLaunchName(string launchName);

        ConfigStatus BuildLaunchUrl(out string url);
    }

    public class ChromeTestClientLauncher : IChromeTestClientLauncher
    {
        public class Factory
        {
            readonly ChromeClientLaunchCommandFormatter _launchCommandFormatter;
            readonly SdkConfig.Factory _sdkConfigFactory;
            readonly IChromeLauncher _chromeLauncher;

            public Factory(ChromeClientLaunchCommandFormatter launchCommandFormatter,
                           SdkConfig.Factory sdkConfigFactory, IChromeLauncher chromeLauncher)
            {
                _launchCommandFormatter = launchCommandFormatter;
                _sdkConfigFactory = sdkConfigFactory;
                _chromeLauncher = chromeLauncher;
            }

            public ChromeTestClientLauncher Create() =>
                new ChromeTestClientLauncher(_sdkConfigFactory, new ChromeLaunchParams(), _chromeLauncher);

            /// <summary>
            /// Create a chrome client launcher given base64 encoded arguments.
            /// </summary>
            /// <exception cref="SerializationException">Thrown if the arguments can't
            /// be deserialized.</exception>
            public ChromeTestClientLauncher Create(string launchArgs) =>
                new ChromeTestClientLauncher(_sdkConfigFactory,
                                             _launchCommandFormatter.DecodeLaunchParams(launchArgs),
                                             _chromeLauncher);
        }

        const string _debugModeValue = "2";

        readonly Lazy<SdkConfig> _sdkConfig;
        SdkConfig SdkConfig => _sdkConfig.Value;

        readonly IChromeLauncher _chromeLauncher;

        public ChromeTestClientLauncher(SdkConfig.Factory sdkConfigFactory,
                                        ChromeLaunchParams launchParams,
                                        IChromeLauncher chromeLauncher)
        {
            LaunchParams = launchParams;
            _chromeLauncher = chromeLauncher;
            _sdkConfig = new Lazy<SdkConfig>(sdkConfigFactory.LoadOrDefault);
        }

        public ChromeLaunchParams LaunchParams { get; }

        public void LaunchGame(string url, string workingDirectory)
        {
            _chromeLauncher.StartChrome(url, workingDirectory, SdkConfig.ChromeProfileDir);
        }

        public string BuildLaunchUrlWithLaunchName(string launchName)
        {
            string portalUrl = SdkConfig.PartnerPortalUrlOrDefault;
            string chromeClientUrl = $"{portalUrl}/organizations/{SdkConfig.OrganizationId}/stream";
            string fragmentString = string.IsNullOrEmpty(LaunchParams.Account)
                ? ""
                : $"#Email={LaunchParams.Account}";

            var additionalUrlParams = new List<QueryParam>
            {
                QueryParam.Create("sdk_version", Uri.EscapeDataString(LaunchParams.SdkVersion)),
                QueryParam.Create("game_launch_name",
                                  Uri.EscapeDataString(launchName ?? string.Empty))
            };
            string queryString =
                additionalUrlParams.Where(p => p != null).Select(p => p.ToString())
                    .Aggregate((a, b) => $"{a}&{b}") +
                (string.IsNullOrWhiteSpace(LaunchParams.QueryParams)
                    ? ""
                    : "&") + LaunchParams.QueryParams;
            return $"{chromeClientUrl}?{queryString}{fragmentString}";
        }

        //TODO: remove the legacy launch flow.
        public ConfigStatus BuildLaunchUrl(out string url)
        {
            var portalUrl = SdkConfig.PartnerPortalUrlOrDefault;
            string chromeUrl = $"{portalUrl}/organizations/{SdkConfig.OrganizationId}/stream";

            var queryParams = new List<QueryParam>
            {
                QueryParam.Create("cmd", WebUtility.UrlEncode(LaunchParams.Cmd)),
                QueryParam.Create("application_name",
                                  Uri.EscapeDataString(
                                      LaunchParams.ApplicationName ?? string.Empty)),
                QueryParam.Create("gamelet_name",
                                  Uri.EscapeDataString(LaunchParams.GameletName ?? string.Empty)),
                QueryParam.Create("test_account", LaunchParams.TestAccount),
                QueryParam.Create("vars",
                                  WebUtility.UrlEncode(LaunchParams.GameletEnvironmentVars)),
                QueryParam.Create("renderdoc", LaunchParams.RenderDoc.ToString().ToLower()),
                QueryParam.Create("rgp", LaunchParams.Rgp.ToString().ToLower()),
                QueryParam.Create("sdk_version", WebUtility.UrlEncode(LaunchParams.SdkVersion)),
                QueryParam.Create("vulkan_driver_variant", LaunchParams.VulkanDriverVariant),
                QueryParam.Create("surface_enforcement_mode",
                                  LaunchParams.SurfaceEnforcementMode.ToString().ToLower()),
                QueryParam.Create("debug_mode", GetDebugMode())
            };

            string fragment = string.IsNullOrEmpty(LaunchParams.Account)
                ? ""
                : $"Email={LaunchParams.Account}";

            ConfigStatus status =
                TryMergeCustomQueryString(queryParams,
                                          out IEnumerable<QueryParam> mergedQueryParams);
            string queryString = mergedQueryParams.Select(p => p.ToString())
                .Aggregate((a, b) => $"{a}&{b}");
            string fragmentString = string.IsNullOrEmpty(fragment)
                ? ""
                : "#" + fragment;

            url = $"{chromeUrl}?{queryString}{fragmentString}";
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
            ConfigStatus status = TryParseQueryString(LaunchParams.QueryParams,
                                                      out IEnumerable<QueryParam>
                                                          customQueryParams);
            if (status.IsOk)
            {
                foreach (QueryParam customParam in customQueryParams)
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
                foreach (string key in nameValueCollection.AllKeys)
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

        string GetDebugMode() => LaunchParams.Debug
            ? _debugModeValue
            : "";

        void LogQueryString(string queryString, List<QueryParam> queryParams)
        {
            string parsedQueryParams = queryParams.Select(p => $"({p.Name},{p.Value})")
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
            public static QueryParam Create(string name, string value) =>
                string.IsNullOrEmpty(value)
                    ? null
                    : new QueryParam() { Name = name, Value = value };
        }
    }

    public class ChromeLaunchParams
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

        public string GameletSdkVersion { get; set; }

        public bool RenderDoc { get; set; }

        public bool Rgp { get; set; }

        public string VulkanDriverVariant { get; set; }

        public SurfaceEnforcementSetting SurfaceEnforcementMode { get; set; } =
            SurfaceEnforcementSetting.Off;

        public string QueryParams { get; set; }
    }
}