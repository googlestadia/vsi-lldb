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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.DebugEngine;
using YetiVSI.DebuggerOptions;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;
using YetiVSI.ProjectSystem.Abstractions;

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
        readonly DebugEngine.DebugEngine.Params.Factory _paramsFactory;
        readonly IGameLauncher _gameLauncher;

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
                                   DebugEngine.DebugEngine.Params.Factory paramsFactory,
                                   IYetiVSIService yetiVsiService, IGameLauncher gameLauncher)
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
            _paramsFactory = paramsFactory;
            _gameLauncher = gameLauncher;
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

                _metrics.UseNewDebugSessionId();
                var actionRecorder = new ActionRecorder(_metrics);

                var targetFileName = await project.GetTargetFileNameAsync();
                var gameletCommand = (targetFileName + " " +
                    await project.GetGameletLaunchArgumentsAsync()).Trim();

                var launchParams = new LaunchParams() {
                    Cmd = gameletCommand,
                    RenderDoc = await project.GetLaunchRenderDocAsync(),
                    Rgp = await project.GetLaunchRgpAsync(),
                    SurfaceEnforcementMode = await project.GetSurfaceEnforcementAsync(),
                    VulkanDriverVariant = await project.GetVulkanDriverVariantAsync(),
                    QueryParams = await project.GetQueryParamsAsync(),
                    Endpoint = await project.GetEndpointAsync()
                };

                if (_sdkVersion != null && !string.IsNullOrEmpty(_sdkVersion.ToString()))
                {
                    launchParams.SdkVersion = _sdkVersion.ToString();
                }

                if (!TrySetupQueries(project, actionRecorder,
                                     out SetupQueriesResult setupQueriesResult))
                {
                    return new IDebugLaunchSettings[] { };
                }

                launchParams.ApplicationName = setupQueriesResult.Application.Name;
                launchParams.ApplicationId = setupQueriesResult.Application.Id;
                if (setupQueriesResult.TestAccount != null)
                {
                    launchParams.TestAccount = setupQueriesResult.TestAccount.Name;
                    launchParams.TestAccountGamerName =
                        setupQueriesResult.TestAccount.GamerStadiaName;
                }

                DeployOnLaunchSetting deployOnLaunchAsync = await project.GetDeployOnLaunchAsync();
                launchParams.Account = _credentialManager.LoadAccount();

                // TODO: Enable PlayerEndpoint Launches for non-internal usage in VS.
                if (launchParams.Endpoint == StadiaEndpoint.PlayerEndpoint &&
                    launchParams.Account != null &&
                    !launchParams.Account.EndsWith("@sparklingsunset.com") &&
                    !launchParams.Account.EndsWith("@subtlesunset.com"))
                {
                    throw new NotImplementedException(
                        "Player Endpoints are not yet supported, please select " +
                        "Test Client in the Project Properties instead.");
                }

                bool launchGameApiEnabled =
                    _yetiVsiService.Options.LaunchGameApiFlow == LaunchGameApiFlow.ENABLED;
                IGameletSelector gameletSelector =
                    _gameletSelectorFactory.Create(launchGameApiEnabled, actionRecorder);
                if (!gameletSelector.TrySelectAndPrepareGamelet(
                    targetPath, deployOnLaunchAsync, setupQueriesResult.Gamelets,
                    setupQueriesResult.TestAccount, launchParams.Account, out Gamelet gamelet))
                {
                    return new IDebugLaunchSettings[] { };
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

                if (!launchOptions.HasFlag(DebugLaunchOptions.NoDebug))
                {
                    var parameters = _paramsFactory.Create();
                    parameters.TargetIp = new SshTarget(gamelet).GetString();
                    parameters.DebugSessionId = _metrics.DebugSessionId;
                    debugLaunchSettings.Options = _paramsFactory.Serialize(parameters);
                }

                IAction action = actionRecorder.CreateToolAction(ActionType.RemoteDeploy);
                bool isDeployed = _cancelableTaskFactory.Create(
                    TaskMessages.DeployingExecutable, async task =>
                    {
                        await _remoteDeploy.DeployGameExecutableAsync(
                            project, gamelet, task, action);
                        task.Progress.Report(TaskMessages.CustomDeployCommand);
                        await _remoteDeploy.ExecuteCustomCommandAsync(project, gamelet, action);
                    }).RunAndRecord(action);

                if (!isDeployed)
                {
                    return new IDebugLaunchSettings[] { };
                }

                if (launchOptions.HasFlag(DebugLaunchOptions.NoDebug))
                {
                    if (_gameLauncher.LaunchGameApiEnabled ||
                        launchParams.Endpoint == StadiaEndpoint.PlayerEndpoint ||
                        launchParams.Endpoint == StadiaEndpoint.AnyEndpoint)
                    {
                        IVsiGameLaunch launch = _gameLauncher.CreateLaunch(launchParams);
                        if (launch != null)
                        {
                            debugLaunchSettings.Arguments =
                                _launchCommandFormatter.CreateWithLaunchName(
                                    launchParams, launch.LaunchName);
                        }
                        else
                        {
                            Trace.WriteLine("Unable to retrieve launch name from the launch api.");
                            return new IDebugLaunchSettings[] { };
                        }
                    }
                    else
                    {
                        debugLaunchSettings.Arguments =
                            _launchCommandFormatter.CreateFromParams(launchParams);
                    }

                    debugLaunchSettings.Executable =
                        Path.Combine(Environment.SystemDirectory, YetiConstants.Command);

                    debugLaunchSettings.LaunchOptions = DebugLaunchOptions.NoDebug |
                        DebugLaunchOptions.MergeEnvironment;
                }
                else
                {
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
                }

                return new IDebugLaunchSettings[] { debugLaunchSettings };
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
                _dialogUtil.ShowError(e.Message, e.ToString());
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
                var runner = _cloudRunner.Intercept(action);
                var loadApplicationTask =
                    LoadApplicationAsync(runner, await project.GetApplicationAsync());
                var loadGameletsTask = _gameletClientFactory.Create(runner).ListGameletsAsync();
                var loadTestAccountTask = LoadTestAccountAsync(
                    runner, sdkConfig.OrganizationId, sdkConfig.ProjectId,
                    await project.GetTestAccountAsync());

                return new SetupQueriesResult
                {
                    Application = await loadApplicationTask,
                    Gamelets = await loadGameletsTask,
                    TestAccount = await loadTestAccountTask
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
                                                     string projectId, string testAccount)
        {
            if (string.IsNullOrEmpty(testAccount))
            {
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

        class SetupQueriesResult
        {
            public Application Application { get; set; }
            public List<Gamelet> Gamelets { get; set; }
            public TestAccount TestAccount { get; set; }
        }
    }
}