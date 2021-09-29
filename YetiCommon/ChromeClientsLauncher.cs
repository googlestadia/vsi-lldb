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
using System.Linq;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiCommon
{
    /// <summary>
    /// The interface to build URLs that can be used to launch a game in Chrome clients. Currently
    /// we support Test Client and Player Portal.
    /// </summary>
    public interface IChromeClientsLauncher
    {
        LaunchParams LaunchParams { get; }

        /// <summary>
        /// Create a url that can be used to launch the game in the Player Portal.
        /// </summary>
        string MakePlayerClientUrl(string launchName);

        /// <summary>
        /// Create a url that can be used to launch the game in the Test Client.
        /// </summary>
        string MakeTestClientUrl(string launchName);

        /// <summary>
        /// Launch game by opening the url in a Chrome window.
        /// </summary>
        void LaunchGame(string url, string workingDirectory);
    }

    public class ChromeClientsLauncher : IChromeClientsLauncher
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

            public ChromeClientsLauncher Create() => new ChromeClientsLauncher(_sdkConfigFactory,
                                                                               new LaunchParams(),
                                                                               _chromeLauncher);

            /// <summary>
            /// Create a chrome client launcher given base64 encoded arguments.
            /// </summary>
            /// <exception cref="SerializationException">Thrown if the arguments can't
            /// be deserialized.</exception>
            public ChromeClientsLauncher Create(string launchArgs) => new ChromeClientsLauncher(
                _sdkConfigFactory, _launchCommandFormatter.DecodeLaunchParams(launchArgs),
                _chromeLauncher);
        }

        const string _debugModeValue = "2";

        readonly Lazy<SdkConfig> _sdkConfig;
        SdkConfig SdkConfig => _sdkConfig.Value;

        readonly IChromeLauncher _chromeLauncher;

        public ChromeClientsLauncher(SdkConfig.Factory sdkConfigFactory, LaunchParams launchParams,
                                     IChromeLauncher chromeLauncher)
        {
            LaunchParams = launchParams;
            _chromeLauncher = chromeLauncher;
            _sdkConfig = new Lazy<SdkConfig>(sdkConfigFactory.LoadOrDefault);
        }

        public LaunchParams LaunchParams { get; }

        public void LaunchGame(string url, string workingDirectory)
        {
            _chromeLauncher.StartChrome(url, workingDirectory, SdkConfig.ChromeProfileDir);
        }

        public string MakePlayerClientUrl(string launchId)
        {
            string playerPortalUrl = SdkConfig.PlayerPortalUrlOrDefault;
            string playerClientUrl = $"{playerPortalUrl}/player/{LaunchParams.ApplicationId}";

            var additionalUrlParams = new List<QueryParam> {
                // bypass playability test, required param for the dev flow
                QueryParam.Create("bypass_pts", "true"),
                QueryParam.Create("launch_id", launchId),
            };
            string queryString = additionalUrlParams.Where(p => p != null)
                                     .Select(p => p.ToString())
                                     .Aggregate((a, b) => $"{a}&{b}");
            return $"{playerClientUrl}?{queryString}";
        }

        public string MakeTestClientUrl(string launchName)
        {
            string portalUrl = SdkConfig.PartnerPortalUrlOrDefault;
            string testClientUrl = $"{portalUrl}/organizations/{SdkConfig.OrganizationId}/stream";
            string fragmentString =
                string.IsNullOrEmpty(LaunchParams.Account) ? "" : $"#Email={LaunchParams.Account}";

            var additionalUrlParams = new List<QueryParam> {
                QueryParam.Create("sdk_version", Uri.EscapeDataString(LaunchParams.SdkVersion)),
                QueryParam.Create("game_launch_name",
                                  Uri.EscapeDataString(launchName ?? string.Empty))
            };
            string queryString = additionalUrlParams.Where(p => p != null)
                                     .Select(p => p.ToString())
                                     .Aggregate((a, b) => $"{a}&{b}") +
                                 (string.IsNullOrWhiteSpace(LaunchParams.QueryParams) ? "" : "&") +
                                 LaunchParams.QueryParams;
            return $"{testClientUrl}?{queryString}{fragmentString}";
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
                string.IsNullOrEmpty(value) ? null
                                            : new QueryParam() { Name = name, Value = value };
        }
    }

    public class LaunchParams
    {
        public string ApplicationName { get; set; }

        public string ApplicationId { get; set; }

        public string GameletName { get; set; }

        public string PoolId { get; set; }

        public string Account { get; set; }

        public string TestAccount { get; set; }

        public string TestAccountGamerName { get; set; }

        public string ExternalAccount { get; set; }

        public string ExternalAccountDisplayName { get; set; }

        public StadiaEndpoint Endpoint { get; set; }

        public string GameletEnvironmentVars { get; set; }

        public string Cmd { get; set; }

        public bool Debug { get; set; }

        public string SdkVersion { get; set; }

        public string GameletSdkVersion { get; set; }

        public bool RenderDoc { get; set; }

        public bool Rgp { get; set; }

        public bool Dive { get; set; }

        public bool Orbit { get; set; }

        public string VulkanDriverVariant { get; set; }

        public SurfaceEnforcementSetting SurfaceEnforcementMode { get; set; } =
            SurfaceEnforcementSetting.Off;

        public string QueryParams { get; set; }
    }
}