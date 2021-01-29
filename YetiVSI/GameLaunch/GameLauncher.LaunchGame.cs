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

using System.Threading.Tasks;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Grpc.Core;
using YetiCommon;
using YetiCommon.Cloud;
using YetiVSI.DebugEngine;

namespace YetiVSI.GameLaunch
{
    public interface IGameLauncher
    {
        bool LaunchGameApiEnabled { get; }
        Task<string> CreateLaunchAsync(YetiCommon.ChromeClientLauncher.Params launchParams);

        Task<bool> LaunchGameAsync(YetiCommon.ChromeClientLauncher chromeLauncher,
                                   string workingDirectory);

        Task<GgpGrpc.Models.GameLaunch> GetLaunchStateAsync();
        bool WaitUntilGameLaunched();
        string GetEndReason(GameLaunchEnded gameLaunchEnded);
        void StopGame();

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

    public partial class GameLauncher : IGameLauncher
    {
        readonly IGameletClient _gameletClient;
        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly YetiVSIService _vsiService;
        readonly LaunchGameParamsConverter _launchGameParamsConverter;
        readonly DialogUtil _dialogUtil;

        string _launchName;

        public GameLauncher(IGameletClient gameletClient, SdkConfig.Factory sdkConfigFactory,
                            CancelableTask.Factory cancelableTaskFactory, YetiVSIService vsiService)
        {
            _gameletClient = gameletClient;
            _cancelableTaskFactory = cancelableTaskFactory;
            _vsiService = vsiService;
            _launchGameParamsConverter =
                new LaunchGameParamsConverter(sdkConfigFactory, new QueryParametersParser());
            _dialogUtil = new DialogUtil();
        }

        public bool LaunchGameApiEnabled =>
            _vsiService.Options.LaunchGameApiFlow == LaunchGameApiFlow.ENABLED;

        bool CurrentLaunchExists => !string.IsNullOrWhiteSpace(_launchName);

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
            GameletSdkCompatibility sdkCompatibility =
                await _gameletClient.CheckSdkCompatibilityAsync(gameletName, sdkVersion);
            return sdkCompatibility.CompatibilityResult == GameletSdkCompatibilityResult.Compatible
                ? null
                : sdkCompatibility.Message;
        }

        public async Task<string> CreateLaunchAsync(
            YetiCommon.ChromeClientLauncher.Params launchParams)
        {
            Task<string> sdkCompatibilityTask = CheckSdkCompatibilityAsync(
                launchParams.GameletName, launchParams.SdkVersion);

            LaunchGameRequest launchRequest = null;
            Task<ConfigStatus> parsingTask =
                Task.Run(() => _launchGameParamsConverter.ToLaunchGameRequest(
                             launchParams, out launchRequest));

            ConfigStatus parsingState = await parsingTask;
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

            string sdkCompatibilityErrorMessage = await sdkCompatibilityTask;
            if (!string.IsNullOrEmpty(sdkCompatibilityErrorMessage))
            {
                // TODO: record actions.
                // SDK versions are not compatible, show a warning message.
                _dialogUtil.ShowWarning(sdkCompatibilityErrorMessage);
            }

            LaunchGameResponse response = await _gameletClient.LaunchGameAsync(launchRequest);
            return response.GameLaunchName;
        }

        public async Task<bool> LaunchGameAsync(YetiCommon.ChromeClientLauncher chromeLauncher,
                                                string workingDirectory)
        {
            // TODO: Show a progressbar of what's currently happening.
            string launchName = await CreateLaunchAsync(chromeLauncher.LaunchParams);
            if (string.IsNullOrEmpty(launchName))
            {
                return false;
            }

            // TODO: refactor GameLauncher to not store launchName.
            _launchName = launchName;
            string launchUrl = chromeLauncher.BuildLaunchUrlWithLaunchName(_launchName);
            chromeLauncher.StartChrome(launchUrl, workingDirectory);
            return true;
        }
    }
}