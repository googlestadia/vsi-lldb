// Copyright 2021 Google LLC
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

﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using YetiCommon;
using YetiCommon.Cloud;
using YetiVSI.DebugEngine;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.GameLaunch
{
    /// <summary>
    /// Is responsible for creating an <see cref="IVsiGameLaunch"/>.
    /// </summary>
    public interface IGameLauncher
    {
        bool LaunchGameApiEnabled { get; }

        /// <summary>
        /// Requests the backend to create a game launch synchronously.
        /// Shows warning and error messages if something goes wrong.
        /// </summary>
        /// <param name="launchParams">Launch parameters.</param>
        /// <returns>Instance of the VsiGameLaunch if successful, otherwise null.</returns>
        IVsiGameLaunch CreateLaunch(ChromeLaunchParams launchParams);
    }

    public class GameLauncher : IGameLauncher
    {
        readonly IGameletClient _gameletClient;
        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly ILaunchGameParamsConverter _launchGameParamsConverter;
        readonly IDialogUtil _dialogUtil;
        readonly ActionRecorder _actionRecorder;
        readonly IVsiGameLaunchFactory _vsiLaunchFactory;
        readonly IYetiVSIService _vsiService;

        public GameLauncher(IGameletClient gameletClient, IYetiVSIService vsiService,
                            ILaunchGameParamsConverter launchGameParamsConverter,
                            CancelableTask.Factory cancelableTaskFactory,
                            ActionRecorder actionRecorder, IDialogUtil dialogUtil,
                            IVsiGameLaunchFactory vsiLaunchFactory)
        {
            _gameletClient = gameletClient;
            _vsiService = vsiService;
            _cancelableTaskFactory = cancelableTaskFactory;
            _actionRecorder = actionRecorder;
            _launchGameParamsConverter = launchGameParamsConverter;
            _dialogUtil = dialogUtil;
            _vsiLaunchFactory = vsiLaunchFactory;
        }

        public bool LaunchGameApiEnabled =>
            _vsiService.Options.LaunchGameApiFlow == LaunchGameApiFlow.ENABLED;

        public IVsiGameLaunch CreateLaunch(ChromeLaunchParams launchParams)
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

        async Task<CreateLaunchResult> CreateLaunchAsync(
            ChromeLaunchParams launchParams, ICancelable cancelable, IAction action)
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
                GameLaunchData = new GameLaunchData { RequestId = launchRequest.RequestId }
            };
            // Updating the event to record the RequestId in case LaunchGameAsync throws exception.
            action.UpdateEvent(devEvent);
            LaunchGameResponse response =
                await _gameletClient.LaunchGameAsync(launchRequest, action);

            IVsiGameLaunch vsiLaunch = _vsiLaunchFactory.Create(response.GameLaunchName);
            devEvent.GameLaunchData.LaunchId = vsiLaunch.LaunchId;
            action.UpdateEvent(devEvent);
            return new CreateLaunchResult(vsiLaunch, parsingState);
        }
    }
}
