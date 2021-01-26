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

using System.Threading.Tasks;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Grpc.Core;
using Microsoft.VisualStudio.PlatformUI;
using YetiCommon;
using YetiCommon.Cloud;

namespace YetiVSI.GameLaunch
{
    public interface IGameLauncher
    {
        bool LaunchGameApiEnabled { get; }
        bool CurrentLaunchExists { get; }

        Task<bool> LaunchGameAsync(YetiCommon.ChromeClientLauncher chromeLauncher,
                                   string workingDirectory);

        bool GetLaunchState();
        Task StopGameAsync();
        Task<bool> DeleteLaunchAsync(string gameLaunchName, ICancelable task);
        Task<GgpGrpc.Models.GameLaunch> GetCurrentGameLaunchAsync(string testAccount);
    }

    public partial class GameLauncher : IGameLauncher
    {
        readonly IGameletClient _gameletClient;
        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly LaunchGameParamsConverter _launchGameParamsConverter;

        string _launchName;

        public GameLauncher(IGameletClient gameletClient, SdkConfig.Factory sdkConfigFactory,
                            CancelableTask.Factory cancelableTaskFactory, bool launchGameApiEnabled)
        {
            _gameletClient = gameletClient;
            _cancelableTaskFactory = cancelableTaskFactory;
            _launchGameParamsConverter =
                new LaunchGameParamsConverter(sdkConfigFactory, new QueryParametersParser());
            LaunchGameApiEnabled = launchGameApiEnabled;
        }

        public bool LaunchGameApiEnabled { get; }

        public bool CurrentLaunchExists => !string.IsNullOrWhiteSpace(_launchName);

        public async Task<GgpGrpc.Models.GameLaunch> GetCurrentGameLaunchAsync(
            string testAccount)
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

        public async Task<bool> LaunchGameAsync(YetiCommon.ChromeClientLauncher chromeLauncher,
                                                string workingDirectory)
        {
            // TODO: Show a progressbar of what's currently happening.
            Task<string> sdkCompatibilityTask = CheckSdkCompatibilityAsync(
                chromeLauncher.LaunchParams.GameletName, chromeLauncher.LaunchParams.SdkVersion);

            LaunchGameRequest launchRequest = null;
            Task<ConfigStatus> parsingTask =
                Task.Run(() => _launchGameParamsConverter.ToLaunchGameRequest(
                             chromeLauncher.LaunchParams, out launchRequest));

            ConfigStatus parsingState = await parsingTask;
            if (parsingState.IsErrorLevel)
            {
                // Critical error occurred while parsing the configuration.
                // Launch can not proceed.
                MessageDialog.Show(ErrorStrings.Error, parsingState.ErrorMessage,
                                   MessageDialogCommandSet.Ok);
                return false;
            }

            if (parsingState.IsWarningLevel)
            {
                // TODO: record actions.
                MessageDialog.Show(ErrorStrings.Warning, parsingState.WarningMessage,
                                   MessageDialogCommandSet.Ok);
            }

            string sdkCompatibilityErrorMessage = await sdkCompatibilityTask;
            if (!string.IsNullOrEmpty(sdkCompatibilityErrorMessage))
            {
                // TODO: record actions.
                // SDK versions are not compatible, show a warning message.
                MessageDialog.Show(ErrorStrings.Warning, sdkCompatibilityErrorMessage,
                                   MessageDialogCommandSet.Ok);
            }

            LaunchGameResponse response = await _gameletClient.LaunchGameAsync(launchRequest);
            _launchName = response.GameLaunchName;

            string launchUrl = chromeLauncher.BuildLaunchUrlWithLaunchName(_launchName);
            chromeLauncher.StartChrome(launchUrl, workingDirectory);
            return true;
        }
    }
}