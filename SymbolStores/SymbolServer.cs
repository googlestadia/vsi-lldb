// Copyright 2021 Google LLC
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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.Logging;

namespace SymbolStores
{
    /// <summary>
    /// Represents a cascading list of symbol stores. Each store is searched in turn, and when
    /// the requested file is found, it is copied to all previously searched stores.
    /// </summary>
    public class SymbolServer : SymbolStoreBase
    {
        public bool IsEmpty => _stores.Count == 0;

        [JsonProperty("Stores")]
        readonly IList<ISymbolStore> _stores;

        public SymbolServer(bool isCache = false) : base(true, isCache)
        {
            _stores = new List<ISymbolStore>();
        }

        public void AddStore(ISymbolStore store)
        {
            _stores.Add(store);
        }

#region SymbolStoreBase functions

        public override IEnumerable<ISymbolStore> Substores => _stores;

        public override async Task<IFileReference> FindFileAsync(string filename, BuildId buildId,
                                                                 bool isDebugInfoFile,
                                                                 TextWriter log,
                                                                 bool forceLoad)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FilenameNullOrEmpty, nameof(filename));
            }

            for (int i = 0; i < _stores.Count; ++i)
            {
                var fileReference =
                    await _stores[i].FindFileAsync(filename, buildId, isDebugInfoFile,
                                                   log, forceLoad);
                if (fileReference != null)
                {
                    var cascadeFileRef =
                        await CascadeAsync(fileReference, filename, buildId, i - 1, log);
                    return cascadeFileRef ?? fileReference;
                }
            }

            return null;
        }

        public override async Task<IFileReference> AddFileAsync(IFileReference source,
                                                                string filename, BuildId buildId,
                                                                TextWriter log)
        {
            if (source == null)
            {
                throw new ArgumentException(
                    Strings.FailedToCopyToSymbolServer(filename, Strings.SourceFileReferenceNull),
                    nameof(source));
            }
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(
                    Strings.FailedToCopyToSymbolServer(filename, Strings.FilenameNullOrEmpty),
                    nameof(filename));
            }
            if (buildId == BuildId.Empty)
            {
                throw new ArgumentException(
                    Strings.FailedToCopyToSymbolServer(filename, Strings.EmptyBuildId),
                    nameof(buildId));
            }

            var fileReference =
                await CascadeAsync(source, filename, buildId, _stores.Count - 1, log);
            if (fileReference == null)
            {
                throw new SymbolStoreException(Strings.FailedToCopyToSymbolServer(filename));
            }

            return fileReference;
        }

        public override bool DeepEquals(ISymbolStore otherStore)
        {
            return otherStore is SymbolServer other 
                && IsCache == other.IsCache 
                && _stores.Count == other._stores.Count 
                && _stores.Zip(other._stores, (a, b) => Tuple.Create(a, b))
                       .All(x => x.Item1.DeepEquals(x.Item2));
        }

#endregion

        // Copies the referenced file to the store at `index`, and then from there to each previous
        // store in reverse order.
        // Returns a reference to the file in the last store that it is successfully copied to, or
        // null if it was not successfully copied.
        async Task<IFileReference> CascadeAsync(IFileReference sourceFileReference, string filename,
                                                BuildId buildId, int index, TextWriter log)
        {
            IFileReference fileReference = null;
            for (int i = index; i >= 0; --i)
            {
                try
                {
                    sourceFileReference = fileReference =
                        await _stores[i].AddFileAsync(sourceFileReference, filename, buildId, log);
                }
                catch (Exception e) when (e is NotSupportedException || e is SymbolStoreException ||
                                          e is ArgumentException)
                {
                    await log.WriteLogAsync(e.Message);
                }
            }
            return fileReference;
        }
    }
}
