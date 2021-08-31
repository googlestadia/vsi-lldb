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
using Grpc.Core;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.Metrics;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiVSI.GameLaunch
{
    public interface IGameletSelector
    {
        /// <summary>
        /// Selects a gamelet from the given list and prepares it for running a game.
        /// </summary>
        /// <exception cref="InvalidStateException">
        /// Thrown when the selected gamelet is in an unexpected state.</exception>
        /// <exception cref="ConfigurationException">
        /// Thrown if there is no gamelet reserved</exception>
        /// <exception cref="CloudException">Thrown if there are any RPC errors.</exception>
        /// <returns>True if the gamelet was prepared successfully, false otherwise.</returns>
        bool TrySelectAndPrepareGamelet(string targetPath,
                                        DeployOnLaunchSetting deployOnLaunchSetting,
                                        List<Gamelet> gamelets,
                                        TestAccount testAccount, string devAccount,
                                        out Gamelet gamelet);
    }

    /// <summary>
    /// GameletSelector is responsible for selecting and preparing a gamelet
    /// for launch when the Game Launch API is enabled.
    /// </summary>
    public class GameletSelector: IGameletSelector
    {
        public const string ClearLogsCmd = "rm -f /var/game/stdout /var/game/stderr";

        readonly InstanceSelectionWindow.Factory _gameletSelectionWindowFactory;
        readonly IDialogUtil _dialogUtil;
        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly IGameletClientFactory _gameletClientFactory;
        readonly ISshManager _sshManager;
        readonly IRemoteCommand _remoteCommand;
        readonly ICloudRunner _runner;
        readonly GameletMountChecker _mountChecker;
        readonly IGameLaunchBeHelper _gameLaunchBeHelper;
        readonly JoinableTaskContext _taskContext;
        readonly ActionRecorder _actionRecorder;

        public GameletSelector(IDialogUtil dialogUtil, ICloudRunner runner,
                               InstanceSelectionWindow.Factory gameletSelectionWindowFactory,
                               CancelableTask.Factory cancelableTaskFactory,
                               IGameletClientFactory gameletClientFactory, ISshManager sshManager,
                               IRemoteCommand remoteCommand, IGameLaunchBeHelper gameLaunchBeHelper,
                               JoinableTaskContext taskContext, ActionRecorder actionRecorder)
        {
            _dialogUtil = dialogUtil;
            _runner = runner;
            _gameletSelectionWindowFactory = gameletSelectionWindowFactory;
            _cancelableTaskFactory = cancelableTaskFactory;
            _gameletClientFactory = gameletClientFactory;
            _sshManager = sshManager;
            _remoteCommand = remoteCommand;
            _mountChecker =
                new GameletMountChecker(remoteCommand, dialogUtil, cancelableTaskFactory);
            _gameLaunchBeHelper = gameLaunchBeHelper;
            _taskContext = taskContext;
            _actionRecorder = actionRecorder;
        }

        /// <summary>
        /// Selects a gamelet from the given list and prepares it for running a game.
        /// </summary>
        /// <exception cref="InvalidStateException">
        /// Thrown when the selected gamelet is in an unexpected state.</exception>
        /// <exception cref="ConfigurationException">
        /// Thrown if there is no gamelet reserved</exception>
        /// <exception cref="CloudException">Thrown if there are any RPC errors.</exception>
        /// <returns>True if the gamelet was prepared successfully, false otherwise.</returns>
        public bool TrySelectAndPrepareGamelet(string targetPath,
                                               DeployOnLaunchSetting deployOnLaunchSetting,
                                               List<Gamelet> gamelets, TestAccount testAccount,
                                               string devAccount, out Gamelet gamelet)
        {
            if (!TrySelectGamelet(gamelets, out gamelet))
            {
                return false;
            }

            if (!StopGameLaunchIfPresent(testAccount, devAccount, gamelets, gamelet))
            {
                return false;
            }

            /// If developer runs a game with a test account first, then switches the account and
            /// tries to run a game on the same gamelet, the <see cref="StopGameLaunchIfPresent"/>
            /// won't stop the running game on the gamelet.
            if (!StopGameIfNeeded(ref gamelet))
            {
                return false;
            }

            if (!EnableSsh(gamelet))
            {
                return false;
            }

            if (!ValidateMountConfiguration(targetPath, deployOnLaunchSetting, gamelet))
            {
                return false;
            }

            if (!ClearLogs(gamelet))
            {
                return false;
            }

            return true;
        }

        bool StopGameIfNeeded(ref Gamelet gamelet)
        {
            IGameletClient gameletClient = _gameletClientFactory.Create(_runner);
            string gameletName = gamelet.Name;
            gamelet = _taskContext.Factory.Run(async () =>
                                                   await gameletClient.GetGameletByNameAsync(
                                                       gameletName));

            return gamelet.State != GameletState.InUse || PromptStopGame(ref gamelet);
        }

        /// <summary>
        /// Confirms that the user wants to stop the gamelet and, if so, stops the gamelet.
        /// </summary>
        /// <returns>True if the user chooses to stop the gamelet and the gamelet stops
        /// successfully, false otherwise.</returns>
        bool PromptStopGame(ref Gamelet gamelet)
        {
            string gameletName = gamelet.DisplayName;
            bool okToStop = _actionRecorder.RecordUserAction(
                ActionType.GameletPrepare,
                () => _dialogUtil.ShowYesNo(ErrorStrings.GameletBusyWithAnotherAccount(gameletName),
                                            ErrorStrings.StopRunningGame));
            if (!okToStop)
            {
                return false;
            }
            gamelet = StopGameOnGamelet(gamelet);
            return gamelet != null;
        }

        /// <summary>
        /// Stop game on the gamelet and wait for it to return to the reserved state.
        /// </summary>
        Gamelet StopGameOnGamelet(Gamelet gamelet)
        {
            IAction action = _actionRecorder.CreateToolAction(ActionType.GameletStop);
            ICancelableTask stopTask =
                _cancelableTaskFactory.Create(TaskMessages.WaitingForGameStop, async (task) => {
                    IGameletClient gameletClient =
                        _gameletClientFactory.Create(_runner.Intercept(action));
                    try
                    {
                        await gameletClient.StopGameAsync(gamelet.Id);
                    }
                    catch (CloudException e) when ((e.InnerException as RpcException)
                        ?.StatusCode == StatusCode.FailedPrecondition)
                    {
                        // FailedPreconditions likely means there is no game session to stop.
                        // For details see (internal).
                        Trace.WriteLine("Potential race condition while stopping game; " +
                                        $"ignoring RPC error: {e.InnerException.Message}");
                    }
                    while (!task.IsCanceled)
                    {
                        gamelet = await gameletClient.GetGameletByNameAsync(gamelet.Name);
                        if (gamelet.State == GameletState.Reserved)
                        {
                            break;
                        }
                        await Task.Delay(1000);
                    }
                });
            if (stopTask.RunAndRecord(action))
            {
                return gamelet;
            }
            return null;
        }

        bool StopGameLaunchIfPresent(TestAccount testAccount,
                                     string devAccount, List<Gamelet> gamelets,
                                     Gamelet selectedGamelet)
        {
            IAction getExistingAction =
                _actionRecorder.CreateToolAction(ActionType.GameLaunchGetExisting);
            ICancelableTask<GgpGrpc.Models.GameLaunch> currentGameLaunchTask =
                _cancelableTaskFactory.Create(TaskMessages.LookingForTheCurrentLaunch,
                                              async task =>
                                                  await _gameLaunchBeHelper
                                                      .GetCurrentGameLaunchAsync(
                                                          testAccount?.Name, getExistingAction));
            if (!currentGameLaunchTask.RunAndRecord(getExistingAction))
            {
                return false;
            }

            GgpGrpc.Models.GameLaunch currentGameLaunch = currentGameLaunchTask.Result;
            if (currentGameLaunch == null)
            {
                return true;
            }

            if (!PromptToDeleteLaunch(currentGameLaunch, gamelets, selectedGamelet, _actionRecorder,
                                      testAccount?.GamerStadiaName, devAccount))
            {
                return false;
            }

            IAction deleteAction =
                _actionRecorder.CreateToolAction(ActionType.GameLaunchDeleteExisting);
            ICancelableTask<DeleteLaunchResult> stopTask = _cancelableTaskFactory.Create(
                TaskMessages.WaitingForGameStop,
                async task =>
                    await _gameLaunchBeHelper.DeleteLaunchAsync(
                        currentGameLaunch.Name, task, deleteAction));
            return stopTask.RunAndRecord(deleteAction) && stopTask.Result.IsSuccessful;
        }

        bool PromptToDeleteLaunch(GgpGrpc.Models.GameLaunch currentGameLaunch,
                                  List<Gamelet> gamelets, Gamelet selectedGamelet,
                                  ActionRecorder actionRecorder,
                                  string testAccount, string devAccount)
        {
            if (currentGameLaunch.GameLaunchState == GameLaunchState.IncompleteLaunch)
            {
                return true;
            }

            bool thisInstance = selectedGamelet.Name == currentGameLaunch.GameletName;
            string instanceName = gamelets
                .SingleOrDefault(g => g.Name == currentGameLaunch.GameletName)?.DisplayName;
            bool okToStop = actionRecorder.RecordUserAction(ActionType.GameLaunchStopPrompt,
                                                            () => _dialogUtil.ShowYesNo(
                                                                ErrorStrings.LaunchExistsDialogText(
                                                                    thisInstance, instanceName,
                                                                    testAccount, devAccount),
                                                                ErrorStrings.StopRunningGame));
            if (!okToStop)
            {
                // Developer opted to not stop the existing launch.
                // Launch can not proceed.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Select the first available gamelet, or let the user pick from multiple gamelets.
        /// Ensure the selected gamelet is in a valid state before returning.
        /// </summary>
        bool TrySelectGamelet(List<Gamelet> gamelets, out Gamelet result)
        {
            Gamelet gamelet = result = null;
            bool res = _actionRecorder.RecordUserAction(ActionType.GameletSelect, delegate
            {
                bool isValid;
                switch (gamelets.Count)
                {
                    case 0:
                        throw new ConfigurationException(ErrorStrings.NoGameletsFound);
                    case 1:
                        gamelet = gamelets[0];
                        isValid = true;
                        break;
                    default:
                        gamelet = _gameletSelectionWindowFactory.Create(gamelets).Run();
                        isValid = gamelet != null;
                        break;
                }

                if (!isValid)
                {
                    return false;
                }

                if (gamelet.State != GameletState.InUse && gamelet.State != GameletState.Reserved)
                {
                    throw new InvalidStateException(ErrorStrings.GameletInUnexpectedState(gamelet));
                }

                return true;
            });
            result = gamelet;
            return res;
        }

        /// <summary>
        /// Enable SSH for communication with the gamelet.
        /// </summary>
        bool EnableSsh(Gamelet gamelet)
        {
            try
            {
                IAction action = _actionRecorder.CreateToolAction(ActionType.GameletEnableSsh);
                ICancelableTask enableSshTask =
                    _cancelableTaskFactory.Create(TaskMessages.EnablingSSH, async _ => {
                        await _sshManager.EnableSshAsync(gamelet, action);
                    });
                return enableSshTask.RunAndRecord(action);
            }
            catch (Exception e) when (e is CloudException || e is SshKeyException)
            {
                Trace.Write($"Received exception while enabling ssh.\n{e}");
                _dialogUtil.ShowError(ErrorStrings.FailedToEnableSsh(e.Message), e.ToString());
                return false;
            }
        }

        const string _mountConfigurationDialogCaption = "Mount configuration";

        /// <summary>
        /// Check whether the deployment configuration of the binary works correctly
        /// with the mount configuration of the gamelet.
        /// </summary>
        /// <param name="targetPath">Path to the generated binary.</param>
        /// <param name="deployOnLaunchSetting">Project's "Deploy On Launch" value.</param>
        /// <param name="gamelet">Gamelet to connect to.</param>
        /// <returns>True if no issues found or the user decided to proceed.</returns>
        bool ValidateMountConfiguration(string targetPath,
                                        DeployOnLaunchSetting deployOnLaunchSetting,
                                        Gamelet gamelet)
        {
            MountConfiguration configuration =
                _mountChecker.GetConfiguration(gamelet, _actionRecorder);

            string targetPathNormalized = GetNormalizedFullPath(targetPath);
            Trace.WriteLine($"TargetPath is set to {targetPathNormalized}");
            // If the /srv/game/assets folder is detached from /mnt/developer then
            // binaries generated by VS won't be used during the run/debug process.
            // Notify the user and let them decide whether this is expected behaviour or not.
            if (_mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration))
            {
                // 'Yes' - continue; 'No' - interrupt (gamelet validation fails).
                return _dialogUtil.ShowYesNo(
                    ErrorStrings.MountConfigurationWarning(YetiConstants.GameAssetsMountingPoint,
                                                           YetiConstants.DeveloperMountingPoint),
                    _mountConfigurationDialogCaption);
            }

            if (_mountChecker.IsAssetStreamingActivated(configuration))
            {
                var sshChannels = new SshTunnels();
                IEnumerable<string> commandLines = sshChannels.GetSshCommandLines();
                string[] mountPoints = sshChannels.ExtractMountingPoints(commandLines).ToArray();

                if (mountPoints.Length == 0)
                {
                    // If asset streaming is set up on the gamelet but there is no ssh tunnels
                    // between the workstation and the gamelet then the connection was
                    // probably lost (or asset streaming is set to a different machine, and
                    // then it's ok).
                    // 'Yes' - continue; 'No' - interrupt (gamelet validation fails).
                    return _dialogUtil.ShowYesNo(ErrorStrings.AssetStreamingBrokenWarning(),
                                                _mountConfigurationDialogCaption);
                }

                if (deployOnLaunchSetting != DeployOnLaunchSetting.FALSE)
                {
                    foreach (string mountPoint in mountPoints)
                    {
                        string mountPointNormalized = GetNormalizedFullPath(mountPoint);
                        if (targetPathNormalized.StartsWith($@"{mountPointNormalized}\"))
                        {
                            // The mount point folder matches the output folder for the binaries;
                            // VS will try to upload the binaries to the gamelet and this might lead
                            // to an exception during 'scp' call. Instead, asset streaming should
                            // take care of uploading the generated data to the gamelet. 'Yes' -
                            // continue; 'No' - interrupt (gamelet validation fails).
                            string current = GgpDeployOnLaunchToDisplayName(deployOnLaunchSetting);
                            string expected =
                                GgpDeployOnLaunchToDisplayName(DeployOnLaunchSetting.FALSE);
                            return _dialogUtil.ShowYesNo(
                                ErrorStrings.AssetStreamingDeployWarning(mountPointNormalized,
                                                                         current, expected),
                                _mountConfigurationDialogCaption);
                        }
                    }
                }
            }

            return true;

            string GetNormalizedFullPath(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }

                string normalizedPath = FileUtil.GetNormalizedPath(path);
                if (File.Exists(normalizedPath) && FileUtil.IsPathSymlink(normalizedPath))
                {
                    string symlinkTarget = NativeMethods.GetTargetPathName(path);
                    return FileUtil.GetNormalizedPath(symlinkTarget);
                }

                return normalizedPath;
            }
        }

        string GgpDeployOnLaunchToDisplayName(DeployOnLaunchSetting enumValue)
        {
            // These values are copied from DisplayNames in debugger_ggp.xml.
            switch (enumValue)
            {
                case DeployOnLaunchSetting.FALSE:
                    return "No";
                case DeployOnLaunchSetting.DELTA:
                    return "Yes - incremental";
                case DeployOnLaunchSetting.ALWAYS:
                    return "Yes - always";
            }

            return "";
        }

        /// <summary>
        /// Clear stdout/stderr so that we don't start to tail before guest_orc clears.
        /// </summary>
        bool ClearLogs(Gamelet gamelet)
        {
            ICancelableTask clearLogsTask =
                _cancelableTaskFactory.Create(TaskMessages.ClearingInstanceLogs,
                                             async _ => await _remoteCommand.RunWithSuccessAsync(
                                                 new SshTarget(gamelet), ClearLogsCmd));
            try
            {
                return clearLogsTask.RunAndRecord(_actionRecorder, ActionType.GameletClearLogs);
            }
            catch (ProcessException e)
            {
                Trace.WriteLine($"Error clearing instance logs: {e.Message}");
                _dialogUtil.ShowError(ErrorStrings.FailedToStartRequiredProcess(e.Message),
                                     e.ToString());
                return false;
            }
        }
    }
}
