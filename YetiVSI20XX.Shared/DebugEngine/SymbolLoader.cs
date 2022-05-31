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
            ModuleFormat format = lldbModule.GetModuleFormat();
            var buildId = new BuildId(lldbModule.GetUUIDString());
            string symbolFileName = lldbModule.GetSymbolFileSpec()?.GetFilename();
            if (format == ModuleFormat.Elf)
            {
                DebugLinkLocation symbolFileLocation =
                    GetSymbolFileDirAndName(lldbModule, searchLog);
                if (string.IsNullOrWhiteSpace(symbolFileLocation.Filename))
                {
                    searchLog.WriteLineAndTrace(ErrorStrings.SymbolFileNameUnknown);
                    return false;
                }

                if (symbolFileLocation.TryGetFullPath(out string fullPath))
                {
                    BuildIdInfo buildIdInfo = _moduleParser.ParseBuildIdInfo(fullPath, format);
                    if (buildIdInfo.HasError)
                    {
                        string moduleName = lldbModule.GetFileSpec().GetFilename();
                        searchLog.WriteLineAndTrace(
                            $"Could not read build Id from {fullPath} for module " +
                            $"{moduleName} (Message: {buildIdInfo.Error}).");
                    }

                    if (buildId == buildIdInfo.Data)
                    {
                        return AddSymbolFile(fullPath, lldbModule, searchLog);
                    }
                }

                symbolFileName = symbolFileLocation.Filename;
            }
            else
            // PE or PDB symbol format.
            {
                string binaryName = lldbModule.GetPlatformFileSpec().GetFilename();
                // If symbol filename is not set or it's the same as the binary filename,
                // we'll try to change the extension of the binary filename to .pdb and we'll
                // search for it in the SymbolStores configured for the current debug session.
                if (string.IsNullOrWhiteSpace(symbolFileName) ||
                    binaryName.Equals(symbolFileName, StringComparison.OrdinalIgnoreCase))
                {
                    symbolFileName = Path.ChangeExtension(binaryName, ".pdb");
                    format = ModuleFormat.Pdb;
                }
            }

            if (string.IsNullOrWhiteSpace(symbolFileName))
            {
                return false;
            }

            var searchQuery = new ModuleSearchQuery(symbolFileName, buildId, format)
            {
                RequireDebugInfo = true,
                ForceLoad = forceLoad
            };

            string filepath = useSymbolStores
                ? await _moduleFileFinder.FindFileAsync(searchQuery, searchLog)
                : null;

            if (filepath == null) { return false; }

            return AddSymbolFile(filepath, lldbModule, searchLog);
        }

        DebugLinkLocation GetSymbolFileDirAndName(
             SbModule lldbModule, TextWriter log)
        {
            var linkLocation = new DebugLinkLocation();
            SbFileSpec symbolFileSpec = lldbModule.GetSymbolFileSpec();
            string symbolFileDirectory = symbolFileSpec?.GetDirectory();
            string symbolFileName = symbolFileSpec?.GetFilename();

            SbFileSpec binaryFileSpec = lldbModule.GetFileSpec();
            string binaryDirectory = binaryFileSpec?.GetDirectory();
            string binaryFilename = binaryFileSpec?.GetFilename();

            // If there is no path to the binary, there is nothing we can do.
            if (string.IsNullOrEmpty(binaryDirectory) || string.IsNullOrEmpty(binaryFilename))
            {
                linkLocation.Directory = symbolFileDirectory;
                linkLocation.Filename = symbolFileName;
                return linkLocation;
            }

            // When lldb can't find the symbol file, it sets the symbol file spec to the path of
            // the binary file. If the file name or path is different, we just return the filename
            // (if it is not empty).
            if (!string.IsNullOrEmpty(symbolFileName) &&
                (symbolFileDirectory != binaryDirectory || symbolFileName != binaryFilename))
            {
                linkLocation.Directory = symbolFileDirectory;
                linkLocation.Filename = symbolFileName;
                return linkLocation;
            }

            // Let us look up the symbol file name and directory in the binary.
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
                return linkLocation;
            }

            DebugLinkLocationInfo modulePathSection = _moduleParser.ParseDebugLinkInfo(binaryPath);

            if (modulePathSection.HasError)
            {
                log.WriteLineAndTrace(modulePathSection.Error);
            }

            return modulePathSection.Data;
        }

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
