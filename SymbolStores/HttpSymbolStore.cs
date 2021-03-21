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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using YetiCommon;

namespace SymbolStores
{
    /// <summary>
    /// Interface that allows HttpSymbolStore to be mocked in tests
    /// </summary>
    public interface IHttpSymbolStore : ISymbolStore { }

    /// <summary>
    /// Represents an http server on which symbol files are stored with the path structure
    /// "url/symbolFileName/buildId/symbolFile".
    /// </summary>
    public class HttpSymbolStore : SymbolStoreBase, IHttpSymbolStore
    {
        public class Factory
        {
            HttpClient httpClient;
            HttpFileReference.Factory httpSymbolFileFactory;

            public Factory(HttpClient httpClient,
                           HttpFileReference.Factory httpSymbolFileFactory)
            {
                this.httpClient = httpClient;
                this.httpSymbolFileFactory = httpSymbolFileFactory;
            }

            // Throws ArgumentException if url is null or empty
            public virtual IHttpSymbolStore Create(string url)
            {
                return new HttpSymbolStore(httpClient, httpSymbolFileFactory, url);
            }
        }

        public static bool IsHttpStore(string pathElement)
        {
            Uri uriResult;
            return Uri.TryCreate(pathElement, UriKind.Absolute, out uriResult) &&
                (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        HttpClient httpClient;
        HttpFileReference.Factory httpSymbolFileFactory;

        [JsonProperty("Url")] string url;

        HttpSymbolStore(HttpClient httpClient,
                        HttpFileReference.Factory httpSymbolFileFactory, string url)
            : base(false, false)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException(
                    Strings.FailedToCreateStructuredStore(Strings.UrlNullOrEmpty));
            }

            this.httpClient = httpClient;
            this.httpSymbolFileFactory = httpSymbolFileFactory;
            this.url = url;
        }

        #region SymbolStoreBase functions

        public override async Task<IFileReference> FindFileAsync(
            string filename, BuildId buildId, bool isDebugInfoFile, TextWriter log)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FilenameNullOrEmpty, nameof(filename));
            }

            if (buildId == BuildId.Empty)
            {
                Trace.WriteLine(
                    Strings.FailedToSearchHttpStore(url, filename, Strings.EmptyBuildId));
                log.WriteLine(
                    Strings.FailedToSearchHttpStore(url, filename, Strings.EmptyBuildId));
                return null;
            }

            try
            {
                var encodedFilename = Uri.EscapeDataString(filename);
                var fileUrl = string.Join("/", url.TrimEnd('/'), encodedFilename,
                                          buildId.ToString(), encodedFilename);

                // Send a HEAD request to check if the file exists at the given url without
                // downloading it.
                var response = await httpClient.SendAsync(
                    new HttpRequestMessage(HttpMethod.Head, fileUrl),
                    HttpCompletionOption.ResponseHeadersRead);

                // If HEAD requests are not supported by the server, fall back to GET requests.
                // The vast majority of servers are expected to support HEAD requests.
                if (response.StatusCode == HttpStatusCode.MethodNotAllowed &&
                    response.Content.Headers.Allow.Contains("GET"))
                {
                    response.Dispose();
                    response = await httpClient.GetAsync(
                        fileUrl, HttpCompletionOption.ResponseHeadersRead);
                }

                using (response)
                {
                    var connectionUri = response.RequestMessage.RequestUri;
                    if (connectionUri.Scheme != Uri.UriSchemeHttps)
                    {
                        Trace.WriteLine(Strings.ConnectionIsUnencrypted(connectionUri.Host));
                        log.WriteLine(Strings.ConnectionIsUnencrypted(connectionUri.Host));
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        Trace.WriteLine(Strings.FileNotFoundInHttpStore(
                                            fileUrl, (int)response.StatusCode,
                                            response.ReasonPhrase));
                        log.WriteLine(
                            Strings.FileNotFoundInHttpStore(
                                fileUrl, (int)response.StatusCode, response.ReasonPhrase));
                        return null;
                    }
                    else
                    {
                        Trace.WriteLine(Strings.FileFound(fileUrl));
                        log.WriteLine(Strings.FileFound(fileUrl));
                        return httpSymbolFileFactory.Create(fileUrl);
                    }
                }
            }
            catch (HttpRequestException e)
            {
                Trace.WriteLine(Strings.FailedToSearchHttpStore(url, filename, e.ToString()));
                log.WriteLine(Strings.FailedToSearchHttpStore(url, filename, e.Message));
                return null;
            }
        }

        public override Task<IFileReference> AddFileAsync(
            IFileReference source, string filename, BuildId buildId, TextWriter log)
        {
            throw new NotSupportedException(Strings.CopyToHttpStoreNotSupported);
        }

        public override bool DeepEquals(ISymbolStore otherStore)
        {
            var other = otherStore as HttpSymbolStore;
            return other != null && url == other.url;
        }

        #endregion
    }
}
