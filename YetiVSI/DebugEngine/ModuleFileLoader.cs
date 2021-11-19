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

using DebuggerApi;
using Microsoft.VisualStudio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.Logging;

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
        /// <exception cref="ArgumentNullException">Thrown if any argument is null.</exception>
        Task<LoadModuleFilesResult> LoadModuleFilesAsync(IList<SbModule> modules, ICancelable task,
                                       IModuleFileLoadMetricsRecorder moduleFileLoadRecorder);

        /// <summary>
        /// Same as LoadModuleFiles(IList<SbModule>, ICancelable, IModuleFileLoadMetricsRecorder),
        /// but allows user to additionally specify modules for which symbols should be loaded
        /// via SymbolInclusionSettings.
        /// </summary>
        /// <param name="useSymbolStores">
        /// If true, then during loading the module the method will try to lookup module in
        /// symbol stores by the name extracted from module.
        /// <see cref="SymbolLoader.GetSymbolFileDirAndNameAsync"/>
        /// </param>
        /// <param name="isStadiaSymbolsServerUsed">
        /// If true, then the method will not return suggestion to enable symbol store server.
        /// A <see cref="LoadModuleFilesResult.SuggestToEnableSymbolStore"/>.
        /// </param>
        Task<LoadModuleFilesResult> LoadModuleFilesAsync(
            IList<SbModule> modules, SymbolInclusionSettings symbolSettings, bool useSymbolStores,
            bool isStadiaSymbolsServerUsed, ICancelable task,
            IModuleFileLoadMetricsRecorder moduleFileLoadRecorder);
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

        void SetSearchLog(SbModule lldbModule, string log);
    }

    public class ModuleSearchLogHolder : IModuleSearchLogHolder
    {
        // Maps platform paths to per-module symbol search logs.
        readonly IDictionary<string, string> _logsByPlatformFileSpec =
            new Dictionary<string, string>();

        public string GetSearchLog(SbModule lldbModule)
        {
            SbFileSpec platformFileSpec = lldbModule.GetPlatformFileSpec();
            if (platformFileSpec == null)
            {
                return "";
            }

            string key = FileUtil.PathCombineLinux(platformFileSpec.GetDirectory(),
                                                   platformFileSpec.GetFilename());
            if (_logsByPlatformFileSpec.TryGetValue(key, out string log))
            {
                return log;
            }

            return "";
        }

        public void SetSearchLog(SbModule lldbModule, string log)
        {
            SbFileSpec platformFileSpec = lldbModule.GetPlatformFileSpec();
            if (platformFileSpec != null)
            {
                string key = FileUtil.PathCombineLinux(platformFileSpec.GetDirectory(),
                                                       platformFileSpec.GetFilename());
                _logsByPlatformFileSpec[key] = log;
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
        readonly static IList<Regex> _importantModulesForCoreDumpDebugging =
            new []
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
            IList<SbModule> modules, SymbolInclusionSettings symbolSettings, bool useSymbolStores,
            bool isStadiaSymbolsServerUsed, ICancelable task,
            IModuleFileLoadMetricsRecorder moduleFileLoadRecorder)
        {
            if (modules == null)
            {
                throw new ArgumentNullException(nameof(modules));
            }

            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (moduleFileLoadRecorder == null)
            {
                throw new ArgumentNullException(nameof(moduleFileLoadRecorder));
            }
            
            // If LoadSymbols is called from the "Modules -> Load Symbols" context menu
            // or by clicking "Load Symbols" in "Symbols" pane in the settings, `symbolSettings`
            // will be empty. On the other hand, when LoadSymbols is called during
            // the debugger attaching, the `symbolSettings` will be set and IsManualLoad = false,
            // only in this case we'll be using cache when trying to load symbols from
            // remote symbolStores.
            bool forceLoad = symbolSettings?.IsManualLoad ?? true;

            // Add some metrics to the event proto before attempting to load symbols, so that they
            // are still recorded if the task is aborted or cancelled.
            moduleFileLoadRecorder.RecordBeforeLoad(modules);

            var result = new LoadModuleFilesResult() { ResultCode = VSConstants.S_OK,
                                                       SuggestToEnableSymbolStore = false };
            for (int i = 0; i < modules.Count; ++i)
            {
                SbModule module = modules[i];
                TextWriter searchLog = new StringWriter();
                string name = module.GetPlatformFileSpec()?.GetFilename() ?? "<unknown>";
                using (new TestBenchmark(name, TestBenchmarkScope.Recorder))
                {
                    try
                    {
                        task.ThrowIfCancellationRequested();

                        if (SkipModule(name, symbolSettings))
                        {
                            await searchLog.WriteLineAndTraceAsync(
                                SymbolInclusionSettings.ModuleExcludedMessage);
                            continue;
                        }

                        task.Progress.Report($"Loading binary for {name} ({i}/{modules.Count})");

                        (SbModule newModule, bool ok) =
                            await _binaryLoader.LoadBinaryAsync(module, searchLog);
                        if (!ok)
                        {
                            if (!isStadiaSymbolsServerUsed &&
                                _isCoreAttach &&
                                !result.SuggestToEnableSymbolStore &&
                                _importantModulesForCoreDumpDebugging.Any(
                                    expr => expr.IsMatch(name)))
                            {
                                result.SuggestToEnableSymbolStore = true;
                            }

                            result.ResultCode = VSConstants.E_FAIL;
                            continue;
                        }

                        module = newModule;

                        task.ThrowIfCancellationRequested();
                        task.Progress.Report($"Loading symbols for {name} ({i}/{modules.Count})");
                        var loaded =
                            await _symbolLoader.LoadSymbolsAsync(
                                module, searchLog, useSymbolStores, forceLoad);
                        if (!loaded)
                        {
                            result.ResultCode = VSConstants.E_FAIL;
                            continue;
                        }
                    }
                    finally
                    {
                        _moduleSearchLogHolder.SetSearchLog(module, searchLog.ToString());
                        modules[i] = module;
                    }
                }
            }

            moduleFileLoadRecorder.RecordAfterLoad(modules);

            return result;
        }

        public Task<LoadModuleFilesResult> LoadModuleFilesAsync(
            IList<SbModule> modules, ICancelable task,
            IModuleFileLoadMetricsRecorder moduleFileLoadRecorder) =>
            LoadModuleFilesAsync(modules, null, true, true, task, moduleFileLoadRecorder);

        bool SkipModule(string module, SymbolInclusionSettings settings)
        {
            if (settings == null)
            {
                return false;
            }

            return !settings.IsModuleIncluded(module);
        }
    }
}