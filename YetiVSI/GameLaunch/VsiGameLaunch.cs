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

using System.Diagnostics;
using System.Threading.Tasks;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using YetiCommon;

namespace YetiVSI.GameLaunch
{
    /// <summary>
    /// Represents a launch created by the current debug session.
    /// </summary>
    public interface IVsiGameLaunch
    {
        /// <summary>
        /// The name of the launch created by the current debug session.
        /// </summary>
        string LaunchName { get; }
        /// <summary>
        /// Constructs a URL withe the launch name and opens a Chrome tab with it.
        /// </summary>
        /// <param name="chromeLauncher">Chrome launcher.</param>
        /// <param name="workingDirectory">The working directory.</param>
        void LaunchInChrome(YetiCommon.ChromeClientLauncher chromeLauncher,
                            string workingDirectory);
        /// <summary>
        /// Retrieves the launch status form the backend.
        /// </summary>
        /// <returns></returns>
        Task<GgpGrpc.Models.GameLaunch> GetLaunchStateAsync();
        /// <summary>
        /// Polls for the launch status until either the game is running, the game has been ended
        /// or polling timed out. Shows a progress dialog during that.
        /// If the final status is not running game, shows a corresponding error message.
        /// </summary>
        /// <returns></returns>
        bool WaitUntilGameLaunched();
        /// <summary>
        /// Stops the game, which is launched by the current debug session.
        /// </summary>
        Task StopGameAsync();
    }

    public class VsiGameLaunch : IVsiGameLaunch
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

        readonly IGameletClient _gameletClient;
        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly DialogUtil _dialogUtil;
        readonly IGameLaunchManager _gameLaunchManager;

        public VsiGameLaunch(string launchName, IGameletClient gameletClient,
                             CancelableTask.Factory cancelableTaskFactory,
                             IGameLaunchManager gameLaunchManager)
        {
            LaunchName = launchName;
            _gameletClient = gameletClient;
            _cancelableTaskFactory = cancelableTaskFactory;
            _gameLaunchManager = gameLaunchManager;
            _dialogUtil = new DialogUtil();
        }

        public string LaunchName { get; }

        public void LaunchInChrome(YetiCommon.ChromeClientLauncher chromeLauncher,
                                   string workingDirectory)
        {
            string launchUrl = chromeLauncher.BuildLaunchUrlWithLaunchName(LaunchName);
            chromeLauncher.StartChrome(launchUrl, workingDirectory);
        }

        public async Task<GgpGrpc.Models.GameLaunch> GetLaunchStateAsync() =>
            await _gameletClient.GetGameLaunchStateAsync(LaunchName);

        public bool WaitUntilGameLaunched()
        {
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

        public async Task StopGameAsync()
        {
            GgpGrpc.Models.GameLaunch gameLaunch =
                    await _gameLaunchManager.DeleteLaunchAsync(LaunchName, new NothingToCancel());

            if (gameLaunch == null ||
                gameLaunch.GameLaunchState == GameLaunchState.GameLaunchEnded &&
                gameLaunch.GameLaunchEnded?.EndReason == EndReason.GameExitedWithSuccessfulCode)
            {
                return;
            }

            if (gameLaunch.GameLaunchState != GameLaunchState.GameLaunchEnded)
            {
                Trace.WriteLine($"Couldn't delete the launch '{LaunchName}'. " +
                                $"Current launch state is: '{gameLaunch.GameLaunchState}'");
            }
            else
            {
                Trace.WriteLine($"Something went wrong while deleting the launch '{LaunchName}'. " +
                            "Current end reason is: " +
                            $"{gameLaunch.GameLaunchEnded?.EndReason.ToString() ?? "empty"}', " +
                            $"Expected end reason is: '{EndReason.GameExitedWithSuccessfulCode}'");
            }
        }

        // Polling statuses until we see RunningGame or GameLaunchEnded. IncompleteLaunch,
        // ReadyToPlay and DelayedLaunch are transitioning states.
        async Task<LaunchStatus> PollForLaunchStatusAsync(ICancelable task)
        {
            int maxPollCount = GameLaunchManager.PollingTimeoutMs / GameLaunchManager.PollDelayMs;
            int currentPollCount = 0;
            while (!task.IsCanceled && ++currentPollCount <= maxPollCount)
            {
                GgpGrpc.Models.GameLaunch launch = await GetLaunchStateAsync();

                if (launch.GameLaunchState == GameLaunchState.RunningGame)
                {
                    return LaunchStatus.Success();
                }

                if (launch.GameLaunchState == GameLaunchState.GameLaunchEnded)
                {
                    string error = LaunchUtils.GetEndReason(launch.GameLaunchEnded);
                    return LaunchStatus.Failed(error);
                }

                await Task.Delay(GameLaunchManager.PollDelayMs);
            }

            if (currentPollCount > maxPollCount)
            {
                return LaunchStatus.Failed(ErrorStrings.LaunchEndedTimeout);
            }

            return LaunchStatus.Canceled();
        }
    }
}
