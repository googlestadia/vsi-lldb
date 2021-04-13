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

using GgpGrpc.Cloud;
using Grpc.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using YetiCommon;

namespace SymbolStores
{
    /// <summary>
    /// Interface that allows StadiaSymbolStore to be mocked in tests
    /// </summary>
    public interface IStadiaSymbolStore : ISymbolStore
    {
    }

    /// <summary>
    /// Represents a Stadia server which queries a cloud API to get the HTTP URL for a symbol file.
    /// </summary>
    public class StadiaSymbolStore : SymbolStoreBase, IStadiaSymbolStore
    {
        public static readonly string MarkerFileName = "stadia-store.txt";

        public static bool IsStadiaStore(IFileSystem fileSystem, string path) =>
            fileSystem.File.Exists(Path.Combine(path, MarkerFileName));

        readonly IFileSystem _fileSystem;
        readonly HttpClient _httpClient;
        readonly ICrashReportClient _crashReportClient;

        public StadiaSymbolStore(IFileSystem fileSystem, HttpClient httpClient,
                                 ICrashReportClient crashReportClient)
            : base(supportsAddingFiles: false, isCache: false)
        {
            _fileSystem = fileSystem;
            _httpClient = httpClient;
            _crashReportClient = crashReportClient;
        }

#region SymbolStoreBase functions

        public override async Task<IFileReference> FindFileAsync(string filename, BuildId buildId,
                                                                 bool isDebugInfoFile,
                                                                 TextWriter log)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FilenameNullOrEmpty, nameof(filename));
            }

            if (buildId == BuildId.Empty)
            {
                // TODO: simplify logging
                Trace.WriteLine(Strings.FailedToSearchStadiaStore(filename, Strings.EmptyBuildId));
                await log.WriteLineAsync(
                    Strings.FailedToSearchStadiaStore(filename, Strings.EmptyBuildId));
                return null;
            }

            string fileUrl;
            try
            {
                // Ask backend for a download URL. This may throw RPC error NotFound, or return a
                // URL that does not exist. The second case is handled later.
                // TODO: figure out how to intercept these calls to record in metrics.
                fileUrl = await _crashReportClient.GenerateSymbolFileDownloadUrlAsync(
                    buildId.ToHexString(), filename);
            }
            catch (CloudException e)
            {
                if (e.InnerException is RpcException inner &&
                    inner.StatusCode == StatusCode.NotFound)
                {
                    // TODO: simplify logging
                    Trace.WriteLine(
                        Strings.FileNotFoundInStadiaStore(buildId.ToHexString(), filename));
                    await log.WriteLineAsync(
                        Strings.FileNotFoundInStadiaStore(buildId.ToHexString(), filename));
                }
                else
                {
                    // TODO: simplify logging
                    Trace.WriteLine(Strings.FailedToSearchStadiaStore(filename, e.ToString()));
                    await log.WriteLineAsync(
                        Strings.FailedToSearchStadiaStore(filename, e.Message));
                }

                return null;
            }

            HttpResponseMessage response;
            try
            {
                // Send a HEAD request to check if the file exists at the given url without
                // downloading it.
                response =
                    await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, fileUrl),
                                                HttpCompletionOption.ResponseHeadersRead);
            }
            catch (HttpRequestException e)
            {
                Trace.WriteLine(Strings.FailedToSearchStadiaStore(filename, e.ToString()));
                await log.WriteLineAsync(Strings.FailedToSearchStadiaStore(filename, e.Message));
                return null;
            }

            using (response)
            {
                // Handle the most common case of NotFound with a nicer error message.
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // TODO: simplify logging
                    Trace.WriteLine(
                        Strings.FileNotFoundInStadiaStore(buildId.ToHexString(), filename));
                    await log.WriteLineAsync(
                        Strings.FileNotFoundInStadiaStore(buildId.ToHexString(), filename));
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    // TODO: simplify logging
                    Trace.WriteLine(Strings.FileNotFoundInHttpStore(
                        filename, (int)response.StatusCode, response.ReasonPhrase));
                    await log.WriteLineAsync(Strings.FileNotFoundInHttpStore(
                        filename, (int)response.StatusCode, response.ReasonPhrase));
                    return null;
                }

                // TODO: simplify logging
                Trace.WriteLine(Strings.FileFound(filename));
                await log.WriteLineAsync(Strings.FileFound(filename));

                return new HttpFileReference(_fileSystem, _httpClient, fileUrl);
            }
        }

        public override Task<IFileReference> AddFileAsync(IFileReference source, string filename,
                                                          BuildId buildId, TextWriter log) =>
            throw new NotSupportedException(Strings.CopyToHttpStoreNotSupported);

        public override bool DeepEquals(ISymbolStore otherStore) => otherStore is StadiaSymbolStore;

#endregion
    }
}
