// Copyright 2021 Google LLC
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
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YetiCommon;
using YetiCommon.Logging;

namespace SymbolStores
{
    /// <summary>
    /// Interface that allows HttpSymbolStore to be mocked in tests
    /// </summary>
    public interface IHttpSymbolStore : ISymbolStore
    {
    }

    /// <summary>
    /// Represents an http server on which symbol files are stored with the path structure
    /// "url/symbolFileName/buildId/symbolFile".
    /// </summary>
    public class HttpSymbolStore : SymbolStoreBase, IHttpSymbolStore
    {
        public static bool IsHttpStore(string pathElement) =>
            Uri.TryCreate(pathElement, UriKind.Absolute, out Uri uriResult) &&
            (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

        readonly IFileSystem _fileSystem;
        readonly HttpClient _httpClient;
        readonly ObjectCache _missingSymbolsCache;

        [JsonProperty("Url")]
        readonly string _url;

        public HttpSymbolStore(IFileSystem fileSystem, HttpClient httpClient, string url)
            : base(false, false)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException(
                    Strings.FailedToCreateStructuredStore(Strings.UrlNullOrEmpty));
            }

            _fileSystem = fileSystem;
            _httpClient = httpClient;
            _url = url;
            _missingSymbolsCache = MemoryCache.Default;
        }

        public override async Task<IFileReference> FindFileAsync(ModuleSearchQuery searchQuery,
                                                                 TextWriter log)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(searchQuery.Filename));
            Debug.Assert(!BuildId.IsNullOrEmpty(searchQuery.BuildId));

            string encodedFilename = Uri.EscapeDataString(searchQuery.Filename);
            string fileUrl = string.Join("/", _url.TrimEnd('/'), encodedFilename,
                                      searchQuery.BuildId.ToPathName(), encodedFilename);
            if (DoesNotExistInSymbolStore(fileUrl, searchQuery.ForceLoad))
            {
                log.WriteLineAndTrace(Strings.DoesNotExistInHttpStore(searchQuery.Filename, _url));
                return null;
            }

            try
            {
                // Send a HEAD request to check if the file exists at the given url without
                // downloading it.
                HttpResponseMessage response =
                    await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, fileUrl),
                                                HttpCompletionOption.ResponseHeadersRead);

                // If HEAD requests are not supported by the server, fall back to GET requests.
                // The vast majority of servers are expected to support HEAD requests.
                if (response.StatusCode == HttpStatusCode.MethodNotAllowed &&
                    response.Content.Headers.Allow.Contains("GET"))
                {
                    response.Dispose();
                    response = await _httpClient.GetAsync(fileUrl,
                                                          HttpCompletionOption.ResponseHeadersRead);
                }

                using (response)
                {
                    Uri connectionUri = response.RequestMessage.RequestUri;
                    if (connectionUri.Scheme != Uri.UriSchemeHttps)
                    {
                        log.WriteLineAndTrace(
                            Strings.ConnectionIsUnencrypted(connectionUri.Host));
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        log.WriteLineAndTrace(
                            Strings.FileNotFoundInHttpStore(
                                fileUrl, (int)response.StatusCode, response.ReasonPhrase));
                        AddAsNonExisting(fileUrl);
                        return null;
                    }

                    log.WriteLineAndTrace(Strings.FileFound(fileUrl));
                    return new HttpFileReference(_fileSystem, _httpClient, fileUrl);
                }
            }
            catch (HttpRequestException e)
            {
                log.WriteLineAndTrace(
                    Strings.FailedToSearchHttpStore(_url, searchQuery.Filename, e.Message));
                AddAsNonExisting(fileUrl);
                return null;
            }
        }

        public override Task<IFileReference> AddFileAsync(IFileReference source, string filename,
                                                          BuildId buildId, TextWriter log) =>
            throw new NotSupportedException(Strings.CopyToHttpStoreNotSupported);

        public override bool DeepEquals(ISymbolStore otherStore) =>
            otherStore is HttpSymbolStore other && _url == other._url;

        bool DoesNotExistInSymbolStore(string symbolStoreKey, bool force = false)
        {
            if (force)
            {
                _missingSymbolsCache.Remove(symbolStoreKey);
                return false;
            }

            return _missingSymbolsCache.Contains(symbolStoreKey);
        }

        void AddAsNonExisting(string symbolStoreKey)
        {
            _missingSymbolsCache.Add(symbolStoreKey, true, DateTimeOffset.MaxValue);
        }

    }
}
