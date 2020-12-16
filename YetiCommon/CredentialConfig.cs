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
using System.IO;
using Newtonsoft.Json;

namespace YetiCommon
{
    // Represents properties stored in the global credential config file.
    public struct CredentialConfig
    {
        public class Factory
        {
            private readonly JsonUtil jsonUtil;

            public Factory(JsonUtil jsonUtil)
            {
                if (jsonUtil == null) { throw new ArgumentNullException(nameof(jsonUtil));}
                this.jsonUtil = jsonUtil;
            }

            // For test substitution.
            public Factory() { }

            public virtual CredentialConfig LoadOrDefault()
            {
                return new CredentialConfig(
                    jsonUtil, Path.Combine(SDKUtil.GetCredentialsPath(), "config.json"));
            }
        }

        [JsonProperty("defaultAccount")]
        public string DefaultAccount { get; set; }

        private CredentialConfig(JsonUtil jsonUtil, string path)
        {
            this = jsonUtil.LoadOrDefault<CredentialConfig>(path);
        }
    }
}