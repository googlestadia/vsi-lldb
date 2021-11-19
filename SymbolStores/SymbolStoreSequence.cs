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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.Logging;

namespace SymbolStores
{
    // Represents a list of symbol stores that are searched sequentially.
    // If a store's IsCache property is set to true it will be used to cache files found later in
    // the list. Each cache overrides previous caches, and caches do not cascade.
    public class SymbolStoreSequence : SymbolStoreBase
    {
        public bool HasCache => _stores.Any(s => s.IsCache == true);

        readonly IBinaryFileUtil _binaryFileUtil;

        [JsonProperty("Stores")]
        readonly IList<ISymbolStore> _stores;

        public SymbolStoreSequence(IBinaryFileUtil binaryFileUtil) : base(false, false)
        {
            _binaryFileUtil = binaryFileUtil;
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

            ISymbolStore currentCache = null;

            foreach (var store in _stores)
            {
                IFileReference fileReference =
                    await store.FindFileAsync(filename, buildId, false, log, forceLoad);

                if (fileReference != null)
                {

                    if (!store.IsCache && currentCache != null)
                    {
                        try
                        {
                            fileReference = await currentCache.AddFileAsync(fileReference, filename,
                                                                            buildId, log);
                        }
                        catch (Exception e)
                            when (e is NotSupportedException || e is SymbolStoreException ||
                                  e is ArgumentException)
                        {
                            await log.WriteLineAndTraceAsync(e.Message);
                        }
                    }

                    if (!fileReference.IsFilesystemLocation ||
                        await VerifySymbolFileAsync(fileReference.Location, buildId,
                                                    isDebugInfoFile, log))
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
                                                          TextWriter log)
        {
            throw new NotSupportedException(Strings.CopyToStoreSequenceNotSupported);
        }

        public override bool DeepEquals(ISymbolStore otherStore)
        {
            return otherStore is SymbolStoreSequence other
                && _stores.Count == other._stores.Count
                && _stores.Zip(other._stores, Tuple.Create)
                    .All(x => x.Item1.DeepEquals(x.Item2));
        }
#endregion

        async Task<bool> VerifySymbolFileAsync(string filepath, BuildId buildId,
                                               bool isDebugInfoFile, TextWriter log)
        {
            try
            {
                await _binaryFileUtil.VerifySymbolFileAsync(filepath, isDebugInfoFile);
                if (buildId != BuildId.Empty)
                {
                    var actualBuildId = await _binaryFileUtil.ReadBuildIdAsync(filepath);
                    if (actualBuildId != buildId)
                    {
                        string errorMessage =
                            Strings.BuildIdMismatch(filepath, buildId, actualBuildId);
                        await log.WriteLineAndTraceAsync(errorMessage);
                        return false;
                    }
                }
            }
            catch (BinaryFileUtilException e)
            {
                await log.WriteLineAndTraceAsync(e.Message);
                return false;
            }
            return true;
        }
    }
}
