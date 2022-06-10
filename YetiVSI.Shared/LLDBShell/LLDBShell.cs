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
using System.Diagnostics;
using System.Linq;
using DebuggerApi;
using Microsoft.VisualStudio.Shell;

namespace YetiVSI.LLDBShell
{
    // LLDB shell command service.  Command output can be printed output to a Command Window.
    //
    // Note this implementation only supports dispatching commands to a single SbDebugger. Many
    // debuggers can be attached, however commands will only be dispatched to the most recently
    // added one.  We track multiple for debugging and error reporting purposes.
    public sealed class LLDBShell : SLLDBShell, ILLDBShell
    {
        readonly CommandWindowWriter _commandWindowWriter;
        readonly HashSet<SbDebugger> _debuggers;

        public LLDBShell(CommandWindowWriter commandWindowWriter)
        {
            _commandWindowWriter = commandWindowWriter;
            _debuggers = new HashSet<SbDebugger>();
        }

        public void AddDebugger(SbDebugger debugger)
        {
            _debuggers.Add(debugger);
        }

        public void RemoveDebugger(SbDebugger debugger)
        {
            _debuggers.Remove(debugger);
        }

        public void ClearAllDebuggers()
        {
            _debuggers.Clear();
        }

        public void ExecuteCommand(string command)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Trace.WriteLine($"Executing LLDB Shell command '{command}'");

            if (_debuggers.Count == 0)
            {
                _commandWindowWriter.PrintErrorMsg(
                    "ERROR: LLDB Shell command not handled. No debuggers attached.");
                return;
            }

            // TODO: Provide a mechanism for the client to pick which debugger to dispatch
            // the command to.
            if (_debuggers.Count > 1)
            {
                _commandWindowWriter.PrintErrorMsg(
                    $"ERROR: There appears to be multiple ({_debuggers.Count}) LLDB debuggers " +
                    "attached and we don't currently support that. If this is unexpected you can " +
                    "try restarting Visual Studio.");
                return;
            }

            var commandInterpreter = _debuggers.First().GetCommandInterpreter();
            if (commandInterpreter == null)
            {
                _commandWindowWriter.PrintErrorMsg(
                    "Unexpected ERROR: No command interpreter was found for the LLDB Shell.");
                return;
            }

            commandInterpreter.HandleCommand(command, out SbCommandReturnObject commandResult);
            PrintCommandResult(commandResult);
        }

        // Prints the output from the |commandResult| to the Command Window.  If a Command Window
        // doesn't exist the output is printed to the logs instead.
        private void PrintCommandResult(SbCommandReturnObject commandResult)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (commandResult == null)
            {
                string errorMessage = "ERROR: The LLDB Shell command failed to return a result.";
                Trace.WriteLine(errorMessage);
                _commandWindowWriter.PrintLine(errorMessage);
                return;
            }
            _commandWindowWriter.PrintLine(commandResult.GetDescription());
        }
    }
}
