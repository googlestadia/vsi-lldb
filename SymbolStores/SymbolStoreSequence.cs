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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YetiCommon;
using YetiCommon.Logging;

namespace SymbolStores
{
    /// <summary>
    /// Represents a list of symbol stores that are searched sequentially.
    /// If a store's IsCache property is set to true it will be used to cache files found later in
    /// the list. Each cache overrides previous caches, and caches do not cascade.
    /// </summary>
    public class SymbolStoreSequence : SymbolStoreBase
    {
        public bool HasCache => _stores.Any(s => s.IsCache);

        readonly IModuleParser _moduleParser;

        [JsonProperty("Stores")]
        readonly IList<ISymbolStore> _stores;

        readonly IList<FlatSymbolStore> _flatSymbolStores = new List<FlatSymbolStore>();

        public SymbolStoreSequence(IModuleParser moduleParser) : base(false, false)
        {
            _moduleParser = moduleParser;
            _stores = new List<ISymbolStore>();
        }

        public void AddStore(ISymbolStore store)
        {
            _stores.Add(store);
            if (store is FlatSymbolStore flatStore)
            {
                _flatSymbolStores.Add(flatStore);
            }
        }

        public override IEnumerable<ISymbolStore> Substores => _stores;

        public override Task<IFileReference> FindFileAsync(ModuleSearchQuery searchQuery,
                                                           TextWriter log) =>
            BuildId.IsNullOrEmpty(searchQuery.BuildId)
                ? SearchFlatSymbolStoresAsync(searchQuery, log)
                : SearchAllSymbolStoresAsync(searchQuery, log);

        async Task<IFileReference> SearchAllSymbolStoresAsync(ModuleSearchQuery searchQuery,
                                                              TextWriter log)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(searchQuery.Filename));

            ISymbolStore currentCache = null;
            foreach (ISymbolStore store in _stores)
            {
                IFileReference fileReference =
                    await store.FindFileAsync(searchQuery, log);

                if (fileReference != null)
                {
                    if (!store.IsCache && currentCache != null)
                    {
                        try
                        {
                            fileReference = await currentCache.AddFileAsync(
                                fileReference, searchQuery.Filename, searchQuery.BuildId,
                                searchQuery.ModuleFormat, log);
                        }
                        catch (Exception e)
                            when (e is NotSupportedException || e is SymbolStoreException ||
                                e is ArgumentException)
                        {
                            log.WriteLineAndTrace(e.Message);
                        }
                    }

                    if (VerifySymbolFile(fileReference, searchQuery, log))
                    {
                        return fileReference;
                    }
                }

                if (store.IsCache)
                {
                    currentCache = store;
                }
            }

            return null;
        }

        async Task<IFileReference> SearchFlatSymbolStoresAsync(ModuleSearchQuery searchQuery,
                                                               TextWriter log)
        {
            foreach (FlatSymbolStore store in _flatSymbolStores)
            {
                IFileReference fileReference = await store.FindFileAsync(searchQuery, log);

                if (VerifySymbolFile(fileReference, searchQuery, log))
                {
                    return fileReference;
                }
            }

            return null;
        }

        public override Task<IFileReference> AddFileAsync(IFileReference sourceFilepath,
                                                          string filename, BuildId buildId,
                                                          ModuleFormat moduleFormat,
                                                          TextWriter log) =>
            throw new NotSupportedException(Strings.CopyToStoreSequenceNotSupported);

        public override bool DeepEquals(ISymbolStore otherStore)
        {
            return otherStore is SymbolStoreSequence other
                && _stores.Count == other._stores.Count
                && _stores.Zip(other._stores, Tuple.Create)
                    .All(x => x.Item1.DeepEquals(x.Item2));
        }

        bool VerifySymbolFile(IFileReference fileReference, ModuleSearchQuery query,
                              TextWriter log)
        {
            if (fileReference == null)
            {
                return false;
            }

            string filepath = fileReference.Location;
            if (!fileReference.IsFilesystemLocation)
            {
                log.WriteLineAndTrace(ErrorStrings.FileNotOnFilesystem(filepath));
                return false;
            }

            if (query.ModuleFormat == ModuleFormat.Elf &&
                !_moduleParser.IsValidElf(filepath, query.RequireDebugInfo,
                                          out string errorMessage))
            {
                log.WriteLineAndTrace(errorMessage);
                return false;
            }

            if (BuildId.IsNullOrEmpty(query.BuildId))
            {
                return true;
            }

            BuildIdInfo actualBuildId =
                _moduleParser.ParseBuildIdInfo(filepath, query.ModuleFormat);
            if (actualBuildId.HasError)
            {
                log.WriteLineAndTrace(actualBuildId.Error);
                return false;
            }

            if (actualBuildId.Data.Matches(query.BuildId, query.ModuleFormat))
            {
                return true;
            }

            string buildIdMismatch =
                Strings.BuildIdMismatch(filepath, query.BuildId, actualBuildId.Data,
                                        query.ModuleFormat);
            log.WriteLineAndTrace(buildIdMismatch);
            return false;
        }
    }
}
