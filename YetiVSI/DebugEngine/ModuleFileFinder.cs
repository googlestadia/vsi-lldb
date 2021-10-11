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

﻿using SymbolStores;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YetiCommon;
using static YetiVSI.Shared.Metrics.DeveloperLogEvent.Types;

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
        /// <param name="isDebugInfoFile">
        /// If true, then the search should ensure that the file contains debug information.
        /// </param>
        /// <param name="searchLog">
        /// A TextWriter that is used to log errors and other information during the search.
        /// </param>
        /// <returns>The filepath of the file on success, or null on failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown if |filename| is null.</exception>
        Task<string> FindFileAsync(
            string filename, BuildId buildId, bool isDebugInfoFile, TextWriter searchLog);

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

        public async Task<string> FindFileAsync(
            string filename, BuildId uuid, bool isDebugInfoFile, TextWriter searchLog)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException(Strings.FilenameNullOrEmpty, nameof(filename));
            }
            searchLog = searchLog ?? TextWriter.Null;

            await searchLog.WriteLineAsync($"Searching for {filename}");
            Trace.WriteLine($"Searching for {filename}");

            if (uuid == BuildId.Empty)
            {
                await searchLog.WriteLineAsync(ErrorStrings.ModuleBuildIdUnknown);
                Trace.WriteLine($"Warning: The build ID of {filename} is unknown.");
            }

            var fileReference =
                await _symbolStore.FindFileAsync(filename, uuid, isDebugInfoFile, searchLog);
            if (fileReference == null)
            {
                await searchLog.WriteLineAsync(ErrorStrings.FailedToFindFile(filename));
                Trace.WriteLine(ErrorStrings.FailedToFindFile(filename));
                return null;
            }
            if (!fileReference.IsFilesystemLocation)
            {
                await searchLog.WriteLineAsync(
                    ErrorStrings.FileNotOnFilesystem(fileReference.Location));
                Trace.WriteLine($"Unable to load file. '{fileReference.Location}' must be " +
                    $"cached in a filesystem location.");
                return null;
            }

            return fileReference.Location;
        }

        public void RecordMetrics(LoadSymbolData loadSymbolData)
        {
            loadSymbolData.FlatSymbolStoresCount =
                _symbolStore.GetAllStores().OfType<IFlatSymbolStore>().Count();
            loadSymbolData.StructuredSymbolStoresCount =
                _symbolStore.GetAllStores().OfType<IStructuredSymbolStore>().Count();
            loadSymbolData.HttpSymbolStoresCount =
                _symbolStore.GetAllStores().OfType<IHttpSymbolStore>().Count();
            loadSymbolData.StadiaSymbolStoresCount =
                _symbolStore.GetAllStores().OfType<IStadiaSymbolStore>().Count();
        }
    }
}
