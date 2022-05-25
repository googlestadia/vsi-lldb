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
        public bool HasCache => _stores.Any(s => s.IsCache == true);

        readonly IModuleParser _moduleParser;

        [JsonProperty("Stores")]
        readonly IList<ISymbolStore> _stores;

        public SymbolStoreSequence(IModuleParser moduleParser) : base(false, false)
        {
            _moduleParser = moduleParser;
            _stores = new List<ISymbolStore>();
        }

        public void AddStore(ISymbolStore store)
        {
            _stores.Add(store);
        }


        public override IEnumerable<ISymbolStore> Substores => _stores;

        public override async Task<IFileReference> FindFileAsync(ModuleSearchQuery searchQuery,
                                                                 TextWriter log)
        {
            if (string.IsNullOrEmpty(searchQuery.FileName))
            {
                throw new ArgumentException(Strings.FilenameNullOrEmpty,
                                            nameof(searchQuery.FileName));
            }

            ISymbolStore currentCache = null;

            foreach (var store in _stores)
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
                                fileReference,
                                searchQuery.FileName,
                                searchQuery.BuildId, log);
                        }
                        catch (Exception e)
                            when (e is NotSupportedException || e is SymbolStoreException ||
                                  e is ArgumentException)
                        {
                            log.WriteLineAndTrace(e.Message);
                        }
                    }

                    if (!fileReference.IsFilesystemLocation ||
                        VerifySymbolFile(fileReference.Location, searchQuery.BuildId,
                                         searchQuery.RequireDebugInfo, log))
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

        public override Task<IFileReference> AddFileAsync(IFileReference sourceFilepath,
                                                          string filename, BuildId buildId,
                                                          TextWriter log) =>
            throw new NotSupportedException(Strings.CopyToStoreSequenceNotSupported);

        public override bool DeepEquals(ISymbolStore otherStore)
        {
            return otherStore is SymbolStoreSequence other
                && _stores.Count == other._stores.Count
                && _stores.Zip(other._stores, Tuple.Create)
                    .All(x => x.Item1.DeepEquals(x.Item2));
        }

        bool VerifySymbolFile(string filepath, BuildId buildId, bool isDebugInfoFile,
                              TextWriter log)
        {
            if (!_moduleParser.IsValidElf(filepath, isDebugInfoFile, out string errorMessage))
            {
                log.WriteLineAndTrace(errorMessage);
                return false;
            }

            if (buildId == BuildId.Empty)
            {
                return true;
            }

            BuildIdInfo actualBuildId = _moduleParser.ParseBuildIdInfo(filepath, true);
            if (actualBuildId.HasError)
            {
                log.WriteLineAndTrace(actualBuildId.Error);
                return false;
            }

            if (actualBuildId.Data == buildId)
            {
                return true;
            }

            string buildIdMismatch =
                Strings.BuildIdMismatch(filepath, buildId, actualBuildId.Data);
            log.WriteLineAndTrace(buildIdMismatch);
            return false;
        }
    }
}
