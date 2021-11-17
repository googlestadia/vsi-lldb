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

﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YetiCommon;

namespace SymbolStores
{
    /// <summary>
    /// Abstract base class for all symbol stores.
    /// </summary>
    public abstract class SymbolStoreBase : ISymbolStore
    {
        public bool SupportsAddingFiles { get; }
        public bool IsCache { get; }

        /// <summary>
        /// Default implementation for any symbol store that does not contain substores.
        /// </summary>
        public virtual IEnumerable<ISymbolStore> Substores => Enumerable.Empty<ISymbolStore>();

        public SymbolStoreBase(bool supportsAddingFiles, bool isCache)
        {
            SupportsAddingFiles = supportsAddingFiles;
            IsCache = isCache;
        }

        public abstract Task<IFileReference> FindFileAsync(string filename, BuildId buildId,
                                                           bool isDebugInfoFile,
                                                           TextWriter logWriter);

        /// <summary>
        ///  Default implementation that delegates to FindFile(string, BuildId, bool, TextWriter).
        /// </summary>
        public Task<IFileReference> FindFileAsync(string filename, BuildId buildId)
        {
            return FindFileAsync(filename, buildId, false, TextWriter.Null);
        }

        public abstract Task<IFileReference> AddFileAsync(IFileReference source, string filename,
                                                          BuildId buildId, TextWriter logWriter);

        /// <summary>
        ///  Default implementation that delegates to AddFile(string, BuildId, TextWriter).
        /// </summary>
        public Task<IFileReference> AddFileAsync(IFileReference source, string filename,
                                                 BuildId buildId)
        {
            return AddFileAsync(source, filename, buildId, TextWriter.Null);
        }

        public abstract bool DeepEquals(ISymbolStore other);
    }
}
