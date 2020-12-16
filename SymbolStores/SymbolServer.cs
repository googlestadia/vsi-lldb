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
    // Represents a cascading list of symbol stores. Each store is searched in turn, and when
    // the requested file is found, it is copied to all previously searched stores.
    public class SymbolServer : SymbolStoreBase
    {
        public class Factory
        {
            public Factory() { }

            public virtual SymbolServer Create(bool isCache = false)
            {
                return new SymbolServer(isCache);
            }
        }

        public bool IsEmpty => stores.Count == 0;

        [JsonProperty("Stores")]
        IList<ISymbolStore> stores;

        SymbolServer(bool isCache) : base(true, isCache)
        {
            stores = new List<ISymbolStore>();
        }

        public void AddStore(ISymbolStore store)
        {
            stores.Add(store);
        }

        #region SymbolStoreBase functions

        public override IEnumerable<ISymbolStore> Substores => stores;

        public override IFileReference FindFile(string filename, BuildId buildId,
                                                bool isDebugInfoFile, TextWriter log)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FilenameNullOrEmpty, "filename");
            }

            for (int i = 0; i < stores.Count; ++i)
            {
                var fileReference = stores[i].FindFile(filename, buildId, isDebugInfoFile, log);
                if (fileReference != null)
                {
                    return Cascade(fileReference, filename, buildId, i - 1, log) ?? fileReference;
                }
            }

            return null;
        }

        public override IFileReference AddFile(IFileReference source, string filename,
            BuildId buildId, TextWriter log)
        {
            if (source == null)
            {
                throw new ArgumentException(Strings.FailedToCopyToSymbolServer(filename,
                    Strings.SourceFileReferenceNull), "sourceFilepath");
            }
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException(Strings.FailedToCopyToSymbolServer(filename,
                    Strings.FilenameNullOrEmpty), "filename");
            }
            if (buildId == BuildId.Empty)
            {
                throw new ArgumentException(Strings.FailedToCopyToSymbolServer(filename,
                    Strings.EmptyBuildId), "buildId");
            }

            var fileReference = Cascade(source, filename, buildId, stores.Count - 1, log);

            if (fileReference == null)
            {
                throw new SymbolStoreException(Strings.FailedToCopyToSymbolServer(filename));
            }

            return fileReference;
        }

        public override bool DeepEquals(ISymbolStore otherStore)
        {
            var other = otherStore as SymbolServer;
            return other != null && IsCache == other.IsCache && stores.Count == other.stores.Count
                && stores.Zip(other.stores, (a, b) => Tuple.Create(a, b)).All(
                    x => x.Item1.DeepEquals(x.Item2));
        }

        #endregion

        // Copies the referenced file to the store at `index`, and then from there to each previous
        // store in reverse order.
        // Returns a reference to the file in the last store that it is succesfully copied to, or
        // null if it was not succesfully copied.
        IFileReference Cascade(IFileReference sourcefileReference, string filename,
            BuildId buildId, int index, TextWriter log)
        {
            IFileReference fileReference = null;
            for (int i = index; i >= 0; --i)
            {
                try
                {
                    sourcefileReference = fileReference = stores[i].AddFile(sourcefileReference,
                        filename, buildId, log);
                }
                catch (Exception e) when (e is NotSupportedException ||
                    e is SymbolStoreException || e is ArgumentException)
                {
                    Trace.WriteLine(e.Message);
                    log.WriteLine(e.Message);
                }
            }
            return fileReference;
        }
    }
}
