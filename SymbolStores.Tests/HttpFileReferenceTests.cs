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
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SymbolStores.Tests
{
    [TestFixture]
    class HttpFileReferenceTests
    {
        const string SOURCE_URL = @"http://example.com/foo";
        const string DEST_PATH = @"C:\dest\foo";
        static readonly byte[] CONTENTS = new byte[] { 0x12, 0x34 };

        MockFileSystem fakeFileSystem;
        FakeHttpMessageHandler fakeHttpMessageHandler;
        HttpClient httpClient;

        [SetUp]
        public void SetUp()
        {
            fakeFileSystem = new MockFileSystem();
            fakeHttpMessageHandler = new FakeHttpMessageHandler();
            httpClient = new HttpClient(fakeHttpMessageHandler);
        }

        [Test]
        public void Create_NullFilepath()
        {
            Assert.Throws<ArgumentNullException>(
                () => new HttpFileReference(fakeFileSystem, httpClient, null));
        }

        [Test]
        public async Task CopyToAsync()
        {
            fakeHttpMessageHandler.ContentMap[new Uri(SOURCE_URL)] = CONTENTS;
            var fileReference = new HttpFileReference(fakeFileSystem, httpClient, SOURCE_URL);

            await fileReference.CopyToAsync(DEST_PATH);

            Assert.AreEqual(CONTENTS, fakeFileSystem.GetFile(DEST_PATH).Contents);
        }

        [Test]
        public async Task CopyTo_DestinationAlreadyExistsAsync()
        {
            var DEST_CONTENTS = new byte[] { 0x56, 0x78 };
            fakeHttpMessageHandler.ContentMap[new Uri(SOURCE_URL)] = CONTENTS;
            fakeFileSystem.AddFile(DEST_PATH, new MockFileData(DEST_CONTENTS));
            var fileReference = new HttpFileReference(fakeFileSystem, httpClient, SOURCE_URL);

            await fileReference.CopyToAsync(DEST_PATH);
            Assert.AreEqual(CONTENTS, fakeFileSystem.GetFile(DEST_PATH).Contents);
        }

        [Test]
        public void CopyTo_HttpRequestException()
        {
            fakeHttpMessageHandler.ExceptionMap[new Uri(SOURCE_URL)] = new HttpRequestException();
            fakeFileSystem.AddFile(DEST_PATH, new MockFileData(CONTENTS));
            var fileReference = new HttpFileReference(fakeFileSystem, httpClient, SOURCE_URL);

            Assert.ThrowsAsync<SymbolStoreException>(() => fileReference.CopyToAsync(DEST_PATH));
        }

        [Test]
        public void CopyTo_NullDestFilepath()
        {
            var fileReference = new HttpFileReference(fakeFileSystem, httpClient, SOURCE_URL);

            Assert.ThrowsAsync<ArgumentNullException>(() => fileReference.CopyToAsync(null));
        }
    }
}
