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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DebuggerApi;
using JetBrains.Annotations;
using Metrics.Shared;
using Microsoft.VisualStudio;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Handles loading binaries and symbols for modules by searching symbol stores for binary files
    /// and symbol files.
    /// </summary>
    public interface IModuleFileLoader
    {
        /// <summary>
        /// Searches for and loads binaries and symbols for the given modules. If |modules| contains
        /// placeholder modules and the matching binaries are successfully loaded, then |modules|
        /// will be modified, replacing the placeholder modules with the newly loaded modules.
        /// </summary>
        /// <returns>
        /// A <see cref="LoadModuleFilesResult"/>.
        /// </returns>
        Task<LoadModuleFilesResult> LoadModuleFilesAsync(
            [NotNull, ItemNotNull] IList<SbModule> modules,
            [NotNull] ICancelable task,
            [NotNull] IModuleFileLoadMetricsRecorder moduleFileLoadRecorder);

        /// <summary>
        /// Same as <see cref="LoadModuleFilesAsync(System.Collections.Generic.IList{DebuggerApi.SbModule},YetiVSI.ICancelable,YetiVSI.DebugEngine.IModuleFileLoadMetricsRecorder)"/>
        /// but allows user to additionally specify modules for which symbols should be loaded
        /// via SymbolInclusionSettings.
        /// </summary>
        /// <param name="modules">List of modules to process.</param>
        /// <param name="symbolSettings">Symbol settings with IncludeList and ExcludeList
        /// to filter out symbols that should be skipped. </param>
        /// <param name="useSymbolStores">
        /// If true, then during loading the module the method will try to lookup module in
        /// symbol stores by the name extracted from module.
        /// <see cref="SymbolLoader.GetSymbolFileDirAndName"/>
        /// </param>
        /// <param name="isStadiaSymbolsServerUsed">
        /// If true, then the method will not return suggestion to enable symbol store server.
        /// A <see cref="LoadModuleFilesResult.SuggestToEnableSymbolStore"/>.
        /// </param>
        /// <param name="task">Long-running operation associated with the process.</param>
        /// <param name="moduleFileLoadRecorder">Instance to record metrics related to the
        /// loading of symbols and binaries.</param>
        /// <returns>
        /// A <see cref="LoadModuleFilesResult"/>.
        /// </returns>
        Task<LoadModuleFilesResult> LoadModuleFilesAsync(
            [NotNull, ItemNotNull] IList<SbModule> modules,
            SymbolInclusionSettings symbolSettings,
            bool useSymbolStores,
            bool isStadiaSymbolsServerUsed,
            [NotNull] ICancelable task,
            [NotNull] IModuleFileLoadMetricsRecorder moduleFileLoadRecorder);
    }

    public interface IModuleFileLoaderFactory
    {
        IModuleFileLoader Create(ISymbolLoader symbolLoader, IBinaryLoader binaryLoader,
                                 bool isCoreAttach,
                                 IModuleSearchLogHolder moduleSearchLogHolder);
    }

    public interface IModuleSearchLogHolder
    {
        string GetSearchLog(SbModule lldbModule);

        void AppendSearchLog(SbModule lldbModule, string log);

        void ResetSearchLog(SbModule lldbModule);
    }

    public class ModuleSearchLogHolder : IModuleSearchLogHolder
    {
        // Maps module Id (it's unique) to per-module symbol search logs.
        readonly Dictionary<long, string> _logsByModuleId =
            new Dictionary<long, string>();

        public string GetSearchLog(SbModule lldbModule)
        {
            long id = lldbModule.GetId();
            return _logsByModuleId.TryGetValue(id, out string log)
                ? log
                : "";
        }

        public void AppendSearchLog(SbModule lldbModule, string log)
        {
            if (string.IsNullOrWhiteSpace(log))
            {
                return;
            }

            long id = lldbModule.GetId();

            if (_logsByModuleId.TryGetValue(id, out string existingLog)
                && !string.IsNullOrWhiteSpace(existingLog))
            {
                _logsByModuleId[id] = $"{existingLog}{Environment.NewLine}{log}";
            }
            else
            {
                _logsByModuleId[id] = log;
            }
        }

        public void ResetSearchLog(SbModule lldbModule)
        {
            long id = lldbModule.GetId();

            if (_logsByModuleId.TryGetValue(id, out string _))
            {
                _logsByModuleId[id] = "";
            }
        }
    }

    public class ModuleFileLoader : IModuleFileLoader
    {
        // TODO: Make ModuleFileLoader implementation thread safe

        public class Factory : IModuleFileLoaderFactory
        {
            public IModuleFileLoader Create(ISymbolLoader symbolLoader, IBinaryLoader binaryLoader,
                                            bool isCoreAttach,
                                            IModuleSearchLogHolder moduleSearchLogHolder) =>
                new ModuleFileLoader(symbolLoader, binaryLoader, isCoreAttach,
                                     moduleSearchLogHolder);
        }

        readonly ISymbolLoader _symbolLoader;
        readonly IBinaryLoader _binaryLoader;
        readonly bool _isCoreAttach;
        readonly IModuleSearchLogHolder _moduleSearchLogHolder;
        static readonly IList<Regex> _importantModulesForCoreDumpDebugging =
            new[]
            {
                new Regex("amdvlk64\\.so", RegexOptions.Compiled),
                new Regex("ggpvlk\\.so", RegexOptions.Compiled),
                new Regex("libanl-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libasound\\.so.*", RegexOptions.Compiled),
                new Regex("libatomic\\.so.*", RegexOptions.Compiled),
                new Regex("libBrokenLocale-?.*\\.so", RegexOptions.Compiled),
                new Regex("libc-?([0-9].[0-9]{2})?\\.so.*", RegexOptions.Compiled),
                new Regex("libc\\+\\+abi\\.so.*", RegexOptions.Compiled),
                new Regex("libcap\\.so.*", RegexOptions.Compiled),
                new Regex("libcidn-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libcrypto\\.so.*", RegexOptions.Compiled),
                new Regex("libcrypt-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libc\\+\\+\\.so.*", RegexOptions.Compiled),
                new Regex("libdbus-1\\.so.*", RegexOptions.Compiled),
                new Regex("libdl-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libdrm_amdgpu\\.so.*", RegexOptions.Compiled),
                new Regex("libdrm\\.so.*", RegexOptions.Compiled),
                new Regex("libgcc_s\\.so.*", RegexOptions.Compiled),
                new Regex("libgcrypt\\.so.*", RegexOptions.Compiled),
                new Regex("libggp_g3\\.so.*", RegexOptions.Compiled),
                new Regex("libggp\\.so", RegexOptions.Compiled),
                new Regex("libggp_with_heap_isolation\\.so.*", RegexOptions.Compiled),
                new Regex("libgomp\\.so.*", RegexOptions.Compiled),
                new Regex("libgpg-error\\.so.*", RegexOptions.Compiled),
                new Regex("libGPUPerfAPIVK\\.so", RegexOptions.Compiled),
                new Regex("libidn\\.so.*", RegexOptions.Compiled),
                new Regex("liblz4\\.so.*", RegexOptions.Compiled),
                new Regex("liblzma\\.so.*", RegexOptions.Compiled),
                new Regex("libmemusage\\.so", RegexOptions.Compiled),
                new Regex("libm-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libmvec-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libnettle\\.so.*", RegexOptions.Compiled),
                new Regex("libnsl-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libnss_compat-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libnss_dns-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libnss_files-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libnss_hesiod-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libnss_nisplus-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libnss_nis-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libpcprofile.so", RegexOptions.Compiled),
                new Regex("libpcre\\.so.*", RegexOptions.Compiled),
                new Regex("libpthread-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libpulsecommon-12\\.0\\.so", RegexOptions.Compiled),
                new Regex("libpulse-simple\\.so.*", RegexOptions.Compiled),
                new Regex("libpulse\\.so.*", RegexOptions.Compiled),
                new Regex("librenderdoc\\.so", RegexOptions.Compiled),
                new Regex("libresolv-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("librgpserver\\.so", RegexOptions.Compiled),
                new Regex("librt-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libSegFault\\.so", RegexOptions.Compiled),
                new Regex("libselinux\\.so.*", RegexOptions.Compiled),
                new Regex("libsndfile\\.so.*", RegexOptions.Compiled),
                new Regex("libssl\\.so.*", RegexOptions.Compiled),
                new Regex("libsystemd\\.so.*", RegexOptions.Compiled),
                new Regex("libthread_db-1\\.0\\.so.*", RegexOptions.Compiled),
                new Regex("libthread_db\\.so.*", RegexOptions.Compiled),
                new Regex("libutil-?.*\\.so.*", RegexOptions.Compiled),
                new Regex("libVkLayer.*\\.so", RegexOptions.Compiled),
                new Regex("libvulkan\\.so.*", RegexOptions.Compiled),
                new Regex("libz\\.so.*", RegexOptions.Compiled),
                new Regex("oskhost\\.so", RegexOptions.Compiled),
            };

        public ModuleFileLoader(ISymbolLoader symbolLoader, IBinaryLoader binaryLoader,
                                bool isCoreAttach,
                                IModuleSearchLogHolder moduleSearchLogHolder)
        {
            _symbolLoader = symbolLoader;
            _binaryLoader = binaryLoader;
            _isCoreAttach = isCoreAttach;
            _moduleSearchLogHolder = moduleSearchLogHolder;
        }

        public async Task<LoadModuleFilesResult> LoadModuleFilesAsync(
            IList<SbModule> modules,
            SymbolInclusionSettings symbolSettings,
            bool useSymbolStores,
            bool isStadiaSymbolsServerUsed,
            ICancelable task,
            IModuleFileLoadMetricsRecorder moduleFileLoadRecorder)
        {
            // If LoadSymbols is called from the "Modules -> Load Symbols" context menu
            // or by clicking "Load Symbols" in "Symbols" pane in the settings, `symbolSettings`
            // will be empty. On the other hand, when LoadSymbols is called during
            // the debugger attaching, the `symbolSettings` will be set and IsManualLoad = false,
            // only in this case we'll be using cache when trying to load symbols from
            // remote symbolStores.
            bool forceLoad = symbolSettings?.IsManualLoad ?? true;
            int modulesWithSymbolsCount = modules.Count(m => m.HasSymbolsLoaded());
            int binariesLoadedCount = modules.Count(m => m.HasBinaryLoaded());
            var loadSymbolData = new DeveloperLogEvent.Types.LoadSymbolData
            {
                ModulesCount = modules.Count,
                ModulesBeforeCount = modules.Count,
                ModulesAfterCount = modules.Count,
                ModulesWithSymbolsLoadedBeforeCount = modulesWithSymbolsCount,
                ModulesWithSymbolsLoadedAfterCount = modulesWithSymbolsCount,
                BinariesLoadedBeforeCount = binariesLoadedCount,
                BinariesLoadedAfterCount = binariesLoadedCount
            };

            // Add some metrics to the event proto before attempting to load symbols, so that they
            // are still recorded if the task is aborted or cancelled.
            moduleFileLoadRecorder.RecordBeforeLoad(loadSymbolData);

            var result = new LoadModuleFilesResult
            {
                ResultCode = VSConstants.S_OK,
                SuggestToEnableSymbolStore = false
            };

            List<SbModule> preFilteredModules =
                PrefilterModulesByName(modules, symbolSettings).ToList();

            List<SbModule> modulesWithBinariesLoaded = await ProcessModulePlaceholdersAsync(
                preFilteredModules, task, isStadiaSymbolsServerUsed, loadSymbolData, result);

            await ProcessModulesWithoutSymbolsAsync(
                modulesWithBinariesLoaded, task, useSymbolStores, forceLoad, loadSymbolData,
                result);

            // Record the final state.
            moduleFileLoadRecorder.RecordAfterLoad(loadSymbolData);
            return result;
        }

        public Task<LoadModuleFilesResult> LoadModuleFilesAsync(
            IList<SbModule> modules, ICancelable task,
            IModuleFileLoadMetricsRecorder moduleFileLoadRecorder) =>
            LoadModuleFilesAsync(modules, null, true, true, task, moduleFileLoadRecorder);

        /// <summary>
        /// Filter out modules that should be skipped based on their name
        /// (empty, deleted or excluded).
        /// </summary>
        IEnumerable<SbModule> PrefilterModulesByName(IEnumerable<SbModule> modules,
                                                     SymbolInclusionSettings settings)
        {
            foreach (SbModule sbModule in modules)
            {
                string name = sbModule.GetPlatformFileSpec()?.GetFilename();
                string error = GetReasonToSkip(name, settings);

                // Modules loading can be an iterative process, we need to clean up the logs.
                _moduleSearchLogHolder.ResetSearchLog(sbModule);

                if (string.IsNullOrEmpty(error))
                {
                    yield return sbModule;
                }
                else
                {
                    Trace.WriteLine(error);
                    _moduleSearchLogHolder.AppendSearchLog(sbModule, error);
                }
            }
        }

        /// <summary>
        /// If module doesn't have a name or its name is in the list
        /// of ExcludedModules, no further processing is needed.
        /// </summary>
        /// <returns>Message to be recorded in the logs.</returns>
        string GetReasonToSkip(string moduleName, SymbolInclusionSettings settings)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                return "Module name not set.";
            }

            if (moduleName.EndsWith("(deleted)"))
            {
                return $"Module '{moduleName}' marked as deleted by LLDB.";
            }

            return settings?.IsModuleIncluded(moduleName) == false
                ? SymbolInclusionSettings.ModuleExcludedMessage(moduleName)
                : null;
        }

        /// <summary>
        /// Attempts to replace placeholder modules with matching binaries.
        /// If this operation fails for one of the so-called "important modules"
        /// and Stadia SymbolStore is not enabled, we'll show a warning message
        /// suggesting to enable symbol stores in the settings.
        /// </summary>
        /// <remarks>
        /// Updates <c>loadSymbolData</c>'s BinariesLoadedAfterCount
        /// property and <c>result</c>'s ResultCode and SuggestToEnableSymbolStore.
        /// </remarks>
        /// <returns>List of modules with binaries loaded.</returns>
        async Task<List<SbModule>> ProcessModulePlaceholdersAsync(
            IReadOnlyList<SbModule> preFilteredModules,
            ICancelable task,
            bool isStadiaSymbolsServerUsed,
            DeveloperLogEvent.Types.LoadSymbolData loadSymbolData,
            LoadModuleFilesResult result)
        {
            var modulesWithBinary = new List<SbModule>();
            for (int index = 0; index < preFilteredModules.Count; index++)
            {
                SbModule sbModule = preFilteredModules[index];
                if (sbModule.HasBinaryLoaded())
                {
                    modulesWithBinary.Add(sbModule);
                    continue;
                }

                string name = sbModule.GetPlatformFileSpec().GetFilename();
                TextWriter searchLog = new StringWriter();

                task.ThrowIfCancellationRequested();
                task.Progress.Report($"Loading binary for {name}" +
                                     $"({index}/{preFilteredModules.Count})");
                (SbModule outputModule, bool ok) =
                    await _binaryLoader.LoadBinaryAsync(sbModule, searchLog);
                if (ok)
                {
                    modulesWithBinary.Add(outputModule);
                    loadSymbolData.BinariesLoadedAfterCount++;
                }
                else
                {
                    result.ResultCode = VSConstants.E_FAIL;
                    result.SuggestToEnableSymbolStore |=
                        ShouldAskToEnableSymbolStores(name, isStadiaSymbolsServerUsed);
                }

                _moduleSearchLogHolder.AppendSearchLog(outputModule, searchLog.ToString());
            }

            return modulesWithBinary;
        }

        /// <summary>
        /// Attempts to load symbols for all modules that don't have them loaded yet (it
        /// includes searching for a separate symbol file locally and in the enabled symbol
        /// stores).
        /// </summary>
        /// <remarks>
        /// Updates <c>loadSymbolData</c>'s ModulesWithSymbolsLoadedAfterCount property and
        /// <c>result</c>'s ResultCode.
        /// </remarks>
        async Task ProcessModulesWithoutSymbolsAsync(
            IReadOnlyList<SbModule> modulesWithBinariesLoaded,
            ICancelable task,
            bool useSymbolStores,
            bool forceLoad,
            DeveloperLogEvent.Types.LoadSymbolData loadSymbolData,
            LoadModuleFilesResult result)
        {
            for (int index = 0; index < modulesWithBinariesLoaded.Count; index++)
            {
                SbModule sbModule = modulesWithBinariesLoaded[index];
                if (sbModule.HasSymbolsLoaded())
                {
                    continue;
                }

                string name = sbModule.GetPlatformFileSpec().GetFilename();
                TextWriter searchLog = new StringWriter();
                task.ThrowIfCancellationRequested();
                task.Progress.Report($"Loading symbols for {name} " +
                                     $"({index}/{modulesWithBinariesLoaded.Count})");
                bool ok = await _symbolLoader.LoadSymbolsAsync(
                    sbModule, searchLog, useSymbolStores, forceLoad);
                if (!ok)
                {
                    result.ResultCode = VSConstants.E_FAIL;
                }
                else
                {
                    loadSymbolData.ModulesWithSymbolsLoadedAfterCount++;
                }

                _moduleSearchLogHolder.AppendSearchLog(sbModule, searchLog.ToString());
            }
        }

        bool ShouldAskToEnableSymbolStores(string name, bool isStadiaSymbolsServerUsed)
        {
            return !isStadiaSymbolsServerUsed &&
                _isCoreAttach &&
                _importantModulesForCoreDumpDebugging.Any(
                    expr => expr.IsMatch(name));
        }
    }
}