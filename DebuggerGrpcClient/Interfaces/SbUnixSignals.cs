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

namespace DebuggerApi
{
    /// <summary>
    /// Interface mirrors the SBUnixSignals API as closely as possible.
    /// </summary>
    public interface SbUnixSignals
    {
        /// <summary>
        /// Get whether the debugger should stop on a specific signal.
        /// </summary>
        /// <param name="signalNumber">Signal number</param>
        /// <returns>True if the signal is a stopping signal.</returns>
        bool GetShouldStop(int signalNumber);

        /// <summary>
        /// Set whether the debugger should stop on a specific signal.
        /// </summary>
        /// <param name="signalNumber">Signal number</param>
        /// <param name="value">Whether the signal should cause the debugger to stop</param>
        /// <returns></returns>
        bool SetShouldStop(int signalNumber, bool value);
    }
}
