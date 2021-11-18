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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YetiCommon;

namespace SymbolStores
{
    /// <summary>
    /// Represents some sort of location where symbol files are stored.
    /// </summary>
    public interface ISymbolStore
    {
        /// <summary>
        /// Whether the symbol store implementation supports adding files.
        /// </summary>
        bool SupportsAddingFiles { get; }

        /// <summary>
        /// Whether or not the symbol store should be treated as a cache
        /// </summary>
        bool IsCache { get; }

        /// <summary>
        /// A list of the symbol stores that this store delegates to, or an empty list if it does
        /// not delegate to other stores.
        /// </summary>
        IEnumerable<ISymbolStore> Substores { get; }

        /// <summary>
        /// Searches for a symbol file with the filename `filename` and build ID `buildId`.
        /// Returns an IFileReference representing the matching file, or null if no such file
        /// exists.
        /// If `buildId` is empty, implementors may return a file without checking the file's build
        /// ID.
        /// </summary>
        /// <param name="log">Entity for storing module's logs.</param>
        /// <param name="forceLoad">Whether we need to reload a module from the remote moduleStore.
        /// </param>
        /// <param name="filename">Module name.</param>
        /// <param name="buildId">Build ID.</param>
        /// <param name="isDebugInfoFile">If set to true, then any validity checking of the file
        /// should also ensure that the file contains a debug info section.</param>
        /// <exception cref="ArgumentNullException">If `filename` is null.</exception>
        Task<IFileReference> FindFileAsync(string filename, BuildId buildId, bool isDebugInfoFile,
                                           TextWriter log, bool forceLoad);
        
        Task<IFileReference> FindFileAsync(string filename, BuildId buildId);

        /// <summary>
        /// Copies the file represented by `source` into the store. The file in the store will be
        /// given the filename `filename`, and may potentially be indexed by the given build ID.
        /// </summary>
        /// <returns>An IFileReference representing the newly copied file.</returns>
        /// <exception cref="SymbolStoreException">On failure, including if the file cannot be
        /// added because it already exists.
        /// </exception>
        /// <exception cref="ArgumentNullException">If `source` or `filename` are null, or if
        /// `buildId` is empty.
        /// </exception>
        /// <exception cref="NotSupportedException">if the store does not support adding files.
        /// </exception>
        Task<IFileReference> AddFileAsync(IFileReference source, string filename, BuildId buildId,
                                          TextWriter log);
        Task<IFileReference> AddFileAsync(IFileReference source, string filename, BuildId buildId);

        /// <summary>
        /// Deep value equality
        /// </summary>
        bool DeepEquals(ISymbolStore other);
    }

    /// <summary>
    /// Indicates an error originating from an operation on a symbol store
    /// </summary>
    public class SymbolStoreException : Exception
    {
        public SymbolStoreException(string message) : base(message) { }

        public SymbolStoreException(string message, Exception e) : base(message, e) { }
    }
}
