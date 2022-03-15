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

using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Metrics.Shared;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.DebugEngine;
using YetiVSI.Metrics;

namespace YetiVSI.PortSupplier
{
    // DebugPort represents a Gamelet as a Visual Studio IDebugPort.
    public class DebugPort : IDebugPort2
    {
        public class Factory
        {
            readonly DebugProcess.Factory _debugProcessFactory;
            readonly ProcessListRequest.Factory _processListRequestFactory;
            readonly CancelableTask.Factory _cancelableTaskFactory;
            readonly IDialogUtil _dialogUtil;
            readonly ISshManager _sshManager;
            readonly IMetrics _metrics;
            readonly string _developerAccount;

            // For test substitution.
            public Factory()
            {
            }

            public Factory(DebugProcess.Factory debugProcessFactory,
                           ProcessListRequest.Factory processListRequestFactory,
                           CancelableTask.Factory cancelableTaskFactory, IDialogUtil dialogUtil,
                           ISshManager sshManager, IMetrics metrics, string developerAccount)
            {
                _debugProcessFactory = debugProcessFactory;
                _processListRequestFactory = processListRequestFactory;
                _cancelableTaskFactory = cancelableTaskFactory;
                _dialogUtil = dialogUtil;
                _sshManager = sshManager;
                _metrics = metrics;
                _developerAccount = developerAccount;
            }

            public virtual IDebugPort2 Create(
                Gamelet gamelet, IDebugPortSupplier2 supplier, string debugSessionId) =>
                new DebugPort(_debugProcessFactory, _processListRequestFactory,
                              _cancelableTaskFactory, _dialogUtil, _sshManager, _metrics, gamelet,
                              supplier, debugSessionId, _developerAccount);
        }

        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly ISshManager _sshManager;
        readonly DebugProcess.Factory _debugProcessFactory;
        readonly ProcessListRequest.Factory _processListRequestFactory;
        readonly IDialogUtil _dialogUtil;
        readonly ActionRecorder _actionRecorder;
        readonly Guid _guid;
        readonly IDebugPortSupplier2 _supplier;
        readonly string _developerAccount;
        readonly DebugSessionMetrics _debugSessionMetrics;

        public Gamelet Gamelet { get; }

        public string DebugSessionId => _debugSessionMetrics.DebugSessionId;

        DebugPort(DebugProcess.Factory debugProcessFactory,
                  ProcessListRequest.Factory processListRequestFactory,
                  CancelableTask.Factory cancelableTaskFactory, IDialogUtil dialogUtil,
                  ISshManager sshManager, IMetrics metrics, Gamelet gamelet,
                  IDebugPortSupplier2 supplier, string debugSessionId, string developerAccount)
        {
            _debugProcessFactory = debugProcessFactory;
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

        List<ProcessListEntry> GetProcessList(IProcessListRequest request)
        {
            // TODO: Use single cancelable task for both actions
            try
            {
                var enableSshAction = _actionRecorder.CreateToolAction(ActionType.GameletEnableSsh);
                if (!_cancelableTaskFactory
                         .Create(TaskMessages.EnablingSSH,
                                 async _ =>
                                     await _sshManager.EnableSshAsync(Gamelet, enableSshAction))
                         .RunAndRecord(enableSshAction))
                {
                    return new List<ProcessListEntry>();
                }
            }
            catch (Exception e) when (e is SshKeyException || e is CloudException)
            {
                Trace.WriteLine($"GetProcessList failed: {e.Demystify()}");
                _dialogUtil.ShowError(ErrorStrings.FailedToEnableSsh(e.Message), e);
                return new List<ProcessListEntry>();
            }

            // TODO: Handle ProcessException
            var processListAction = _actionRecorder.CreateToolAction(ActionType.ProcessList);
            var queryProcessesTask = _cancelableTaskFactory.Create(
                "Querying instance processes...",
                async () => await request.GetBySshAsync(new SshTarget(Gamelet)));
            queryProcessesTask.RunAndRecord(processListAction);
            return queryProcessesTask.Result;
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
            List<IDebugProcess2> processes;
            try
            {
                var results = GetProcessList(_processListRequestFactory.Create());
                processes =
                    results
                        .Select(r => _debugProcessFactory.Create(this, r.Pid, r.Title, r.Command))
                        .ToList();
            }
            catch (ProcessException e)
            {
                Trace.WriteLine($"ProcessException: {e.Demystify()}");
                _dialogUtil.ShowError(ErrorStrings.ErrorQueryingGameletProcesses(e.Message), e);
                processes = new List<IDebugProcess2>();
            }

            processesEnum = new ProcessesEnum(processes.ToArray());
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

        public int LaunchSuspended(string exe, string ags, string dir, string env, uint stdInput,
                                   uint stdOutput, uint stdError, out IDebugProcess2 process)
        {
            process = null;
            return VSConstants.E_NOTIMPL;
        }

        public int ResumeProcess(IDebugProcess2 process) => VSConstants.E_NOTIMPL;
    }
}