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
using System.Linq;

namespace SymbolStores
{
    /// <summary>
    /// Extension method for working with symbol stores
    /// </summary>
    public static class SymbolStoreExtensions
    {
        /// <summary>
        /// Recursively enumerates the given store and all of its substores
        /// </summary>
        public static IEnumerable<ISymbolStore> GetAllStores(this ISymbolStore store)
        {
            yield return store;
            foreach (var substore in store.Substores.SelectMany(GetAllStores))
            {
                yield return substore;
            }
        }
    }
}
