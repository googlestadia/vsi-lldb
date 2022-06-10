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

﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using Microsoft.VisualStudio.Threading;
using YetiVSI.Util;
using static YetiVSI.DebugEngine.NatvisEngine.NatvisDiagnosticLogger;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Redirects NatvisLogEvents to the Natvis pane of the Output Window.
    ///
    /// IMPORTANT: Must be used on the UI thread.
    /// </summary>
    public class NatvisLoggerOutputWindowListener
    {
        private static readonly Guid NatvisPane_guid =
            new Guid("44DD190E-311B-4CE8-937F-ED77115C7B4B");

        /// <summary>
        /// Creates a NatvisLoggerOutputWindowListener and attaches it to the Natvis pane retrieved
        /// from the given output window.
        ///
        /// MUST be used on the UI thread.
        /// </summary>
        /// <param name="vsOutputWindow">The output window to redirect output to.</param>
        public static NatvisLoggerOutputWindowListener Create(JoinableTaskContext taskContext,
            IVsOutputWindow vsOutputWindow)
        {
            taskContext.ThrowIfNotOnMainThread();

            IVsOutputWindowPane natvisWindowPane = null;

            // TODO: Send output to the Debug pane similar to native behaviour.  There are
            // two reasons we don't do this currently:
            //  - VS2015 was clearing the Debug pane when starting a debug session and some natvis
            //    log messages were getting lost.
            //  - The Debug pane because also captures program output and it is very verbose.
            var hr = vsOutputWindow.CreatePane(NatvisPane_guid, "Natvis", 1, 0);
            ErrorHandler.ThrowOnFailure(hr);

            vsOutputWindow.GetPane(NatvisPane_guid, out natvisWindowPane);
            ErrorHandler.ThrowOnFailure(hr);

            return new NatvisLoggerOutputWindowListener(taskContext, natvisWindowPane);
        }

        private JoinableTaskContext taskContext;
        private IVsOutputWindowPane outputWindowPane;

        public NatvisLoggerOutputWindowListener(JoinableTaskContext taskContext,
            IVsOutputWindowPane outputWindowPane)
        {
            this.taskContext = taskContext;
            this.outputWindowPane = outputWindowPane;
        }

        /// <summary>
        /// Sends NatvisLogEvent messages to the Output Window.
        ///
        /// IMPORTANT: Must be used on the UI thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public void OnNatvisLogEvent(object sender, NatvisLogEventArgs args)
        {
            taskContext.ThrowIfNotOnMainThread();

            if (outputWindowPane == null)
            {
                return;
            }
            outputWindowPane.OutputStringThreadSafe("Natvis: " + args.Message + Environment.NewLine);
        }
    }
}
