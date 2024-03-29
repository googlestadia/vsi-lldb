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
using System.IO;
using System.Threading.Tasks;
using DebuggerApi;
using JetBrains.Annotations;
using YetiCommon;
using YetiCommon.CastleAspects;
using YetiCommon.Logging;

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
        /// Checks if <c>lldbModule</c> is a placeholder module, and if so, attempts to load the
        /// matching binary and replace <c>lldbModule</c> with the newly loaded module.
        /// </summary>
        /// <returns>
        /// A new (updated) module or <c>lldbModule</c> if binary loading failed,
        /// and <c>true</c> if the binary was loaded successfully, <c>false</c> otherwise.
        /// </returns>
        Task<(SbModule, bool)> LoadBinaryAsync([NotNull] SbModule lldbModule,
                                               [NotNull] TextWriter searchLog,
                                               bool forceLoad);
    }

    public class LldbModuleReplacedEventArgs : EventArgs
    {
        public SbModule AddedModule { get; }
        public SbModule RemovedModule { get; }

        public LldbModuleReplacedEventArgs(SbModule addedModule, SbModule removedModule)
        {
            AddedModule = addedModule;
            RemovedModule = removedModule;
        }
    }

    public class BinaryLoader : SimpleDecoratorSelf<IBinaryLoader>, IBinaryLoader
    {
        public class Factory
        {
            readonly IModuleFileFinder _moduleFileFinder;

            public Factory(IModuleFileFinder moduleFileFinder)
            {
                _moduleFileFinder = moduleFileFinder;
            }

            public virtual IBinaryLoader Create(RemoteTarget lldbTarget)
            {
                return new BinaryLoader(_moduleFileFinder, lldbTarget);
            }
        }

        public event EventHandler<LldbModuleReplacedEventArgs> LldbModuleReplaced;

        readonly IModuleFileFinder _moduleFileFinder;
        readonly RemoteTarget _lldbTarget;

        public BinaryLoader(IModuleFileFinder moduleFileFinder,
                            RemoteTarget lldbTarget)
        {
            _moduleFileFinder = moduleFileFinder;
            _lldbTarget = lldbTarget;
        }

        public virtual async Task<(SbModule, bool)> LoadBinaryAsync(
            SbModule lldbModule, TextWriter searchLog, bool forceLoad)
        {
            string binaryName = lldbModule.GetPlatformFileSpec()?.GetFilename();
            if (string.IsNullOrWhiteSpace(binaryName))
            {
                return (lldbModule, false);
            }

            BuildId buildId = new BuildId(lldbModule.GetUUIDString());

            var searchQuery =
                new ModuleSearchQuery(binaryName, buildId, lldbModule.GetModuleFormat())
                {
                    ForceLoad = forceLoad,
                    RequireDebugInfo = false
                };

            string binaryPath = await _moduleFileFinder.FindFileAsync(searchQuery, searchLog);

            if (binaryPath == null)
            {
                return (lldbModule, false);
            }

            if (TryReplaceModule(lldbModule, binaryPath, out SbModule addedModule))
            {
                searchLog.WriteLineAndTrace($"Successfully loaded binary '{binaryPath}'.");
                LldbModuleReplaced?.Invoke(
                    Self, new LldbModuleReplacedEventArgs(addedModule, lldbModule));

                return (addedModule, true);
            }

            searchLog.WriteLineAndTrace(ErrorStrings.FailedToLoadBinary(binaryPath));
            return (lldbModule, false);
        }

        bool TryReplaceModule(SbModule placeholder, string binaryPath, out SbModule addedModule)
        {
            PlaceholderModuleProperties properties =
                placeholder.GetPlaceholderProperties(_lldbTarget);
            if (properties == null)
            {
                addedModule = null;
                return false;
            }

            string triple = placeholder.GetTriple();
            string uuid = placeholder.GetUUIDString();
            _lldbTarget.RemoveModule(placeholder);
            addedModule = _lldbTarget.AddModule(binaryPath, triple, uuid);
            
            return addedModule != null
                && addedModule.ApplyPlaceholderProperties(properties, _lldbTarget);
        }
    }
}
