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
using YetiCommon.CastleAspects;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Handles loading binary files for individual modules.
    /// </summary>
    public interface IBinaryLoader
    {
        /// <summary>
        /// Raised when a placeholder module is removed and replaced with a newly loaded module.
        /// </summary>
        event EventHandler<LldbModuleReplacedEventArgs> LldbModuleReplaced;

        /// <summary>
        /// Checks if |lldbModule| is a placeholder module, and if so, attempts to load the matching
        /// binary and replace |lldbModule| with the newly loaded module.
        /// </summary>
        /// <returns>
        /// True if the binary was loaded successfully or was already loaded, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when |lldbModule| is null.
        /// </exception>
        bool LoadBinary(ref SbModule lldbModule, TextWriter searchLog);
    }

    public class LldbModuleReplacedEventArgs : EventArgs
    {
        public SbModule AddedModule { get; }
        public SbModule RemovedModule { get; }

        public LldbModuleReplacedEventArgs(SbModule addedModule, SbModule removedModule)
        {
            AddedModule = addedModule;
            RemovedModule  = removedModule;
        }
    }

    public class BinaryLoader : SimpleDecoratorSelf<IBinaryLoader>, IBinaryLoader
    {
        public class Factory
        {
            ILldbModuleUtil moduleUtil;
            IModuleFileFinder moduleFileFinder;

            public Factory(ILldbModuleUtil moduleUtil, IModuleFileFinder moduleFileFinder)
            {
                this.moduleUtil = moduleUtil;
                this.moduleFileFinder = moduleFileFinder;
            }

            public virtual IBinaryLoader Create(RemoteTarget lldbTarget)
            {
                return new BinaryLoader(moduleUtil, moduleFileFinder, lldbTarget);
            }
        }

        public event EventHandler<LldbModuleReplacedEventArgs> LldbModuleReplaced;

        ILldbModuleUtil moduleUtil;
        IModuleFileFinder moduleFileFinder;
        RemoteTarget lldbTarget;


        public BinaryLoader(ILldbModuleUtil moduleUtil, IModuleFileFinder moduleFileFinder,
            RemoteTarget lldbTarget)
        {
            this.moduleUtil = moduleUtil;
            this.moduleFileFinder = moduleFileFinder;
            this.lldbTarget = lldbTarget;
        }

        public virtual bool LoadBinary(ref SbModule lldbModule, TextWriter searchLog)
        {
            if (lldbModule == null) { throw new ArgumentNullException(nameof(lldbModule)); }
            searchLog = searchLog ?? TextWriter.Null;

            if (!moduleUtil.IsPlaceholderModule(lldbModule))
            {
                return true;
            }

            var binaryName = lldbModule.GetPlatformFileSpec()?.GetFilename();
            if (string.IsNullOrEmpty(binaryName))
            {
                searchLog.WriteLine(ErrorStrings.BinaryFileNameUnknown);
                Trace.WriteLine(ErrorStrings.BinaryFileNameUnknown);
                return false;
            }

            var binaryPath = moduleFileFinder.FindFile(
                binaryName, new BuildId(lldbModule.GetUUIDString()), false, searchLog);
            if (binaryPath == null)
            {
                return false;
            }

            PlaceholderModuleProperties properties =
                moduleUtil.GetPlaceholderProperties(lldbModule, lldbTarget);
            if (properties == null)
            {
                return false;
            }
            RemoveModule(lldbModule);

            var newModule = AddModule(binaryPath, lldbModule.GetUUIDString(), searchLog);
            if (newModule == null)
            {
                return false;
            }

            if (!moduleUtil.ApplyPlaceholderProperties(newModule, properties, lldbTarget))
            {
                return false;
            }

            LldbModuleReplaced?.Invoke(Self,
                new LldbModuleReplacedEventArgs(newModule, lldbModule));

            lldbModule = newModule;
            return true;
        }

        SbModule AddModule(string binaryPath, string id, TextWriter searchLog)
        {
            var newModule = lldbTarget.AddModule(binaryPath, null, id);
            if (newModule == null)
            {
                searchLog.WriteLine(ErrorStrings.FailedToLoadBinary(binaryPath));
                Trace.WriteLine(ErrorStrings.FailedToLoadBinary(binaryPath));
                return null;
            }

            searchLog.WriteLine("Binary loaded successfully.");
            Trace.WriteLine($"Successfully loaded binary '{binaryPath}'.");
            return newModule;
        }

        void RemoveModule(SbModule placeholderModule)
        {
            lldbTarget.RemoveModule(placeholderModule);
        }
    }
}
