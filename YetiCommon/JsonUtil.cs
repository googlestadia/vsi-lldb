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

using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Abstractions;

namespace YetiCommon
{
    // Provides utility functions for loading json files.
    public class JsonUtil : ISerializer
    {
        IFileSystem filesystem;

        public JsonUtil() : this(new FileSystem()) { }

        public JsonUtil(IFileSystem filesystem)
        {
            this.filesystem = filesystem;
        }

        // Parses the target file into an annotated class, or returns an empty instance if the file
        // is not found or cannot be parsed.
        public T LoadOrDefault<T>(string file) where T : new()
        {
            var result = LoadOrNull<T>(file);
            return result == null ? new T() : result;
        }

        // Parses the target file into an annotated class, or returns null if the file is not found
        // or cannot be parsed.
        public T LoadOrNull<T>(string file)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(
                    filesystem.File.ReadAllText(file));
            }
            catch (Exception ex) when (
                ex is FileNotFoundException ||
                ex is DirectoryNotFoundException ||
                ex is JsonException)
            {
                return default(T);
            }
        }

        #region ISerializer functions

        public string Serialize<T>(T obj)
        {
            try
            {
                return JsonConvert.SerializeObject(obj);
            }
            catch (JsonException ex)
            {
                throw new SerializationException(ex);
            }
        }

        public T Deserialize<T>(string text)
        {
            try
            {
                var result = JsonConvert.DeserializeObject<T>(text);
                if (result == null)
                {
                    throw new SerializationException(
                        $"Json serialization failed for string: {text}");
                }
                return result;
            }
            catch (Exception ex) when(!(ex is SerializationException))
            {
                throw new SerializationException(ex);
            }
        }

        #endregion
    }
}