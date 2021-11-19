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
    class HttpSymbolStoresTests : SymbolStoreBaseTests
    {
        const string _storeUrl = "https://example.com/foo";
        const string _storeUrlB = "https://example.net/bar";
        static readonly string _urlInStore = $"{_storeUrl}/{_filename}/{_buildId}/{_filename}";
        FakeHttpMessageHandler _fakeHttpMessageHandler;

        HttpClient _httpClient;

        public override void SetUp()
        {
            base.SetUp();

            _fakeHttpMessageHandler = new FakeHttpMessageHandler();
            _httpClient = new HttpClient(_fakeHttpMessageHandler);
        }

        [TearDown]
        public void TearDown()
        {
            _httpClient.Dispose();
        }

        [Test]
        public void Create_EmptyUrl()
        {
            Assert.Throws<ArgumentException>(
                () => new HttpSymbolStore(_fakeFileSystem, _httpClient, ""));
        }

        [Test]
        public async Task FindFile_EmptyBuildIdAsync()
        {
            var store = GetEmptyStore();

            var fileReference = await store.FindFileAsync(_filename, BuildId.Empty, true, _log,
                                                          _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(
                Strings.FailedToSearchHttpStore(_storeUrl, _filename, Strings.EmptyBuildId),
                _log.ToString());
        }

        [Test]
        public async Task FindFile_HttpRequestExceptionAsync()
        {
            var store = GetEmptyStore();
            _fakeHttpMessageHandler.ExceptionMap[new Uri(_urlInStore)] =
                new HttpRequestException("message");

            var fileReference = await store.FindFileAsync(_filename, _buildId, true,
                                                          _log, _forceLoad);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchHttpStore(_storeUrl, _filename, "message"),
                                  _log.ToString());
        }

        [Test]
        public async Task FindFile_WontSearchAgainAfterHttpRequestExceptionAsync()
        {
            var store = GetEmptyStore();
            _fakeHttpMessageHandler.ExceptionMap[new Uri(_urlInStore)] =
                new HttpRequestException("message");

            await store.FindFileAsync(_filename, _buildId, true, TextWriter.Null,
                                      _forceLoad);
            var fileReference = await store.FindFileAsync(_filename, _buildId, true,
                                                          _log, false);
            Assert.Null(fileReference);
            StringAssert.Contains(Strings.DoesNotExistInHttpStore(_filename, _storeUrl),
                                  _log.ToString());
        }

        [Test]
        public async Task FindFile_HeadRequestsNotSupportedAsync()
        {
            var store = await GetStoreWithFileAsync();
            _fakeHttpMessageHandler.SupportsHeadRequests = false;

            var fileReference = await store.FindFileAsync(_filename, _buildId, true,
                                                          _log, _forceLoad);

            Assert.AreEqual(_urlInStore, fileReference.Location);
            StringAssert.Contains(Strings.FileFound(_urlInStore), _log.ToString());
        }

        [Test]
        public async Task FindFile_ConnectionIsUnencryptedAsync()
        {
            var store = new HttpSymbolStore(_fakeFileSystem, _httpClient, "http://example.com/");

            await store.FindFileAsync(_filename, _buildId, true, _log, false);

            StringAssert.Contains(Strings.ConnectionIsUnencrypted("example.com"), _log.ToString());
        }

        [Test]
        public async Task FindFile_WontSearchAgainAfterConnectionIsUnencryptedAsync()
        {
            var store = new HttpSymbolStore(_fakeFileSystem, _httpClient, "http://example.com/");

            await store.FindFileAsync(_filename, _buildId, true, TextWriter.Null,
                                      _forceLoad);
            var fileReference = await store.FindFileAsync(_filename, _buildId, true,
                                                          _log, false);
            Assert.Null(fileReference);
            StringAssert.Contains(Strings.DoesNotExistInHttpStore(_filename, "http://example.com/"),
                                  _log.ToString());
        }

        [Test]
        public void DeepEquals()
        {
            var storeA = new HttpSymbolStore(_fakeFileSystem, _httpClient, _storeUrl);
            var storeB = new HttpSymbolStore(_fakeFileSystem, _httpClient, _storeUrl);

            Assert.True(storeA.DeepEquals(storeB));
            Assert.True(storeB.DeepEquals(storeA));
        }

        [Test]
        public void DeepEquals_NotEqual()
        {
            var storeA = new HttpSymbolStore(_fakeFileSystem, _httpClient, _storeUrl);
            var storeB = new HttpSymbolStore(_fakeFileSystem, _httpClient, _storeUrlB);

            Assert.False(storeA.DeepEquals(storeB));
            Assert.False(storeB.DeepEquals(storeA));
        }

#region SymbolStoreBaseTests functions

        protected override ISymbolStore GetEmptyStore()
        {
            return new HttpSymbolStore(_fakeFileSystem, _httpClient, _storeUrl);
        }

        protected override Task<ISymbolStore> GetStoreWithFileAsync()
        {
            _fakeHttpMessageHandler.ContentMap[new Uri(_urlInStore)] =
                Encoding.UTF8.GetBytes(_buildId.ToHexString());

            return Task.FromResult<ISymbolStore>(
                new HttpSymbolStore(_fakeFileSystem, _httpClient, _storeUrl));
        }

#endregion
    }
}
