// Copyright 2022 Google LLC
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
using System.Diagnostics;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using YetiCommon;
using YetiVSI.Util;

namespace YetiVSI.Orbit
{
    // A menu command to launch a game and profile it with Orbit.
    public sealed class LaunchWithOrbitCommand
    {
        readonly JoinableTaskContext _taskContext;
        readonly IVsSolutionBuildManager _solutionBuildManager;

        // Events have to be stored in this object, see
        // https://stackoverflow.com/questions/3874015/subscription-to-dte-events-doesnt-seem-to-work-events-dont-get-called
        readonly EnvDTE.SelectionEvents _selectionEvents;

        public static LaunchWithOrbitCommand Register(JoinableTaskContext taskContext,
                                                      Package package)
        {
            taskContext.ThrowIfNotOnMainThread();

            const string baseErrorMessage = "Failed to register LaunchWithOrbitCommand: ";
            var serviceProvider = ((IServiceProvider)package);
            if (!(serviceProvider.GetService(typeof(IVsSolutionBuildManager)) is
                IVsSolutionBuildManager solutionBuildManager))
            {
                Trace.WriteLine(baseErrorMessage + "IVsSolutionBuildManager not found.");
                return null;
            }

            if (!(serviceProvider.GetService(typeof(IMenuCommandService)) is OleMenuCommandService
                commandService))
            {
                Trace.WriteLine(baseErrorMessage + "IMenuCommandService not found.");
                return null;
            }

            if (!(serviceProvider.GetService(typeof(EnvDTE.DTE)) is DTE2 dte2))
            {
                Trace.WriteLine(baseErrorMessage + "DTE2 not found.");
                return null;
            }

            EnvDTE.SelectionEvents selectionEvents = dte2.Events.SelectionEvents;
            var command =
                new LaunchWithOrbitCommand(taskContext, solutionBuildManager, selectionEvents);
            var menuCommandId = new CommandID(YetiConstants.CommandSetGuid,
                                              PkgCmdID.cmdidLaunchWithOrbitCommandMenu);
            var menuItem = new OleMenuCommand(command.Execute, menuCommandId);
            menuItem.BeforeQueryStatus += command.OnBeforeQueryStatus;
            commandService.AddCommand(menuItem);

            var toolbarCommandId = new CommandID(YetiConstants.CommandSetGuid,
                                                 PkgCmdID.cmdidLaunchWithOrbitCommandToolbar);
            var toolbarItem = new OleMenuCommand(command.Execute, toolbarCommandId);
            toolbarItem.BeforeQueryStatus += command.OnBeforeQueryStatus;
            commandService.AddCommand(toolbarItem);

            // Toggle the toolbar item if the active configuration or the startup
            // project change (apparently, anything in VS is a selection).
            selectionEvents.OnChange += () => { command.OnBeforeQueryStatus(toolbarItem, null); };

            return command;
        }

        /// <summary>
        /// Constructor public for testing. Use Register() in production code.
        /// </summary>
        public LaunchWithOrbitCommand(JoinableTaskContext taskContext,
                                      IVsSolutionBuildManager solutionBuildManager,
                                      EnvDTE.SelectionEvents selectionEvents)
        {
            _taskContext = taskContext;
            _solutionBuildManager = solutionBuildManager;
            _selectionEvents = selectionEvents;
        }

        /// <summary>
        /// Determines the visibility of the command. The command is visible if the active
        /// configuration of the startup project has GGP as platform.
        /// Public for testing.
        /// </summary>
        public void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            _taskContext.ThrowIfNotOnMainThread();

            var command = sender as OleMenuCommand;
            if (command == null)
            {
                Trace.WriteLine("Failed to determine visibility of LaunchWithOrbitCommand: " +
                                "Command is not an OleMenuCommand.");
                return;
            }

            bool ggpProject = IsGgpStartupProject();
            command.Visible = ggpProject; // Works for the menu item.
            command.Enabled = ggpProject; // Works for the toolbar item.
        }

        /// <summary>
        /// Builds and launches the startup project, starts Orbit and instructs it to attach to the
        /// game process.
        /// Public for testing.
        /// </summary>
        public void Execute(object sender, EventArgs e)
        {
            _taskContext.ThrowIfNotOnMainThread();

            var launchOptions = DebugLaunchOptions.NoDebug | DebugLaunchOptions.Profiling;
            int res = _solutionBuildManager.DebugLaunch((uint) launchOptions);
            if (res != VSConstants.S_OK)
            {
                // GgpDebugQueryTarget shows a proper error message if this fails.
                Trace.WriteLine($"Launch with Orbit command failed: DebugLaunch() return {res}");
                return;
            }
        }

        /// <summary>
        /// Returns true if the the active configuration of the startup project has GGP as platform.
        /// In other words, if you hit F5, a game would run on a gamelet.
        /// </summary>
        bool IsGgpStartupProject()
        {
            _taskContext.ThrowIfNotOnMainThread();

            string baseErrorMessage =
                "Failed to determine whether active config in startup project is GGP: ";
            if (_solutionBuildManager.get_StartupProject(out IVsHierarchy hier) != VSConstants.S_OK)
            {
                Trace.WriteLine(baseErrorMessage + "Failed to get startup project");
                return false;
            }

            var activeCfg = new IVsProjectCfg[1];
            _solutionBuildManager.FindActiveProjectCfg(IntPtr.Zero, IntPtr.Zero, hier, activeCfg);
            if (activeCfg[0] == null)
            {
                Trace.WriteLine(baseErrorMessage + "Failed to get active project configuration");
                return false;
            }

            if (activeCfg[0].get_CanonicalName(out string cname) != VSConstants.S_OK)
            {
                Trace.WriteLine(baseErrorMessage + "Failed to get canonical name");
                return false;
            }

            // E.g. "Debug|GGP".
            return cname.EndsWith("|GGP");
        }
    }
}