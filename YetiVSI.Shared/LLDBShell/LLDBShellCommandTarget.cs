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

using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using YetiCommon;

namespace YetiVSI.LLDBShell
{
    // Handles LLDB Shell commands from a Command Window.
    public class LLDBShellCommandTarget
    {
        private readonly IServiceProvider serviceProvider;

        private readonly CommandWindowWriter commandWindowWriter;

        public static LLDBShellCommandTarget Register(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return new LLDBShellCommandTarget(serviceProvider);
        }

        private LLDBShellCommandTarget(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            this.serviceProvider = serviceProvider;
            commandWindowWriter = new CommandWindowWriter(
                (IVsCommandWindow)serviceProvider.GetService(typeof(SVsCommandWindow)));
            var menuCommand = new OleMenuCommand(HandleLLDBCommand,
                new CommandID(YetiConstants.CommandSetGuid, PkgCmdID.cmdidLLDBShellExec));
            // Accept any parameter value.
            menuCommand.ParametersDescription = "$";

            (serviceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService)
                ?.AddCommand(menuCommand);
        }

        private void HandleLLDBCommand(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!TryGetCommand(e, out string shellCommand))
            {
                return;
            }

            var lldbShell = (ILLDBShell)serviceProvider.GetService(typeof(SLLDBShell));
            if (lldbShell == null)
            {
                commandWindowWriter.PrintErrorMsg("ERROR: Unable to execute LLDB Shell command." +
                    "  No shell service found.");
                return;
            }

            try
            {
                lldbShell.ExecuteCommand(shellCommand);
            }
            catch (Exception ex)
            {
                commandWindowWriter.PrintErrorMsg(
                    "ERROR: LLDB Shell command failed. Reason: '" + ex.Message + "'");
            }
        }

        /// <summary>
        /// Extracts the string command from args. Logs errors when unsuccessful.
        /// </summary>
        /// <param name="args">The event args containing.  Expected to be of type
        /// OleMenuCmdEventArgs</param>
        /// <param name="shellCommand">The command extracted from args.</param>
        /// <returns>True if a non-null, non-empty string command was extracted.</returns>
        private bool TryGetCommand(EventArgs args, out string shellCommand)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            shellCommand = null;

            var cmdEventArgs = args as OleMenuCmdEventArgs;
            if (cmdEventArgs == null || !(cmdEventArgs.InValue is string))
            {
                commandWindowWriter.PrintErrorMsg(string.Format("ERROR: LLDB Shell command not " +
                    "handled. Unexpected parameter type ({0}).", cmdEventArgs?.GetType()));
                return false;
            }

            var command = cmdEventArgs.InValue as string;
            if (string.IsNullOrWhiteSpace(command))
            {
                commandWindowWriter.PrintErrorMsg("ERROR: LLDB Shell command not handled. No " +
                    "command found.  Try 'LLDB.Shell help' for a list of commands.");
                return false;
            }

            shellCommand = command;
            return true;
        }
    }
}
