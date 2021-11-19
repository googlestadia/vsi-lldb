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

﻿using GgpGrpc.Cloud;
using Grpc.Core;
using NSubstitute;
using NUnit.Framework;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using YetiCommon;

namespace SymbolStores.Tests
{
    [TestFixture]
    class StadiaSymbolStoreTest : SymbolStoreBaseTests
    {
        const string _storeUrl = "https://example.com/foo";
        const string _signature = "urlsignature";
        static readonly string _urlInStore = $"{_storeUrl}/{_buildId}/{_filename}?{_signature}";

        FakeHttpMessageHandler _fakeHttpMessageHandler;
        HttpClient _httpClient;
        ICrashReportClient _crashReportClient;

        public override void SetUp()
        {
            base.SetUp();

            _fakeHttpMessageHandler = new FakeHttpMessageHandler();
            _httpClient = new HttpClient(_fakeHttpMessageHandler);
            _crashReportClient = Substitute.For<ICrashReportClient>();
        }

        [TearDown]
        public void TearDown()
        {
            _httpClient.Dispose();
        }

        [Test]
        public async Task FindFile_EmptyBuildIdAsync()
        {
            ISymbolStore store =
                new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient);

            IFileReference fileReference =
                await store.FindFileAsync(_filename, BuildId.Empty, true, _log, _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(
                Strings.FailedToSearchStadiaStore(_filename, Strings.EmptyBuildId),
                _log.ToString());
        }

        [Test]
        public async Task FindFile_APINotFoundAsync()
        {
            var ex =
                new CloudException("Failed to generate download URL: not found",
                                   new RpcException(new Status(StatusCode.NotFound, "message")));
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(_buildId.ToHexString(), _filename)
                .Returns(x => Task.FromException<string>(ex));
            ISymbolStore store =
                new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient);

            IFileReference fileReference = await store.FindFileAsync(_filename, _buildId, true,
                _log, _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(
                Strings.FileNotFoundInStadiaStore(_buildId.ToHexString(), _filename),
                _log.ToString());
        }

        [Test]
        public async Task FindFile_WontSearchAgainAfterAPINotFoundAsync()
        {
            var ex =
                new CloudException("Failed to generate download URL: not found",
                                   new RpcException(new Status(StatusCode.NotFound, "message")));
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(_buildId.ToHexString(), _filename)
                .Returns(x => Task.FromException<string>(ex));
            ISymbolStore store =
                new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient);

            await store.FindFileAsync(_filename, _buildId, true,
                                      TextWriter.Null, true);
            IFileReference fileReference = await store.FindFileAsync(_filename, _buildId, true,
                _log, false);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.DoesNotExistInStadiaStore(_filename,
                                      _buildId.ToHexString()),
                                  _log.ToString());
        }

        [Test]
        public async Task FindFile_APIExceptionAsync()
        {
            var ex = new CloudException(
                "Failed to generate download URL: permission denied",
                new RpcException(new Status(StatusCode.PermissionDenied, "message")));
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(_buildId.ToHexString(), _filename)
                .Returns(x => Task.FromException<string>(ex));
            ISymbolStore store =
                new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient);

            IFileReference fileReference = await store.FindFileAsync(_filename, _buildId, true,
                _log, _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchStadiaStore(_filename, ex.Message),
                                  _log.ToString());
        }

        [Test]
        public async Task FindFile_WontSearchAgainAfterAPIExceptionAsync()
        {
            var ex = new CloudException(
                "Failed to generate download URL: permission denied",
                new RpcException(new Status(StatusCode.PermissionDenied, "message")));
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(_buildId.ToHexString(), _filename)
                .Returns(x => Task.FromException<string>(ex));
            ISymbolStore store =
                new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient);

            await store.FindFileAsync(_filename, _buildId, true,
                TextWriter.Null, true);
            IFileReference fileReference = await store.FindFileAsync(_filename, _buildId, true,
                _log, false);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.DoesNotExistInStadiaStore(_filename,
                                      _buildId.ToHexString()),
                                  _log.ToString());
        }

        [Test]
        public async Task FindFile_HttpNotFoundAsync()
        {
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(_buildId.ToHexString(), _filename)
                .Returns(_urlInStore);
            // By default, HTTP client returns Not Found for every file.
            ISymbolStore store =
                new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient);

            IFileReference fileReference = await store.FindFileAsync(_filename, _buildId, true,
                _log, _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(
                Strings.FileNotFoundInStadiaStore(_buildId.ToHexString(), _filename),
                _log.ToString());
        }

        [Test]
        public async Task FindFile_WontSearchAgainAfterHttpNotFoundAsync()
        {
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(_buildId.ToHexString(), _filename)
                .Returns(_urlInStore);
            // By default, HTTP client returns Not Found for every file.
            ISymbolStore store =
                new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient);

            await store.FindFileAsync(_filename, _buildId, true,
                                      TextWriter.Null, true);
            IFileReference fileReference = await store.FindFileAsync(_filename, _buildId, true,
                _log, false);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.DoesNotExistInStadiaStore(_filename,
                                      _buildId.ToHexString()),
                                  _log.ToString());
        }

        [Test]
        public async Task FindFile_HttpRequestExceptionAsync()
        {
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(_buildId.ToHexString(), _filename)
                .Returns(_urlInStore);
            _fakeHttpMessageHandler.ExceptionMap[new Uri(_urlInStore)] =
                new HttpRequestException("message");
            ISymbolStore store =
                new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient);

            IFileReference fileReference = await store.FindFileAsync(_filename, _buildId, true,
                _log, _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchStadiaStore(_filename, "message"),
                                  _log.ToString());
        }

        [Test]
        public async Task FindFile_WntSearchAgainAfterHttpRequestExceptionAsync()
        {
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(_buildId.ToHexString(), _filename)
                .Returns(_urlInStore);
            _fakeHttpMessageHandler.ExceptionMap[new Uri(_urlInStore)] =
                new HttpRequestException("message");
            ISymbolStore store =
                new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient);

            await store.FindFileAsync(_filename, _buildId, true,
                                      TextWriter.Null, true);
            IFileReference fileReference = await store.FindFileAsync(_filename, _buildId, true,
                _log, false);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.DoesNotExistInStadiaStore(_filename,
                                      _buildId.ToHexString()),
                                  _log.ToString());
        }

        [Test]
        public void DeepEquals()
        {
            ISymbolStore storeA =
                new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient);
            ISymbolStore storeB =
                new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient);

            Assert.True(storeA.DeepEquals(storeB));
            Assert.True(storeB.DeepEquals(storeA));
        }

#region SymbolStoreBaseTests functions

        protected override ISymbolStore GetEmptyStore()
        {
            // We need the API to return something, but it doesn't matter what.
            // By default, the HTTP client responds to all requests with Not Found.
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(new BuildId().ToHexString(), null)
                .ReturnsForAnyArgs(_urlInStore);

            return new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient);
        }

        protected override Task<ISymbolStore> GetStoreWithFileAsync()
        {
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(_buildId.ToHexString(), _filename)
                .Returns(_urlInStore);
            _fakeHttpMessageHandler.ContentMap[new Uri(_urlInStore)] =
                Encoding.UTF8.GetBytes(_buildId.ToHexString());

            return Task.FromResult<ISymbolStore>(
                new StadiaSymbolStore(_fakeFileSystem, _httpClient, _crashReportClient));
        }

#endregion
    }
}
