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

﻿using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using YetiCommon;

namespace YetiVSI.DebugEngine.CoreDumpViewer
{
    /// <summary>
    /// Interaction logic for CoreDumpViewerWindow.xaml
    /// </summary>
    public partial class CoreDumpViewerWindow : UserControl
    {
        readonly ViewModel _context;
        readonly string _filename;
        public CoreDumpViewerWindow(string filename)
        {
            InitializeComponent();
            _filename = filename;

            // ViewModel contains all the fields shown in the window
            _context = new ViewModel(filename);
            DataContext = _context;
        }

        /// <summary>
        /// Copy all data from ViewModel to Clipboard (similar to a .dmp window
        /// functionality).
        /// </summary>
        void CopyAllToClipboardClicked(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_context.GetFullDescription());
        }

        /// <summary>
        /// Start GGP Debugger attached to the core file.
        /// </summary>
        void DebugWithGgpClicked(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var vsDebugger =
                (IVsDebugger4)ServiceProvider.GlobalProvider.GetService(typeof(IVsDebugger));
            Assumes.Present(vsDebugger);
            try
            {
                var debugTarget =
                    new VsDebugTargetInfo4
                    {
                        dlo = (uint)DEBUG_LAUNCH_OPERATION.DLO_CreateProcess,
                        bstrExe = _filename,
                        bstrCurDir = Path.GetTempPath(),
                        guidLaunchDebugEngine =
                                                 YetiConstants.DebugEngineGuid
                    };
                VsDebugTargetInfo4[] debugTargets = { debugTarget };
                uint debugTargetCount = 1;
                VsDebugTargetProcessInfo[] processInfo =
                    new VsDebugTargetProcessInfo[debugTargetCount];

                vsDebugger.LaunchDebugTargets4(debugTargetCount, debugTargets, processInfo);
            }
            catch (COMException exception)
            {
                string errorMessage = $"Failed to start debugger: {exception}";
                Trace.WriteLine(errorMessage);
                MessageBox.Show(ErrorStrings.FailedToStartDebugger, "Core dump debugger",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}