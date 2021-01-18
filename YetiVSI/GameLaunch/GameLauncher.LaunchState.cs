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
using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using YetiCommon;

namespace YetiVSI.GameLaunch
{
    public partial class GameLauncher
    {
        class LaunchStatus
        {
            public bool GameLaunched { get; }
            public string Error { get; }

            LaunchStatus(bool gameLaunched, string error)
            {
                GameLaunched = gameLaunched;
                Error = error;
            }

            public static LaunchStatus Success() => new LaunchStatus(true, "");

            public static LaunchStatus Failed(string error) =>
                new LaunchStatus(false, error);

            public static LaunchStatus Canceled() => new LaunchStatus(false, "");
        }

        public bool GetLaunchState()
        {
            LaunchStatus status = LaunchStatus.Failed("");
            if (!CurrentLaunchExists)
            {
                Trace.WriteLine("Current launch doesn't exist.");
                return false;
            }

            ICancelableTask pollForLaunchStatusTask = _cancelableTaskFactory.Create(
                TaskMessages.LaunchingGame,
                async (task) => { status = await PollForLaunchStatusAsync(task); });

            // TODO: use RunAndRecord to collect metrics.
            pollForLaunchStatusTask.Run();

            if (!status.GameLaunched && !string.IsNullOrEmpty(status.Error))
            {
                Trace.WriteLine(status.Error);
                MessageDialog.Show(ErrorStrings.Error, status.Error, MessageDialogCommandSet.Ok);
            }

            return status.GameLaunched;
        }

        public async Task StopGameAsync()
        {
            await _gameletClient.DeleteGameLaunchAsync(_launchName);
        }

        // Polling statuses until we see RunningGame or GameLaunchEnded. IncompleteLaunch,
        // ReadyToPlay and DelayedLaunch are transitioning states.
        async Task<LaunchStatus> PollForLaunchStatusAsync(ICancelable task)
        {
            while (!task.IsCanceled)
            {
                GgpGrpc.Models.GameLaunch launch =
                    await _gameletClient.GetGameLaunchStateAsync(_launchName);

                if (launch.GameLaunchState == GameLaunchState.RunningGame)
                {
                    return LaunchStatus.Success();
                }

                if (launch.GameLaunchState == GameLaunchState.GameLaunchEnded)
                {
                    string error = ProcessEndReason(launch.GameLaunchEnded);
                    return LaunchStatus.Failed(error);
                }

                await Task.Delay(500);
            }

            return LaunchStatus.Canceled();
        }

        // Some of the statuses might not be applicable for the dev flow.
        string ProcessEndReason(GameLaunchEnded gameLaunchEnded)
        {
            string message = ErrorStrings.LaunchEndedCommonMessage;
            switch (gameLaunchEnded.EndReason)
            {
                case EndReason.Unspecified:
                    message += ErrorStrings.LaunchEndedUnspecified;
                    break;
                case EndReason.ExitedByUser:
                    message += ErrorStrings.LaunchEndedExitedByUser;
                    break;
                case EndReason.InactivityTimeout:
                    message += ErrorStrings.LaunchEndedInactivityTimeout;
                    break;
                case EndReason.ClientNeverConnected:
                    message += ErrorStrings.LaunchEndedClientNeverConnected;
                    break;
                case EndReason.GameExitedWithSuccessfulCode:
                    message += ErrorStrings.LaunchEndedGameExitedWithSuccessfulCode;
                    break;
                case EndReason.GameExitedWithErrorCode:
                    message += ErrorStrings.LaunchEndedGameExitedWithErrorCode;
                    break;
                case EndReason.GameShutdownBySystem:
                    message += ErrorStrings.LaunchEndedGameShutdownBySystem;
                    break;
                case EndReason.UnexpectedGameShutdownBySystem:
                    message += ErrorStrings.LaunchEndedUnexpectedGameShutdownBySystem;
                    break;
                case EndReason.GameBinaryNotFound:
                    message += ErrorStrings.LaunchEndedGameBinaryNotFound;
                    break;
                case EndReason.QueueAbandonedByUser:
                    message += ErrorStrings.LaunchEndedQueueAbandonedByUser;
                    break;
                case EndReason.QueueReadyTimeout:
                    message += ErrorStrings.LaunchEndedQueueReadyTimeout;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(gameLaunchEnded.EndReason),
                                                          gameLaunchEnded.EndReason,
                                                          "Unexpected end reason received.");
            }

            return message;
        }

        public async Task<bool> DeleteLaunchAsync(string gameLaunchName, ICancelable task)
        {
            if (task.IsCanceled)
            {
                return false;
            }

            GgpGrpc.Models.GameLaunch launch;

            try
            {
                launch =
                    await _gameletClient.DeleteGameLaunchAsync(gameLaunchName);
            }
            catch (CloudException e) when ((e.InnerException as RpcException)?.StatusCode ==
                StatusCode.NotFound)
            {
                // There is no launch with the specified name.
                return true;
            }

            while (!task.IsCanceled && launch.GameLaunchState != GameLaunchState.GameLaunchEnded)
            {
                launch = await _gameletClient.GetGameLaunchStateAsync(gameLaunchName);
                await Task.Delay(500);
            }

            return launch.GameLaunchState == GameLaunchState.GameLaunchEnded;
        }
    }
}