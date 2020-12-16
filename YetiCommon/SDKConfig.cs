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

        const string DefaultUrl = "https://cloudcast-pa.googleapis.com";
        const string DefaultPartnerUrl = "https://console.ggp.google.com";

        public class Factory : ISdkConfigFactory
        {
            JsonUtil jsonUtil;

            public Factory(JsonUtil jsonUtil)
            {
                this.jsonUtil = jsonUtil;
            }

            // For test substitution.
            public Factory() { }

            public ISdkConfig LoadGgpSdkConfigOrDefault() => LoadOrDefault();

            public virtual SdkConfig LoadOrDefault()
            {
                return jsonUtil.LoadOrDefault<SdkConfig>(
                    Path.Combine(SDKUtil.GetUserConfigPath(), SdkConfigFilename));
            }
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
        public string PortalUrl { get; set; }

        [JsonProperty("disableMetrics")]
        public bool DisableMetrics { get; set; }

        public string UrlOrDefault
        {
            get
            {
                if (string.IsNullOrEmpty(Url))
                {
                    return DefaultUrl;
                }
                return Url;
            }
        }

        public string PortalUrlOrDefault
        {
            get
            {
                if (string.IsNullOrEmpty(PortalUrl))
                {
                    return DefaultPartnerUrl;
                }
                return PortalUrl;
            }
        }
    }
}