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

﻿using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using YetiCommon;

namespace SymbolStores.Tests
{
    [TestFixture]
    class HttpSymbolStoresTests : SymbolStoreBaseTests
    {
        const string STORE_URL = "https://example.com/foo";
        const string STORE_URL_B = "https://example.net/bar";
        const string CACHE_PATH = @"C:\cache";
        static string URL_IN_STORE = $"{STORE_URL}/{FILENAME}/{BUILD_ID}/{FILENAME}";

        FakeHttpMessageHandler fakeHttpMessageHandler;
        HttpClient httpClient;
        HttpSymbolStore.Factory httpSymbolStoreFactory;

        public override void SetUp()
        {
            base.SetUp();

            var taskContext = new JoinableTaskContext();
            fakeHttpMessageHandler = new FakeHttpMessageHandler();
            httpClient = new HttpClient(fakeHttpMessageHandler);

            var httpFileReferenceFactory = new HttpFileReference.Factory(taskContext.Factory,
                fakeFileSystem, httpClient);
            httpSymbolStoreFactory = new HttpSymbolStore.Factory(taskContext.Factory,
                httpClient, httpFileReferenceFactory);
        }

        [TearDown]
        public void TearDown()
        {
            httpClient.Dispose();
        }

        [Test]
        public void Create_EmptyUrl()
        {
            Assert.Throws<ArgumentException>(() => httpSymbolStoreFactory.Create(""));
        }

        [Test]
        public void FindFile_EmptyBuildId()
        {
            var store = GetEmptyStore();

            var fileReference = store.FindFile(FILENAME, BuildId.Empty, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchHttpStore(STORE_URL, FILENAME,
                Strings.EmptyBuildId), log.ToString());
        }

        [Test]
        public void FindFile_HttpRequestException()
        {
            var store = GetEmptyStore();
            fakeHttpMessageHandler.ExceptionMap[new Uri(URL_IN_STORE)]
                = new HttpRequestException("message");

            var fileReference = store.FindFile(FILENAME, BUILD_ID, true, log);

            Assert.Null(fileReference);
            StringAssert.Contains(Strings.FailedToSearchHttpStore(STORE_URL, FILENAME, "message"),
                log.ToString());
        }

        [Test]
        public void FindFile_HeadRequestsNotSupported()
        {
            var store = GetStoreWithFile();
            fakeHttpMessageHandler.SupportsHeadRequests = false;

            var fileReference = store.FindFile(FILENAME, BUILD_ID, true, log);

            Assert.AreEqual(URL_IN_STORE, fileReference.Location);
            StringAssert.Contains(Strings.FileFound(URL_IN_STORE), log.ToString());
        }

        [Test]
        public void FindFile_ConnectionIsUnencrypted()
        {
            var store = httpSymbolStoreFactory.Create("http://example.com/");

            var fileReference = store.FindFile(FILENAME, BUILD_ID, true, log);

            StringAssert.Contains(Strings.ConnectionIsUnencrypted("example.com"), log.ToString());
        }

        [Test]
        public void DeepEquals()
        {
            var storeA = httpSymbolStoreFactory.Create(STORE_URL);
            var storeB = httpSymbolStoreFactory.Create(STORE_URL);

            Assert.True(storeA.DeepEquals(storeB));
            Assert.True(storeB.DeepEquals(storeA));
        }

        [Test]
        public void DeepEquals_NotEqual()
        {
            var storeA = httpSymbolStoreFactory.Create(STORE_URL);
            var storeB = httpSymbolStoreFactory.Create(STORE_URL_B);

            Assert.False(storeA.DeepEquals(storeB));
            Assert.False(storeB.DeepEquals(storeA));
        }

        #region SymbolStoreBaseTests functions

        protected override ISymbolStore GetEmptyStore()
        {
            return httpSymbolStoreFactory.Create(STORE_URL);
        }

        protected override ISymbolStore GetStoreWithFile()
        {
            fakeHttpMessageHandler.ContentMap[new Uri(URL_IN_STORE)] =
                Encoding.UTF8.GetBytes(BUILD_ID.ToHexString());

            return httpSymbolStoreFactory.Create(STORE_URL);
        }

        #endregion
    }
}
