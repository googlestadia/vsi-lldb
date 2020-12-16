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
using YetiCommon.VSProject;
using YetiVSI.DebuggerOptions;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using ICredentialManager = YetiCommon.ICredentialManager;

namespace YetiVSI
{
    public class GgpDebugQueryTarget
    {
        readonly IFileSystem fileSystem;
        readonly SdkConfig.Factory sdkConfigFactory;
        readonly GameletClient.Factory gameletClientFactory;
        readonly ApplicationClient.Factory applicationClientFactory;
        readonly IExtensionOptions options;
        readonly CancelableTask.Factory cancelableTaskFactory;
        readonly IDialogUtil dialogUtil;
        readonly IRemoteDeploy remoteDeploy;
        readonly IMetrics metrics;
        readonly ServiceManager serviceManager;
        readonly ICredentialManager credentialManager;
        readonly TestAccountClient.Factory testAccountClientFactory;
        readonly ICloudRunner cloudRunner;
        readonly YetiVSIService yetiVsiService;
        readonly IGameletSelector gameletSelector;
        readonly Versions.SdkVersion sdkVersion;
        readonly ChromeClientLaunchCommandFormatter launchCommandFormatter;
        readonly DebugEngine.DebugEngine.Params.Factory paramsFactory;

        // Constructor for tests.
        public GgpDebugQueryTarget(IFileSystem fileSystem, SdkConfig.Factory sdkConfigFactory,
                                   GameletClient.Factory gameletClientFactory,
                                   ApplicationClient.Factory applicationClientFactory,
                                   IExtensionOptions options,
                                   CancelableTask.Factory cancelableTaskFactory,
                                   IDialogUtil dialogUtil, IRemoteDeploy remoteDeploy,
                                   IMetrics metrics, ServiceManager serviceManager,
                                   ICredentialManager credentialManager,
                                   TestAccountClient.Factory testAccountClientFactory,
                                   IGameletSelector gameletSelector, ICloudRunner cloudRunner,
                                   Versions.SdkVersion sdkVersion,
                                   ChromeClientLaunchCommandFormatter launchCommandFormatter,
                                   DebugEngine.DebugEngine.Params.Factory paramsFactory)
        {
            this.fileSystem = fileSystem;
            this.sdkConfigFactory = sdkConfigFactory;
            this.gameletClientFactory = gameletClientFactory;
            this.applicationClientFactory = applicationClientFactory;
            this.options = options;
            this.cancelableTaskFactory = cancelableTaskFactory;
            this.dialogUtil = dialogUtil;
            this.remoteDeploy = remoteDeploy;
            this.metrics = metrics;
            this.serviceManager = serviceManager;
            this.credentialManager = credentialManager;
            this.testAccountClientFactory = testAccountClientFactory;
            this.cloudRunner = cloudRunner;
            yetiVsiService =
                (YetiVSIService) serviceManager.GetGlobalService(typeof(YetiVSIService));
            this.gameletSelector = gameletSelector;
            this.sdkVersion = sdkVersion;
            this.launchCommandFormatter = launchCommandFormatter;
            this.paramsFactory = paramsFactory;
        }

        public async Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(
            IAsyncProject project, DebugLaunchOptions launchOptions)
        {
            try
            {
                // Make sure we can find the target executable.
                var targetPath = await project.GetTargetPathAsync();
                if (!fileSystem.File.Exists(targetPath))
                {
                    Trace.WriteLine($"Unable to find target executable: {targetPath}");
                    dialogUtil.ShowError(ErrorStrings.UnableToFindTargetExecutable(targetPath));
                    return new IDebugLaunchSettings[] { };
                }

                var debugSessionMetrics = new DebugSessionMetrics(metrics);
                debugSessionMetrics.UseNewDebugSessionId();
                var actionRecorder = new ActionRecorder(debugSessionMetrics);

                var targetFileName = await project.GetTargetFileNameAsync();
                var gameletCommand = (targetFileName + " " +
                    await project.GetGameletLaunchArgumentsAsync()).Trim();

                var launchParams = new YetiCommon.ChromeClientLauncher.Params()
                {
                    Cmd = gameletCommand,
                    RenderDoc = await project.GetLaunchRenderDocAsync(),
                    Rgp = await project.GetLaunchRgpAsync(),
                    SurfaceEnforcementMode = await project.GetSurfaceEnforcementAsync(),
                    VulkanDriverVariant = await project.GetVulkanDriverVariantAsync(),
                    QueryParams = await project.GetQueryParamsAsync()
                };

                if (sdkVersion != null && !string.IsNullOrEmpty(sdkVersion.ToString()))
                {
                    launchParams.SdkVersion = sdkVersion.ToString();
                }

                SetupQueriesResult setupQueriesResult;
                if (!TrySetupQueries(project, actionRecorder, out setupQueriesResult))
                {
                    return new IDebugLaunchSettings[] { };
                }

                launchParams.ApplicationId = setupQueriesResult.Application.Id;
                if (setupQueriesResult.TestAccount != null)
                {
                    launchParams.TestAccount = setupQueriesResult.TestAccount.Name;
                }

                Gamelet gamelet;
                DeployOnLaunchSetting deployOnLaunchAsync = await project.GetDeployOnLaunchAsync();

                if (!gameletSelector.TrySelectAndPrepareGamelet(
                    targetPath, deployOnLaunchAsync, actionRecorder,
                    setupQueriesResult.Gamelets, out gamelet))
                {
                    return new IDebugLaunchSettings[] { };
                }

                launchParams.GameletId = gamelet.Id;
                launchParams.PoolId = gamelet.PoolId;
                launchParams.GameletEnvironmentVars =
                    await project.GetGameletEnvironmentVariablesAsync();
                launchParams.Account = credentialManager.LoadAccount();

                // Prepare for debug launch using these settings.
                var debug_launch_settings = new DebugLaunchSettings(launchOptions);
                debug_launch_settings.Environment["PATH"] = await project.GetExecutablePathAsync();
                debug_launch_settings.LaunchOperation = DebugLaunchOperation.CreateProcess;
                debug_launch_settings.CurrentDirectory = await project.GetAbsoluteRootPathAsync();

                if ((launchOptions & DebugLaunchOptions.NoDebug) != DebugLaunchOptions.NoDebug)
                {
                    var parameters = paramsFactory.Create();
                    parameters.TargetIp = new SshTarget(gamelet).GetString();
                    parameters.DebugSessionId = debugSessionMetrics.DebugSessionId;
                    debug_launch_settings.Options = paramsFactory.Serialize(parameters);
                }

                var action = actionRecorder.CreateToolAction(ActionType.RemoteDeploy);
                var isDeployed = cancelableTaskFactory.Create(
                    TaskMessages.DeployingExecutable,
                    async task =>
                    {
                        await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);
                        task.Progress.Report(TaskMessages.CustomDeployCommand);
                        await remoteDeploy.ExecuteCustomCommandAsync(project, gamelet, action);
                    }).RunAndRecord(action);

                if (!isDeployed)
                {
                    return new IDebugLaunchSettings[] { };
                }

                if ((launchOptions & DebugLaunchOptions.NoDebug) == DebugLaunchOptions.NoDebug)
                {
                    debug_launch_settings.Executable =
                        Path.Combine(Environment.SystemDirectory, YetiConstants.Command);
                    debug_launch_settings.Arguments = launchCommandFormatter.Create(launchParams);
                    debug_launch_settings.LaunchOptions = DebugLaunchOptions.NoDebug |
                        DebugLaunchOptions.MergeEnvironment;
                }
                else
                {
                    if (yetiVsiService.DebuggerOptions[DebuggerOption.SKIP_WAIT_LAUNCH] ==
                        DebuggerOptionState.DISABLED)
                    {
                        launchParams.Debug = true;
                    }

                    // TODO: This should really be the game_client executable, since
                    // the args we pass are for game_client as well.  We just need to find another
                    // way to pass the game executable.
                    debug_launch_settings.Executable = targetPath;
                    debug_launch_settings.LaunchDebugEngineGuid = YetiConstants.DebugEngineGuid;
                    debug_launch_settings.Arguments =
                        launchCommandFormatter.EncodeLaunchParams(launchParams);
                    debug_launch_settings.LaunchOptions = DebugLaunchOptions.MergeEnvironment;
                }

                return new IDebugLaunchSettings[] {debug_launch_settings};
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
                dialogUtil.ShowError(e.Message, e.ToString());
                return new IDebugLaunchSettings[] { };
            }
        }

        // Query project information required to initialize the debugger. Will return false if the
        // action is canceled by the user.
        private bool TrySetupQueries(IAsyncProject project, ActionRecorder actionRecorder,
                                     out SetupQueriesResult result)
        {
            var sdkConfig = sdkConfigFactory.LoadOrDefault();
            var action = actionRecorder.CreateToolAction(ActionType.DebugSetupQueries);

            Func<Task<SetupQueriesResult>> queryInformationTask =
                    async delegate ()
                    {
                        var runner = cloudRunner.Intercept(action);
                        var loadApplicationTask =
                            LoadApplicationAsync(runner, await project.GetApplicationAsync());
                        var loadGameletsTask =
                            gameletClientFactory.Create(runner).ListGameletsAsync();
                        var loadTestAccountTask =
                            LoadTestAccountAsync(
                                runner, sdkConfig.OrganizationId, sdkConfig.ProjectId,
                                await project.GetTestAccountAsync());

                        return new SetupQueriesResult
                        {
                            Application = await loadApplicationTask,
                            Gamelets = await loadGameletsTask,
                            TestAccount = await loadTestAccountTask
                        };
                    };

            var task = cancelableTaskFactory.Create("Querying project information...",
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
        private async Task<Application> LoadApplicationAsync(ICloudRunner runner,
                                                             string applicationNameOrId)
        {
            if (string.IsNullOrEmpty(applicationNameOrId))
            {
                throw new ConfigurationException(ErrorStrings.NoApplicationConfigured);
            }

            var application = await applicationClientFactory.Create(runner)
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
        private async Task<TestAccount> LoadTestAccountAsync(ICloudRunner runner,
                                                             string organizationId,
                                                             string projectId, string testAccount)
        {
            if (string.IsNullOrEmpty(testAccount))
            {
                return null;
            }

            var testAccounts = await testAccountClientFactory.Create(runner)
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

        private class SetupQueriesResult
        {
            public Application Application { get; set; }
            public List<Gamelet> Gamelets { get; set; }
            public TestAccount TestAccount { get; set; }
        }
    }
}