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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DebuggerApi;
using JetBrains.Annotations;
using SymbolStores;
using YetiCommon;
using YetiCommon.Logging;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Handles loading missing symbols for individual modules.
    /// </summary>
    public interface ISymbolLoader
    {
        /// <summary>
        /// Attempts to load symbols for |lldbModule|, if they haven't already been loaded.
        /// </summary>
        /// <returns>
        /// True if the module's symbols were successfully loaded or were already loaded, false
        /// otherwise.
        /// </returns>
        Task<bool> LoadSymbolsAsync([NotNull] SbModule lldbModule,
                                    [NotNull] TextWriter searchLog,
                                    bool useSymbolStores, bool forceLoad);
    }

    public class SymbolLoader : ISymbolLoader
    {
        public class Factory
        {
            readonly IModuleFileFinder _moduleFileFinder;
            readonly IModuleParser _moduleParser;

            public Factory(IModuleParser moduleParser,
                IModuleFileFinder moduleFileFinder)
            {
                _moduleParser = moduleParser;
                _moduleFileFinder = moduleFileFinder;
            }

            public virtual ISymbolLoader Create(SbCommandInterpreter lldbCommandInterpreter) =>
                new SymbolLoader(_moduleParser, _moduleFileFinder,
                    lldbCommandInterpreter);
        }

        readonly IModuleParser _moduleParser;
        readonly IModuleFileFinder _moduleFileFinder;
        readonly SbCommandInterpreter _lldbCommandInterpreter;

        public SymbolLoader(IModuleParser binaryFileUtil,
            IModuleFileFinder moduleFileFinder, SbCommandInterpreter lldbCommandInterpreter)
        {
            _moduleParser = binaryFileUtil;
            _moduleFileFinder = moduleFileFinder;
            _lldbCommandInterpreter = lldbCommandInterpreter;
        }

        public virtual async Task<bool> LoadSymbolsAsync(
            SbModule lldbModule, TextWriter searchLog, bool useSymbolStores, bool forceLoad)
        {
            SymbolFileLocation symbolPath = GetSymbolPathFromSbModule(lldbModule);
            ModuleFormat format = lldbModule.GetModuleFormat();

            if (symbolPath.IsInvalid)
            {
                symbolPath = GetSymbolPathFromAuxiliaryData(lldbModule, searchLog, ref format);
                if (symbolPath.IsInvalid)
                {
                    searchLog.WriteLineAndTrace(ErrorStrings.SymbolFileNameUnknown);
                    return false;
                }
            }

            var buildId = new BuildId(lldbModule.GetUUIDString());
            // If we have a full path to the symbol file, we'll load it when build ids match.
            if (symbolPath.TryGetFullPath(out string fullPath)
                && ShouldAddSymbolFile(fullPath, buildId, format, searchLog))
            {
                return AddSymbolFile(fullPath, lldbModule, searchLog);
            }

            if (!useSymbolStores)
            {
                return false;
            }

            string binaryName = lldbModule.GetFileSpec()?.GetFilename();
            var searchQuery = new ModuleSearchQuery(symbolPath.Filename, buildId, format)
            {
                RequireDebugInfo = true,
                ForceLoad = forceLoad
            };

            string filepath = await SearchSymbolFileInSymbolStoresAsync(
                binaryName, searchQuery, searchLog);
            return !(string.IsNullOrWhiteSpace(filepath))
                && AddSymbolFile(filepath, lldbModule, searchLog);
        }

        SymbolFileLocation GetSymbolPathFromSbModule(SbModule lldbModule)
        {
            SbFileSpec symbolFileSpec = lldbModule.GetSymbolFileSpec();
            SbFileSpec binaryFileSpec = lldbModule.GetFileSpec();

            if (string.IsNullOrWhiteSpace(binaryFileSpec?.GetDirectory()) ||
                string.IsNullOrWhiteSpace(binaryFileSpec.GetFilename()) ||
                binaryFileSpec.GetDirectory() != symbolFileSpec?.GetDirectory() ||
                binaryFileSpec.GetFilename() != symbolFileSpec?.GetFilename())
            {
                return string.IsNullOrWhiteSpace(symbolFileSpec?.GetFilename())
                    ? SymbolFileLocation.Empty
                    : new SymbolFileLocation(symbolFileSpec.GetDirectory(),
                                             symbolFileSpec.GetFilename());
            }

            return SymbolFileLocation.Empty;
        }

        /// <summary>
        /// Symbol path cannot be identified from the SbModule::GetSymbolFileSpec():
        /// - for PE modules look for a file with the same name as the binary but change
        /// its extension to PDB (and fix the moduleFormat);
        /// - for PDB modules - do nothing;
        /// - for ELF modules - parse binary file to get a symbol location from the
        /// '.gnu_debuglink' and '.note.debug_info_dir' sections.
        /// </summary>
        /// <returns>Possible path to the symbol file (directory, filename).</returns>
        SymbolFileLocation GetSymbolPathFromAuxiliaryData(SbModule lldbModule,
            TextWriter searchLog, ref ModuleFormat format)
        {
            string binaryName = lldbModule.GetFileSpec()?.GetFilename();
            string binaryDirectory = lldbModule.GetFileSpec()?.GetDirectory();
            if (string.IsNullOrWhiteSpace(binaryName)
                || string.IsNullOrWhiteSpace(binaryDirectory))
            {
                return SymbolFileLocation.Empty;
            }

            switch (format)
            {
                case ModuleFormat.Elf:
                    // Try to parse binary to get path to symbol.
                    return GetSymbolPathFromBinaryFile(binaryDirectory, binaryName, searchLog);
                case ModuleFormat.Pdb:
                    // Nothing to do here.
                    return SymbolFileLocation.Empty;
                case ModuleFormat.Pe:
                    // Change extension to pdb and update the format.
                    format = ModuleFormat.Pdb;
                    return new SymbolFileLocation(
                        binaryDirectory, Path.ChangeExtension(binaryName, ".pdb"));
                default:
                    throw new ArgumentException(format.ToString());
            }
        }

        /// <summary>
        /// Parse ELF binary file to extract symbol filename and directory.
        /// </summary>
        SymbolFileLocation GetSymbolPathFromBinaryFile(string binaryDirectory,
            string binaryFilename, TextWriter log)
        {
            string binaryPath;
            try
            {
                binaryPath = Path.Combine(binaryDirectory, binaryFilename);
            }
            catch (ArgumentException e)
            {
                string errorString = ErrorStrings.InvalidBinaryPathOrName(binaryDirectory,
                    binaryFilename, e.Message);
                log.WriteLineAndTrace(errorString);
                return SymbolFileLocation.Empty;
            }

            DebugLinkLocationInfo modulePathSection = _moduleParser.ParseDebugLinkInfo(binaryPath);
            if (modulePathSection.HasError)
            {
                log.WriteLineAndTrace(modulePathSection.Error);
            }

            return modulePathSection.Data;
        }

        /// <summary>
        /// Check that the symbol file exists and has correct BuildId.
        /// </summary>
        bool ShouldAddSymbolFile(string fullPath, BuildId buildId, ModuleFormat format,
                                 TextWriter log)
        {
            BuildIdInfo buildIdInfo = _moduleParser.ParseBuildIdInfo(fullPath, format);
            if (buildIdInfo.HasError)
            {
                log.WriteLineAndTrace(
                    YetiCommon.ErrorStrings.FailedToReadBuildId(fullPath, buildIdInfo.Error));
            }

            if (buildId.Matches(buildIdInfo.Data, format))
            {
                return true;
            }

            log.WriteLineAndTrace(
                Strings.BuildIdMismatch(fullPath, buildId, buildIdInfo.Data, format));
            return false;
        }

        /// <summary>
        /// Search for a symbol in the configured symbol stores.
        /// If the initial query fails and the module format is ELF also
        /// try to look for a <binaryFilename>.debug file with the same BuildId.
        /// </summary>
        /// <returns>Fullpath of the symbol file.</returns>
        async Task<string> SearchSymbolFileInSymbolStoresAsync(string binaryFilename,
            ModuleSearchQuery searchQuery, TextWriter searchLog)
        {
            string filepath = await _moduleFileFinder.FindFileAsync(searchQuery, searchLog);

            if (string.IsNullOrWhiteSpace(filepath) &&
                searchQuery.ModuleFormat == ModuleFormat.Elf &&
                !string.IsNullOrWhiteSpace(binaryFilename))
            {
                searchQuery.Filename = $"{binaryFilename}.debug";
                filepath = await _moduleFileFinder.FindFileAsync(searchQuery, searchLog);
            }

            return filepath;
        }

        /// <summary>
        /// Call `target symbols add` in Lldb shell to update SbModule's symbol info.
        /// </summary>
        bool AddSymbolFile(string filepath, SbModule module, TextWriter searchLog)
        {
            string command = "target symbols add";

            SbFileSpec platformFileSpec = module.GetPlatformFileSpec();
            if (platformFileSpec != null)
            {
                string platformPath = FileUtil.PathCombineLinux(platformFileSpec.GetDirectory(),
                                                                platformFileSpec.GetFilename());
                // The -s flag specifies the path of the module to add symbols to.
                command += " -s " + LldbCommandUtil.QuoteArgument(platformPath);
            }

            command += " " + LldbCommandUtil.QuoteArgument(filepath);

            _lldbCommandInterpreter.HandleCommand(command,
                                                  out SbCommandReturnObject commandResult);
            Trace.WriteLine($"Executed LLDB command '{command}' with result:" +
                Environment.NewLine + commandResult.GetDescription());

            if (!commandResult.Succeeded())
            {
                searchLog.WriteLineAndTrace("LLDB error: " + commandResult.GetError());
                return false;
            }

            string text = $"LLDB output: {commandResult.GetOutput()}{Environment.NewLine}" +
                $"Successfully loaded symbol file '{filepath}'.";
            searchLog.WriteLineAndTrace(text);

            return true;
        }
    }
}
