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

﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;

namespace SymbolStores
{
    // Parses lists of symbol paths, given in the format used by the _NT_SYMBOL_PATH environment
    // variable.
    // This format consists of a semicolon-seperated list of path elements, in which each path
    // element can either represent a symbol server, a cache, or a symbol store path.
    // If the path element begins with "srv*" or "symsrv*symsrv.dll*" it represents a server, and
    // the remainder of the path element represents an asterisk-seperated list of symbol store
    // paths.
    // If the path element begins with "cache*" it represents a cache, but uses the same syntax as
    // regular symbol servers.
    // If the path element does not begin with "srv*", "symsrv*", or "cache*", then it is an
    // unadorned path that represents either a flat or structured symbol store. Structured stores
    // are disambiguated by the existance of a marker file named "pingme.txt".
    public class SymbolPathParser
    {
        IFileSystem fileSystem;
        StructuredSymbolStore.Factory structuredStoreFactory;
        FlatSymbolStore.Factory flatStoreFactory;
        SymbolStoreSequence.Factory storeSequenceFactory;
        SymbolServer.Factory symbolServerFactory;
        HttpSymbolStore.Factory httpStoreFactory;
        StadiaSymbolStore.Factory stadiaStoreFactory;

        // Used to replace any empty cache path elements that are encountered, and as a cache for
        // any HTTP stores that would otherwise not be cached.
        string defaultCachePath;

        // Used to replace any empty symbol server path elements that are encountered.
        string defaultSymbolStorePath;

        // Any http store where the hostname matches an entry in this list is skipped and not
        // parsed.
        ISet<string> hostExcludeList;

        public SymbolPathParser(IFileSystem fileSystem,
            StructuredSymbolStore.Factory structuredStoreFactory,
            FlatSymbolStore.Factory flatStoreFactory,
            SymbolStoreSequence.Factory storeSequenceFactory,
            SymbolServer.Factory symbolServerFactory,
            HttpSymbolStore.Factory httpStoreFactory,
            StadiaSymbolStore.Factory stadiaStoreFactory,
            string defaultCachePath, string defaultSymbolStorePath,
            IEnumerable<string> hostExcludeList = null)
        {
            this.fileSystem = fileSystem;
            this.structuredStoreFactory = structuredStoreFactory;
            this.flatStoreFactory = flatStoreFactory;
            this.storeSequenceFactory = storeSequenceFactory;
            this.symbolServerFactory = symbolServerFactory;
            this.httpStoreFactory = httpStoreFactory;
            this.defaultCachePath = defaultCachePath;
            this.defaultSymbolStorePath = defaultSymbolStorePath;
            this.stadiaStoreFactory = stadiaStoreFactory;
            this.hostExcludeList = new HashSet<string>(
                hostExcludeList ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
        protected SymbolPathParser() { }

        // symbolPaths is expected to be in the format described above
        public virtual ISymbolStore Parse(string symbolPaths)
        {
            if (symbolPaths == null)
            {
                throw new ArgumentNullException("symbolPaths");
            }

            Trace.WriteLine($"Parsing symbol paths: '{symbolPaths}'");

            var storeSequence = storeSequenceFactory.Create();

            foreach (var pathElement in symbolPaths.Split(';'))
            {
                if (string.IsNullOrEmpty(pathElement))
                {
                    continue;
                }

                var components = pathElement.Split('*');

                if (components.Length > 2 &&
                    string.Equals(components[0], "symsrv", StringComparison.OrdinalIgnoreCase))
                {
                    // "symsrv*" is followed by the name of the symbol server dll. "symsrv.dll" is
                    // the name of the dll whose behavior we're trying to imitate.
                    if (string.Equals(components[1], "symsrv.dll",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        AddSymbolServer(storeSequence, components.Skip(2).ToList(),
                            defaultSymbolStorePath);
                    }
                    else
                    {
                        Trace.WriteLine(Strings.UnsupportedSymbolServer(components[1]));
                    }
                }
                else if (components.Length > 1 &&
                    string.Equals(components[0], "srv", StringComparison.OrdinalIgnoreCase))
                {
                    // "srv*" is a shortform for "symsrv*symsrv.dll*"
                    AddSymbolServer(storeSequence, components.Skip(1).ToList(),
                        defaultSymbolStorePath);
                }
                else if (components.Length > 1 &&
                    string.Equals(components[0], "cache", StringComparison.OrdinalIgnoreCase))
                {
                    // Any empty cache paths are replaced with the defalt cache path
                    AddSymbolServer(storeSequence, components.Skip(1).ToList(), defaultCachePath,
                        isCache: true);
                }
                // The path element doesn't refer to a symbol server, but it could still be
                // a Stadia store, an http store, a structured symbol store, or a flat directory.
                else if (StadiaSymbolStore.IsStadiaStore(fileSystem, pathElement))
                {
                    // Stadia stores need to be part of an implicit symbol server just in case
                    // we need to add a local cache downstream from the remote store.
                    SymbolServer server = symbolServerFactory.Create();
                    if (TryAddStadiaStore(server, storeSequence.HasCache))
                    {
                        storeSequence.AddStore(server);
                    }
                }
                else if (HttpSymbolStore.IsHttpStore(pathElement))
                {
                    // HTTP stores need to be part of an implicit symbol server just in case
                    // we need to add a local cache downstream from the remote store.
                    SymbolServer server = symbolServerFactory.Create();
                    if (TryAddHttpStore(server, pathElement, storeSequence.HasCache))
                    {
                        storeSequence.AddStore(server);
                    }
                }
                else if (StructuredSymbolStore.IsStructuredStore(fileSystem, pathElement))
                {
                    storeSequence.AddStore(structuredStoreFactory.Create(pathElement));
                }
                else
                {
                    storeSequence.AddStore(flatStoreFactory.Create(pathElement));
                }
            }

            var jsonRepresentation = JsonConvert.SerializeObject(storeSequence,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
            Trace.WriteLine($"Symbol path parsing result: {jsonRepresentation}");

            return storeSequence;
        }

        void AddSymbolServer(SymbolStoreSequence storeSequence, IList<string> paths,
            string defaultPath, bool isCache = false)
        {
            SymbolServer server = symbolServerFactory.Create(isCache);

            for (int i = 0; i < paths.Count; ++i)
            {
                var path = paths[i];

                if (StadiaSymbolStore.IsStadiaStore(fileSystem, path))
                {
                    Trace.WriteLine("Warning: Stadia store not supported in symbol server " +
                        "configuration; it will be ignored.");
                    continue;
                }
                else if (HttpSymbolStore.IsHttpStore(path))
                {
                    bool hasDownstreamCache = storeSequence.HasCache || !server.IsEmpty;
                    if (!TryAddHttpStore(server, path, hasDownstreamCache))
                    {
                        continue;
                    }
                    if (isCache)
                    {
                        Trace.WriteLine($"Warning: '{path}' is being used as a symbol cache, " +
                            "but caching symbols in HTTP symbol stores is not supported.");
                    }
                    else if (i != paths.Count - 1)
                    {
                        Trace.WriteLine($"Warning: '{path}' is being used as a downstream " +
                            "store, but copying symbols to HTTP symbol stores is not supported.");
                    }
                }
                else if (!string.IsNullOrEmpty(path))
                {
                    server.AddStore(structuredStoreFactory.Create(path));
                }
                else if (!string.IsNullOrEmpty(defaultPath))
                {
                    server.AddStore(structuredStoreFactory.Create(defaultPath));
                }
            }
            storeSequence.AddStore(server);
        }

        bool TryAddHttpStore(SymbolServer server, string path, bool hasDownstreamCache)
        {
            // HTTP stores need a cache so that we can generate a local file reference for LLDB.
            if (!hasDownstreamCache && !TryAddDefaultCache(server, path))
            {
                return false;
            }
            if (hostExcludeList.Contains(new Uri(path, UriKind.Absolute).Host))
            {
                Trace.WriteLine(
                    $"Skipped parsing http store '{path}' due to host excludelist.");
                return false;
            }
            server.AddStore(httpStoreFactory.Create(path));
            return true;
        }

        bool TryAddStadiaStore(SymbolServer server, bool hasDownstreamCache)
        {
            // Stadia stores need a cache so that we can generate a local file reference for LLDB.
            if (!hasDownstreamCache && !TryAddDefaultCache(server, "Stadia Symbol Store"))
            {
                return false;
            }
            server.AddStore(stadiaStoreFactory.Create());
            return true;
        }

        bool TryAddDefaultCache(SymbolServer server, string upstreamPathForLogging)
        {
            // TODO: require the default cache path to be non-empty.
            if (string.IsNullOrEmpty(defaultCachePath))
            {
                Trace.WriteLine($"'{upstreamPathForLogging}' must be cached, " +
                    "but no downstream cache exists and no default cache path has been provided.");
                return false;
            }
            server.AddStore(structuredStoreFactory.Create(defaultCachePath));
            Trace.WriteLine($"Automatically added default cache as '{upstreamPathForLogging}' " +
                "would not otherwise be cached.");
            return true;
        }
    }
}
