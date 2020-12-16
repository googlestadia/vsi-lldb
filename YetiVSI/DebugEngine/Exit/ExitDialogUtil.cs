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

﻿using DebuggerGrpcClient;
using Grpc.Core;
using System;
using System.Diagnostics;
using YetiCommon;

namespace YetiVSI.DebugEngine.Exit
{
    /// <summary>
    /// A callback function that executes the given action. This exists for testing and is
    /// hardcoded to System.Windows.Application.Current.Dispatcher.Invoke in production.
    /// </summary>
    public delegate void DialogExecutionContext(Action action);

    /// <summary>
    /// Show an exit dialog when the program exits.
    /// </summary>
    public class ExitDialogUtil
    {
        readonly Action<string, string> showErrorDialogWithDetails;
        readonly Action<string> showErrorDialog;
        readonly object thisLock = new object();

        public ExitDialogUtil(IDialogUtil dialogUtil, DialogExecutionContext executionContext)
        {
            showErrorDialogWithDetails = (message, details) =>
                executionContext(() => dialogUtil.ShowError(message, details));
            showErrorDialog = message => executionContext(() => dialogUtil.ShowError(message));
        }

        /// <summary>
        /// Show a dialog if exiting on error.
        /// </summary>
        public void ShowExitDialog(Exception ex)
        {
            Trace.WriteLine("Aborting debug session: " + ex);

            if (ex is ProcessExecutionException)
            {
                showErrorDialogWithDetails(ErrorStrings.ProcessExitedUnexpectedly, ex.ToString());
            }
            else if (ex is RpcException)
            {
                showErrorDialogWithDetails(ErrorStrings.RpcFailure, ex.ToString());
            }
            else if (ex is IUserVisibleError)
            {
                showErrorDialogWithDetails(ex.Message, ((IUserVisibleError)ex).UserDetails);
            }
        }
    }
}
