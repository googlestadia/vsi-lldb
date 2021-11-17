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

﻿using DebuggerApi;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
        /// <exception cref="ArgumentNullException">Thrown when |lldbModule| is null.</exception>
        Task<bool> LoadSymbolsAsync(SbModule lldbModule, TextWriter searchLog, bool useSymbolStores);
    }

    public class SymbolLoader : ISymbolLoader
    {
        public class Factory
        {
            readonly IBinaryFileUtil _binaryFileUtil;
            readonly IModuleFileFinder _moduleFileFinder;

            public Factory(IBinaryFileUtil binaryFileUtil,
                IModuleFileFinder moduleFileFinder)
            {
                _binaryFileUtil = binaryFileUtil;
                _moduleFileFinder = moduleFileFinder;
            }

            public virtual ISymbolLoader Create(SbCommandInterpreter lldbCommandInterpreter) =>
                new SymbolLoader(_binaryFileUtil, _moduleFileFinder,
                    lldbCommandInterpreter);
        }

        readonly IBinaryFileUtil _binaryFileUtil;
        readonly IModuleFileFinder _moduleFileFinder;
        readonly SbCommandInterpreter _lldbCommandInterpreter;

        public SymbolLoader(IBinaryFileUtil binaryFileUtil,
            IModuleFileFinder moduleFileFinder, SbCommandInterpreter lldbCommandInterpreter)
        {
            _binaryFileUtil = binaryFileUtil;
            _moduleFileFinder = moduleFileFinder;
            _lldbCommandInterpreter = lldbCommandInterpreter;
        }

        public virtual async Task<bool> LoadSymbolsAsync(
            SbModule lldbModule, TextWriter searchLog, bool useSymbolStores)
        {
            if (lldbModule == null) { throw new ArgumentNullException(nameof(lldbModule)); }
            searchLog = searchLog ?? TextWriter.Null;

            // Return early if symbols are already loaded
            if (lldbModule.HasSymbolsLoaded()) { return true; }

            (string symbolFileDir, string symbolFileName) =
                await GetSymbolFileDirAndNameAsync(lldbModule, searchLog);
            if (string.IsNullOrEmpty(symbolFileName))
            {
                await searchLog.WriteLogAsync(ErrorStrings.SymbolFileNameUnknown);
                return false;
            }
            var uuid = new BuildId(lldbModule.GetUUIDString());

            // If we have a search directory, let us look up the symbol file in there.
            if (!string.IsNullOrEmpty(symbolFileDir))
            {
                string symbolFilePath = string.Empty;
                try
                {
                    symbolFilePath = Path.Combine(symbolFileDir, symbolFileName);
                    BuildId fileUuid = await _binaryFileUtil.ReadBuildIdAsync(symbolFilePath);
                    if (fileUuid == uuid)
                    {
                        return await AddSymbolFileAsync(symbolFilePath, lldbModule, searchLog);
                    }
                }
                catch (Exception e) when (e is InvalidBuildIdException ||
                                          e is BinaryFileUtilException || e is ArgumentException)
                {
                    // Just ignore the symbol file path if we could not read the build Id.
                    Trace.WriteLine($"Could not read build Id from {symbolFilePath} " +
                                    $"for module {lldbModule.GetFileSpec().GetFilename()} " +
                                    $"(Message: {e.Message}).");
                }
            }

            string filepath = useSymbolStores
                               ? await _moduleFileFinder.FindFileAsync(
                                   symbolFileName, uuid, true, searchLog)
                               : null;
            if (filepath == null) { return false; }

            return await AddSymbolFileAsync(filepath, lldbModule, searchLog);
        }

        async Task<(string, string)> GetSymbolFileDirAndNameAsync(
            SbModule lldbModule, TextWriter log)
        {
            SbFileSpec symbolFileSpec = lldbModule.GetSymbolFileSpec();
            string symbolFileDirectory = symbolFileSpec?.GetDirectory();
            string symbolFileName = symbolFileSpec?.GetFilename();

            SbFileSpec binaryFileSpec = lldbModule.GetFileSpec();
            string binaryDirectory = binaryFileSpec?.GetDirectory();
            string binaryFilename = binaryFileSpec?.GetFilename();

            // If there is no path to the binary, there is nothing we can do.
            if (string.IsNullOrEmpty(binaryDirectory) || string.IsNullOrEmpty(binaryFilename))
            {
                return (symbolFileDirectory, symbolFileName);
            }

            // When lldb can't find the symbol file, it sets the symbol file spec to the path of
            // the binary file. If the file name or path is different, we just return the filename
            // (if it is not empty).
            if (!string.IsNullOrEmpty(symbolFileName) &&
                (symbolFileDirectory != binaryDirectory || symbolFileName != binaryFilename))
            {
                return (symbolFileDirectory, symbolFileName);
            }

            symbolFileDirectory = null;

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
                await log.WriteLogAsync(errorString);
                return (null, null);
            }

            // Read the symbol file name from the binary.
            try
            {
                symbolFileName = await _binaryFileUtil.ReadSymbolFileNameAsync(binaryPath);
            }
            catch (BinaryFileUtilException e)
            {
                await log.WriteLogAsync(e.Message);
                return (null, null);
            }

            // Try to read the debug info directory.
            try
            {
                symbolFileDirectory = await _binaryFileUtil.ReadSymbolFileDirAsync(binaryPath);
            }
            catch (BinaryFileUtilException e)
            {
                // Just log the message (the directory section is optional).
                await log.WriteLogAsync(e.Message);
            }

            return (symbolFileDirectory, symbolFileName);
        }

        async Task<bool> AddSymbolFileAsync(string filepath, SbModule module, TextWriter searchLog)
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
                await searchLog.WriteLogAsync("LLDB error: " + commandResult.GetError());
                return false;
            }

            string text = $"LLDB output: {commandResult.GetOutput()}{Environment.NewLine}" +
                $"Successfully loaded symbol file '{filepath}'.";
            await searchLog.WriteLogAsync(text);

            return true;
        }
    }
}
