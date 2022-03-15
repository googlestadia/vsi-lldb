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

using SymbolStores;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.Logging;
using static Metrics.Shared.DeveloperLogEvent.Types;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Handles searching for a module's symbol and binary files.
    /// </summary>
    public interface IModuleFileFinder
    {
        /// <summary>
        /// Configures the paths for FindFile to search.
        /// </summary>
        /// <param name="searchPaths">
        /// The paths to search, in the format used by Visual Studio and _NT_SYMBOL_PATH.
        /// </param>
        void SetSearchPaths(string searchPaths);


        /// <summary>
        /// Returns if search paths contains stadia symbol store placeholder path and server
        /// symbol store enabled.
        /// </summary>
        bool IsStadiaSymbolsServerUsed { get; }

        /// <summary>
        /// Searches the paths set through <see cref="SetSearchPaths"/> for a file with the
        /// specified name and build ID.
        /// </summary>
        /// <param name="filename">File name to look for.</param>
        /// <param name="buildId">Build ID of the file.</param>
        /// <param name="isDebugInfoFile">
        ///     If true, then the search should ensure that the file contains debug information.
        /// </param>
        /// <param name="searchLog">
        ///     A TextWriter that is used to log errors and other information during the search.
        /// </param>
        /// <param name="forceLoad">Whether we need to reload a module from the remote
        /// moduleStore.</param>
        /// <returns>The filepath of the file on success, or null on failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown if |filename| is null.</exception>
        Task<string> FindFileAsync(
            string filename, BuildId buildId, bool isDebugInfoFile, TextWriter searchLog,
            bool forceLoad);

        /// <summary>
        /// Adds metrics related to the current search paths to the data.
        /// </summary>
        void RecordMetrics(LoadSymbolData loadSymbolData);
    }

    public class ModuleFileFinder : IModuleFileFinder
    {
        readonly SymbolPathParser _symbolPathParser;

        ISymbolStore _symbolStore;

        public ModuleFileFinder(SymbolPathParser symbolPathParser)
        {
            _symbolPathParser = symbolPathParser;
            _symbolStore = new NullSymbolStore();
            IsStadiaSymbolsServerUsed = false;
        }

        public bool IsStadiaSymbolsServerUsed { get; private set; }

        public void SetSearchPaths(string searchPaths)
        {
            _symbolStore = _symbolPathParser.Parse(searchPaths);
            IsStadiaSymbolsServerUsed =
                _symbolStore.Substores.Any(store => store is StadiaSymbolStore);
        }

        public async Task<string> FindFileAsync(string filename, BuildId uuid, bool isDebugInfoFile,
                                                TextWriter searchLog, bool forceLoad)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException(Strings.FilenameNullOrEmpty, nameof(filename));
            }
            searchLog = searchLog ?? TextWriter.Null;

            searchLog.WriteLineAndTrace($"Searching for '{filename}'");

            if (uuid == BuildId.Empty)
            {
                searchLog.WriteLineAndTrace(ErrorStrings.ModuleBuildIdUnknown(filename));
            }

            IFileReference fileReference =
                await _symbolStore.FindFileAsync(filename, uuid, isDebugInfoFile, searchLog,
                                                 forceLoad);
            if (fileReference == null)
            {
                searchLog.WriteLineAndTrace(ErrorStrings.FailedToFindFile(filename));
                return null;
            }

            if (!fileReference.IsFilesystemLocation)
            {
                searchLog.WriteLineAndTrace(
                    ErrorStrings.FileNotOnFilesystem(fileReference.Location));
                return null;
            }

            return fileReference.Location;
        }

        public void RecordMetrics(LoadSymbolData data)
        {
            var stores = _symbolStore.GetAllStores().ToList();

            data.FlatSymbolStoresCount = stores.OfType<IFlatSymbolStore>().Count();
            data.StructuredSymbolStoresCount = stores.OfType<IStructuredSymbolStore>().Count();
            data.HttpSymbolStoresCount = stores.OfType<IHttpSymbolStore>().Count();
            data.StadiaSymbolStoresCount = stores.OfType<IStadiaSymbolStore>().Count();
        }
    }
}
