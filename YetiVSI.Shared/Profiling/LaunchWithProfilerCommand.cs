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
using YetiCommon;

namespace YetiVSI.Profiling
{
    public enum ProfilerType
    {
        Orbit,
        Dive
    }

    // A menu command to launch a game and profile it with a profiler.
    public sealed class LaunchWithProfilerCommand
    {
        readonly IVsSolutionBuildManager _solutionBuildManager;
        readonly DebugLaunchOptions _profilerLaunchOption;
        readonly string _profilerName;
        readonly IDialogUtil _dialogUtil;

        // Events have to be stored in this object, see
        // https://stackoverflow.com/questions/3874015/subscription-to-dte-events-doesnt-seem-to-work-events-dont-get-called
        readonly EnvDTE.SelectionEvents _selectionEvents;

        public static LaunchWithProfilerCommand Register(Package package,
                                                         ProfilerType profilerType,
                                                         string profilerName,
                                                         IDialogUtil dialogUtil)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            const string baseErrorMessage = "Failed to register LaunchWithProfilerCommand: ";
            var serviceProvider = ((IServiceProvider) package);
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
            var command = new LaunchWithProfilerCommand(
                solutionBuildManager, GetLaunchOption(profilerType), profilerName, dialogUtil,
                selectionEvents);
            (int menuCmdId, int toolbarCmdId) = GetMenuAndToolbarCmdIds(profilerType);

            var menuCommandId = new CommandID(YetiConstants.CommandSetGuid, menuCmdId);
            var menuItem = new OleMenuCommand(command.Execute, menuCommandId);
            menuItem.BeforeQueryStatus += command.OnBeforeQueryStatus;
            commandService.AddCommand(menuItem);

            var toolbarCommandId = new CommandID(YetiConstants.CommandSetGuid, toolbarCmdId);
            var toolbarItem = new OleMenuCommand(command.Execute, toolbarCommandId);
            toolbarItem.BeforeQueryStatus += command.OnBeforeQueryStatus;
            commandService.AddCommand(toolbarItem);

            // Toggle the toolbar item if the active configuration or the startup
            // project change (apparently, anything in VS is a selection).
            selectionEvents.OnChange += () => { command.OnBeforeQueryStatus(toolbarItem, null); };

            return command;
        }

        static (int, int) GetMenuAndToolbarCmdIds(ProfilerType profilerType)
        {
            switch (profilerType)
            {
                case ProfilerType.Orbit:
                    return (PkgCmdID.cmdidLaunchWithOrbitCommandMenu,
                        PkgCmdID.cmdidLaunchWithOrbitCommandToolbar);
                case ProfilerType.Dive:
                    return (PkgCmdID.cmdidLaunchWithDiveCommandMenu,
                        PkgCmdID.cmdidLaunchWithDiveCommandToolbar);
            }

            throw new NotImplementedException($"Unhandled ProfilerType {profilerType}");
        }

        public static DebugLaunchOptions GetLaunchOption(ProfilerType profilerType)
        {
            switch (profilerType)
            {
                // These DebugLaunchOptions determine which profiler to use. It's the only
                // information we can pass into GgpDebugQueryTarget.
                case ProfilerType.Orbit:
                    return DebugLaunchOptions.Profiling;
                case ProfilerType.Dive:
                    // Unfortunately, there's no good way to pass a flag to "launch the other
                    // profiler", so use DetachOnStop because that has no meaning in combination
                    // with NoDebug (see Execute() below).
                    return DebugLaunchOptions.DetachOnStop;
            }

            throw new NotImplementedException($"Unhandled ProfilerType {profilerType}");
        }

        /// <summary>
        /// Constructor public for testing. Use Register() in production code.
        /// </summary>
        public LaunchWithProfilerCommand(IVsSolutionBuildManager solutionBuildManager,
                                         DebugLaunchOptions profilerLaunchOption,
                                         string profilerName, IDialogUtil dialogUtil,
                                         EnvDTE.SelectionEvents selectionEvents)
        {
            _solutionBuildManager = solutionBuildManager;
            _profilerLaunchOption = profilerLaunchOption;
            _profilerName = profilerName;
            _dialogUtil = dialogUtil;
            _selectionEvents = selectionEvents;
        }

        /// <summary>
        /// Determines the visibility of the command. The command is visible if the active
        /// configuration of the startup project has GGP as platform.
        /// Public for testing.
        /// </summary>
        public void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var command = sender as OleMenuCommand;
            if (command == null)
            {
                Trace.WriteLine("Failed to determine visibility of LaunchWithProfilerCommand: " +
                                "Command is not an OleMenuCommand.");
                return;
            }

            bool ggpProject = IsGgpStartupProject();
            command.Visible = ggpProject; // Works for the menu item.
            command.Enabled = ggpProject; // Works for the toolbar item.
        }

        /// <summary>
        /// Builds and launches the startup project, starts the profiler and instructs it to attach
        /// to the game process.
        /// Public for testing.
        /// </summary>
        public void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Warn if game was built in a debug configuration.
            if (IsDebug() && !_dialogUtil.ShowYesNoWarning(
                YetiCommon.ErrorStrings.ProfilingInDebugMode,
                YetiCommon.ErrorStrings.ProfilingInDebugModeCaption(_profilerName)))
            {
                return;
            }

            var launchOptions = DebugLaunchOptions.NoDebug | _profilerLaunchOption;
            int res = _solutionBuildManager.DebugLaunch((uint) launchOptions);
            if (res != VSConstants.S_OK)
            {
                // GgpDebugQueryTarget shows a proper error message if this fails.
                Trace.WriteLine($"Launch with profiler command failed: DebugLaunch() return {res}");
                return;
            }
        }

        /// <summary>
        /// Returns the name of the active configuration of the startup project, e.g. "Debug|GGP".
        /// Logs and returns null on error.
        /// </summary>
        string GetActiveConfigurationName()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string baseErrorMessage =
                "Failed to determine whether active config in startup project is GGP: ";
            if (_solutionBuildManager.get_StartupProject(out IVsHierarchy hier) != VSConstants.S_OK)
            {
                Trace.WriteLine(baseErrorMessage + "Failed to get startup project");
                return null;
            }

            var activeCfg = new IVsProjectCfg[1];
            _solutionBuildManager.FindActiveProjectCfg(IntPtr.Zero, IntPtr.Zero, hier, activeCfg);
            if (activeCfg[0] == null)
            {
                Trace.WriteLine(baseErrorMessage + "Failed to get active project configuration");
                return null;
            }

            if (activeCfg[0].get_CanonicalName(out string cname) != VSConstants.S_OK)
            {
                Trace.WriteLine(baseErrorMessage + "Failed to get canonical name");
                return null;
            }

            return cname;
        }

        /// <summary>
        /// Returns true if the active configuration of the startup project has GGP as platform.
        /// In other words, if you hit F5, a game would run on a gamelet.
        /// </summary>
        bool IsGgpStartupProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // E.g. "Debug|GGP".
            string cname = GetActiveConfigurationName();
            return cname != null && cname.EndsWith("|GGP");
        }

        /// <summary>
        /// Returns true if the active configuration of the startup project is a debug
        /// configuration. This is just a guess based on the name.
        /// </summary>
        bool IsDebug()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // E.g. "Debug|GGP".
            string cname = GetActiveConfigurationName();
            return cname != null && cname.StartsWith("Debug");
        }
    }
}