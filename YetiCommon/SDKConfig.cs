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
using Newtonsoft.Json;
using System.IO;

namespace YetiCommon
{
    // Represents properties stored in the global sdk config file.
    public struct SdkConfig: ISdkConfig
    {
        public const string SdkConfigFilename = "config.json";

        const string _defaultUrl = "https://cloudcast-pa.googleapis.com";
        const string _defaultPartnerUrl = "https://console.ggp.google.com";
        const string _defaultPlayerPortalUrl = "https://stadia.google.com";

        public class Factory : ISdkConfigFactory
        {
            readonly JsonUtil _jsonUtil;

            public Factory(JsonUtil jsonUtil)
            {
                _jsonUtil = jsonUtil;
            }

            // For test substitution.
            public Factory() { }

            public ISdkConfig LoadGgpSdkConfigOrDefault() => LoadOrDefault();

            public virtual SdkConfig LoadOrDefault() =>
                _jsonUtil.LoadOrDefault<SdkConfig>(
                    Path.Combine(SDKUtil.GetUserConfigPath(), SdkConfigFilename));
        }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("organizationId")]
        public string OrganizationId { get; set; }

        [JsonProperty("projectId")]
        public string ProjectId { get; set; }

        [JsonProperty("chromeProfileDir")]
        public string ChromeProfileDir { get; set; }

        [JsonProperty("portalUrl")]
        public string PartnerPortalUrl { get; set; }

        [JsonProperty("playerPortalUrl")]
        public string PlayerPortalUrl { get; set; }

        [JsonProperty("disableMetrics")]
        public bool DisableMetrics { get; set; }

        public string UrlOrDefault =>
            string.IsNullOrEmpty(Url)
                ? _defaultUrl
                : Url;

        public string PartnerPortalUrlOrDefault =>
            string.IsNullOrEmpty(PartnerPortalUrl)
                ? _defaultPartnerUrl
                : PartnerPortalUrl;

        public string PlayerPortalUrlOrDefault =>
            string.IsNullOrEmpty(PlayerPortalUrl)
                ? _defaultPlayerPortalUrl
                : PlayerPortalUrl;
    }
}