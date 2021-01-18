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

        public bool WaitUntilGameLaunched()
        {
            if (!CurrentLaunchExists)
            {
                Trace.WriteLine("Current launch doesn't exist.");
                return false;
            }

            ICancelableTask<LaunchStatus> pollForLaunchStatusTask = _cancelableTaskFactory.Create(
                TaskMessages.LaunchingGame, async (task) => await PollForLaunchStatusAsync(task));

            // TODO: use RunAndRecord to collect metrics.
            if (!pollForLaunchStatusTask.Run())
            {
                Trace.WriteLine("Polling for the launch status has been canceled by user.");
                return false;
            }

            LaunchStatus status = pollForLaunchStatusTask.Result;
            if (!status.GameLaunched && !string.IsNullOrEmpty(status.Error))
            {
                Trace.WriteLine(status.Error);
                _dialogUtil.ShowError(status.Error);
            }

            return status.GameLaunched;
        }

        public void StopGame()
        {
            if (!CurrentLaunchExists)
            {
                Trace.WriteLine("Current launch doesn't exist.");
                return;
            }

            ICancelableTask<GgpGrpc.Models.GameLaunch> waitForDeletedLaunchTask =
                _cancelableTaskFactory.Create(TaskMessages.StoppingGame,
                                              async task =>
                                                  await DeleteLaunchAsync(_launchName, task));

            // TODO: use RunAndRecord to collect metrics.
            waitForDeletedLaunchTask.Run();
            GgpGrpc.Models.GameLaunch gameLaunch = waitForDeletedLaunchTask.Result;

            if (gameLaunch == null ||
                gameLaunch.GameLaunchState == GameLaunchState.GameLaunchEnded &&
                gameLaunch.GameLaunchEnded?.EndReason == EndReason.GameExitedWithSuccessfulCode)
            {
                return;
            }

            string message;

            if (gameLaunch.GameLaunchState != GameLaunchState.GameLaunchEnded)
            {
                Trace.WriteLine($"Couldn't delete the launch '{_launchName}'." +
                                $"Current launch state is: '{gameLaunch.GameLaunchState}'");
                message = ErrorStrings.GameNotStopped;
            }
            else
            {
                Trace.WriteLine(
                    $"Something went wrong while deleting the launch '{_launchName}'. " +
                    "Current end reason is: " +
                    $"{gameLaunch.GameLaunchEnded?.EndReason.ToString() ?? "empty"}', " +
                    $"Expected end reason is: '{EndReason.GameExitedWithSuccessfulCode}'");
                message = ErrorStrings.GameStoppedWithError +
                    GetEndReasonSuffix(gameLaunch.GameLaunchEnded);
            }

            _dialogUtil.ShowWarning(message);
        }

        public async Task<GgpGrpc.Models.GameLaunch> GetLaunchStateAsync() =>
            await _gameletClient.GetGameLaunchStateAsync(_launchName);

        // Some of the statuses might not be applicable for the dev flow.
        public string GetEndReason(GameLaunchEnded gameLaunchEnded) =>
            ErrorStrings.LaunchEndedCommonMessage + GetEndReasonSuffix(gameLaunchEnded);

        string GetEndReasonSuffix(GameLaunchEnded gameLaunchEnded)
        {
            switch (gameLaunchEnded.EndReason)
            {
                case EndReason.Unspecified:
                    return ErrorStrings.LaunchEndedUnspecified;
                case EndReason.ExitedByUser:
                    return ErrorStrings.LaunchEndedExitedByUser;
                case EndReason.InactivityTimeout:
                    return ErrorStrings.LaunchEndedInactivityTimeout;
                case EndReason.ClientNeverConnected:
                    return ErrorStrings.LaunchEndedClientNeverConnected;
                case EndReason.GameExitedWithSuccessfulCode:
                    return ErrorStrings.LaunchEndedGameExitedWithSuccessfulCode;
                case EndReason.GameExitedWithErrorCode:
                    return ErrorStrings.LaunchEndedGameExitedWithErrorCode;
                case EndReason.GameShutdownBySystem:
                    return ErrorStrings.LaunchEndedGameShutdownBySystem;
                case EndReason.UnexpectedGameShutdownBySystem:
                    return ErrorStrings.LaunchEndedUnexpectedGameShutdownBySystem;
                case EndReason.GameBinaryNotFound:
                    return ErrorStrings.LaunchEndedGameBinaryNotFound;
                case EndReason.QueueAbandonedByUser:
                    return ErrorStrings.LaunchEndedQueueAbandonedByUser;
                case EndReason.QueueReadyTimeout:
                    return ErrorStrings.LaunchEndedQueueReadyTimeout;
                default:
                    throw new ArgumentOutOfRangeException(nameof(gameLaunchEnded.EndReason),
                                                          gameLaunchEnded.EndReason,
                                                          "Unexpected end reason received.");
            }
        }

        public async Task<GgpGrpc.Models.GameLaunch> DeleteLaunchAsync(
            string gameLaunchName, ICancelable task)
        {
            GgpGrpc.Models.GameLaunch launch;

            try
            {
                launch = await _gameletClient.DeleteGameLaunchAsync(gameLaunchName);
            }
            catch (CloudException e) when ((e.InnerException as RpcException)?.StatusCode ==
                StatusCode.NotFound)
            {
                // There is no launch with the specified name.
                return null;
            }

            while (!task.IsCanceled && launch.GameLaunchState != GameLaunchState.GameLaunchEnded)
            {
                launch = await _gameletClient.GetGameLaunchStateAsync(gameLaunchName);
                await Task.Delay(500);
            }

            return launch;
        }

        // Polling statuses until we see RunningGame or GameLaunchEnded. IncompleteLaunch,
        // ReadyToPlay and DelayedLaunch are transitioning states.
        async Task<LaunchStatus> PollForLaunchStatusAsync(ICancelable task)
        {
            while (!task.IsCanceled)
            {
                GgpGrpc.Models.GameLaunch launch = await GetLaunchStateAsync();

                if (launch.GameLaunchState == GameLaunchState.RunningGame)
                {
                    return LaunchStatus.Success();
                }

                if (launch.GameLaunchState == GameLaunchState.GameLaunchEnded)
                {
                    string error = GetEndReason(launch.GameLaunchEnded);
                    return LaunchStatus.Failed(error);
                }

                await Task.Delay(500);
            }

            return LaunchStatus.Canceled();
        }
    }
}