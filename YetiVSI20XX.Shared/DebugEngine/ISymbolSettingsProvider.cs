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

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine
{
    public interface ISymbolSettingsProvider
    {
        /// <summary>
        /// Get information on modules inclusion / exclusion settings.
        /// </summary>
        SymbolInclusionSettings GetInclusionSettings();

        /// <summary>
        /// Returns the value of the Stadia-specific setting used to enable/disable symbols server
        /// support. It returns the value of the setting in the beginning of the debug session since
        /// at the moment we only use it when loading symbols during the session start. In order for
        /// the changes to the setting to take effect, users need to restart the debug session.
        /// </summary>
        bool IsSymbolServerEnabled { get; }

        /// <summary>
        /// Gets the string containing all currently enabled symbol store paths and cache.
        /// </summary>
        void GetStorePaths(out string paths, out string cache);
    }

    public class SymbolInclusionSettings
    {
        public static string ModuleExcludedMessage(string name) =>
            $"Symbol loading for {name} disabled by Include/Exclude setting.";

        public bool IsManualLoad { get; }

        public IList<string> ExcludeList { get; }
        public IList<string> IncludeList { get; }

        public SymbolInclusionSettings(bool isManualLoad, IList<string> excludeList,
                                       IList<string> includeList)
        {
            IsManualLoad = isManualLoad;
            ExcludeList = excludeList;
            IncludeList = includeList;
        }

        public bool IsModuleIncluded(string module)
        {
            // Use "Include" list.
            if (IsManualLoad)
            {
                return IncludeList.Any(includePattern =>
                                           Regex.IsMatch(module, WildcardToRegex(includePattern),
                                                         RegexOptions.IgnoreCase));
            }

            // Use "Exclude" list.
            return !ExcludeList.Any(excludePattern =>
                                        Regex.IsMatch(module, WildcardToRegex(excludePattern),
                                                      RegexOptions.IgnoreCase));
        }

        string WildcardToRegex(string pattern) =>
            "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
    }

    public class SymbolSettingsProvider : ISymbolSettingsProvider
    {
        readonly IVsDebuggerSymbolSettingsManager120A _symbolSettingsManager;

        // This service is used to read symbol store paths. Though
        // IVsDebuggerSymbolSettingsManager120A also has methods to access store paths, it doesn't
        // include pre-defined Microsoft and Nuget services.
        readonly IVsDebugger2 _debuggerService;
        readonly JoinableTaskContext _taskContext;

        public bool IsSymbolServerEnabled { get; }

        public SymbolSettingsProvider(IVsDebuggerSymbolSettingsManager120A symbolSettingsManager,
                                      IVsDebugger2 debuggerService, bool symbolServerEnabled,
                                      JoinableTaskContext taskContext)
        {
            _symbolSettingsManager = symbolSettingsManager ?? throw new ArgumentNullException();
            _debuggerService = debuggerService;
            IsSymbolServerEnabled = symbolServerEnabled;
            _taskContext = taskContext ?? throw new ArgumentNullException();
        }

        public SymbolInclusionSettings GetInclusionSettings()
        {
            _taskContext.ThrowIfNotOnMainThread();

            IVsDebuggerSymbolSettings120A currentSettings =
                _symbolSettingsManager.GetCurrentSymbolSettings();
            bool isManualLoad = currentSettings.IsManualLoad;
            var excludeList =
                FilterEnabledModules(_symbolSettingsManager.GetCurrentSymbolSettings().ExcludeList);
            var includeList =
                FilterEnabledModules(_symbolSettingsManager.GetCurrentSymbolSettings().IncludeList);

            return new SymbolInclusionSettings(isManualLoad, excludeList, includeList);
        }

        public void GetStorePaths(out string paths, out string cache)
        {
            _taskContext.ThrowIfNotOnMainThread();

            int result = _debuggerService.GetSymbolPath(out paths, out cache);
            if (result != VSConstants.S_OK)
            {
                throw new InvalidOperationException(
                    $"Unable to retrieve store paths, the operation returned status code {result}.");
            }

            paths = paths ?? "";
            cache = cache ?? "";
        }

        IList<string> FilterEnabledModules(IDebugOptionList120A exceptionsList)
        {
            var enabledIncludedModules = new List<string>();
            for (int i = 0; i < exceptionsList.Count; i++)
            {
                if (exceptionsList[i].IsEnabled)
                {
                    enabledIncludedModules.Add(exceptionsList[i].Name);
                }
            }

            return enabledIncludedModules;
        }
    }
}