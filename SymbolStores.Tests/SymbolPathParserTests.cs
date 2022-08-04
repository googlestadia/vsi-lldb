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

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Net.Http;
using GgpGrpc.Cloud;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using TestsCommon.TestSupport;
using YetiCommon;

namespace SymbolStores.Tests
{
    [TestFixture]
    class SymbolPathParserTests
    {
        const string _defaultCache = @"C:\defaultCache";
        const string _defaultStore = @"C:\defaultStore";
        const string _cacheA = @"C:\cacheA";
        const string _cacheB = @"C:\cacheB";
        const string _storeA = @"C:\a";
        const string _storeB = @"C:\b";
        const string _initializedStore = @"C:\initialized";
        const string _stadiaStore = @"C:\stadia";
        const string _flatStore = @"C:\flat";
        const string _httpStore = @"http://example.com/symbols";

        MockFileSystem _fakeFileSystem;
        IModuleParser _moduleParser;
        HttpClient _httpClient;
        ICrashReportClient _crashReportClient;
        SymbolPathParser _pathParser;
        LogSpy _logSpy;

        [SetUp]
        public void SetUp()
        {
            _fakeFileSystem = new MockFileSystem();
            _moduleParser = Substitute.For<IModuleParser>();
            _httpClient = new HttpClient(new FakeHttpMessageHandler());
            _crashReportClient = Substitute.For<ICrashReportClient>();

            var store = new StructuredSymbolStore(_fakeFileSystem, _initializedStore);
            store.AddMarkerFileIfNeeded();

            _fakeFileSystem.AddFile(Path.Combine(_stadiaStore, StadiaSymbolStore.MarkerFileName),
                                   new MockFileData(""));
            _pathParser = new SymbolPathParser(_fakeFileSystem, _moduleParser, _httpClient,
                                              _crashReportClient, null, null);
            _logSpy = new LogSpy();
            _logSpy.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            _logSpy.Detach();
        }

        [Test]
        public void Parse_Null()
        {
            Assert.Throws<ArgumentNullException>(() => _pathParser.Parse(null));
        }

        [Test]
        public void Parse_Empty()
        {
            ISymbolStore store = _pathParser.Parse("");

            var expected = new SymbolStoreSequence(_moduleParser);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_EmptyPathElements()
        {
            ISymbolStore store = _pathParser.Parse(";;");

            var expected = new SymbolStoreSequence(_moduleParser);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_FlatStore()
        {
            ISymbolStore store = _pathParser.Parse(_flatStore);

            var expected = new SymbolStoreSequence(_moduleParser);
            expected.AddStore(new FlatSymbolStore(_fakeFileSystem, _flatStore));
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_StructuredStore()
        {
            ISymbolStore store = _pathParser.Parse(_initializedStore);

            var expected = new SymbolStoreSequence(_moduleParser);
            expected.AddStore(new StructuredSymbolStore(_fakeFileSystem, _initializedStore));
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_HttpStore_ExplicitCache()
        {
            MarkFoldersAsStructuredSymbolStores(new[] { _cacheA });
            ISymbolStore store = _pathParser.Parse($"cache*{_cacheA};{_httpStore}");

            var expected = new SymbolStoreSequence(_moduleParser);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(_fakeFileSystem, _cacheA));
            expected.AddStore(cache);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new HttpSymbolStore(_fakeFileSystem, _httpClient, _httpStore));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_HttpStore_DefaultCache()
        {
            _pathParser = new SymbolPathParser(_fakeFileSystem, _moduleParser, _httpClient,
                                              _crashReportClient, _defaultCache, null);

            ISymbolStore store = _pathParser.Parse(_httpStore);

            var expected = new SymbolStoreSequence(_moduleParser);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _defaultCache));
            server.AddStore(new HttpSymbolStore(_fakeFileSystem, _httpClient, _httpStore));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_HttpStore_NoDefaultCache()
        {
            ISymbolStore store = _pathParser.Parse(_httpStore);

            var expected = new SymbolStoreSequence(_moduleParser);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_HttpStore_Excluded()
        {
            _pathParser =
                new SymbolPathParser(_fakeFileSystem, _moduleParser, _httpClient,
                                     _crashReportClient, null, null, new[] { "example.com" });

            ISymbolStore store = _pathParser.Parse(_httpStore);

            var expected = new SymbolStoreSequence(_moduleParser);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_StadiaStore_ExplicitCache()
        {
            MarkFoldersAsStructuredSymbolStores(new[] { _cacheA });
            ISymbolStore store = _pathParser.Parse($"cache*{_cacheA};{_stadiaStore}");

            var expected = new SymbolStoreSequence(_moduleParser);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(_fakeFileSystem, _cacheA));
            expected.AddStore(cache);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StadiaSymbolStore(_fakeFileSystem, _httpClient,
                                                  _crashReportClient));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_StadiaStore_ExplicitCacheNotStructured()
        {
            ISymbolStore store = _pathParser.Parse($"cache*{_cacheA};{_stadiaStore}");

            var expected = new SymbolStoreSequence(_moduleParser);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(_fakeFileSystem, _cacheA));
            expected.AddStore(cache);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StadiaSymbolStore(_fakeFileSystem, _httpClient,
                                                  _crashReportClient));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_StadiaStore_DefaultCache()
        {
            MarkFoldersAsStructuredSymbolStores(new[] { _defaultCache });
            _pathParser = new SymbolPathParser(_fakeFileSystem, _moduleParser, _httpClient,
                                              _crashReportClient, _defaultCache, null);

            ISymbolStore store = _pathParser.Parse(_stadiaStore);

            var expected = new SymbolStoreSequence(_moduleParser);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _defaultCache));
            server.AddStore(new StadiaSymbolStore(_fakeFileSystem, _httpClient,
                                                  _crashReportClient));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_StadiaStore_NoDefaultCache()
        {
            ISymbolStore store = _pathParser.Parse(_stadiaStore);

            var expected = new SymbolStoreSequence(_moduleParser);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_Cache()
        {
            MarkFoldersAsStructuredSymbolStores(new[] { _cacheA });
            ISymbolStore store = _pathParser.Parse($"cache*{_cacheA}");

            var expected = new SymbolStoreSequence(_moduleParser);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(_fakeFileSystem, _cacheA));
            expected.AddStore(cache);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_CacheWithMultiplePaths()
        {
            MarkFoldersAsStructuredSymbolStores(new[] { _cacheA, _cacheB });
            ISymbolStore store = _pathParser.Parse($"cache*{_cacheA}*{_cacheB}");

            var expected = new SymbolStoreSequence(_moduleParser);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(_fakeFileSystem, _cacheA));
            cache.AddStore(new StructuredSymbolStore(_fakeFileSystem, _cacheB));
            expected.AddStore(cache);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_DefaultCache()
        {
            MarkFoldersAsStructuredSymbolStores(new[] { _defaultCache });
            _pathParser = new SymbolPathParser(_fakeFileSystem, _moduleParser, _httpClient,
                                              _crashReportClient, _defaultCache, null);

            ISymbolStore store = _pathParser.Parse($"cache*");

            var expected = new SymbolStoreSequence(_moduleParser);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(_fakeFileSystem, _defaultCache));
            expected.AddStore(cache);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_NoDefaultCache()
        {
            var store = _pathParser.Parse($"cache*");

            var expected = new SymbolStoreSequence(_moduleParser);
            var cache = new SymbolServer(isCache: true);
            expected.AddStore(cache);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_CacheWithHttpStore()
        {
            _pathParser = new SymbolPathParser(_fakeFileSystem, _moduleParser, _httpClient,
                                              _crashReportClient, _defaultCache, null);

            ISymbolStore store = _pathParser.Parse($"cache*{_httpStore}");

            var expected = new SymbolStoreSequence(_moduleParser);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(_fakeFileSystem, _defaultCache));
            cache.AddStore(new HttpSymbolStore(_fakeFileSystem, _httpClient, _httpStore));
            expected.AddStore(cache);
            AssertEqualStores(expected, store);
            StringAssert.Contains("Warning", _logSpy.GetOutput());
        }

        [Test]
        public void Parse_Srv()
        {
            MarkFoldersAsStructuredSymbolStores(new[] { _storeA, _storeB });
            ISymbolStore store = _pathParser.Parse($"srv*{_storeA}*{_storeB}");

            var expected = new SymbolStoreSequence(_moduleParser);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _storeA));
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _storeB));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_EmptySrv()
        {
            ISymbolStore store = _pathParser.Parse("srv*");

            var expected = new SymbolStoreSequence(_moduleParser);
            var server = new SymbolServer(isCache: false);
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_SrvWithHttpStore()
        {
            MarkFoldersAsStructuredSymbolStores(new[] { _storeA });
            ISymbolStore store = _pathParser.Parse($"srv*{_storeA}*{_httpStore}");

            var expected = new SymbolStoreSequence(_moduleParser);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _storeA));
            server.AddStore(new HttpSymbolStore(_fakeFileSystem, _httpClient, _httpStore));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_SrvWithStadiaStore()
        {
            MarkFoldersAsStructuredSymbolStores(new[] { _storeA });
            ISymbolStore store = _pathParser.Parse($"srv*{_storeA}*{_stadiaStore}");

            // Stadia store not supported in symsrv configuration.
            var expected = new SymbolStoreSequence(_moduleParser);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _storeA));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_DefaultStore()
        {
            MarkFoldersAsStructuredSymbolStores(new[] { _defaultStore });
            _pathParser = new SymbolPathParser(_fakeFileSystem, _moduleParser, _httpClient,
                                              _crashReportClient, null, _defaultStore);

            ISymbolStore store = _pathParser.Parse($"srv*");

            var expected = new SymbolStoreSequence(_moduleParser);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _defaultStore));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_NoDefaultStore()
        {
            ISymbolStore store = _pathParser.Parse($"srv*");

            var expected = new SymbolStoreSequence(_moduleParser);
            var server = new SymbolServer(isCache: false);
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_DownstreamHttpStore()
        {
            MarkFoldersAsStructuredSymbolStores(new[] { _storeA, _storeB });
            ISymbolStore store = _pathParser.Parse($"srv*{_storeA}*{_httpStore}*{_storeB}");

            var expected = new SymbolStoreSequence(_moduleParser);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _storeA));
            server.AddStore(new HttpSymbolStore(_fakeFileSystem, _httpClient, _httpStore));
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _storeB));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
            StringAssert.Contains("Warning", _logSpy.GetOutput());
        }

        [Test]
        public void Parse_DownstreamHttpStore_DefaultCache()
        {
            MarkFoldersAsStructuredSymbolStores(new[] { _defaultCache, _storeB });
            _pathParser = new SymbolPathParser(_fakeFileSystem, _moduleParser, _httpClient,
                                              _crashReportClient, _defaultCache, null);

            ISymbolStore store = _pathParser.Parse($"srv*{_httpStore}*{_storeB}");

            var expected = new SymbolStoreSequence(_moduleParser);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _defaultCache));
            server.AddStore(new HttpSymbolStore(_fakeFileSystem, _httpClient, _httpStore));
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _storeB));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
            StringAssert.Contains("Warning", _logSpy.GetOutput());
        }

        [Test]
        public void Parse_SymSrv()
        {
            MarkFoldersAsStructuredSymbolStores( new[] { _storeA, _storeB });

            ISymbolStore store = _pathParser.Parse($"symsrv*symsrv.dll*{_storeA}*{_storeB}");

            var expected = new SymbolStoreSequence(_moduleParser);
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _storeA));
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _storeB));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_EmptySymSrv()
        {
            ISymbolStore store = _pathParser.Parse("symsrv*symsrv.dll*");

            var expected = new SymbolStoreSequence(_moduleParser);
            var server = new SymbolServer(isCache: false);
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        [Test]
        public void Parse_UnsupportedSymSrv()
        {
            var logSpy = new LogSpy();
            logSpy.Attach();

            ISymbolStore store = _pathParser.Parse("symsrv*unsupported.dll*");

            var expected = new SymbolStoreSequence(_moduleParser);
            AssertEqualStores(expected, store);
            StringAssert.Contains(Strings.UnsupportedSymbolServer("unsupported.dll"),
                                  logSpy.GetOutput());
        }

        [Test]
        public void Parse_MultipleElements()
        {
            MarkFoldersAsStructuredSymbolStores(
                new [] { _storeA, _storeB, _cacheA, _initializedStore});
            ISymbolStore store = _pathParser.Parse(
                $"cache*{_cacheA};{_flatStore};{_initializedStore};srv*{_storeA}*{_storeB}");

            var expected = new SymbolStoreSequence(_moduleParser);
            var cache = new SymbolServer(isCache: true);
            cache.AddStore(new StructuredSymbolStore(_fakeFileSystem, _cacheA));
            expected.AddStore(cache);
            expected.AddStore(new FlatSymbolStore(_fakeFileSystem, _flatStore));
            expected.AddStore(new StructuredSymbolStore(_fakeFileSystem, _initializedStore));
            var server = new SymbolServer(isCache: false);
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _storeA));
            server.AddStore(new StructuredSymbolStore(_fakeFileSystem, _storeB));
            expected.AddStore(server);
            AssertEqualStores(expected, store);
        }

        void MarkFoldersAsStructuredSymbolStores(IEnumerable<string> folders)
        {
            foreach (string folder in folders)
            {
                _fakeFileSystem.AddFile(Path.Combine(folder, "pingme.txt"), "");
            }
        }

        void AssertEqualStores(ISymbolStore expected, ISymbolStore actual) =>
            Assert.That(expected.DeepEquals(actual), () => {
                var serializerSettings = new JsonSerializerSettings {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Formatting = Formatting.Indented,
                };
                string expectedStr = JsonConvert.SerializeObject(expected, serializerSettings);
                string actualStr = JsonConvert.SerializeObject(actual, serializerSettings);
                return $"Expected: {expectedStr}\nBut was: {actualStr}";
            });
    }
}
