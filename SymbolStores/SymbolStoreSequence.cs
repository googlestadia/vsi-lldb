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
using System.IO;
using System.Linq;
using YetiCommon;

namespace SymbolStores
{
    // Represents a list of symbol stores that are searched sequentially.
    // If a store's IsCache property is set to true it will be used to cache files found later in
    // the list. Each cache overrides previous caches, and caches do not cascade.
    public class SymbolStoreSequence : SymbolStoreBase
    {
        public class Factory
        {
            readonly IBinaryFileUtil _binaryFileUtil;

            public Factory(IBinaryFileUtil binaryFileUtil)
            {
                _binaryFileUtil = binaryFileUtil;
            }

            public virtual SymbolStoreSequence Create()
            {
                return new SymbolStoreSequence(_binaryFileUtil);
            }
        }

        public bool HasCache => _stores.Any(s => s.IsCache == true);

        [JsonProperty("Stores")]
        readonly IList<ISymbolStore> _stores;
        readonly IBinaryFileUtil _binaryFileUtil;

        SymbolStoreSequence(IBinaryFileUtil binaryFileUtil) : base(false, false)
        {
            _stores = new List<ISymbolStore>();
            _binaryFileUtil = binaryFileUtil;
        }

        public void AddStore(ISymbolStore store)
        {
            _stores.Add(store);
        }

        #region SymbolStoreBase functions

        public override IEnumerable<ISymbolStore> Substores => _stores;

        public override IFileReference FindFile(string filename, BuildId buildId,
                                                bool isDebugInfoFile, TextWriter log)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FilenameNullOrEmpty, "filename");
            }

            ISymbolStore currentCache = null;

            foreach (var store in _stores)
            {
                IFileReference fileReference = store.FindFile(filename, buildId, false, log);

                if (fileReference != null)
                {

                    if (!store.IsCache && currentCache != null)
                    {
                        try
                        {
                            fileReference =
                                currentCache.AddFile(fileReference, filename, buildId, log);
                        }
                        catch (Exception e) when (e is NotSupportedException ||
                            e is SymbolStoreException || e is ArgumentException)
                        {
                            Trace.WriteLine(e.Message);
                            log.WriteLine(e.Message);
                        }
                    }

                    if (!fileReference.IsFilesystemLocation ||
                        VerifySymbolFile(fileReference.Location, buildId, isDebugInfoFile, log))
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

        public override IFileReference AddFile(IFileReference sourceFilepath, string filename,
            BuildId buildId, TextWriter log)
        {
            throw new NotSupportedException(Strings.CopyToStoreSequenceNotSupported);
        }

        public override bool DeepEquals(ISymbolStore otherStore)
        {
            var other = otherStore as SymbolStoreSequence;
            return other != null && _stores.Count == other._stores.Count &&
                   _stores.Zip(other._stores, (a, b) => Tuple.Create(a, b))
                       .All(x => x.Item1.DeepEquals(x.Item2));
        }
        #endregion

        bool VerifySymbolFile(string filepath, BuildId buildId, bool isDebugInfoFile,
                              TextWriter log)
        {
            try
            {
                _binaryFileUtil.VerifySymbolFile(filepath, isDebugInfoFile);
                if (buildId != BuildId.Empty)
                {
                    var actualBuildId = _binaryFileUtil.ReadBuildId(filepath);
                    if (actualBuildId != buildId)
                    {
                        string errorMessage =
                            Strings.BuildIdMismatch(filepath, buildId, actualBuildId);
                        Trace.WriteLine(errorMessage);
                        log.WriteLine(errorMessage);
                        return false;
                    }
                }
            }
            catch (BinaryFileUtilException e)
            {
                Trace.WriteLine(e.Message);
                log.WriteLine(e.Message);
                return false;
            }
            return true;
        }
    }
}
