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
using System.IO;
using System.Threading.Tasks;
using YetiCommon;

namespace SymbolStores
{
    // A SymbolStore which implements FindFile as a noop.
    // Intended to act as a placeholder when an actual symbol store has not been provided.
    public class NullSymbolStore : SymbolStoreBase
    {
        public NullSymbolStore() : base(false, false)
        {
        }

        public override Task<IFileReference> AddFileAsync(IFileReference source, string filename,
                                                          BuildId buildId, TextWriter logWriter)
        {
            throw new NotImplementedException();
        }

        public override bool DeepEquals(ISymbolStore other)
        {
            return other is NullSymbolStore;
        }

        public override Task<IFileReference> FindFileAsync(string filename, BuildId buildId,
                                                           bool isDebugInfoFile,
                                                           TextWriter logWriter)
        {
            return Task.FromResult<IFileReference>(null);
        }
    }
}
