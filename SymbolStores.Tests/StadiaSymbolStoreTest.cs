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
        static string _urlInStore = $"{_storeUrl}/{BUILD_ID}/{FILENAME}?{_signature}";

        FakeHttpMessageHandler _fakeHttpMessageHandler;
        HttpClient _httpClient;
        StadiaSymbolStore.Factory _stadiaSymbolStoreFactory;
        ICrashReportClient _crashReportClient;

        public override void SetUp()
        {
            base.SetUp();

            _fakeHttpMessageHandler = new FakeHttpMessageHandler();
            _httpClient = new HttpClient(_fakeHttpMessageHandler);
            var httpFileReferenceFactory = new HttpFileReference.Factory(
                fakeFileSystem, _httpClient);

            _crashReportClient = Substitute.For<ICrashReportClient>();

            _stadiaSymbolStoreFactory = new StadiaSymbolStore.Factory(
                _httpClient, httpFileReferenceFactory, _crashReportClient);
        }

        [TearDown]
        public void TearDown()
        {
            _httpClient.Dispose();
        }

        [Test]
        public async Task FindFile_EmptyBuildId()
        {
            ISymbolStore store = _stadiaSymbolStoreFactory.Create();

            IFileReference fileReference =
                await store.FindFileAsync(FILENAME, BuildId.Empty, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchStadiaStore(FILENAME, Strings.EmptyBuildId),
                                  log.ToString());
        }

        [Test]
        public async Task FindFile_APINotFound()
        {
            var ex =
                new CloudException("Failed to generate download URL: not found",
                                   new RpcException(new Status(StatusCode.NotFound, "message")));
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(BUILD_ID.ToHexString(), FILENAME)
                .Returns(x => Task.FromException<string>(ex));
            ISymbolStore store = _stadiaSymbolStoreFactory.Create();

            IFileReference fileReference =
                await store.FindFileAsync(FILENAME, BUILD_ID, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(
                Strings.FileNotFoundInStadiaStore(BUILD_ID.ToHexString(), FILENAME),
                log.ToString());
        }

        [Test]
        public async Task FindFile_APIException()
        {
            var ex = new CloudException(
                "Failed to generate download URL: permission denied",
                new RpcException(new Status(StatusCode.PermissionDenied, "message")));
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(BUILD_ID.ToHexString(), FILENAME)
                .Returns(x => Task.FromException<string>(ex));
            ISymbolStore store = _stadiaSymbolStoreFactory.Create();

            IFileReference fileReference =
                await store.FindFileAsync(FILENAME, BUILD_ID, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchStadiaStore(FILENAME, ex.Message),
                                  log.ToString());
        }

        [Test]
        public async Task FindFile_HttpNotFound()
        {
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(BUILD_ID.ToHexString(), FILENAME)
                .Returns(_urlInStore);
            // By default, HTTP client returns Not Found for every file.
            ISymbolStore store = _stadiaSymbolStoreFactory.Create();

            IFileReference fileReference =
                await store.FindFileAsync(FILENAME, BUILD_ID, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(
                Strings.FileNotFoundInStadiaStore(BUILD_ID.ToHexString(), FILENAME),
                log.ToString());
        }

        [Test]
        public async Task FindFile_HttpRequestException()
        {
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(BUILD_ID.ToHexString(), FILENAME)
                .Returns(_urlInStore);
            _fakeHttpMessageHandler.ExceptionMap[new Uri(_urlInStore)] =
                new HttpRequestException("message");
            ISymbolStore store = _stadiaSymbolStoreFactory.Create();

            IFileReference fileReference =
                await store.FindFileAsync(FILENAME, BUILD_ID, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchStadiaStore(FILENAME, "message"),
                                  log.ToString());
        }

        [Test]
        public void DeepEquals()
        {
            ISymbolStore storeA = _stadiaSymbolStoreFactory.Create();
            ISymbolStore storeB = _stadiaSymbolStoreFactory.Create();

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

            return _stadiaSymbolStoreFactory.Create();
        }

        protected override Task<ISymbolStore> GetStoreWithFileAsync()
        {
            _crashReportClient.GenerateSymbolFileDownloadUrlAsync(BUILD_ID.ToHexString(), FILENAME)
                .Returns(_urlInStore);
            _fakeHttpMessageHandler.ContentMap[new Uri(_urlInStore)] =
                Encoding.UTF8.GetBytes(BUILD_ID.ToHexString());

            return Task.FromResult<ISymbolStore>(_stadiaSymbolStoreFactory.Create());
        }

#endregion
    }
}
