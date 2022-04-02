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
using GgpGrpc.Models;
using Metrics.Shared;
using YetiCommon;
using YetiCommon.Cloud;
using YetiCommon.SSH;
using YetiVSI.Metrics;
using Microsoft.VisualStudio.Shell;

namespace YetiVSI.PortSupplier
{
    // DebugPortSupplier provides a list of remote Gamelets (as 'ports') to Visual Studio for use
    // in the "Attach to Process" UI Dialog.
    [Guid("79a01686-eae4-4d57-9ffd-78ea68d131e9")]
    public class DebugPortSupplier : IDebugPortSupplier2, IDebugPortSupplierDescription2
    {
        readonly DebugPort.Factory _debugPortFactory;
        readonly GameletClient.Factory _gameletClientFactory;
        readonly IDialogUtil _dialogUtil;
        readonly IVsiMetrics _metrics;
        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly ICloudRunner _cloudRunner;
        readonly string _developerAccount;

        List<IDebugPort2> _ports = new List<IDebugPort2>();

        // Creates a DebugPortSupplier.  This will be invoked by Visual Studio based on this class
        // Guid being in the registry.
        public DebugPortSupplier()
        {
            var managedProcessFactory = new ManagedProcess.Factory();
            var processListRequestFactory = new ProcessListRequest.Factory(managedProcessFactory);
            var jsonUtil = new JsonUtil();
            var sdkConfigFactory = new SdkConfig.Factory(jsonUtil);
            var credentialConfigFactory = new CredentialConfig.Factory(jsonUtil);
            var vsiService = (YetiVSIService)Package.GetGlobalService(typeof(YetiVSIService));
            var accountOptionLoader = new VsiAccountOptionLoader(vsiService.Options);
            var credentialManager =
                new CredentialManager(credentialConfigFactory, accountOptionLoader);
            _developerAccount = credentialManager.LoadAccount();
            _dialogUtil = new DialogUtil();
            var progressDialogFactory = new ProgressDialog.Factory();
            _cancelableTaskFactory = new CancelableTask.Factory(
                ThreadHelper.JoinableTaskContext, progressDialogFactory);
            var cloudConnection = new CloudConnection();
            // NOTE: this CloudRunner is re-used for all subsequent Attach to Process windows.
            _cloudRunner = new CloudRunner(sdkConfigFactory, credentialManager, cloudConnection,
                                           new GgpSDKUtil());
            var sshKeyLoader = new SshKeyLoader(managedProcessFactory);
            var sshKnownHostsWriter = new SshKnownHostsWriter();
            _gameletClientFactory = new GameletClient.Factory();
            var sshManager =
                new SshManager(_gameletClientFactory, _cloudRunner, sshKeyLoader,
                               sshKnownHostsWriter, new RemoteCommand(managedProcessFactory));

            _metrics = (IVsiMetrics)Package.GetGlobalService(typeof(SMetrics));
            _debugPortFactory = new DebugPort.Factory(
                processListRequestFactory, _cancelableTaskFactory, _dialogUtil,
                sshManager, _metrics, _developerAccount);
        }

        // Creates a DebugPortSupplier with specific factories.  Used by tests.
        public DebugPortSupplier(DebugPort.Factory debugPortFactory,
                                 GameletClient.Factory gameletClientFactory,
                                 IExtensionOptions options, IDialogUtil dialogUtil,
                                 CancelableTask.Factory cancelableTaskFactory, IVsiMetrics metrics,
                                 ICloudRunner cloudRunner, string developerAccount)
        {
            _debugPortFactory = debugPortFactory;
            _gameletClientFactory = gameletClientFactory;
            _dialogUtil = dialogUtil;
            _cancelableTaskFactory = cancelableTaskFactory;
            _metrics = metrics;
            _cloudRunner = cloudRunner;
            _developerAccount = developerAccount;
        }

        public int AddPort(IDebugPortRequest2 request, out IDebugPort2 port)
        {
            var debugSessionMetrics = new DebugSessionMetrics(_metrics);
            debugSessionMetrics.UseNewDebugSessionId();
            var actionRecorder = new ActionRecorder(debugSessionMetrics);

            port = null;

            if (request.GetPortName(out string gameletIdOrName) != VSConstants.S_OK)
            {
                return VSConstants.E_FAIL;
            }

            var action = actionRecorder.CreateToolAction(ActionType.GameletGet);
            var gameletClient = _gameletClientFactory.Create(_cloudRunner.Intercept(action));
            var gameletTask = _cancelableTaskFactory.Create(
                "Querying instance...",
                async () => await gameletClient.LoadByNameOrIdAsync(gameletIdOrName));
            try
            {
                gameletTask.RunAndRecord(action);
            }
            catch (CloudException e)
            {
                Trace.WriteLine($"LoadByNameOrIdAsync failed: {e.Demystify()}");
                _dialogUtil.ShowError(e.Message);
                return VSConstants.S_OK;
            }

            var debugPort = _debugPortFactory.Create(gameletTask.Result, this,
                                                     debugSessionMetrics.DebugSessionId);
            _ports.Add(debugPort);
            port = debugPort;
            return VSConstants.S_OK;
        }

        public int CanAddPort() => VSConstants.S_OK;

        public int EnumPorts(out IEnumDebugPorts2 portsEnum)
        {
            var debugSessionMetrics = new DebugSessionMetrics(_metrics);
            debugSessionMetrics.UseNewDebugSessionId();
            var actionRecorder = new ActionRecorder(debugSessionMetrics);

            var action = actionRecorder.CreateToolAction(ActionType.GameletsList);
            var gameletClient = _gameletClientFactory.Create(_cloudRunner.Intercept(action));
            var gameletsTask = _cancelableTaskFactory.Create(
                "Querying instances...", () => gameletClient.ListGameletsAsync(onlyOwned: false));
            try
            {
                gameletsTask.RunAndRecord(action);
                List<Gamelet> gamelets = gameletsTask.Result;
                // show reserved instances first
                gamelets.Sort((g1, g2) =>
                {
                    if (g1.ReserverEmail != _developerAccount &&
                        g2.ReserverEmail != _developerAccount)
                    {
                        return string.CompareOrdinal(g2.ReserverEmail, g1.ReserverEmail);
                    }

                    if (g1.ReserverEmail == _developerAccount &&
                        g2.ReserverEmail == _developerAccount)
                    {
                        return string.CompareOrdinal(g2.DisplayName, g1.DisplayName);
                    }

                    return g1.ReserverEmail == _developerAccount ? 1 : -1;
                });
                _ports = gamelets
                             .Select(gamelet => _debugPortFactory.Create(
                                         gamelet, this, debugSessionMetrics.DebugSessionId))
                             .ToList();
            }
            catch (CloudException e)
            {
                Trace.WriteLine($"ListGameletsAsync failed: {e.Demystify()}");
                _dialogUtil.ShowError(e.Message);
                _ports.Clear();
            }

            portsEnum = new PortsEnum(_ports.ToArray());
            return VSConstants.S_OK;
        }

        public int GetPort(ref Guid portGuid, out IDebugPort2 resultPort)
        {
            foreach (IDebugPort2 port in _ports)
            {
                if (port.GetPortId(out Guid guid) == VSConstants.S_OK && guid == portGuid)
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

        public int RemovePort(IDebugPort2 port) => VSConstants.E_NOTIMPL;

        public int GetDescription(enum_PORT_SUPPLIER_DESCRIPTION_FLAGS[] flags, out string text)
        {
            text = "The Stadia transport lets you select an instance from the Qualifier " +
                   "drop-down menu and remotely attach to an existing process on that instance";
            return VSConstants.S_OK;
        }
    }
}