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
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.GameLaunch
{
    public interface IGameLaunchManager
    {
        bool LaunchGameApiEnabled { get; }
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
        /// <param name="action">Ongoing action</param>
        /// <returns><see cref="DeleteLaunchResult"/></returns>
        Task<DeleteLaunchResult> DeleteLaunchAsync(string gameLaunchName, ICancelable task,
                                                   IAction action);

        /// <summary>
        /// Polls the backend for a current launch.
        /// </summary>
        /// <param name="testAccount">Test account.</param>
        /// <param name="action">The ongoing action.</param>
        /// <returns>Current game launch, if exists, otherwise null.</returns>
        Task<GgpGrpc.Models.GameLaunch> GetCurrentGameLaunchAsync(string testAccount,
                                                                  IAction action);

        /// <summary>
        /// Polls the backend for the current launch state until it is
        /// ended ot until polling is timed out.
        /// </summary>
        /// <param name="gameLaunchName">Game launch name.</param>
        /// <param name="task">Cancelable token.</param>
        /// <param name="action">Ongoing action.</param>
        /// <param name="timeoutMs">Number of milliseconds to wait.</param>
        /// <returns><see cref="DeleteLaunchResult"/></returns>
        Task<DeleteLaunchResult> WaitUntilGameLaunchEndedAsync(
            string gameLaunchName, ICancelable task, IAction action, int? timeoutMs = null);
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
        readonly ActionRecorder _actionRecorder;

        public GameLaunchManager(IGameletClient gameletClient, SdkConfig.Factory sdkConfigFactory,
                                 CancelableTask.Factory cancelableTaskFactory, YetiVSIService vsiService,
                                 JoinableTaskContext taskContext, ActionRecorder actionRecorder)
        {
            _gameletClient = gameletClient;
            _cancelableTaskFactory = cancelableTaskFactory;
            _vsiService = vsiService;
            _taskContext = taskContext;
            _actionRecorder = actionRecorder;
            _launchGameParamsConverter =
                new LaunchGameParamsConverter(sdkConfigFactory, new QueryParametersParser());
            _dialogUtil = new DialogUtil();
        }

        public bool LaunchGameApiEnabled =>
            _vsiService.Options.LaunchGameApiFlow == LaunchGameApiFlow.ENABLED;

        public async Task<DeleteLaunchResult> DeleteLaunchAsync(
            string gameLaunchName, ICancelable task, IAction action)
        {
            try
            {
                await _gameletClient.DeleteGameLaunchAsync(gameLaunchName, action);
            }
            catch (CloudException e) when ((e.InnerException as RpcException)?.StatusCode ==
                StatusCode.NotFound)
            {
                // There is no launch with the specified name.
                return DeleteLaunchResult.Success(null);
            }

            return await WaitUntilGameLaunchEndedAsync(gameLaunchName, task, action);
        }

        public async Task<DeleteLaunchResult> WaitUntilGameLaunchEndedAsync(
            string gameLaunchName, ICancelable task, IAction action, int? timeoutMs = null)
        {
            GgpGrpc.Models.GameLaunch launch =
                await _gameletClient.GetGameLaunchStateAsync(gameLaunchName, action);
            int maxPollCount = (timeoutMs??PollingTimeoutMs) / PollDelayMs;
            int currentPollCount = 0;
            while (launch.GameLaunchState != GameLaunchState.GameLaunchEnded &&
                ++currentPollCount <= maxPollCount)
            {
                task.ThrowIfCancellationRequested();
                await Task.Delay(PollDelayMs);
                launch = await _gameletClient.GetGameLaunchStateAsync(gameLaunchName, action);
            }

            return new DeleteLaunchResult(
                launch, launch.GameLaunchState == GameLaunchState.GameLaunchEnded);
        }

        public async Task<GgpGrpc.Models.GameLaunch> GetCurrentGameLaunchAsync(string testAccount,
            IAction action)
        {
            GgpGrpc.Models.GameLaunch currentGameLaunch;
            try
            {
                currentGameLaunch =
                    await _gameletClient.GetGameLaunchStateAsync(
                        _launchGameParamsConverter.FullGameLaunchName(null, testAccount), action);
            }
            catch (CloudException e) when ((e.InnerException as RpcException)?.StatusCode ==
                StatusCode.NotFound)
            {
                // There is no current launch on the gamelet.
                return null;
            }

            return currentGameLaunch;
        }

        async Task<string> CheckSdkCompatibilityAsync(string gameletName, string sdkVersion,
                                                      IAction action)
        {
            GameletSdkCompatibility sdkCompatibility;
            try
            {
                sdkCompatibility =
                    await _gameletClient.CheckSdkCompatibilityAsync(
                        gameletName, sdkVersion, action);
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

        public async Task<CreateLaunchResult> CreateLaunchAsync(
            ChromeTestClientLauncher.Params launchParams, ICancelable cancelable,
            IAction action)
        {
            Task<string> sdkCompatibilityTask = CheckSdkCompatibilityAsync(
                launchParams.GameletName, launchParams.SdkVersion, action);

            LaunchGameRequest launchRequest = null;
            Task<ConfigStatus> parsingTask =
                Task.Run(() => _launchGameParamsConverter.ToLaunchGameRequest(
                             launchParams, out launchRequest));

            cancelable.ThrowIfCancellationRequested();

            ConfigStatus parsingState = await parsingTask;
            if (parsingState.IsErrorLevel)
            {
                // Critical error occurred while parsing the configuration.
                // Launch can not proceed.
                throw new ConfigurationException(parsingState.ErrorMessage);
            }
            parsingState.CompressMessages();
            cancelable.ThrowIfCancellationRequested();
            string sdkCompatibilityErrorMessage = await sdkCompatibilityTask;
            if (!string.IsNullOrEmpty(sdkCompatibilityErrorMessage))
            {
                parsingState =
                    parsingState.Merge(ConfigStatus.WarningStatus(sdkCompatibilityErrorMessage));
            }

            cancelable.ThrowIfCancellationRequested();

            var devEvent = new DeveloperLogEvent
            {
                GameLaunchData = new GameLaunchData
                    { RequestId = launchRequest.RequestId }
            };
            // Updating the event to record the RequestId in case LaunchGameAsync throws exception.
            action.UpdateEvent(devEvent);
            LaunchGameResponse response =
                await _gameletClient.LaunchGameAsync(launchRequest, action);

            var vsiLaunch = new VsiGameLaunch(response.GameLaunchName, response.RequestId,
                                              _gameletClient, _cancelableTaskFactory, this,
                                              _actionRecorder, _taskContext);
            devEvent.GameLaunchData.LaunchId = vsiLaunch.LaunchId;
            action.UpdateEvent(devEvent);
            return new CreateLaunchResult(vsiLaunch, parsingState);
        }

        public IVsiGameLaunch CreateLaunch(ChromeTestClientLauncher.Params launchParams)
        {
            IAction action = _actionRecorder.CreateToolAction(ActionType.GameLaunchCreate);
            CreateLaunchResult launchRes = null;
            ICancelableTask launchTask = _cancelableTaskFactory.Create(
                TaskMessages.LaunchingGame,
                async task => { launchRes = await CreateLaunchAsync(launchParams, task, action); });
            try
            {
                if (!launchTask.RunAndRecord(action))
                {
                    Trace.WriteLine("Launching a game has been canceled by user.");
                    return null;
                }
            }
            catch (ConfigurationException e)
            {
                _dialogUtil.ShowError(e.Message);
                return null;
            }
            catch (CloudException)
            {
                _dialogUtil.ShowError(ErrorStrings.LaunchEndedCommonMessage + " " +
                                      ErrorStrings.SeeLogs);
                return null;
            }

            foreach (string warningMessage in launchRes.Status.WarningMessages)
            {
                _dialogUtil.ShowWarning(warningMessage);
            }

            return launchRes.GameLaunch;
        }
    }
}