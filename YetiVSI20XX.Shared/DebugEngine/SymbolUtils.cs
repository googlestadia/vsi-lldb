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

﻿using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Contains methods to resolve symbols and modules storage paths.
    /// </summary>
    public static class SymbolUtils
    {
        /// <summary>
        /// Combine symbol store paths with symbol cach path. Method also add paths from system
        /// environment variables _NT_ALT_SYMBOL_PATH and _NT_SYMBOL_PATH.
        /// </summary>
        /// <returns>All paths to lookup binary and symbols combined with ';' separator.</returns>
        public static string GetCombinedLookupPaths(string searchPath, string cachePath)
        {
            searchPath = Environment.ExpandEnvironmentVariables(searchPath);
            cachePath = Environment.ExpandEnvironmentVariables(cachePath);
            cachePath = string.IsNullOrEmpty(cachePath) ? "" : "cache*" + cachePath;
            string ntAltSymbolPath =
                Environment.GetEnvironmentVariable("_NT_ALT_SYMBOL_PATH") ?? "";
            string ntSymbolPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH") ?? "";
            return string.Join(";", cachePath, searchPath, ntAltSymbolPath, ntSymbolPath);
        }
    }
}