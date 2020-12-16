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

﻿using GgpGrpc.Cloud;
using System.IO.Abstractions;
using YetiCommon;
using YetiCommon.Cloud;
using YetiCommon.SSH;
using YetiVSI.Shared.Metrics;
using YetiVSI.Util;

namespace YetiVSI
{
    public class GgpDebugQueryTargetCompRoot
    {
        protected readonly ServiceManager _serviceManager;
        protected readonly IDialogUtil _dialogUtil;

        CancelableTask.Factory _cancelableTaskFactory;
        GameletSelectionWindow.Factory _gameletSelectionWindowFactory;

        public GgpDebugQueryTargetCompRoot(ServiceManager serviceManager, IDialogUtil dialogUtil)
        {
            _serviceManager = serviceManager;
            _dialogUtil = dialogUtil;
        }

        public virtual GgpDebugQueryTarget Create()
        {
            // Factory creation for the YetiGameletDebugger entry point.
            var taskContext = _serviceManager.GetJoinableTaskContext();
            taskContext.ThrowIfNotOnMainThread();
            var fileSystem = new FileSystem();
            var jsonUtil = new JsonUtil();
            var sdkConfigFactory = new SdkConfig.Factory(jsonUtil);
            var yetiVsiService =
                (YetiVSIService) _serviceManager.GetGlobalService(typeof(YetiVSIService));
            var options = yetiVsiService.Options;
            var accountOptionLoader = new VsiAccountOptionLoader(options);
            var credentialConfigFactory = new CredentialConfig.Factory(jsonUtil);
            var credentialManager =
                new CredentialManager(credentialConfigFactory, accountOptionLoader);
            var cloudConnection = new CloudConnection();
            // NOTE: this CloudRunner is re-used for all subsequent debug sessions.
            var cloudRunner = new CloudRunner(sdkConfigFactory, credentialManager, cloudConnection,
                                              new GgpSDKUtil());
            var applicationClientFactory = new ApplicationClient.Factory();
            var gameletClientFactory = new GameletClient.Factory();
            var testAccountClientFactory = new TestAccountClient.Factory();
            var managedProcessFactory = new ManagedProcess.Factory();
            var remoteCommand = new RemoteCommand(managedProcessFactory);
            var socketSender = new LocalSocketSender();
            var transportSessionFactory =
                new DebugEngine.LldbTransportSession.Factory(new MemoryMappedFileFactory());
            var remoteFile = new RemoteFile(managedProcessFactory, transportSessionFactory,
                                            socketSender, fileSystem);
            var remoteDeploy = new RemoteDeploy(remoteCommand, remoteFile, managedProcessFactory,
                                                fileSystem,
                                                new ElfFileUtil(taskContext.Factory,
                                                                managedProcessFactory));
            var metrics = _serviceManager.GetGlobalService(typeof(SMetrics)) as IMetrics;
            var sdkVersion = Versions.GetSdkVersion();
            var sshKeyLoader = new SshKeyLoader(managedProcessFactory);
            var sshKnownHostsWriter = new SshKnownHostsWriter();
            var sshManager = new SshManager(gameletClientFactory, cloudRunner, sshKeyLoader,
                                            sshKnownHostsWriter, remoteCommand);
            var gameletSelector = new GameletSelector(_dialogUtil, cloudRunner,
                                                      GetGameletSelectorWindowFactory(),
                                                      GetCancelableTaskFactory(),
                                                      gameletClientFactory, sshManager,
                                                      remoteCommand);
            var serializer = new JsonUtil();
            var launchCommandFormatter = new ChromeClientLaunchCommandFormatter(serializer);
            var paramsFactory = new DebugEngine.DebugEngine.Params.Factory(serializer);
            return new GgpDebugQueryTarget(fileSystem, sdkConfigFactory, gameletClientFactory,
                                           applicationClientFactory, options,
                                           GetCancelableTaskFactory(), _dialogUtil, remoteDeploy,
                                           metrics, _serviceManager, credentialManager,
                                           testAccountClientFactory, gameletSelector, cloudRunner,
                                           sdkVersion, launchCommandFormatter, paramsFactory);
        }

        public virtual CancelableTask.Factory GetCancelableTaskFactory()
        {
            if (_cancelableTaskFactory == null)
            {
                var taskContext = _serviceManager.GetJoinableTaskContext();
                var progressDialogFactory = new ProgressDialog.Factory();
                _cancelableTaskFactory =
                    new CancelableTask.Factory(taskContext, progressDialogFactory);
            }

            return _cancelableTaskFactory;
        }

        public virtual GameletSelectionWindow.Factory GetGameletSelectorWindowFactory()
        {
            if (_gameletSelectionWindowFactory == null)
            {
                _gameletSelectionWindowFactory = new GameletSelectionWindow.Factory();
            }

            return _gameletSelectionWindowFactory;
        }
    }
}