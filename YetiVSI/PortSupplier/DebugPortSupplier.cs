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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using YetiCommon;
using YetiCommon.Cloud;
using YetiCommon.SSH;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.PortSupplier
{
    // DebugPortSupplier provides a list of remote Gamelets (as 'ports') to Visual Studio for use
    // in the "Attach to Process" UI Dialog.
    [Guid("79a01686-eae4-4d57-9ffd-78ea68d131e9")]
    public class DebugPortSupplier : IDebugPortSupplier2, IDebugPortSupplierDescription2
    {
        readonly DebugPort.Factory debugPortFactory;
        readonly GameletClient.Factory gameletClientFactory;
        readonly IExtensionOptions options;
        readonly IDialogUtil dialogUtil;
        readonly IMetrics metrics;
        readonly CancelableTask.Factory cancelableTaskFactory;
        readonly ICloudRunner cloudRunner;

        List<IDebugPort2> ports = new List<IDebugPort2>();

        // Creates a DebugPortSupplier.  This will be invoked by Visual Studio based on this class
        // Guid being in the registry.
        public DebugPortSupplier()
        {
            // Factory creation for the PortSupplier entry point.
            var serviceManager = new ServiceManager();
            options = ((YetiVSIService)serviceManager.RequireGlobalService(
                    typeof(YetiVSIService))).Options;
            var taskContext = serviceManager.GetJoinableTaskContext();
            var debugPropertyFactory = new DebugProperty.Factory();
            var debugProgramFactory = new DebugProgram.Factory(debugPropertyFactory, options);
            var debugProcessFactory = new DebugProcess.Factory(debugProgramFactory);
            var managedProcessFactory = new ManagedProcess.Factory();
            var processListRequestFactory = new ProcessListRequest.Factory(managedProcessFactory);
            var jsonUtil = new JsonUtil();
            var sdkConfigFactory = new SdkConfig.Factory(jsonUtil);
            var credentialConfigFactory = new CredentialConfig.Factory(jsonUtil);
            var accountOptionLoader = new VsiAccountOptionLoader(options);
            var credentialManager =
                new CredentialManager(credentialConfigFactory, accountOptionLoader);
            dialogUtil = new DialogUtil();
            var progressDialogFactory = new ProgressDialog.Factory();
            cancelableTaskFactory = new CancelableTask.Factory(taskContext, progressDialogFactory);
            var cloudConnection = new CloudConnection();
            // NOTE: this CloudRunner is re-used for all subsequent Attach to Process windows.
            cloudRunner = new CloudRunner(sdkConfigFactory, credentialManager,
                cloudConnection, new GgpSDKUtil());
            var sshKeyLoader = new SshKeyLoader(managedProcessFactory);
            var sshKnownHostsWriter = new SshKnownHostsWriter();
            gameletClientFactory = new GameletClient.Factory();
            var sshManager = new SshManager(gameletClientFactory, cloudRunner, sshKeyLoader,
                sshKnownHostsWriter, new RemoteCommand(managedProcessFactory));
            metrics = (IMetrics)serviceManager.RequireGlobalService(typeof(SMetrics));
            debugPortFactory = new DebugPort.Factory(
                debugProcessFactory, processListRequestFactory, cancelableTaskFactory,
                dialogUtil, sshManager, metrics);
        }

        // Creates a DebugPortSupplier with specific factories.  Used by tests.
        public DebugPortSupplier(
            DebugPort.Factory debugPortFactory,
            GameletClient.Factory gameletClientFactory, IExtensionOptions options,
            IDialogUtil dialogUtil, CancelableTask.Factory cancelableTaskFactory, IMetrics metrics,
            ICloudRunner cloudRunner)
        {
            this.debugPortFactory = debugPortFactory;
            this.gameletClientFactory = gameletClientFactory;
            this.options = options;
            this.dialogUtil = dialogUtil;
            this.cancelableTaskFactory = cancelableTaskFactory;
            this.metrics = metrics;
            this.cloudRunner = cloudRunner;
        }

        public int AddPort(IDebugPortRequest2 request, out IDebugPort2 port)
        {
            var debugSessionMetrics = new DebugSessionMetrics(metrics);
            debugSessionMetrics.UseNewDebugSessionId();
            var actionRecorder = new ActionRecorder(debugSessionMetrics);

            port = null;

            string gameletIdOrName;
            if (request.GetPortName(out gameletIdOrName) != VSConstants.S_OK)
            {
                return VSConstants.E_FAIL;
            }

            var action = actionRecorder.CreateToolAction(ActionType.GameletGet);
            var gameletClient = gameletClientFactory.Create(cloudRunner.Intercept(action));
            var gameletTask = cancelableTaskFactory.Create(
                "Querying instance...",
                async () => await gameletClient.LoadByNameOrIdAsync(gameletIdOrName));
            try
            {
                gameletTask.RunAndRecord(action);
            }
            catch (CloudException e)
            {
                Trace.WriteLine(e.ToString());
                dialogUtil.ShowError(e.Message);
                return VSConstants.S_OK;
            }
            var debugPort = debugPortFactory.Create(gameletTask.Result, this,
                debugSessionMetrics.DebugSessionId);
            ports.Add(debugPort);
            port = debugPort;
            return VSConstants.S_OK;
        }

        public int CanAddPort()
        {
            return VSConstants.S_OK;
        }

        public int EnumPorts(out IEnumDebugPorts2 portsEnum)
        {
            var debugSessionMetrics = new DebugSessionMetrics(metrics);
            debugSessionMetrics.UseNewDebugSessionId();
            var actionRecorder = new ActionRecorder(debugSessionMetrics);

            var action = actionRecorder.CreateToolAction(ActionType.GameletsList);
            var gameletClient = gameletClientFactory.Create(cloudRunner.Intercept(action));
            var gameletsTask = cancelableTaskFactory
                .Create("Querying instances...", gameletClient.ListGameletsAsync);
            try
            {
                gameletsTask.RunAndRecord(action);
                ports = gameletsTask.Result
                    .Select(gamelet => debugPortFactory.Create(gamelet, this,
                        debugSessionMetrics.DebugSessionId))
                    .ToList();
            }
            catch (CloudException e)
            {
                Trace.WriteLine(e.ToString());
                dialogUtil.ShowError(e.Message);
                ports.Clear();
            }
            portsEnum = new PortsEnum(ports.ToArray());
            return VSConstants.S_OK;
        }

        public int GetPort(ref Guid portGuid, out IDebugPort2 resultPort)
        {
            foreach (var port in ports)
            {
                Guid guid;
                if (port.GetPortId(out guid) == VSConstants.S_OK && guid == portGuid)
                {
                    resultPort = port;
                    return VSConstants.S_OK;
                }
            }
            resultPort = null;
            return DebugEngine.AD7Constants.E_PORTSUPPLIER_NO_PORT;
        }

        public int GetPortSupplierId(out Guid portSupplierGuid)
        {
            portSupplierGuid = YetiConstants.PortSupplierGuid;
            return VSConstants.S_OK;
        }

        public int GetPortSupplierName(out string name)
        {
            name = YetiConstants.YetiTitle;
            return VSConstants.S_OK;
        }

        public int RemovePort(IDebugPort2 port)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int GetDescription(
            enum_PORT_SUPPLIER_DESCRIPTION_FLAGS[] flags, out string text)
        {
            text = "The Stadia transport lets you select an instance from the Qualifier " + 
                "drop-down menu and remotely attach to an existing process on that instance";
            return VSConstants.S_OK;
        }
    }
}
