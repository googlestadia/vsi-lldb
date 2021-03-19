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

﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using YetiCommon;

namespace SymbolStores
{
    // Abstract base class for all symbol stores.
    public abstract class SymbolStoreBase : ISymbolStore
    {
        public bool SupportsAddingFiles { get; }
        public bool IsCache { get; }

        // Default implementation for any symbol store that does not contain substores.
        public virtual IEnumerable<ISymbolStore> Substores => Enumerable.Empty<ISymbolStore>();

        public SymbolStoreBase(bool supportsAddingFiles, bool isCache)
        {
            SupportsAddingFiles = supportsAddingFiles;
            IsCache = isCache;
        }

        public abstract IFileReference FindFile(string filename, BuildId buildId,
                                                bool isDebugInfoFile, TextWriter logWriter);

        // Default implementation that delegates to FindFile(string, BuildId, bool, TextWriter)
        public IFileReference FindFile(string filename, BuildId buildId)
        {
            return FindFile(filename, buildId, false, TextWriter.Null);
        }

        public abstract IFileReference AddFile(IFileReference source, string filename,
            BuildId buildId, TextWriter logWriter);

        // Default implementation that delegates to AddFile(string, BuildId, TextWriter)
        public IFileReference AddFile(IFileReference source, string filename, BuildId buildId)
        {
            return AddFile(source, filename, buildId, TextWriter.Null);
        }

        public abstract bool DeepEquals(ISymbolStore other);
    }
}
