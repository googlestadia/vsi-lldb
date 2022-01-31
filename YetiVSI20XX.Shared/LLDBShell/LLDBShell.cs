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
using Microsoft.VisualStudio.Threading;
using YetiVSI.Util;

namespace YetiVSI.LLDBShell
{
    // LLDB shell command service.  Command output can be printed output to a Command Window.
    //
    // Note this implementation only supports dispatching commands to a single SbDebugger. Many
    // debuggers can be attached, however commands will only be dispatched to the most recently
    // added one.  We track multiple for debugging and error reporting purposes.
    public sealed class LLDBShell : SLLDBShell, ILLDBShell
    {
        private JoinableTaskContext taskContext;
        private CommandWindowWriter commandWindowWriter;

        private HashSet<SbDebugger> debuggers;

        public LLDBShell(JoinableTaskContext taskContext, CommandWindowWriter commandWindowWriter)
        {
            this.taskContext = taskContext;
            this.commandWindowWriter = commandWindowWriter;
            debuggers = new HashSet<SbDebugger>();
        }

        #region ILLDBShell

        // Should only be called from the UI thread!
        public void AddDebugger(SbDebugger debugger)
        {
            taskContext.ThrowIfNotOnMainThread();

            debuggers.Add(debugger);
        }

        // Should only be called from the UI thread!
        public void RemoveDebugger(SbDebugger debugger)
        {
            taskContext.ThrowIfNotOnMainThread();

            debuggers.Remove(debugger);
        }

        // Should only be called from the UI thread!
        public void ClearAllDebuggers()
        {
            taskContext.ThrowIfNotOnMainThread();

            debuggers.Clear();
        }

        // Should only be called from the UI thread!
        public void ExecuteCommand(string command)
        {
            taskContext.ThrowIfNotOnMainThread();

            Trace.WriteLine($"Executing LLDB Shell command '{command}'");

            if (debuggers.Count == 0)
            {
                commandWindowWriter.PrintErrorMsg(
                    "ERROR: LLDB Shell command not handled. No debuggers attached.");
                return;
            }

            // TODO: Provide a mechanism for the client to pick which debugger to dispatch
            // the command to.
            if (debuggers.Count > 1)
            {
                commandWindowWriter.PrintErrorMsg(
                    $"ERROR: There appears to be multiple ({debuggers.Count}) LLDB debuggers " +
                    "attached and we don't currently support that. If this is unexpected you can " +
                    "try restarting Visual Studio.");
                return;
            }

            var commandInterpreter = debuggers.First().GetCommandInterpreter();
            if (commandInterpreter == null)
            {
                commandWindowWriter.PrintErrorMsg(
                    "Unexpected ERROR: No command interpreter was found for the LLDB Shell.");
                return;
            }

            SbCommandReturnObject commandResult;
            commandInterpreter.HandleCommand(command, out commandResult);
            PrintCommandResult(commandResult);
        }

        #endregion

        // Prints the output from the |commandResult| to the Command Window.  If a Command Window
        // doesn't exist the output is printed to the logs instead.
        private void PrintCommandResult(SbCommandReturnObject commandResult)
        {
            taskContext.ThrowIfNotOnMainThread();

            if (commandResult == null)
            {
                string errorMessage = "ERROR: The LLDB Shell command failed to return a result.";
                Trace.WriteLine(errorMessage);
                commandWindowWriter.PrintLine(errorMessage);
                return;
            }
            commandWindowWriter.PrintLine(commandResult.GetDescription());
        }
    }
}
