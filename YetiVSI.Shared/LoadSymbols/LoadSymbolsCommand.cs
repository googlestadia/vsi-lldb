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

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Automation;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using YetiCommon;
using YetiCommon.ExceptionRecorder;
using YetiVSI.DebugEngine;
using YetiVSI.DebuggerOptions;

namespace YetiVSI.LoadSymbols
{
    // TODO: cover this functionality with E2E.
    public class LoadSymbolsCommand
    {
        const string _listView = "SysListView32";
        const string _treeGrid = "TreeGrid";
        const string _treeGridItem = "TreeGridItem";
        const string _loadSymbolsGuid = "{C9DD4A59-47FB-11D2-83E7-00C04F9902C1}";
        const int _loadSymbolsFromModuleId = 295;

        readonly Guid _moduleWindowGuid = new Guid("37ABA9BE-445A-11D3-9949-00C04F68FD0A");
        readonly IServiceProvider _serviceProvider;

        readonly IExceptionRecorder _exceptionRecorder;
        readonly YetiVSIService _vsiService;

        // This should be stored as a class variable, otherwise VS recreates CommandEvents:
        // https://stackoverflow.com/questions/3874015/subscription-to-dte-events-doesnt-seem-to-work-events-dont-get-called
        CommandEvents _commandEvents;

        readonly List<ILldbAttachedProgram> _programs;

        public LoadSymbolsCommand(IServiceProvider serviceProvider,
                                  IExceptionRecorder exceptionRecorder, YetiVSIService vsiService)
        {
            _serviceProvider = serviceProvider;
            _exceptionRecorder = exceptionRecorder;
            _vsiService = vsiService;

            _programs = new List<ILldbAttachedProgram>();
        }

        public void OnSessionLaunched(object sender, SessionLaunchedEventArgs args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                SubstituteDefaultCommand(args);
            }
            catch (Exception e)
            {
                Trace.WriteLine(
                    $"Error while attempting to substitute LoadSymbols command: {e.Demystify()}");
            }
        }

        public void OnSessionStopped(object sender, SessionStoppedEventArgs args)
        {
#pragma warning disable VSTHRD010
            _programs.Remove(args.Program);
            if (_programs.Count == 0 && _commandEvents != null)
            {
                _commandEvents.BeforeExecute -= LoadSymbolsCustomCommand;
                _commandEvents = null;
            }
#pragma warning restore VSTHRD010
        }

        // We substitute the default behavior for Module > Load Symbols because for
        // some scenarios it requires pdb symbol file through file selection dialog and
        // if none provided, it doesn't attempt to load the symbols from the symbols
        // store. See (internal) for more details.
        void SubstituteDefaultCommand(SessionLaunchedEventArgs sessionLaunchedArgs)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _programs.Add(sessionLaunchedArgs.Program);

            if (_commandEvents == null && sessionLaunchedArgs.Mode ==
                DebugEngine.DebugEngine.LaunchOption.LaunchGame)
            {
                _commandEvents = GetDte().Events.CommandEvents;
                _commandEvents.BeforeExecute += LoadSymbolsCustomCommand;
            }
        }

        void LoadSymbolsCustomCommand(string guid, int id, object customIn, object customOut,
                                      ref bool cancelDefault)
        {
            if (guid != _loadSymbolsGuid || id != _loadSymbolsFromModuleId)
            {
                return;
            }

            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                _programs.ForEach(LoadSymbols);
                RefreshSymbolsLoadingStatus();
            }
            catch (Exception e)
            {
                // Users will see that module is not loaded via Modules window,
                // thus not showing any extra messages.
                Trace.WriteLine($"Error while loading symbols: {e.Demystify()}");

                if (_vsiService.DebuggerOptions[DebuggerOption.EXCEPTION_METRICS] ==
                    DebuggerOptionState.ENABLED)
                {
                    SafeErrorUtil.SafelyLogError(() => {
                        _exceptionRecorder.Record(MethodBase.GetCurrentMethod(), e);
                    }, "Failed to record exception");
                }
            }

            // Do not invoke the handler for the command.
            cancelDefault = true;
        }

        void RefreshSymbolsLoadingStatus()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            RefreshCallStackWindow();
            RefreshModulesWindow();
        }

        // We attempt to switch current thread to another one and back again to force VS to reload
        // call stack window.
        // See (internal) for more details.
        void RefreshCallStackWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Thread currentThread = GetDte().Debugger?.CurrentThread;
            Thread newThread = null;
            Threads threads = GetDte().Debugger?.CurrentProgram?.Threads;

            if (threads == null || currentThread == null)
            {
                return;
            }

            foreach (Thread thread in threads)
            {
                if (currentThread != thread)
                {
                    newThread = thread;
                    GetDte().Debugger.CurrentThread = newThread;
                    break;
                }
            }

            if (newThread != null)
            {
                GetDte().Debugger.CurrentThread = currentThread;
            }
        }

        // We attempt to close and reopen the module window to refresh it.
        void RefreshModulesWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var vsUiShell = (IVsUIShell)_serviceProvider.GetService(typeof(SVsUIShell));

            if (vsUiShell == null)
            {
                return;
            }

            int result = vsUiShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFindFirst,
                                                  _moduleWindowGuid, out IVsWindowFrame winFrame);
            if (result == VSConstants.S_OK && winFrame.IsVisible() == VSConstants.S_OK)
            {
                winFrame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
                winFrame.Show();
            }
        }

        void LoadSymbols(ILldbAttachedProgram program)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (string moduleName in ListSelectedModules())
            {
                foreach (var module in program.GetModulesByName(moduleName))
                {
                    module.LoadSymbols();
                }
            }
        }

        IEnumerable<string> ListSelectedModules()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            AutomationElement activeWindow = GetActiveWindow();

            AutomationElement moduleWindowView = GetDescendant(activeWindow, _listView);
            if (moduleWindowView != null)
            {
                return FindSelectedModulesInModulesWindow(moduleWindowView);
            }

            AutomationElement callStackWindowView = GetDescendant(activeWindow, _treeGrid);
            if (callStackWindowView != null)
            {
                return FindSelectedModulesInCallStackWindow(callStackWindowView);
            }

            throw new InvalidOperationException(
                $"Can not list selected modules in {activeWindow.Current.Name} window.");
        }

        IEnumerable<string> FindSelectedModulesInModulesWindow(AutomationElement moduleWindowView)
        {
            var selectionPattern =
                (SelectionPattern)moduleWindowView.GetCurrentPattern(SelectionPattern.Pattern);
            AutomationElement[] selection = selectionPattern.Current.GetSelection();

            if (selection.Length == 0)
            {
                throw new InvalidOperationException(
                    "No selected rows found to proceed with load symbols command.");
            }

            return selection.Select(element => element.Current.Name);
        }

        IEnumerable<string> FindSelectedModulesInCallStackWindow(
            AutomationElement callStackWindowView)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var selectionPattern =
                (SelectionPattern)callStackWindowView.GetCurrentPattern(SelectionPattern.Pattern);
            AutomationElement[] selection = selectionPattern.Current.GetSelection();

            if (selection.Length == 0)
            {
                throw new InvalidOperationException(
                    "No selected rows found to proceed with load symbols command.");
            }

            return selection.Select(
                element => ConvertCallStackEntryToModule(element, callStackWindowView));
        }

        string ConvertCallStackEntryToModule(AutomationElement selectedRow,
                                             AutomationElement callStackWindowView)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string rowText = selectedRow.Current.Name;

            // Call stack window may display the module name in a format module_name!fun_info.
            if (rowText.Contains("!"))
            {
                string moduleName = rowText.Substring(0, rowText.IndexOf('!'));
                if (ModuleExists(moduleName))
                {
                    return moduleName;
                }
            }

            // When the call stack window doesn't show the module name, we will try to match
            // the selected row index with the i-th frame returned by DTE service.
            AutomationElementCollection framesRows =
                GetAllDescendants(callStackWindowView, _treeGridItem);

            int selectedFrameIndex = 0;
            foreach (AutomationElement frameRow in framesRows)
            {
                selectedFrameIndex++;
                if (frameRow == selectedRow)
                {
                    break;
                }
            }

            // First index is 1 for frames.
            StackFrames frames = GetDte().Debugger.CurrentThread.StackFrames;
            if (selectedFrameIndex <= 0 || selectedFrameIndex > frames.Count)
            {
                throw new InvalidOperationException(
                    "Cannot match selected row with a stack frame from DTE.");
            }

            EnvDTE.StackFrame frame = frames.Item(selectedFrameIndex);
            if (!rowText.Contains(frame.FunctionName))
            {
                throw new InvalidOperationException(
                    "The function name of the selected call stack frame doesn't match the " +
                    "entry in DTE.");
            }

            return frame.Module;
        }

        AutomationElement GetActiveWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
#if VS2019
            int windowHandle = GetDte().ActiveWindow.HWnd;
            var activeWindow = AutomationElement.FromHandle(new IntPtr(windowHandle));
#elif VS2022
            IntPtr windowHandle = GetDte().ActiveWindow.HWnd;
            var activeWindow = AutomationElement.FromHandle(windowHandle);
#endif
            if (activeWindow == null)
            {
                throw new InvalidOperationException(
                    "Cannot retrieve active window to process load symbols command.");
            }

            return activeWindow;
        }

        AutomationElement GetDescendant(AutomationElement parent, string className) =>
            parent.FindFirst(TreeScope.Descendants,
                             new PropertyCondition(AutomationElement.ClassNameProperty, className));

        AutomationElementCollection GetAllDescendants(AutomationElement parent, string className) =>
            parent.FindAll(TreeScope.Descendants,
                           new PropertyCondition(AutomationElement.ClassNameProperty, className));

        DTE GetDte()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = (DTE) _serviceProvider.GetService(typeof(DTE));
            if (dte == null)
            {
                throw new InvalidOperationException(
                    "Failed to retrieve DTE object to process load symbols command.");
            }

            return dte;
        }

        bool ModuleExists(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return false;
            }

            return _programs.Any(program => program.GetModulesByName(moduleName).Count > 0);
        }
    }
}