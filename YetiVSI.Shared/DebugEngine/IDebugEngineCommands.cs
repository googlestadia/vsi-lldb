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

﻿using System.IO;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// UI accessible commands that can be invoked on a DebugEngine.
    /// </summary>
    public interface IDebugEngineCommands
    {
        /// <summary>
        /// Reload the .natvis files.
        /// </summary>
        bool ReloadNatvis(TextWriter writer, out string resultDescription);

        /// <summary>
        /// Logs Natvis stats at the given verbosity level.
        /// </summary>
        /// <param name="verbosityLevel"></param>
        void LogNatvisStats(TextWriter writer, int verbosityLevel);
    }
}
