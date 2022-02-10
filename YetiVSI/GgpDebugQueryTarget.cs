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
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.DebuggerOptions;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;
using YetiVSI.Profiling;
using YetiVSI.ProjectSystem.Abstractions;
using YetiVSI.Shared.Metrics;

namespace YetiVSI
{
    public class GgpDebugQueryTarget
    {
        readonly IFileSystem _fileSystem;
        readonly SdkConfig.Factory _sdkConfigFactory;
        readonly IGameletClientFactory _gameletClientFactory;
        readonly IApplicationClientFactory _applicationClientFactory;
        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly IDialogUtil _dialogUtil;
        readonly IRemoteDeploy _remoteDeploy;
        readonly DebugSessionMetrics _metrics;
        readonly ICredentialManager _credentialManager;
        readonly ITestAccountClientFactory _testAccountClientFactory;
        readonly ICloudRunner _cloudRunner;
        readonly IYetiVSIService _yetiVsiService;
        readonly IGameletSelectorFactory _gameletSelectorFactory;
        readonly Versions.SdkVersion _sdkVersion;
        readonly ChromeClientLaunchCommandFormatter _launchCommandFormatter;
        readonly IGameLauncher _gameLauncher;
        readonly JoinableTaskContext _taskContext;
        readonly IProjectPropertiesMetricsParser _projectPropertiesParser;
        readonly IIdentityClient _identityClient;
        readonly IOrbitLauncher _orbitLauncher;

        // Constructor for tests.
        public GgpDebugQueryTarget(IFileSystem fileSystem, SdkConfig.Factory sdkConfigFactory,
                                   IGameletClientFactory gameletClientFactory,
                                   IApplicationClientFactory applicationClientFactory,
                                   CancelableTask.Factory cancelableTaskFactory,
                                   IDialogUtil dialogUtil, IRemoteDeploy remoteDeploy,
                                   DebugSessionMetrics metrics,
                                   ICredentialManager credentialManager,
                                   ITestAccountClientFactory testAccountClientFactory,
                                   IGameletSelectorFactory gameletSelectorFactory,
                                   ICloudRunner cloudRunner, Versions.SdkVersion sdkVersion,
                                   ChromeClientLaunchCommandFormatter launchCommandFormatter,
                                   IYetiVSIService yetiVsiService, IGameLauncher gameLauncher,
                                   JoinableTaskContext taskContext,
                                   IProjectPropertiesMetricsParser projectPropertiesParser,
                                   IIdentityClient identityClient, IOrbitLauncher orbitLauncher)
        {
            _fileSystem = fileSystem;
            _sdkConfigFactory = sdkConfigFactory;
            _gameletClientFactory = gameletClientFactory;
            _applicationClientFactory = applicationClientFactory;
            _cancelableTaskFactory = cancelableTaskFactory;
            _dialogUtil = dialogUtil;
            _remoteDeploy = remoteDeploy;
            _metrics = metrics;
            _credentialManager = credentialManager;
            _testAccountClientFactory = testAccountClientFactory;
            _cloudRunner = cloudRunner;
            _yetiVsiService = yetiVsiService;
            _gameletSelectorFactory = gameletSelectorFactory;
            _sdkVersion = sdkVersion;
            _launchCommandFormatter = launchCommandFormatter;
            _gameLauncher = gameLauncher;
            _taskContext = taskContext;
            _projectPropertiesParser = projectPropertiesParser;
            _identityClient = identityClient;
            _orbitLauncher = orbitLauncher;
        }

        public async Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(
            IAsyncProject project, DebugLaunchOptions launchOptions)
        {
            try
            {
                // Make sure we can find the target executable.
                var targetPath = await project.GetTargetPathAsync();
                if (!_fileSystem.File.Exists(targetPath))
                {
                    Trace.WriteLine($"Unable to find target executable: {targetPath}");
                    _dialogUtil.ShowError(ErrorStrings.UnableToFindTargetExecutable(targetPath));
                    return new IDebugLaunchSettings[] { };
                }

                // Check if Orbit is installed.
                bool launchWithOrbit = launchOptions.HasFlag(
                    LaunchWithProfilerCommand.GetLaunchOption(ProfilerType.Orbit));
                if (launchWithOrbit && !_orbitLauncher.IsOrbitInstalled())
                {
                    _dialogUtil.ShowError(
                        YetiCommon.ErrorStrings.OrbitNotInstalled(_orbitLauncher.OrbitBinaryPath));
                    return new IDebugLaunchSettings[] { };
                }

                _metrics.UseNewDebugSessionId();
                var actionRecorder = new ActionRecorder(_metrics);

                // Path relative to /srv/game/assets.
                var gameletExecutableRelPath = await project.GetGameletLaunchExecutableAsync();
                var gameletCommand = (gameletExecutableRelPath + " " +
                    await project.GetGameletLaunchArgumentsAsync()).Trim();

                var launchParams = new LaunchParams()
                {
                    Cmd = gameletCommand,
                    RenderDoc = await project.GetLaunchRenderDocAsync(),
                    Rgp = await project.GetLaunchRgpAsync(),
                    Dive = await project.GetLaunchDiveAsync(),
                    Orbit = await project.GetLaunchOrbitAsync(),
                    SurfaceEnforcementMode = await project.GetSurfaceEnforcementAsync(),
                    VulkanDriverVariant = await project.GetVulkanDriverVariantAsync(),
                    QueryParams = await project.GetQueryParamsAsync(),
                    Endpoint = await project.GetEndpointAsync()
                };

                if (_sdkVersion != null && !string.IsNullOrEmpty(_sdkVersion.ToString()))
                {
                    launchParams.SdkVersion = _sdkVersion.ToString();
                }

                SetupQueriesResult setupQueriesResult;
                using (new TestBenchmark("QueryProjectInfo", TestBenchmarkScope.Recorder))
                {
                    if (!TrySetupQueries(project, actionRecorder, out setupQueriesResult))
                    {
                        return new IDebugLaunchSettings[] { };
                    }
                }

                launchParams.ApplicationName = setupQueriesResult.Application.Name;
                launchParams.ApplicationId = setupQueriesResult.Application.Id;
                if (setupQueriesResult.TestAccount != null)
                {
                    launchParams.TestAccount = setupQueriesResult.TestAccount.Name;
                    launchParams.TestAccountGamerName =
                        setupQueriesResult.TestAccount.GamerStadiaName;
                }

                if (setupQueriesResult.ExternalAccount != null)
                {
                    launchParams.ExternalAccount = setupQueriesResult.ExternalAccount.Name;
                    launchParams.ExternalAccountDisplayName =
                        setupQueriesResult.ExternalAccount.ExternalId;
                }

                DeployOnLaunchSetting deployOnLaunch = await project.GetDeployOnLaunchAsync();
                launchParams.Account = _credentialManager.LoadAccount();
                IGameletSelector gameletSelector = _gameletSelectorFactory.Create(actionRecorder);
                Gamelet gamelet;
                using (new TestBenchmark("SelectAndPrepareGamelet", TestBenchmarkScope.Recorder))
                {
                    if (!gameletSelector.TrySelectAndPrepareGamelet(
                        deployOnLaunch, setupQueriesResult.Gamelets, setupQueriesResult.TestAccount,
                        launchParams.Account, out gamelet))
                    {
                        return new IDebugLaunchSettings[] { };
                    }
                }

                launchParams.GameletName = gamelet.Name;
                launchParams.PoolId = gamelet.PoolId;
                launchParams.GameletSdkVersion = gamelet.GameletVersions.DevToolingVersion;
                launchParams.GameletEnvironmentVars =
                    await project.GetGameletEnvironmentVariablesAsync();

                // Prepare for debug launch using these settings.
                var debugLaunchSettings = new DebugLaunchSettings(launchOptions);
                debugLaunchSettings.Environment["PATH"] = await project.GetExecutablePathAsync();
                debugLaunchSettings.LaunchOperation = DebugLaunchOperation.CreateProcess;
                debugLaunchSettings.CurrentDirectory = await project.GetAbsoluteRootPathAsync();

                if (launchParams.Orbit)
                {
                    IAction deployOrbitLayerAction =
                        actionRecorder.CreateToolAction(ActionType.RemoteDeploy);
                    bool isLayerDeployed = _cancelableTaskFactory
                        .Create(TaskMessages.DeployingOrbitVulkanLayer,
                                async task =>
                                {
                                    await _remoteDeploy.DeployOrbitVulkanLayerAsync(
                                        project, new SshTarget(gamelet), task);
                                }).RunAndRecord(deployOrbitLayerAction);

                    if (!isLayerDeployed)
                    {
                        return new IDebugLaunchSettings[] { };
                    }
                }

                IAction action = actionRecorder.CreateToolAction(ActionType.RemoteDeploy);
                bool isDeployed = _cancelableTaskFactory.Create(
                    TaskMessages.DeployingExecutable, async task =>
                    {
                        using (new TestBenchmark("DeployGameExecutable",
                                                 TestBenchmarkScope.Recorder))
                        {
                            await _remoteDeploy.DeployGameExecutableAsync(
                                project, new SshTarget(gamelet), task, action);
                        }

                        task.Progress.Report(TaskMessages.CustomDeployCommand);
                        using (new TestBenchmark("ExecuteCustomCommand",
                                                 TestBenchmarkScope.Recorder))
                        {
                            await _remoteDeploy.ExecuteCustomCommandAsync(project, gamelet, action);
                        }
                    }).RunAndRecord(action);

                if (!isDeployed)
                {
                    return new IDebugLaunchSettings[] { };
                }

                if (launchOptions.HasFlag(DebugLaunchOptions.NoDebug))
                {
                    // Code path without debugging. Calls an RPC to launch the game and populates
                    // debugLaunchSettings in a way that instructs Visual Studio to launch
                    // cmd.exe -c "ChromeClientLauncher.exe <base64 encoded params>".

                    IVsiGameLaunch launch = _gameLauncher.CreateLaunch(launchParams);
                    if (launch == null)
                    {
                        Trace.WriteLine("Unable to retrieve launch name from the launch api.");
                        return new IDebugLaunchSettings[] { };
                    }

                    if (launchParams.Endpoint == StadiaEndpoint.AnyEndpoint)
                    {
                        // We dont need to start the ChromeClientLauncher,
                        // as we won't open a Chrome window.
                        debugLaunchSettings.Arguments = "/c exit";
                        await _taskContext.Factory.SwitchToMainThreadAsync();
                        string message = string.IsNullOrWhiteSpace(launchParams.ExternalAccount)
                            ? TaskMessages.LaunchingDeferredGameRunFlow
                            : TaskMessages.LaunchingDeferredGameWithExternalId(
                                launchParams.ApplicationId);
                        _dialogUtil.ShowMessage(message, TaskMessages.LaunchingDeferredGameTitle);
                    }
                    else
                    {
                        debugLaunchSettings.Arguments =
                            _launchCommandFormatter.CreateWithLaunchName(
                                launchParams, launch.LaunchName);
                    }

                    debugLaunchSettings.Executable =
                        Path.Combine(Environment.SystemDirectory, YetiConstants.Command);

                    debugLaunchSettings.LaunchOptions = DebugLaunchOptions.NoDebug |
                        DebugLaunchOptions.MergeEnvironment;

                    if (launchWithOrbit)
                    {
                        // Launch Orbit.
                        string gameletExecutablePath =
                            YetiConstants.RemoteGamePath + gameletExecutableRelPath;
                        _orbitLauncher.Launch(gameletExecutablePath, gamelet.Id);
                    }
                }
                else
                {
                    // Code path with debugging. Puts all parameters into debugLaunchSettings and
                    // leaves it to the debugger to actually launch the game and start Chrome.

                    if (_yetiVsiService.DebuggerOptions[DebuggerOption.SKIP_WAIT_LAUNCH] ==
                        DebuggerOptionState.DISABLED)
                    {
                        launchParams.Debug = true;
                    }

                    // TODO: This should really be the game_client executable, since
                    // the args we pass are for game_client as well.  We just need to find another
                    // way to pass the game executable.
                    debugLaunchSettings.Executable = targetPath;
                    debugLaunchSettings.LaunchDebugEngineGuid = YetiConstants.DebugEngineGuid;
                    debugLaunchSettings.Arguments =
                        _launchCommandFormatter.EncodeLaunchParams(launchParams);
                    debugLaunchSettings.LaunchOptions = DebugLaunchOptions.MergeEnvironment;
                    var parameters = new DebugEngine.DebugEngine.Params
                    {
                        TargetIp = new SshTarget(gamelet).GetString(),
                        DebugSessionId = _metrics.DebugSessionId
                    };
                    debugLaunchSettings.Options = JsonConvert.SerializeObject(parameters);
                }

                return new IDebugLaunchSettings[] { debugLaunchSettings };
            }
            catch (Exception e)
            {
                Trace.WriteLine($"QueryDebugTargetsAsync failed: {e.Demystify()}");
                _dialogUtil.ShowError(e.Message, e);
                return new IDebugLaunchSettings[] { };
            }
        }

        // Query project information required to initialize the debugger. Will return false if the
        // action is canceled by the user.
        bool TrySetupQueries(IAsyncProject project, ActionRecorder actionRecorder,
                             out SetupQueriesResult result)
        {
            var sdkConfig = _sdkConfigFactory.LoadOrDefault();
            var action = actionRecorder.CreateToolAction(ActionType.DebugSetupQueries);

            Func<Task<SetupQueriesResult>> queryInformationTask = async delegate()
            {
                action.UpdateEvent(new DeveloperLogEvent
                {
                    ProjectProperties =
                        await _projectPropertiesParser.GetStadiaProjectPropertiesAsync(project)
                });
                ICloudRunner runner = _cloudRunner.Intercept(action);
                Task<Application> loadApplicationTask =
                    LoadApplicationAsync(runner, await project.GetApplicationAsync());
                Task<List<Gamelet>> loadGameletsTask;
                loadGameletsTask = _gameletClientFactory.Create(runner).ListGameletsAsync();
                Task<TestAccount> loadTestAccountTask = LoadTestAccountAsync(
                    runner, sdkConfig.OrganizationId, sdkConfig.ProjectId,
                    await project.GetTestAccountAsync(), await project.GetEndpointAsync(),
                    await project.GetExternalIdAsync());
                Task<Player> loadExternalAccountTask = LoadExternalAccountAsync(
                    runner, loadApplicationTask, await project.GetExternalIdAsync(),
                    await project.GetEndpointAsync());

                return new SetupQueriesResult
                {
                    Application = await loadApplicationTask,
                    Gamelets = await loadGameletsTask,
                    TestAccount = await loadTestAccountTask,
                    ExternalAccount = await loadExternalAccountTask
                };
            };

            var task = _cancelableTaskFactory.Create("Querying project information...",
                                                     queryInformationTask);

            if (!task.RunAndRecord(action))
            {
                result = null;
                return false;
            }

            result = task.Result;
            return true;
        }

        /// <summary>
        /// Load the application given a string representing the app id or name.
        /// </summary>
        /// <exception cref="InvalidStateException">Thrown if the application doesn't exist
        /// </exception>
        /// <exception cref="ConfigurationException">Thrown if applicationNameOrId is null
        /// or empty</exception>
        async Task<Application> LoadApplicationAsync(ICloudRunner runner,
                                                     string applicationNameOrId)
        {
            if (string.IsNullOrEmpty(applicationNameOrId))
            {
                throw new ConfigurationException(ErrorStrings.NoApplicationConfigured);
            }

            var application = await _applicationClientFactory.Create(runner)
                .LoadByNameOrIdAsync(applicationNameOrId);
            if (application == null)
            {
                throw new InvalidStateException(
                    ErrorStrings.FailedToRetrieveApplication(applicationNameOrId));
            }

            return application;
        }

        /// <summary>
        /// Load a test account given the organization id, project id and test account
        /// (Stadia Name). Returns null if the test account is empty or null.
        /// </summary>
        /// <exception cref="ConfigurationException">Thrown if the given test account doesn't exist
        /// or if there is more than one test account with the given name.
        /// </exception>
        async Task<TestAccount> LoadTestAccountAsync(ICloudRunner runner, string organizationId,
                                                     string projectId, string testAccount,
                                                     StadiaEndpoint endpoint,
                                                     string externalAccount)
        {
            if (string.IsNullOrEmpty(testAccount))
            {
                return null;
            }

            bool testAccountSupportedWithEndpoint = endpoint != StadiaEndpoint.PlayerEndpoint &&
                endpoint != StadiaEndpoint.AnyEndpoint;
            if (!testAccountSupportedWithEndpoint)
            {
                await _taskContext.Factory.SwitchToMainThreadAsync();
                _dialogUtil.ShowWarning(
                    YetiCommon.ErrorStrings.TestAccountsNotSupported(testAccount));
                return null;
            }

            bool testAccountSupportedWithExternalId = string.IsNullOrWhiteSpace(externalAccount);
            if (!testAccountSupportedWithExternalId)
            {
                await _taskContext.Factory.SwitchToMainThreadAsync();
                _dialogUtil.ShowWarning(
                    YetiCommon.ErrorStrings.TestAccountsNotSupportedWithExternalId(
                        testAccount, externalAccount));
                return null;
            }

            var testAccounts = await _testAccountClientFactory.Create(runner)
                .LoadByIdOrGamerTagAsync(organizationId, projectId, testAccount);
            if (testAccounts.Count == 0)
            {
                throw new ConfigurationException(ErrorStrings.InvalidTestAccount(testAccount));
            }

            if (testAccounts.Count > 1)
            {
                throw new ConfigurationException(
                    ErrorStrings.MoreThanOneTestAccount(testAccounts[0].GamerTagName));
            }

            return testAccounts[0];
        }

        /// <summary>
        /// Load an external account given the application.
        /// </summary>
        /// <exception cref="ConfigurationException">Thrown if the given external account
        /// doesn't exist.
        /// </exception>
        async Task<Player> LoadExternalAccountAsync(ICloudRunner runner,
                                                    Task<Application> applicationTask,
                                                    string externalAccount, StadiaEndpoint endpoint)
        {
            if (string.IsNullOrEmpty(externalAccount))
            {
                return null;
            }

            Application application = await applicationTask;
            if (application == null)
            {
                return null;
            }

            bool externalAccountSupportedWithEndpoint = endpoint != StadiaEndpoint.PlayerEndpoint;
            if (!externalAccountSupportedWithEndpoint)
            {
                throw new ConfigurationException(YetiCommon.ErrorStrings
                                                     .LaunchOnWebNotSupportedForExternalId);
            }

            var externalAccounts =
                await _identityClient.SearchPlayers(application.PlatformName, externalAccount);
            if (externalAccounts.Count == 0)
            {
                throw new ConfigurationException(
                    ErrorStrings.InvalidExternalAccount(externalAccount));
            }

            // This should not happen unless there is an issue on backend.
            if (externalAccounts.Count > 1)
            {
                throw new ConfigurationException(
                    ErrorStrings.MultipleExternalAccounts(externalAccount));
            }

            return externalAccounts[0];
        }

        class SetupQueriesResult
        {
            public Application Application { get; set; }
            public List<Gamelet> Gamelets { get; set; }
            public TestAccount TestAccount { get; set; }
            public Player ExternalAccount { get; set; }
        }
    }
}