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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Metrics.Shared;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.DebugEngine;
using YetiVSI.Metrics;

namespace YetiVSI.PortSupplier
{
    // Defined in "Visual Studio 2022\VSSDK\VisualStudioIntegration\Common\IDL\msdbg170.idl"
    [ComImport]
    [Guid("761BC6BB-D7C0-45A5-8033-3106019426A6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAsyncDebugEnumProcessesCompletionHandler
    {
        [PreserveSig]
        int OnComplete(int hr, IEnumDebugProcesses2 pEnum);
    };

    // Defined in "Visual Studio 2022\VSSDK\VisualStudioIntegration\Common\IDL\msdbg170.idl"
    [ComImport]
    [Guid("59B9DCD4-CB85-47C6-B0F1-12E43E3EBF2E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDebugPort170
    {
        [PreserveSig]
#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods
        int EnumProcessesAsync(
#pragma warning restore VSTHRD200 // Use "Async" suffix for async methods
            bool fIncludeFromAllUsers,
            IAsyncDebugEnumProcessesCompletionHandler pCompletionHandler,
            out IAsyncDebugEngineOperation ppDebugOperation
        );

        [PreserveSig]
        int SupportsAutoRefresh(out bool pSupportsAutoRefresh);

        [PreserveSig]
        int SupportsFetchingParentProcessIds(out bool pSupportsParentProcessIds);
    }

    public class EnumProcessesAsyncOperation : IAsyncDebugEngineOperation
    {
        readonly DebugPort _debugPort;
        readonly bool _includeFromAllUsers;
        readonly IAsyncDebugEnumProcessesCompletionHandler _completionHandler;
        ICancelableTask _processListOperation;
        JoinableTask _taskFactoryOperation;

        public EnumProcessesAsyncOperation(
            DebugPort debugPort,
            bool includeFromAllUsers,
            IAsyncDebugEnumProcessesCompletionHandler pCompletionHandler)
        {
            _debugPort = debugPort;
            _includeFromAllUsers = includeFromAllUsers;
            _completionHandler = pCompletionHandler;
            _processListOperation = null;
            _taskFactoryOperation = null;
        }

        public int BeginExecute()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var (task, action) = _debugPort.GetProcessListTask(_includeFromAllUsers);

            // Save the task, so that we can cancel it from Cancel().
            _processListOperation = task;

            // Save the task factory operation, so that we can properly join it in Cancel().
            _taskFactoryOperation = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                IDebugProcess2[] processes = _debugPort.RunProcessListTask(task, action);
                _completionHandler.OnComplete(VSConstants.S_OK, new ProcessesEnum(processes));
            });

            return VSConstants.S_OK;
        }

        public int Cancel()
        {
            _processListOperation?.Cancel();
            _taskFactoryOperation?.Join();

            return VSConstants.S_OK;
        }
    }

    /// <summary>
    /// DebugPort represents a Gamelet as a Visual Studio IDebugPort.
    /// </summary>
    public class DebugPort : IDebugPort2, IDebugPort170
    {
        public class Factory
        {
            readonly ProcessListRequest.Factory _processListRequestFactory;
            readonly CancelableTask.Factory _cancelableTaskFactory;
            readonly IDialogUtil _dialogUtil;
            readonly ISshManager _sshManager;
            readonly IVsiMetrics _metrics;
            readonly string _developerAccount;

            // For test substitution.
            public Factory()
            {
            }

            public Factory(ProcessListRequest.Factory processListRequestFactory,
                           CancelableTask.Factory cancelableTaskFactory, IDialogUtil dialogUtil,
                           ISshManager sshManager, IVsiMetrics metrics, string developerAccount)
            {
                _processListRequestFactory = processListRequestFactory;
                _cancelableTaskFactory = cancelableTaskFactory;
                _dialogUtil = dialogUtil;
                _sshManager = sshManager;
                _metrics = metrics;
                _developerAccount = developerAccount;
            }

            public virtual IDebugPort2 Create(
                Gamelet gamelet, IDebugPortSupplier2 supplier, string debugSessionId) =>
                new DebugPort(_processListRequestFactory, _cancelableTaskFactory, _dialogUtil,
                              _sshManager, _metrics, gamelet, supplier, debugSessionId,
                              _developerAccount);
        }

        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly ISshManager _sshManager;
        readonly ProcessListRequest.Factory _processListRequestFactory;
        readonly IDialogUtil _dialogUtil;
        readonly ActionRecorder _actionRecorder;
        readonly Guid _guid;
        readonly IDebugPortSupplier2 _supplier;
        readonly string _developerAccount;
        readonly DebugSessionMetrics _debugSessionMetrics;

        public Gamelet Gamelet { get; }

        public string DebugSessionId => _debugSessionMetrics.DebugSessionId;

        DebugPort(ProcessListRequest.Factory processListRequestFactory,
                  CancelableTask.Factory cancelableTaskFactory, IDialogUtil dialogUtil,
                  ISshManager sshManager, IVsiMetrics metrics, Gamelet gamelet,
                  IDebugPortSupplier2 supplier, string debugSessionId, string developerAccount)
        {
            _processListRequestFactory = processListRequestFactory;
            _dialogUtil = dialogUtil;
            _guid = Guid.NewGuid();
            _supplier = supplier;
            _developerAccount = developerAccount;
            _cancelableTaskFactory = cancelableTaskFactory;
            _sshManager = sshManager;
            _debugSessionMetrics = new DebugSessionMetrics(metrics);
            _debugSessionMetrics.DebugSessionId = debugSessionId;
            _actionRecorder = new ActionRecorder(_debugSessionMetrics);
            Gamelet = gamelet;
        }

        public (ICancelableTask<List<ProcessListEntry>>, IAction) GetProcessListTask(
            bool includeFromAllUsers)
        {
            // TODO: Don't explicitely enable SSH. Instead execute the command
            // optimistically and properly handle the "ssh is not enabled" situation when the
            // command fails. This will happen only once per gamelet "lifetime", so it will save
            // us calling /bin/true every time.

            var action = _actionRecorder.CreateToolAction(ActionType.ProcessList);
            var getProcessListTask = _cancelableTaskFactory.Create(
                "Fetching the process list from the gamelet...",
                async task =>
                {
                    // First enable SSH.
                    task.Progress.Report("Checking SSH access");
                    await _sshManager.EnableSshAsync(Gamelet, action);

                    // Get the process list.
                    task.Progress.Report("Listing processes");
                    return await _processListRequestFactory.Create().GetBySshAsync(
                        new SshTarget(Gamelet), includeFromAllUsers);
                });

            return (getProcessListTask, action);
        }

        public DebugProcess[] RunProcessListTask(
            ICancelableTask<List<ProcessListEntry>> task, IAction action)
        {
            try
            {
                if (!task.RunAndRecord(action))
                {
                    // The operation was cancelled.
                    return Array.Empty<DebugProcess>();
                }
                // Convert the results to DebugProcess.
                // TODO: Add process user name here.
                return task.Result
                    .Select(r => new DebugProcess(this, r.Pid, r.Ppid, r.Title, r.Command))
                    .ToArray();
            }
            catch (Exception e) when (e is SshKeyException || e is CloudException)
            {
                Trace.WriteLine($"EnableSsh failed: {e.Demystify()}");
                _dialogUtil.ShowError(ErrorStrings.FailedToEnableSsh(e.Message), e);
                return Array.Empty<DebugProcess>();
            }
            catch (ProcessException e)
            {
                Trace.WriteLine($"ProcessException: {e.Demystify()}");
                _dialogUtil.ShowError(ErrorStrings.ErrorQueryingGameletProcesses(e.Message), e);
                return Array.Empty<DebugProcess>();
            }
        }

        public int GetPortName(out string name)
        {
            if (_developerAccount != Gamelet.ReserverEmail)
            {
                string reserver = string.IsNullOrEmpty(Gamelet.ReserverName)
                    ? Gamelet.ReserverEmail
                    : Gamelet.ReserverName;
                string instance = string.IsNullOrEmpty(Gamelet.DisplayName)
                    ? Gamelet.Id
                    : Gamelet.DisplayName;
                name = $"Reserver: {reserver}; Instance: {instance}";
            }
            else
            {
                name = string.IsNullOrEmpty(Gamelet.DisplayName)
                    ? Gamelet.Id
                    : Gamelet.DisplayName + " [" + Gamelet.Id + "]";
            }

            return VSConstants.S_OK;
        }

        public int EnumProcesses(out IEnumDebugProcesses2 processesEnum)
        {
            var (task, action) = GetProcessListTask(includeFromAllUsers: false);
            IDebugProcess2[] processes = RunProcessListTask(task, action);
            processesEnum = new ProcessesEnum(processes);
            return VSConstants.S_OK;
        }

        public int GetPortId(out Guid guid)
        {
            guid = _guid;
            return VSConstants.S_OK;
        }

        public int GetPortRequest(out IDebugPortRequest2 request)
        {
            request = null;
            return AD7Constants.E_PORT_NO_REQUEST;
        }

        public int GetPortSupplier(out IDebugPortSupplier2 supplier)
        {
            supplier = _supplier;
            return VSConstants.S_OK;
        }

        public int GetProcess(AD_PROCESS_ID processId, out IDebugProcess2 process)
        {
            process = null;
            return VSConstants.E_NOTIMPL;
        }

        public int SupportsFetchingParentProcessIds(out bool pSupportsParentProcessIds)
        {
            pSupportsParentProcessIds = true;
            return VSConstants.S_OK;
        }

        public int EnumProcessesAsync(
            bool fIncludeFromAllUsers,
            IAsyncDebugEnumProcessesCompletionHandler pCompletionHandler,
            out IAsyncDebugEngineOperation ppDebugOperation)
        {
            ppDebugOperation = new EnumProcessesAsyncOperation(
                this, fIncludeFromAllUsers, pCompletionHandler);
            return VSConstants.S_OK;
        }

        public int SupportsAutoRefresh(out bool pSupportsAutoRefresh)
        {
            pSupportsAutoRefresh = true;
            return VSConstants.S_OK;
        }
    }
}
