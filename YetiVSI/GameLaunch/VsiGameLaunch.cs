﻿// Copyright 2020 Google LLC
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

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using YetiCommon;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;

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
        /// The short launch Id.
        /// </summary>
        string LaunchId { get; }

        /// <summary>
        /// Constructs a URL withe the launch name and opens a Chrome tab with it.
        /// </summary>
        /// <param name="chromeTestClient">Chrome test client launcher.</param>
        /// <param name="workingDirectory">The working directory.</param>
        void LaunchInChrome(IChromeTestClientLauncher chromeTestClient, string workingDirectory);

        /// <summary>
        /// Retrieves the launch status form the backend.
        /// </summary>
        /// <returns></returns>
        Task<GgpGrpc.Models.GameLaunch> GetLaunchStateAsync(IAction action);

        /// <summary>
        /// Polls for the launch status until either the game is running, the game has been ended
        /// or polling timed out. Shows a progress dialog during that.
        /// If the final status is not running game, shows a corresponding error message.
        /// </summary>
        /// <returns></returns>
        bool WaitUntilGameLaunched();

        /// <summary>
        /// Polls the backend for the current launch state until it is
        /// ended ot until polling is timed out. Records the resulting state.
        /// </summary>
        /// <returns><see cref="DeleteLaunchResult"/></returns>
        Task<DeleteLaunchResult> WaitForGameLaunchEndedAndRecordAsync();
    }

    public interface IVsiGameLaunchFactory
    {
        IVsiGameLaunch Create(string launchName);

        IVsiGameLaunch Create(string launchName, int pollingTimeoutMs, int pollDelayMs);
    }

    public class VsiGameLaunchFactory : IVsiGameLaunchFactory
    {
        readonly IGameletClient _gameletClient;
        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly IDialogUtil _dialogUtil;
        readonly IGameLaunchBeHelper _gameLaunchBeHelper;
        readonly ActionRecorder _actionRecorder;

        public VsiGameLaunchFactory(IGameletClient gameletClient,
                                    CancelableTask.Factory cancelableTaskFactory,
                                    IGameLaunchBeHelper gameLaunchBeHelper,
                                    ActionRecorder actionRecorder, IDialogUtil dialogUtil)
        {
            _gameletClient = gameletClient;
            _cancelableTaskFactory = cancelableTaskFactory;
            _gameLaunchBeHelper = gameLaunchBeHelper;
            _actionRecorder = actionRecorder;
            _dialogUtil = dialogUtil;
        }

        public IVsiGameLaunch Create(string launchName) => new VsiGameLaunch(
            launchName, _gameletClient, _cancelableTaskFactory, _gameLaunchBeHelper,
            _actionRecorder, _dialogUtil);

        public IVsiGameLaunch Create(string launchName, int pollingTimeoutMs, int pollDelayMs) =>
            new VsiGameLaunch(launchName, _gameletClient, _cancelableTaskFactory,
                              _gameLaunchBeHelper, _actionRecorder, _dialogUtil, pollingTimeoutMs,
                              pollDelayMs);
    }

    public class VsiGameLaunch : IVsiGameLaunch
    {
        readonly IGameletClient _gameletClient;
        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly IDialogUtil _dialogUtil;
        readonly IGameLaunchBeHelper _gameLaunchBeHelper;
        readonly ActionRecorder _actionRecorder;
        readonly int _pollingTimeoutMs;
        readonly int _pollDelayMs;

        public VsiGameLaunch(string launchName, IGameletClient gameletClient,
                             CancelableTask.Factory cancelableTaskFactory,
                             IGameLaunchBeHelper gameLaunchBeHelper, ActionRecorder actionRecorder,
                             IDialogUtil dialogUtil, int pollingTimeoutMs = 120000,
                             int pollDelayMs = 500)
        {
            LaunchName = launchName;
            _gameletClient = gameletClient;
            _cancelableTaskFactory = cancelableTaskFactory;
            _gameLaunchBeHelper = gameLaunchBeHelper;
            _actionRecorder = actionRecorder;
            _dialogUtil = dialogUtil;
            _pollingTimeoutMs = pollingTimeoutMs;
            _pollDelayMs = pollDelayMs;
        }

        public string LaunchName { get; }

        public string LaunchId => LaunchName.Split('/').Last();

        public void LaunchInChrome(IChromeTestClientLauncher chromeTestClient,
                                   string workingDirectory)
        {
            string launchUrl = chromeTestClient.BuildLaunchUrlWithLaunchName(LaunchName);
            chromeTestClient.LaunchGame(launchUrl, workingDirectory);
        }

        public async Task<GgpGrpc.Models.GameLaunch> GetLaunchStateAsync(IAction action) =>
            await _gameletClient.GetGameLaunchStateAsync(LaunchName, action);

        public async Task<DeleteLaunchResult> WaitForGameLaunchEndedAndRecordAsync()
        {
            IAction action = _actionRecorder.CreateToolAction(ActionType.GameLaunchWaitForEnd);
            DeleteLaunchResult result = null;

            async Task WaitAndRecordTask()
            {
                result = await _gameLaunchBeHelper.WaitUntilGameLaunchEndedAsync(
                    LaunchName, new NothingToCancel(), action);
                EndReason? eReason = result.DeletedLaunch?.GameLaunchEnded?.EndReason;
                var endData = new DeveloperLogEvent
                {
                    GameLaunchData = new GameLaunchData
                        { LaunchId = LaunchId, EndReason = (int?) eReason }
                };
                action.UpdateEvent(endData);
                if (eReason.HasValue && eReason != EndReason.GameExitedWithSuccessfulCode)
                {
                    throw new GameLaunchFailError("Game stopped due to unexpected reason.");
                }
            }

            await action.RecordAsync(WaitAndRecordTask());
            return result;
        }

        public bool WaitUntilGameLaunched()
        {
            IAction action = _actionRecorder.CreateToolAction(ActionType.GameLaunchWaitForStart);
            ICancelableTask pollForLaunchStatusTask = _cancelableTaskFactory.Create(
                TaskMessages.LaunchingGame,
                async task => await PollForLaunchStatusAsync(task, action));
            try
            {
                if (!pollForLaunchStatusTask.RunAndRecord(action))
                {
                    Trace.WriteLine("Polling for the launch status has been canceled by user.");
                    return false;
                }
            }
            catch (Exception e) when (e is TimeoutException || e is GameLaunchFailError)
            {
                Trace.WriteLine(e.Message);
                _dialogUtil.ShowError(e.Message);
                return false;
            }

            return true;
        }

        // Polling statuses until we see RunningGame or GameLaunchEnded. IncompleteLaunch,
        // ReadyToPlay and DelayedLaunch are transitioning states.
        async Task PollForLaunchStatusAsync(ICancelable task, IAction action)
        {
            int maxPollCount = _pollingTimeoutMs / _pollDelayMs;
            int currentPollCount = 0;
            var devEvent = new DeveloperLogEvent
            {
                GameLaunchData = new GameLaunchData { LaunchId = LaunchId }
            };
            action.UpdateEvent(devEvent);
            while (++currentPollCount <= maxPollCount)
            {
                task.ThrowIfCancellationRequested();
                GgpGrpc.Models.GameLaunch launch = await GetLaunchStateAsync(action);

                if (launch.GameLaunchState == GameLaunchState.RunningGame)
                {
                    return;
                }

                if (launch.GameLaunchState == GameLaunchState.GameLaunchEnded)
                {
                    string error = LaunchUtils.GetEndReason(launch.GameLaunchEnded);
                    devEvent.GameLaunchData.EndReason = (int) launch.GameLaunchEnded.EndReason;
                    action.UpdateEvent(devEvent);
                    throw new GameLaunchFailError(error);
                }

                await Task.Delay(_pollDelayMs);
            }

            if (currentPollCount > maxPollCount)
            {
                throw new TimeoutException(ErrorStrings.LaunchEndedTimeout);
            }
        }
    }
}