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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using YetiCommon.VSProject;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;

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
        bool TrySelectAndPrepareGamelet(
            string targetPath, DeployOnLaunchSetting deployOnLaunchSetting,
            ActionRecorder actionRecorder, List<Gamelet> gamelets, string testAccount,
            out Gamelet result);
    }

    //TODO: remove the legacy launch flow.
    /// <summary>
    /// GameletSelectorLegacyFlow is responsible for selecting and preparing a gamelet for launch.
    /// </summary>
    public class GameletSelectorLegacyFlow : IGameletSelector
    {
        public const string CLEAR_LOGS_CMD = "rm -f /var/game/stdout /var/game/stderr";

        public const string GAMELET_BUSY_DIALOG_TEXT =
            "Game cannot be launched as one is already running on the instance.\n" +
            "Do you want to stop it?";

        readonly GameletSelectionWindow.Factory gameletSelectionWindowFactory;
        readonly IDialogUtil dialogUtil;
        readonly CancelableTask.Factory cancelableTaskFactory;
        readonly GameletClient.Factory gameletClientFactory;
        readonly ISshManager sshManager;
        readonly IRemoteCommand remoteCommand;
        readonly ICloudRunner runner;
        readonly GameletMountChecker mountChecker;

        public GameletSelectorLegacyFlow(IDialogUtil dialogUtil, ICloudRunner runner,
                               GameletSelectionWindow.Factory gameletSelectionWindowFactory,
                               CancelableTask.Factory cancelableTaskFactory,
                               GameletClient.Factory gameletClientFactory, ISshManager sshManager,
                               IRemoteCommand remoteCommand)
        {
            this.dialogUtil = dialogUtil;
            this.runner = runner;
            this.gameletSelectionWindowFactory = gameletSelectionWindowFactory;
            this.cancelableTaskFactory = cancelableTaskFactory;
            this.gameletClientFactory = gameletClientFactory;
            this.sshManager = sshManager;
            this.remoteCommand = remoteCommand;
            mountChecker =
                new GameletMountChecker(remoteCommand, dialogUtil, cancelableTaskFactory);
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
                                               ActionRecorder actionRecorder,
                                               List<Gamelet> gamelets, string testAccount,
                                               out Gamelet result)
        {
            Gamelet gamelet = result = null;
            if (!TrySelectGamelet(actionRecorder, gamelets, out gamelet))
            {
                return false;
            }

            if (gamelet.State == GameletState.InUse)
            {
                // TODO: Do not stop the running game on the gamelet
                // in the new launch flow, it is taken care of in the game launcher.
                if (!PromptStopGamelet(actionRecorder, ref gamelet))
                {
                    return false;
                }
            }

            if (!EnableSsh(actionRecorder, gamelet))
            {
                return false;
            }

            if (!ValidateMountConfiguration(targetPath, deployOnLaunchSetting, actionRecorder,
                                            gamelet))
            {
                return false;
            }

            if (!ClearLogs(actionRecorder, gamelet))
            {
                return false;
            }

            result = gamelet;
            return true;
        }

        /// <summary>
        /// Select the first available gamelet, or let the user pick from multiple gamelets.
        /// Ensure the selected gamelet is in a valid state before returning.
        /// </summary>
        bool TrySelectGamelet(ActionRecorder actionRecorder, List<Gamelet> gamelets,
                              out Gamelet result)
        {
            Gamelet gamelet = result = null;
            if (!actionRecorder.RecordUserAction(ActionType.GameletSelect, delegate {
                switch (gamelets.Count)
                {
                    case 0:
                        throw new ConfigurationException(ErrorStrings.NoGameletsFound);
                    case 1:
                        gamelet = gamelets[0];
                        return true;
                    default:
                        gamelet = gameletSelectionWindowFactory.Create(gamelets).Run();
                        return gamelet != null;
                }
            }))
            {
                return false;
            }
            result = EnsureValidState(actionRecorder, gamelet);
            return true;
        }

        /// <summary>
        /// Confirms that the user wants to stop the gamelet and, if so, stops the gamelet.
        /// </summary>
        /// <returns>True if the user chooses to stop the gamelet and the gamelet stops
        /// successfully, false otherwise.</returns>
        bool PromptStopGamelet(ActionRecorder actionRecorder, ref Gamelet gamelet)
        {
            bool okToStop = actionRecorder.RecordUserAction(
                ActionType.GameletPrepare,
                () => dialogUtil.ShowYesNo(GAMELET_BUSY_DIALOG_TEXT, "Stop running game?"));
            if (!okToStop)
            {
                return false;
            }
            gamelet = StopGamelet(actionRecorder, gamelet);
            return gamelet != null;
        }

        /// <summary>
        /// Stop a gamelet and wait for it to return to the reserved state.
        /// </summary>
        Gamelet StopGamelet(ActionRecorder actionRecorder, Gamelet gamelet)
        {
            IAction action = actionRecorder.CreateToolAction(ActionType.GameletStop);
            ICancelableTask stopTask =
                cancelableTaskFactory.Create(TaskMessages.WaitingForGameStop, async (task) => {
                    IGameletClient gameletClient =
                        gameletClientFactory.Create(runner.Intercept(action));
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

        /// <summary>
        /// Enable SSH for communication with the gamelet.
        /// </summary>
        bool EnableSsh(ActionRecorder actionRecorder, Gamelet gamelet)
        {
            try
            {
                IAction action = actionRecorder.CreateToolAction(ActionType.GameletEnableSsh);
                ICancelableTask enableSshTask =
                    cancelableTaskFactory.Create(TaskMessages.EnablingSSH, async _ => {
                        await sshManager.EnableSshAsync(gamelet, action);
                    });
                return enableSshTask.RunAndRecord(action);
            }
            catch (Exception e) when (e is CloudException || e is SshKeyException)
            {
                Trace.Write($"Received exception while enabling ssh.\n{e}");
                dialogUtil.ShowError(ErrorStrings.FailedToEnableSsh(e.Message), e.ToString());
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
        /// <param name="actionRecorder">Recorder for the actions performed asynchronously
        /// (reading /proc/mounts).</param>
        /// <param name="gamelet">Gamelet to connect to.</param>
        /// <returns>True if no issues found or the user decided to proceed.</returns>
        bool ValidateMountConfiguration(string targetPath,
                                        DeployOnLaunchSetting deployOnLaunchSetting,
                                        ActionRecorder actionRecorder, Gamelet gamelet)
        {
            MountConfiguration configuration =
                mountChecker.GetConfiguration(gamelet, actionRecorder);

            string targetPathNormalized = GetNormalizedFullPath(targetPath);
            Trace.WriteLine($"TargetPath is set to {targetPathNormalized}");
            // If the /srv/game/assets folder is detached from /mnt/developer then
            // binaries generated by VS won't be used during the run/debug process.
            // Notify the user and let them decide whether this is expected behaviour or not.
            if (mountChecker.IsGameAssetsDetachedFromDeveloperFolder(configuration))
            {
                // 'Yes' - continue; 'No' - interrupt (gamelet validation fails).
                return dialogUtil.ShowYesNo(
                    ErrorStrings.MountConfigurationWarning(YetiConstants.GameAssetsMountingPoint,
                                                           YetiConstants.DeveloperMountingPoint),
                    _mountConfigurationDialogCaption);
            }

            if (mountChecker.IsAssetStreamingActivated(configuration))
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
                    return dialogUtil.ShowYesNo(ErrorStrings.AssetStreamingBrokenWarning(),
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
                            return dialogUtil.ShowYesNo(
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
                case DeployOnLaunchSetting.TRUE:
                    return "Yes - when changed";
                case DeployOnLaunchSetting.ALWAYS:
                    return "Yes - always";
                case DeployOnLaunchSetting.DELTA:
                    return "Yes - binary diff";
            }

            return "";
        }

        /// <summary>
        /// Clear stdout/stderr so that we don't start to tail before guest_orc clears.
        /// </summary>
        bool ClearLogs(ActionRecorder actionRecorder, Gamelet gamelet)
        {
            ICancelableTask clearLogsTask =
                cancelableTaskFactory.Create(TaskMessages.ClearingInstanceLogs,
                                             async _ => await remoteCommand.RunWithSuccessAsync(
                                                 new SshTarget(gamelet), CLEAR_LOGS_CMD));
            try
            {
                return clearLogsTask.RunAndRecord(actionRecorder, ActionType.GameletClearLogs);
            }
            catch (ProcessException e)
            {
                Trace.WriteLine($"Error clearing instance logs: {e.Message}");
                dialogUtil.ShowError(ErrorStrings.FailedToStartRequiredProcess(e.Message),
                                     e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Ensure that the gamelet is either in use or reserved. All other states will throw
        /// an InvalidStateException.
        /// </summary>
        Gamelet EnsureValidState(ActionRecorder actionRecorder, Gamelet gamelet)
        {
            if (gamelet.State == GameletState.InUse || gamelet.State == GameletState.Reserved)
            {
                return gamelet;
            }
            var error = new InvalidStateException(ErrorStrings.GameletInUnexpectedState(gamelet));
            try
            {
                actionRecorder.RecordFailure(
                    ActionType.GameletPrepare, error,
                    new DeveloperLogEvent { GameletData = GameletData.FromGamelet(gamelet) });
            }
            catch
            {
                // We ignore errors from recording and instead throw the actual error below.
                // TODO ((internal)) Implement safe logging for catch and finally statements.
            }
            throw error;
        }
    }
}
