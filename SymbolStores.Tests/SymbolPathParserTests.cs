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
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Net.Http;
using TestsCommon.TestSupport;
using YetiCommon;

namespace SymbolStores.Tests
{
    [TestFixture]
    class SymbolPathParserTests
    {
        const string DEFAULT_CACHE = @"C:\defaultCache";
        const string DEFAULT_STORE = @"C:\defaultStore";
        const string CACHE_A = @"C:\cacheA";
        const string CACHE_B = @"C:\cacheB";
        const string STORE_A = @"C:\a";
        const string STORE_B = @"C:\b";
        const string INITIALIZED_STORE = @"C:\initialized";
        const string STADIA_STORE = @"C:\stadia";
        const string FLAT_STORE = @"C:\flat";
        const string HTTP_STORE = @"http://example.com/symbols";

        MockFileSystem fakeFileSystem;
        FakeBinaryFileUtil fakeBinaryFileUtil;
        HttpClient httpClient;
        ICrashReportClient crashReportClient;

        SymbolPathParser pathParser;
        LogSpy logSpy;

        [SetUp]
        public void SetUp()
        {
            fakeFileSystem = new MockFileSystem();
            fakeBinaryFileUtil = new FakeBinaryFileUtil(fakeFileSystem);
            httpClient = new HttpClient(new FakeHttpMessageHandler());
            crashReportClient = Substitute.For<ICrashReportClient>();

            var store = new StructuredSymbolStore(fakeFileSystem, INITIALIZED_STORE);
            store.AddMarkerFileIfNeeded();

            fakeFileSystem.AddFile(Path.Combine(STADIA_STORE, StadiaSymbolStore.MarkerFileName),
                                   new MockFileData(""));
            pathParser = new SymbolPathParser(fakeFileSystem, fakeBinaryFileUtil, httpClient,
                                              crashReportClient, null, null);
            logSpy = new LogSpy();
            logSpy.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            logSpy.Detach();
        }

        [Test]
        public void Parse_Null()
        {
            Assert.Throws<ArgumentNullException>(() => pathParser.Parse(null));
        }

        [Test]
        public void Parse_Empty()
        {
            var store = pathParser.Parse("");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_EmptyPathElements()
        {
            var store = pathParser.Parse(";;");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_FlatStore()
        {
            var store = pathParser.Parse(FLAT_STORE);

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            expected.AddStore(new FlatSymbolStore(fakeFileSystem, fakeBinaryFileUtil, FLAT_STORE));
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_StructuredStore()
        {
            var store = pathParser.Parse(INITIALIZED_STORE);

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            expected.AddStore(new StructuredSymbolStore(fakeFileSystem, INITIALIZED_STORE));
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_HttpStore_ExplicitCache()
        {
            var store = pathParser.Parse($"cache*{CACHE_A};{HTTP_STORE}");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(fakeFileSystem, CACHE_A));
            expected.AddStore(cache);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new HttpSymbolStore(fakeFileSystem, httpClient, HTTP_STORE));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_HttpStore_DefaultCache()
        {
            pathParser = new SymbolPathParser(fakeFileSystem, fakeBinaryFileUtil, httpClient,
                                              crashReportClient, DEFAULT_CACHE, null);

            var store = pathParser.Parse(HTTP_STORE);

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, DEFAULT_CACHE));
            server.AddStore(new HttpSymbolStore(fakeFileSystem, httpClient, HTTP_STORE));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_HttpStore_NoDefaultCache()
        {
            var store = pathParser.Parse(HTTP_STORE);

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_HttpStore_Excluded()
        {
            pathParser =
                new SymbolPathParser(fakeFileSystem, fakeBinaryFileUtil, httpClient,
                                     crashReportClient, null, null, new string[] { "example.com" });

            var store = pathParser.Parse(HTTP_STORE);

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_StadiaStore_ExplicitCache()
        {
            var store = pathParser.Parse($"cache*{CACHE_A};{STADIA_STORE}");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(fakeFileSystem, CACHE_A));
            expected.AddStore(cache);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StadiaSymbolStore(fakeFileSystem, httpClient, crashReportClient));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_StadiaStore_DefaultCache()
        {
            pathParser = new SymbolPathParser(fakeFileSystem, fakeBinaryFileUtil, httpClient,
                                              crashReportClient, DEFAULT_CACHE, null);

            var store = pathParser.Parse(STADIA_STORE);

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, DEFAULT_CACHE));
            server.AddStore(new StadiaSymbolStore(fakeFileSystem, httpClient, crashReportClient));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_StadiaStore_NoDefaultCache()
        {
            var store = pathParser.Parse(STADIA_STORE);

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_Cache()
        {
            var store = pathParser.Parse($"cache*{CACHE_A}");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(fakeFileSystem, CACHE_A));
            expected.AddStore(cache);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_CacheWithMultiplePaths()
        {
            var store = pathParser.Parse($"cache*{CACHE_A}*{CACHE_B}");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(fakeFileSystem, CACHE_A));
            cache.AddStore(new StructuredSymbolStore(fakeFileSystem, CACHE_B));
            expected.AddStore(cache);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_DefaultCache()
        {
            pathParser = new SymbolPathParser(fakeFileSystem, fakeBinaryFileUtil, httpClient,
                                              crashReportClient, DEFAULT_CACHE, null);

            var store = pathParser.Parse($"cache*");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(fakeFileSystem, DEFAULT_CACHE));
            expected.AddStore(cache);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_NoDefaultCache()
        {
            var store = pathParser.Parse($"cache*");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var cache = new SymbolServer(isCache: true);
            expected.AddStore(cache);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_CacheWithHttpStore()
        {
            pathParser = new SymbolPathParser(fakeFileSystem, fakeBinaryFileUtil, httpClient,
                                              crashReportClient, DEFAULT_CACHE, null);

            var store = pathParser.Parse($"cache*{HTTP_STORE}");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(fakeFileSystem, DEFAULT_CACHE));
            cache.AddStore(new HttpSymbolStore(fakeFileSystem, httpClient, HTTP_STORE));
            expected.AddStore(cache);
            AssertEqualStores(expected, store);
            StringAssert.Contains("Warning", logSpy.GetOutput());
        }

        [Test]
        public void Parse_Srv()
        {
            var store = pathParser.Parse($"srv*{STORE_A}*{STORE_B}");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, STORE_A));
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, STORE_B));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_EmptySrv()
        {
            var store = pathParser.Parse("srv*");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var server = new SymbolServer(isCache: false);
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_SrvWithHttpStore()
        {
            var store = pathParser.Parse($"srv*{STORE_A}*{HTTP_STORE}");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, STORE_A));
            server.AddStore(new HttpSymbolStore(fakeFileSystem, httpClient, HTTP_STORE));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_SrvWithStadiaStore()
        {
            var store = pathParser.Parse($"srv*{STORE_A}*{STADIA_STORE}");

            // Stadia store not supported in symsrv configuration.
            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, STORE_A));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_DefaultStore()
        {
            pathParser = new SymbolPathParser(fakeFileSystem, fakeBinaryFileUtil, httpClient,
                                              crashReportClient, null, DEFAULT_STORE);

            var store = pathParser.Parse($"srv*");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, DEFAULT_STORE));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_NoDefaultStore()
        {
            var store = pathParser.Parse($"srv*");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var server = new SymbolServer(isCache: false);
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_DownstreamHttpStore()
        {
            var store = pathParser.Parse($"srv*{STORE_A}*{HTTP_STORE}*{STORE_B}");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, STORE_A));
            server.AddStore(new HttpSymbolStore(fakeFileSystem, httpClient, HTTP_STORE));
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, STORE_B));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
            StringAssert.Contains("Warning", logSpy.GetOutput());
        }

        [Test]
        public void Parse_DownstreamHttpStore_DefaultCache()
        {
            pathParser = new SymbolPathParser(fakeFileSystem, fakeBinaryFileUtil, httpClient,
                                              crashReportClient, DEFAULT_CACHE, null);

            var store = pathParser.Parse($"srv*{HTTP_STORE}*{STORE_B}");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, DEFAULT_CACHE));
            server.AddStore(new HttpSymbolStore(fakeFileSystem, httpClient, HTTP_STORE));
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, STORE_B));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
            StringAssert.Contains("Warning", logSpy.GetOutput());
        }

        [Test]
        public void Parse_SymSrv()
        {
            var store = pathParser.Parse($"symsrv*symsrv.dll*{STORE_A}*{STORE_B}");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, STORE_A));
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, STORE_B));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_EmptySymSrv()
        {
            var store = pathParser.Parse("symsrv*symsrv.dll*");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var server = new SymbolServer(isCache: false);
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_UnsupportedSymSrv()
        {
            var logSpy = new LogSpy();
            logSpy.Attach();

            var store = pathParser.Parse("symsrv*unsupported.dll*");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            AssertEqualStores(expected, store);
            StringAssert.Contains(Strings.UnsupportedSymbolServer("unsupported.dll"),
                                  logSpy.GetOutput());
        }

        [Test]
        public void Parse_MultipleElements()
        {
            var store = pathParser.Parse(
                $"cache*{CACHE_A};{FLAT_STORE};{INITIALIZED_STORE};srv*{STORE_A}*{STORE_B}");

            var expected = new SymbolStoreSequence(fakeBinaryFileUtil);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(fakeFileSystem, CACHE_A));
            expected.AddStore(cache);
            expected.AddStore(new FlatSymbolStore(fakeFileSystem, fakeBinaryFileUtil, FLAT_STORE));
            expected.AddStore(new StructuredSymbolStore(fakeFileSystem, INITIALIZED_STORE));
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, STORE_A));
            server.AddStore(new StructuredSymbolStore(fakeFileSystem, STORE_B));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        void AssertEqualStores(ISymbolStore expected, ISymbolStore actual) =>
            Assert.That(expected.DeepEquals(actual), () => {
                var serializerSettings = new JsonSerializerSettings {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Formatting = Formatting.Indented,
                };
                var expectedStr = JsonConvert.SerializeObject(expected, serializerSettings);
                var actualStr = JsonConvert.SerializeObject(actual, serializerSettings);
                return $"Expected: {expectedStr}\nBut was: {actualStr}";
            });
    }
}
