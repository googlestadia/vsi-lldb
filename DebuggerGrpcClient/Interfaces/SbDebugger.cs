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

using System.Collections.Generic;

namespace DebuggerApi
{
    /// <summary>
    /// Interface mirrors the SBDebugger API as closely as possible.
    /// </summary>
    public interface SbDebugger
    {
        /// <summary>
        /// Set async execution for the command interpreter.
        /// </summary>
        // Disable "Avoid "Async" suffix in names of methods that do not return an awaitable type."
        // since the name of the method should reflect the name in LLDB.
#pragma warning disable VSTHRD200
        void SetAsync(bool async);
#pragma warning restore VSTHRD200

        /// <summary>
        /// Set init file skipping policy.
        /// </summary>
        void SkipLLDBInitFiles(bool skip);

        /// <summary>
        /// Returns a command interpreter that can execute LLDB shell commands.
        /// </summary>
        SbCommandInterpreter GetCommandInterpreter();

        /// <summary>
        /// Create a new target that represents the program being debugged.  You can
        /// either create a target for a specific program by passing in the filename,
        /// or create an empty target that will be populated later (ie. by attach).
        /// </summary>
        RemoteTarget CreateTarget(string filename);

        /// <summary>
        /// Deletes an IDebuggerTarget.  Any targets that are created by 'CreateTarget'
        /// must be explicitly deleted, so that all resources are cleaned up
        /// immediately instead of waiting for GC.
        /// </summary>
        bool DeleteTarget(RemoteTarget target);

        /// <summary>
        /// Set the selected platform.
        /// </summary>
        void SetSelectedPlatform(SbPlatform platform);

        /// <summary>
        /// Gets the currently selected platform.
        /// </summary>
        SbPlatform GetSelectedPlatform();

        /// <summary>
        /// Enable LLDB internal logging.  Takes a log channel and a list of log types.
        /// </summary>
        bool EnableLog(string channel, IEnumerable<string> types);

        /// <summary>
        /// Checks whether specified platform is available.
        /// </summary>
        bool IsPlatformAvailable(string platformName);
    }
}
