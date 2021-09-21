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

using DebuggerApi;
using Microsoft.VisualStudio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YetiCommon;

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
        /// <returns>S_OK if all binaries and symbols are loaded, E_FAIL otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if any argument is null.</exception>
        Task<int> LoadModuleFilesAsync(IList<SbModule> modules, ICancelable task,
                                       IModuleFileLoadMetricsRecorder moduleFileLoadRecorder);

        /// <summary>
        /// Same as LoadModuleFiles(IList<SbModule>, ICancelable, IModuleFileLoadMetricsRecorder),
        /// but allows user to additionally specify modules for which symbols should be loaded
        /// via SymbolInclusionSettings.
        /// </summary>
        Task<int> LoadModuleFilesAsync(
            IList<SbModule> modules, SymbolInclusionSettings symbolSettings, bool useSymbolStores,
            ICancelable task, IModuleFileLoadMetricsRecorder moduleFileLoadRecorder);
    }

    public interface IModuleFileLoaderFactory
    {
        IModuleFileLoader Create(ISymbolLoader symbolLoader, IBinaryLoader binaryLoader,
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
                                            IModuleSearchLogHolder moduleSearchLogHolder) =>
                new ModuleFileLoader(symbolLoader, binaryLoader, moduleSearchLogHolder);
        }

        readonly ISymbolLoader _symbolLoader;
        readonly IBinaryLoader _binaryLoader;
        readonly IModuleSearchLogHolder _moduleSearchLogHolder;

        public ModuleFileLoader(ISymbolLoader symbolLoader, IBinaryLoader binaryLoader,
                                IModuleSearchLogHolder moduleSearchLogHolder)
        {
            _symbolLoader = symbolLoader;
            _binaryLoader = binaryLoader;
            _moduleSearchLogHolder = moduleSearchLogHolder;
        }

        public async Task<int> LoadModuleFilesAsync(
            IList<SbModule> modules, SymbolInclusionSettings symbolSettings, bool useSymbolStores,
            ICancelable task, IModuleFileLoadMetricsRecorder moduleFileLoadRecorder)
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

            // Add some metrics to the event proto before attempting to load symbols, so that they
            // are still recorded if the task is aborted or cancelled.
            moduleFileLoadRecorder.RecordBeforeLoad(modules);

            int result = VSConstants.S_OK;

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
                            await searchLog.WriteLineAsync(
                                SymbolInclusionSettings.ModuleExcludedMessage);
                            continue;
                        }

                        task.Progress.Report($"Loading binary for {name} ({i}/{modules.Count})");

                        (SbModule newModule, bool ok) =
                            await _binaryLoader.LoadBinaryAsync(module, searchLog);
                        if (!ok)
                        {
                            result = VSConstants.E_FAIL;
                            continue;
                        }

                        module = newModule;

                        task.ThrowIfCancellationRequested();
                        task.Progress.Report($"Loading symbols for {name} ({i}/{modules.Count})");
                        var loaded =
                            await _symbolLoader.LoadSymbolsAsync(
                                module, searchLog, useSymbolStores);
                        if (!loaded)
                        {
                            result = VSConstants.E_FAIL;
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

        public Task<int> LoadModuleFilesAsync(
            IList<SbModule> modules, ICancelable task,
            IModuleFileLoadMetricsRecorder moduleFileLoadRecorder) =>
            LoadModuleFilesAsync(modules, null, true, task, moduleFileLoadRecorder);

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