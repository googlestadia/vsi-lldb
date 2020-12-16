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

using DebuggerApi;

namespace YetiVSI.LLDBShell
{
    // Enables the package to execute LLDB shell commands.
    public interface ILLDBShell
    {
        // Adds the underlying |debugger| to execute LLDB shell commands.
        //
        // Client should make sure to call RemoveDebugger() when the instance is no longer valid.
        void AddDebugger(SbDebugger debugger);

        // Removes the |debugger| from executing LLDB shell commands.
        void RemoveDebugger(SbDebugger debugger);

        // Executes the LLDB shell |command| and logs errors to the |errorReporter|.
        void ExecuteCommand(string command);
    }
}
