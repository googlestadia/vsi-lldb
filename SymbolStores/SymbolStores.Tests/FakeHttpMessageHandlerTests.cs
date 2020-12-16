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

﻿using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using YetiCommon;

namespace SymbolStores.Tests
{
    [TestFixture]
    class FakeHttpMessageHandlerTests
    {
        static Uri URL = new Uri("http://example.com/test");
        static byte[] CONTENT = new byte[] { 0x12, 0x34 };

        FakeHttpMessageHandler fakeMessageHandler;
        HttpClient httpClient;

        [SetUp]
        public void SetUp()
        {
            fakeMessageHandler = new FakeHttpMessageHandler();
            httpClient = new HttpClient(fakeMessageHandler);
        }

        [Test]
        public async Task SendAsync_NotFoundAsync()
        {
            var response = await httpClient.GetAsync(URL);

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.AreEqual(response.RequestMessage.RequestUri, URL);
        }

        [Test]
        public async Task SendAsync_ReturnsContentAsync()
        {
            fakeMessageHandler.ContentMap[URL] = CONTENT;

            var response = await httpClient.GetAsync(URL);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(CONTENT, await response.Content.ReadAsByteArrayAsync());
            Assert.AreEqual(response.RequestMessage.RequestUri, URL);
        }

        [Test]
        public async Task SendAsync_HeadRequestAsync()
        {
            fakeMessageHandler.SupportsHeadRequests = true;
            fakeMessageHandler.ContentMap[URL] = CONTENT;

            var response = await httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, URL));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(response.RequestMessage.RequestUri, URL);
        }

        [Test]
        public async Task SendAsync_HeadRequestNotAllowedAsync()
        {
            fakeMessageHandler.SupportsHeadRequests = false;
            fakeMessageHandler.ContentMap[URL] = CONTENT;

            var response = await httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, URL));

            Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            CollectionAssert.Contains(response.Content.Headers.Allow, "GET");
            Assert.AreEqual(response.RequestMessage.RequestUri, URL);
        }

        [Test]
        public void SendAsync_ThrowException()
        {
            fakeMessageHandler.ExceptionMap[URL] = new HttpRequestException();

            Assert.ThrowsAsync<HttpRequestException>(async () => await httpClient.GetAsync(URL));
        }
    }
}
