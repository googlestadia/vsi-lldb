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

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Grpc.Core;
using Microsoft.VisualStudio.Threading;
using YetiCommon;
using YetiCommon.Cloud;
using YetiVSI.DebugEngine;

namespace YetiVSI.GameLaunch
{
    public interface IGameLaunchManager
    {
        bool LaunchGameApiEnabled { get; }
        /// <summary>
        /// Requests the backend to create a game launch asynchronously.
        /// Shows warning and error messages if something goes wrong.
        /// </summary>
        /// <param name="launchParams">Launch parameters.</param>
        /// <param name="cancelable">ICancelable token.</param>
        /// <returns>Instance of the VsiGameLaunch if successful, otherwise null.</returns>
        Task<IVsiGameLaunch> CreateLaunchAsync(ChromeTestClientLauncher.Params launchParams,
                                               ICancelable cancelable);
        /// <summary>
        /// Requests the backend to create a game launch synchronously.
        /// Shows warning and error messages if something goes wrong.
        /// </summary>
        /// <param name="launchParams">Launch parameters.</param>
        /// <returns>Instance of the VsiGameLaunch if successful, otherwise null.</returns>
        IVsiGameLaunch CreateLaunch(ChromeTestClientLauncher.Params launchParams);
        /// <summary>
        /// Attempts to delete a launch by the gameLaunchName. Returns null when
        /// specified launch doesn't exists. Otherwise returns a GgpGrpc.Models.GameLaunch
        /// instance containing current GameLaunchState after delete attempt.
        /// </summary>
        /// <param name="gameLaunchName">Game launch name.</param>
        /// <param name="task">Cancelable token.</param>
        /// <returns>GgpGrpc.Models.GameLaunch.</returns>
        Task<GgpGrpc.Models.GameLaunch> DeleteLaunchAsync(string gameLaunchName, ICancelable task);

        /// <summary>
        /// Polls the backend for a current launch.
        /// </summary>
        /// <param name="testAccount">Test account.</param>
        /// <returns>Current game launch, if exists, otherwise null.</returns>
        Task<GgpGrpc.Models.GameLaunch> GetCurrentGameLaunchAsync(string testAccount);
    }

    public class GameLaunchManager : IGameLaunchManager
    {
        public const int PollingTimeoutMs = 120000;
        public const int PollDelayMs = 500;

        readonly IGameletClient _gameletClient;
        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly JoinableTaskContext _taskContext;
        readonly YetiVSIService _vsiService;
        readonly LaunchGameParamsConverter _launchGameParamsConverter;
        readonly DialogUtil _dialogUtil;

        public GameLaunchManager(IGameletClient gameletClient, SdkConfig.Factory sdkConfigFactory,
                            CancelableTask.Factory cancelableTaskFactory, YetiVSIService vsiService,
                            JoinableTaskContext taskContext)
        {
            _gameletClient = gameletClient;
            _cancelableTaskFactory = cancelableTaskFactory;
            _vsiService = vsiService;
            _taskContext = taskContext;
            _launchGameParamsConverter =
                new LaunchGameParamsConverter(sdkConfigFactory, new QueryParametersParser());
            _dialogUtil = new DialogUtil();
        }

        public bool LaunchGameApiEnabled =>
            _vsiService.Options.LaunchGameApiFlow == LaunchGameApiFlow.ENABLED;

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
            int maxPollCount = PollingTimeoutMs / PollDelayMs;
            int currentPollCount = 0;
            while (!task.IsCanceled && launch.GameLaunchState != GameLaunchState.GameLaunchEnded &&
                ++currentPollCount <= maxPollCount)
            {
                launch = await _gameletClient.GetGameLaunchStateAsync(gameLaunchName);
                await Task.Delay(PollDelayMs);
            }

            return launch;
        }

        /// <summary>
        /// Polls the backend for a current launch.
        /// </summary>
        /// <param name="testAccount">Test account.</param>
        /// <returns>Current game launch, if exists, otherwise null.</returns>
        public async Task<GgpGrpc.Models.GameLaunch> GetCurrentGameLaunchAsync(string testAccount)
        {
            GgpGrpc.Models.GameLaunch currentGameLaunch;
            try
            {
                currentGameLaunch =
                    await _gameletClient.GetGameLaunchStateAsync(
                        _launchGameParamsConverter.FullGameLaunchName(null, testAccount));
            }
            catch (CloudException e) when ((e.InnerException as RpcException)?.StatusCode ==
                StatusCode.NotFound)
            {
                // There is no current launch on the gamelet.
                return null;
            }

            return currentGameLaunch;
        }

        async Task<string> CheckSdkCompatibilityAsync(string gameletName, string sdkVersion)
        {
            GameletSdkCompatibility sdkCompatibility;
            try
            {
                sdkCompatibility =
                    await _gameletClient.CheckSdkCompatibilityAsync(gameletName, sdkVersion);
            }
            catch (Exception e)
            {
                Trace.WriteLine(
                    $"Exception happened while checking the SDK compatibility. {e.Message}");
                return ErrorStrings.ErrorWhileSdkCheck;
            }

            return sdkCompatibility.CompatibilityResult == GameletSdkCompatibilityResult.Compatible
                ? null
                : sdkCompatibility.Message;
        }

        public async Task<IVsiGameLaunch> CreateLaunchAsync(
            ChromeTestClientLauncher.Params launchParams, ICancelable cancelable)
        {
            Task<string> sdkCompatibilityTask = CheckSdkCompatibilityAsync(
                launchParams.GameletName, launchParams.SdkVersion);

            LaunchGameRequest launchRequest = null;
            Task<ConfigStatus> parsingTask =
                Task.Run(() => _launchGameParamsConverter.ToLaunchGameRequest(
                             launchParams, out launchRequest));
            if (cancelable.IsCanceled)
            {
                return null;
            }

            ConfigStatus parsingState = await parsingTask;
            await _taskContext.Factory.SwitchToMainThreadAsync();
            if (parsingState.IsErrorLevel)
            {
                // Critical error occurred while parsing the configuration.
                // Launch can not proceed.
                _dialogUtil.ShowError(parsingState.ErrorMessage);
                return null;
            }

            if (parsingState.IsWarningLevel)
            {
                // TODO: record actions.
                _dialogUtil.ShowWarning(parsingState.WarningMessage);
            }
            if (cancelable.IsCanceled)
            {
                return null;
            }

            string sdkCompatibilityErrorMessage = await sdkCompatibilityTask;
            await _taskContext.Factory.SwitchToMainThreadAsync();
            if (!string.IsNullOrEmpty(sdkCompatibilityErrorMessage))
            {
                // TODO: record actions.
                // SDK versions are not compatible, show a warning message.
                _dialogUtil.ShowWarning(sdkCompatibilityErrorMessage);
            }
            if (cancelable.IsCanceled)
            {
                return null;
            }

            LaunchGameResponse response;
            try
            {
                response = await _gameletClient.LaunchGameAsync(launchRequest);
            }
            catch (CloudException e)
            {
                await _taskContext.Factory.SwitchToMainThreadAsync();
                _dialogUtil.ShowError(ErrorStrings.LaunchEndedCommonMessage + " " + e.Message);
                return null;
            }

            var vsiLaunch = new VsiGameLaunch(response.GameLaunchName, _gameletClient,
                                              _cancelableTaskFactory, this);

            return vsiLaunch;
        }

        public IVsiGameLaunch CreateLaunch(ChromeTestClientLauncher.Params launchParams)
        {
            IVsiGameLaunch vsiLaunch = null;
            ICancelableTask launchTask = _cancelableTaskFactory.Create(
                TaskMessages.LaunchingGame,
                async task =>
                {
                    vsiLaunch = await CreateLaunchAsync(launchParams, task);
                });
            if (!launchTask.Run())
            {
                Trace.WriteLine("Launching a game has been canceled by user.");
                return null;
            }
            return vsiLaunch;
        }
    }
}