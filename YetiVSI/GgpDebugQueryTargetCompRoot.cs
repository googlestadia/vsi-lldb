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
using System.IO.Abstractions;
using Metrics.Shared;
using YetiCommon;
using YetiCommon.Cloud;
using YetiCommon.SSH;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;
using YetiVSI.Profiling;
using YetiVSI.Util;

namespace YetiVSI
{
    public class GgpDebugQueryTargetCompRoot
    {
        protected readonly ServiceManager _serviceManager;
        protected readonly IDialogUtil _dialogUtil;

        CancelableTask.Factory _cancelableTaskFactory;
        InstanceSelectionWindow.Factory _gameletSelectionWindowFactory;
        ApplicationClient.Factory _applicationClientFactory;
        GameletClient.Factory _gameletClientFactory;
        RemoteCommand _remoteCommand;
        SshManager _sshManager;
        TestAccountClient.Factory _testAccountClientFactory;

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
            var applicationClientFactory = GetApplicationClientFactory();
            var gameletClientFactory = GetGameletClientFactory();
            var testAccountClientFactory = GetTestAccountClientFactory();
            var managedProcessFactory = new ManagedProcess.Factory();
            var remoteCommand = GetRemoteCommand(managedProcessFactory);
            var remoteFile = new RemoteFile(managedProcessFactory);
            var remoteDeploy = new RemoteDeploy(remoteCommand, remoteFile, managedProcessFactory,
                                                fileSystem);
            var metrics = _serviceManager.GetGlobalService(typeof(SMetrics)) as IVsiMetrics;
            var sdkVersion = GetSdkVersion();
            var sshManager = GetSshManager(managedProcessFactory, cloudRunner);
            var launchParamsConverter = new LaunchGameParamsConverter(new QueryParametersParser());
            var debugSessionMetrics = new DebugSessionMetrics(metrics);
            var actionRecorder = new ActionRecorder(debugSessionMetrics);
            var gameLaunchManager = new GameLaunchBeHelper(gameletClientFactory.Create(cloudRunner),
                                                           launchParamsConverter);
            var vsiLaunchFactory = new VsiGameLaunchFactory(
                gameletClientFactory.Create(cloudRunner), GetCancelableTaskFactory(),
                gameLaunchManager, actionRecorder, _dialogUtil);
            var gameLauncher = new GameLauncher(gameletClientFactory.Create(cloudRunner),
                                                yetiVsiService, launchParamsConverter,
                                                GetCancelableTaskFactory(), actionRecorder,
                                                _dialogUtil, vsiLaunchFactory);
            var gameletSelectorFactory = new GameletSelectorFactory(
                _dialogUtil, cloudRunner, GetGameletSelectorWindowFactory(),
                GetCancelableTaskFactory(), gameletClientFactory, sshManager, remoteCommand,
                gameLaunchManager, taskContext);
            var launchCommandFormatter = new ChromeClientLaunchCommandFormatter();
            var identityClient = new IdentityClient(cloudRunner);
            var backgroundProcessFactory = new BackgroundProcess.Factory();
            var orbitLauncher =
                ProfilerLauncher<OrbitArgs>.CreateForOrbit(backgroundProcessFactory, fileSystem);
            return new GgpDebugQueryTarget(fileSystem, sdkConfigFactory, gameletClientFactory,
                                           applicationClientFactory, GetCancelableTaskFactory(),
                                           _dialogUtil, remoteDeploy, debugSessionMetrics,
                                           credentialManager, testAccountClientFactory,
                                           gameletSelectorFactory, cloudRunner, sdkVersion,
                                           launchCommandFormatter, yetiVsiService, gameLauncher,
                                           taskContext, new ProjectPropertiesMetricsParser(),
                                           identityClient, orbitLauncher);
        }

        public virtual Versions.SdkVersion GetSdkVersion() => Versions.GetSdkVersion();

        public virtual ISshManager GetSshManager(ManagedProcess.Factory managedProcessFactory,
                                                 ICloudRunner cloudRunner)
        {
            if (_sshManager == null)
            {
                var sshKeyLoader = new SshKeyLoader(managedProcessFactory);
                var sshKnownHostsWriter = new SshKnownHostsWriter();
                _sshManager = new SshManager(GetGameletClientFactory(), cloudRunner, sshKeyLoader,
                                             sshKnownHostsWriter,
                                             GetRemoteCommand(managedProcessFactory));
            }

            return _sshManager;
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

        public virtual InstanceSelectionWindow.Factory GetGameletSelectorWindowFactory()
        {
            if (_gameletSelectionWindowFactory == null)
            {
                _gameletSelectionWindowFactory = new InstanceSelectionWindow.Factory();
            }

            return _gameletSelectionWindowFactory;
        }

        public virtual IApplicationClientFactory GetApplicationClientFactory()
        {
            if (_applicationClientFactory == null)
            {
                _applicationClientFactory = new ApplicationClient.Factory();
            }

            return _applicationClientFactory;
        }

        public virtual ITestAccountClientFactory GetTestAccountClientFactory()
        {
            if (_testAccountClientFactory == null)
            {
                _testAccountClientFactory = new TestAccountClient.Factory();
            }

            return _testAccountClientFactory;
        }

        public virtual IGameletClientFactory GetGameletClientFactory()
        {
            if (_gameletClientFactory == null)
            {
                _gameletClientFactory = new GameletClient.Factory();
            }

            return _gameletClientFactory;
        }

        public virtual IRemoteCommand GetRemoteCommand(ManagedProcess.Factory managedProcessFactory)
        {
            if (_remoteCommand == null)
            {
                _remoteCommand = new RemoteCommand(managedProcessFactory);
            }

            return _remoteCommand;
        }
    }
}