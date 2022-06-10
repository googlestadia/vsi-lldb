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
using System.Diagnostics;
using System.Reflection;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using YetiCommon;
using YetiCommon.ExceptionRecorder;
using YetiVSI.DebuggerOptions;
using YetiVSI.LoadSymbols;
using Process = EnvDTE.Process;
using StackFrame = EnvDTE.StackFrame;

namespace YetiVSI
{
    /// <summary>
    /// This class hides NoSourceWindow, which appears when users navigate to a stack frame with no
    /// symbols loaded.
    /// We hide it because NoSourceWindow is not customizable and assumes pdb symbols format. This
    /// causes confusion - e.g. file selections dialog allows users to only select pdb files.
    ///
    /// So far we are not aware of any official option to customize the window or switch it off via
    /// DebugEngine. The current approach is to enable "Debugging > General > Show disassembly if
    /// source is not available" option when we have active debug sessions.
    ///
    /// This class tracks the current sessions and adds a handler that enables disassebly window
    /// before each stack frame change. Once all the sessions are stopped, it removes the handler
    /// and restores initial values of the disassembly preference.
    /// </summary>
    class NoSourceWindowHider
    {
        const string _enableAddressLevelDebugging = "EnableAddressLevelDebugging";
        const string _showDisassembly = "ShowDisassemblyIfNoSource";

        readonly IServiceProvider _serviceProvider;
        readonly DebuggerEvents _debuggerEvents;

        bool _showDisassemblyValue;
        bool _addressLevelDebuggingValue;

        int _sessionsCount;

        readonly IExceptionRecorder _exceptionRecorder;
        readonly YetiVSIService _vsiService;

        public NoSourceWindowHider(IServiceProvider serviceProvider,
                                   IExceptionRecorder exceptionRecorder, YetiVSIService vsiService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _serviceProvider = serviceProvider;
            _exceptionRecorder = exceptionRecorder;
            _vsiService = vsiService;
            _debuggerEvents = GetDte().Events.DebuggerEvents;
        }

        void DisableNoSourceWindow(Process newProcess, Program newProgram, Thread newThread,
                                   StackFrame newStackFrame)
        {
            RecordErrors(() =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (newStackFrame != null)
                {
                    Properties debuggingProperties = GetDte().Properties["Debugging", "General"];

                    debuggingProperties.Item(_enableAddressLevelDebugging).Value = true;
                    debuggingProperties.Item(_showDisassembly).Value = true;
                }
            });
        }

        public void OnSessionLaunched(object sender, SessionLaunchedEventArgs args)
        {
            RecordErrors(() =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (_sessionsCount == 0)
                {
                    _debuggerEvents.OnContextChanged += DisableNoSourceWindow;
                    ReadSettings();
                }

                _sessionsCount++;
            });
        }

        public void OnSessionStopped(object sender, SessionStoppedEventArgs args)
        {
            RecordErrors(() =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                _sessionsCount--;

                if (_sessionsCount == 0)
                {
                    _debuggerEvents.OnContextChanged -= DisableNoSourceWindow;
                    RestoreSettings();
                }
            });
        }

        void ReadSettings()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Properties debuggingProperties = GetDte().Properties["Debugging", "General"];

            _addressLevelDebuggingValue =
                (bool) debuggingProperties.Item(_enableAddressLevelDebugging).Value;

            _showDisassemblyValue = (bool) debuggingProperties.Item(_showDisassembly).Value;
        }

        void RestoreSettings()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Properties debuggingProperties = GetDte().Properties["Debugging", "General"];
            debuggingProperties.Item(_enableAddressLevelDebugging).Value =
                _addressLevelDebuggingValue;
            debuggingProperties.Item(_showDisassembly).Value = _showDisassemblyValue;
        }

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

        void RecordErrors(Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Error processing NoSourceWindow: {e.Demystify()}");

                if (_vsiService.DebuggerOptions[DebuggerOption.EXCEPTION_METRICS] ==
                    DebuggerOptionState.ENABLED)
                {
                    SafeErrorUtil.SafelyLogError(
                        () => { _exceptionRecorder.Record(MethodBase.GetCurrentMethod(), e); },
                        "Failed to record exception");
                }
            }
        }
    }
}