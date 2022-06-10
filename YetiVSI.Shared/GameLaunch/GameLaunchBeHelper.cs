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
using YetiCommon.Cloud;
using YetiVSI.Metrics;

namespace YetiVSI.GameLaunch
{
    /// <summary>
    /// Use this class to make some manipulations with backend Launches.
    /// </summary>
    public interface IGameLaunchBeHelper
    {
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
        /// <returns><see cref="DeleteLaunchResult"/></returns>
        Task<DeleteLaunchResult> WaitUntilGameLaunchEndedAsync(
            string gameLaunchName, ICancelable task, IAction action);
    }

    public class GameLaunchBeHelper : IGameLaunchBeHelper
    {
        readonly IGameletClient _gameletClient;
        readonly ILaunchGameParamsConverter _launchGameParamsConverter;
        readonly int _pollingTimeoutMs;
        readonly int _pollDelayMs;

        public GameLaunchBeHelper(IGameletClient gameletClient,
                                 ILaunchGameParamsConverter launchGameParamsConverter,
                                 int pollingTimeoutMs = 120000, int pollDelayMs = 500)
        {
            _gameletClient = gameletClient;
            _launchGameParamsConverter = launchGameParamsConverter;
            _pollingTimeoutMs = pollingTimeoutMs;
            _pollDelayMs = pollDelayMs;
        }

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
            string gameLaunchName, ICancelable task, IAction action)
        {
            GgpGrpc.Models.GameLaunch launch =
                await _gameletClient.GetGameLaunchStateAsync(gameLaunchName, action);
            int maxPollCount = _pollingTimeoutMs / _pollDelayMs;
            int currentPollCount = 0;
            while (launch.GameLaunchState != GameLaunchState.GameLaunchEnded &&
                ++currentPollCount <= maxPollCount)
            {
                task.ThrowIfCancellationRequested();
                await Task.Delay(_pollDelayMs);
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
                // There is no current launch for the specified account.
                return null;
            }

            return currentGameLaunch;
        }
    }
}