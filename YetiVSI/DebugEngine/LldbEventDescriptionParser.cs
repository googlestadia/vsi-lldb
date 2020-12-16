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

﻿using System.Diagnostics;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace YetiVSI.DebugEngine
{
    public class FileProcessingUpdate
    {
        [JsonProperty("method")]
        public FileProcessingState Method { get; set; }
        [JsonProperty("file")] public string File { get; set; }
        [JsonProperty("size")] public uint Size { get; set; }
    }

    public enum FileProcessingState
    {
        Read = 0,
        Close,
    }

    public class LldbEventDescriptionParser
    {
        static readonly string _pattern =
            ", type = 0x00000020 \\(file-update\\), data = \\{(.+)\\}";
        readonly Regex _regex = new Regex(_pattern);
        public TData Parse<TData>(string description)
        {
            description = description.Replace("\r", "").Replace("\n", "");

            var matchedPattern = _regex.Match(description);
            if (matchedPattern.Success && (matchedPattern.Groups.Count == 2))
            {
                var toDeserialize = matchedPattern.Groups[1].Value;
                try
                {
                    return JsonConvert.DeserializeObject<TData>(toDeserialize);
                }
                catch (JsonException e)
                {
                    Trace.WriteLine($"Exception while deserializing {toDeserialize}: {e}");
                }
            }

            Trace.WriteLine($"Failed to parse data field from event's description ({description})");
            return default(TData);
        }
    }
}