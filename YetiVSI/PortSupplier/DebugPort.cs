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
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.DebugEngine;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.PortSupplier
{
    // DebugPort represents a Gamelet as a Visual Studio IDebugPort.
    public class DebugPort : IDebugPort2
    {
        public class Factory
        {
            readonly DebugProcess.Factory debugProcessFactory;
            readonly ProcessListRequest.Factory processListRequestFactory;
            readonly CancelableTask.Factory cancelableTaskFactory;
            readonly IDialogUtil dialogUtil;
            readonly ISshManager sshManager;
            readonly IMetrics metrics;
            readonly string developerAccount;

            // For test substitution.
            public Factory() { }

            public Factory(DebugProcess.Factory debugProcessFactory,
                           ProcessListRequest.Factory processListRequestFactory,
                           CancelableTask.Factory cancelableTaskFactory, IDialogUtil dialogUtil,
                           ISshManager sshManager, IMetrics metrics, string developerAccount)
            {
                this.debugProcessFactory = debugProcessFactory;
                this.processListRequestFactory = processListRequestFactory;
                this.cancelableTaskFactory = cancelableTaskFactory;
                this.dialogUtil = dialogUtil;
                this.sshManager = sshManager;
                this.metrics = metrics;
                this.developerAccount = developerAccount;
            }

            public virtual IDebugPort2 Create(Gamelet gamelet, IDebugPortSupplier2 supplier,
                string debugSessionId)
            {
                return new DebugPort(debugProcessFactory, processListRequestFactory,
                                     cancelableTaskFactory, dialogUtil, sshManager, metrics,
                                     gamelet, supplier, debugSessionId, developerAccount);
            }
        }

        readonly CancelableTask.Factory cancelableTaskFactory;
        readonly ISshManager sshManager;
        readonly DebugProcess.Factory debugProcessFactory;
        readonly ProcessListRequest.Factory processListRequestFactory;
        readonly IDialogUtil dialogUtil;
        readonly ActionRecorder actionRecorder;
        readonly Guid guid;
        readonly IDebugPortSupplier2 supplier;
        readonly string developerAccount;
        readonly DebugSessionMetrics debugSessionMetrics;


        public Gamelet Gamelet { get; }

        public string DebugSessionId => debugSessionMetrics.DebugSessionId;

        DebugPort(DebugProcess.Factory debugProcessFactory,
                  ProcessListRequest.Factory processListRequestFactory,
                  CancelableTask.Factory cancelableTaskFactory, IDialogUtil dialogUtil,
                  ISshManager sshManager, IMetrics metrics, Gamelet gamelet,
                  IDebugPortSupplier2 supplier, string debugSessionId, string developerAccount)
        {
            this.debugProcessFactory = debugProcessFactory;
            this.processListRequestFactory = processListRequestFactory;
            this.dialogUtil = dialogUtil;
            guid = Guid.NewGuid();
            this.supplier = supplier;
            this.developerAccount = developerAccount;
            this.cancelableTaskFactory = cancelableTaskFactory;
            this.sshManager = sshManager;
            debugSessionMetrics = new DebugSessionMetrics(metrics);
            debugSessionMetrics.DebugSessionId = debugSessionId;
            actionRecorder = new ActionRecorder(debugSessionMetrics);
            Gamelet = gamelet;
        }

        private List<ProcessListEntry> GetProcessList(IProcessListRequest request)
        {
            // TODO: Use single cancelable task for both actions
            try
            {
                var enableSshAction = actionRecorder.CreateToolAction(ActionType.GameletEnableSsh);
                if (!cancelableTaskFactory.Create(TaskMessages.EnablingSSH,
                    async _ => await sshManager.EnableSshAsync(Gamelet, enableSshAction))
                        .RunAndRecord(enableSshAction))
                {
                    return new List<ProcessListEntry>();
                }
            }
            catch (Exception e) when (e is SshKeyException || e is CloudException)
            {
                Trace.WriteLine(e.ToString());
                dialogUtil.ShowError(ErrorStrings.FailedToEnableSsh(e.Message), e.ToString());
                return new List<ProcessListEntry>();
            }

            // TODO: Handle ProcessException
            var processListAction = actionRecorder.CreateToolAction(ActionType.ProcessList);
            var queryProcessesTask = cancelableTaskFactory.Create("Querying instance processes...",
                async () => await request.GetBySshAsync(new SshTarget(Gamelet)));
            queryProcessesTask.RunAndRecord(processListAction);
            return queryProcessesTask.Result;
        }

        public int GetPortName(out string name)
        {
            if (developerAccount != Gamelet.ReserverEmail)
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
            List<IDebugProcess2> processes = null;
            try
            {
                var results = GetProcessList(processListRequestFactory.Create());
                processes = results.Select(r =>
                {
                    return debugProcessFactory.Create(this, r.Pid, r.Title, r.Command);
                }).ToList();
            }
            catch (ProcessException e)
            {
                Trace.WriteLine("ProcessException:" + e.ToString());
                dialogUtil.ShowError(ErrorStrings.ErrorQueryingGameletProcesses(e.Message),
                    e.ToString());
                processes = new List<IDebugProcess2>();
            }
            processesEnum = new ProcessesEnum(processes.ToArray());
            return VSConstants.S_OK;
        }

        public int GetPortId(out Guid guid)
        {
            guid = this.guid;
            return VSConstants.S_OK;
        }

        public int GetPortRequest(out IDebugPortRequest2 request)
        {
            request = null;
            return AD7Constants.E_PORT_NO_REQUEST;
        }

        public int GetPortSupplier(out IDebugPortSupplier2 supplier)
        {
            supplier = this.supplier;
            return VSConstants.S_OK;
        }

        public int GetProcess(AD_PROCESS_ID processId, out IDebugProcess2 process)
        {
            process = null;
            return VSConstants.E_NOTIMPL;
        }

        public int LaunchSuspended(
            string exe, string ags, string dir, string env, uint stdInput, uint stdOutput,
            uint stdError, out IDebugProcess2 process)
        {
            process = null;
            return VSConstants.E_NOTIMPL;
        }

        public int ResumeProcess(IDebugProcess2 process)
        {
            return VSConstants.E_NOTIMPL;
        }
    }
}
