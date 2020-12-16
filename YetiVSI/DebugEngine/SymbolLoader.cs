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

﻿using DebuggerApi;
using System;
using System.Diagnostics;
using System.IO;
using YetiCommon;
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
        bool LoadSymbols(SbModule lldbModule, TextWriter searchLog);
    }

    public class SymbolLoader : ISymbolLoader
    {
        public class Factory
        {
            ILldbModuleUtil moduleUtil;
            IBinaryFileUtil binaryFileUtil;
            IModuleFileFinder moduleFileFinder;

            public Factory(ILldbModuleUtil moduleUtil, IBinaryFileUtil binaryFileUtil,
                IModuleFileFinder moduleFileFinder)
            {
                this.moduleUtil = moduleUtil;
                this.binaryFileUtil = binaryFileUtil;
                this.moduleFileFinder = moduleFileFinder;
            }

            public virtual ISymbolLoader Create(SbCommandInterpreter lldbCommandInterpreter) =>
                new SymbolLoader(moduleUtil, binaryFileUtil, moduleFileFinder,
                    lldbCommandInterpreter);
        }

        ILldbModuleUtil moduleUtil;
        IBinaryFileUtil binaryFileUtil;
        IModuleFileFinder moduleFileFinder;
        SbCommandInterpreter lldbCommandInterpreter;

        public SymbolLoader(ILldbModuleUtil moduleUtil, IBinaryFileUtil binaryFileUtil,
            IModuleFileFinder moduleFileFinder, SbCommandInterpreter lldbCommandInterpreter)
        {
            this.moduleUtil = moduleUtil;
            this.binaryFileUtil = binaryFileUtil;
            this.moduleFileFinder = moduleFileFinder;
            this.lldbCommandInterpreter = lldbCommandInterpreter;
        }

        public virtual bool LoadSymbols(SbModule lldbModule, TextWriter searchLog)
        {
            if (lldbModule == null) { throw new ArgumentNullException(nameof(lldbModule)); }
            searchLog = searchLog ?? TextWriter.Null;

            // Return early if symbols are already loaded
            if (moduleUtil.HasSymbolsLoaded(lldbModule)) { return true; }

            var symbolFileName = GetSymbolFileName(lldbModule, searchLog);
            if (string.IsNullOrEmpty(symbolFileName))
            {
                searchLog.WriteLine(ErrorStrings.SymbolFileNameUnknown);
                Trace.WriteLine(ErrorStrings.SymbolFileNameUnknown);
                return false;
            }
            BuildId uuid = new BuildId(lldbModule.GetUUIDString());

            var filepath = moduleFileFinder.FindFile(symbolFileName, uuid, true, searchLog);
            if (filepath == null) { return false; }

            return AddSymbolFile(filepath, lldbModule, searchLog);
        }

        string GetSymbolFileName(SbModule lldbModule, TextWriter log)
        {
            var symbolFileSpec = lldbModule.GetSymbolFileSpec();
            var symbolFileDirectory = symbolFileSpec?.GetDirectory();
            var symbolFileName = symbolFileSpec?.GetFilename();

            var binaryFileSpec = lldbModule.GetFileSpec();
            var binaryDirectory = binaryFileSpec?.GetDirectory();
            var binaryFilename = binaryFileSpec?.GetFilename();

            // When lldb can't find the symbol file, it sets the symbol file spec to the path of
            // the binary file. We check for that so we can attempt to extract the symbol file name
            // ourselves.
            if (!string.IsNullOrEmpty(binaryDirectory) && !string.IsNullOrEmpty(binaryFilename) &&
                (string.IsNullOrEmpty(symbolFileName) || (symbolFileDirectory == binaryDirectory &&
                symbolFileName == binaryFilename)))
            {
                string symbolPath;
                try
                {
                    symbolPath = Path.Combine(binaryDirectory, binaryFilename);
                }
                catch (ArgumentException e)
                {
                    var errorString = ErrorStrings.InvalidBinaryPathOrName(binaryDirectory,
                        binaryFilename, e.Message);
                    Trace.WriteLine(errorString);
                    log.WriteLine(errorString);
                    return null;
                }

                try
                {
                    symbolFileName = binaryFileUtil.ReadSymbolFileName(symbolPath);
                }
                catch (BinaryFileUtilException e)
                {
                    Trace.WriteLine(e.ToString());
                    log.WriteLine(e.Message);
                    return null;
                }
            }

            return symbolFileName;
        }

        bool AddSymbolFile(string filepath, SbModule module, TextWriter searchLog)
        {
            var command = "target symbols add";

            var platformFileSpec = module.GetPlatformFileSpec();
            if (platformFileSpec != null)
            {
                var platformPath = FileUtil.PathCombineLinux(platformFileSpec.GetDirectory(),
                    platformFileSpec.GetFilename());
                // The -s flag specifies the path of the module to add symbols to.
                command += " -s " + LldbCommandUtil.QuoteArgument(platformPath);
            }

            command += " " + LldbCommandUtil.QuoteArgument(filepath);

            SbCommandReturnObject commandResult;
            lldbCommandInterpreter.HandleCommand(command, out commandResult);
            Trace.WriteLine($"Executed LLDB command '{command}' with result:" +
                Environment.NewLine + commandResult.GetDescription());
            if (!commandResult.Succeeded())
            {
                searchLog.WriteLine("LLDB error: " + commandResult.GetError());
                return false;
            }
            searchLog.WriteLine("LLDB output: " + commandResult.GetOutput());

            searchLog.WriteLine("Symbols loaded successfully.");
            Trace.WriteLine($"Successfully loaded symbol file '{filepath}'.");

            return true;
        }
    }
}
