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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using YetiCommon;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;
using YetiVSI.ProjectSystem.Abstractions;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.Test.GameLaunch
{
    [TestFixture]
    public class VsiGameLaunchTests
    {
        const string _launchId = "game-launch-id";
        const string _appId = "app-id";

        static readonly string _launchName = "organizations/org-id/projects/proj-id/testAccounts/" +
            $"test-account-id/gameLaunches/{_launchId}";

        VsiGameLaunch _target;

        IGameletClient _gameletClient;
        CancelableTask.Factory _cancelableTaskFactory;
        IGameLaunchBeHelper _gameLaunchBeHelper;
        IMetrics _metrics;
        ActionRecorder _actionRecorder;
        IDialogUtil _dialogUtil;
        IChromeClientsLauncher _launcher;
        LaunchParams _params;

        [SetUp]
        public void Setup()
        {
            _gameletClient = Substitute.For<IGameletClient>();
            _cancelableTaskFactory = Substitute.For<CancelableTask.Factory>();
            _gameLaunchBeHelper = Substitute.For<IGameLaunchBeHelper>();
            _metrics = Substitute.For<IMetrics>();
            _actionRecorder = Substitute.For<ActionRecorder>(_metrics);
            _dialogUtil = Substitute.For<IDialogUtil>();
            _launcher = Substitute.For<IChromeClientsLauncher>();
            _params = new LaunchParams();
            _launcher.LaunchParams.Returns(_params);
        }

        [Test]
        public void IdentifiersTest()
        {
            _target = GetGameLaunch(false, false);
            Assert.That(_target.LaunchName, Is.EqualTo(_launchName));
            Assert.That(_target.LaunchId, Is.EqualTo(_launchId));
        }

        [Test]
        public async Task GetLaunchStateAsyncTestAsync()
        {
            _target = GetGameLaunch(false, false);
            var action = Substitute.For<IAction>();
            var gameLaunch = GetGameLaunch();
            _gameletClient.GetGameLaunchStateAsync(_launchName, action)
                .Returns(Task.FromResult(gameLaunch));

            GgpGrpc.Models.GameLaunch result = await _target.GetLaunchStateAsync(action);

            Assert.That(result, Is.EqualTo(gameLaunch));
        }

        [TestCase(EndReason.GameExitedWithSuccessfulCode, false,
                  TestName = "GameExitedWithSuccessfulCode")]
        [TestCase(EndReason.GameExitedWithErrorCode, true, TestName = "GameExitedWithErrorCode")]
        [TestCase(EndReason.ExitedByUser, true, TestName = "GameExitedWithErrorCode")]
        [TestCase(EndReason.GameShutdownBySystem, true, TestName = "GameShutdownBySystem")]
        [TestCase(null, false, TestName = "GameNotEnded")]
        public async Task WaitForGameLaunchEndedAndRecordTestAsync(EndReason? reason, bool throws)
        {
            _target = GetGameLaunch(false, false);
            var action = Substitute.For<IAction>();
            DeveloperLogEvent devEvent = SetupUpdateEvent(action);
            action.RecordAsync(Arg.Any<Task>()).Returns(callInfo => callInfo.Arg<Task>());
            _actionRecorder.CreateToolAction(ActionType.GameLaunchWaitForEnd).Returns(action);
            var endResult = new DeleteLaunchResult(
                GetGameLaunch(GameLaunchState.GameLaunchEnded, reason), true);
            _gameLaunchBeHelper
                .WaitUntilGameLaunchEndedAsync(_launchName, Arg.Any<ICancelable>(), action)
                .Returns(Task.FromResult(endResult));

            if (throws)
            {
                Assert.ThrowsAsync<GameLaunchFailError>(
                    async () => await _target.WaitForGameLaunchEndedAndRecordAsync());
            }
            else
            {
                await _target.WaitForGameLaunchEndedAndRecordAsync();
            }

            Assert.That(devEvent.GameLaunchData.LaunchId, Is.EqualTo(_launchId));
            Assert.That(devEvent.GameLaunchData.EndReason, Is.EqualTo((int?) reason));
            await action.Received(1).RecordAsync(Arg.Any<Task>());
        }

        [TestCase(new[] { GameLaunchState.IncompleteLaunch, GameLaunchState.RunningGame },
                  new[] { 4, 1 }, true, TestName = "RunningAfterIncomplete")]
        [TestCase(new[] { GameLaunchState.ReadyToPlay, GameLaunchState.RunningGame },
                  new[] { 3, 1 }, true, TestName = "RunningAfterLaunching")]
        [TestCase(new[] { GameLaunchState.IncompleteLaunch, GameLaunchState.GameLaunchEnded },
                  new[] { 3, 1 }, false, EndReason.GameShutdownBySystem,
                  TestName = "EndAfterIncomplete")]
        [TestCase(new[] { GameLaunchState.IncompleteLaunch, GameLaunchState.RunningGame },
                  new[] { 7, 1 }, false, TestName = "RunningTimeout")]
        [TestCase(new[] { GameLaunchState.IncompleteLaunch, GameLaunchState.RunningGame },
                  new[] { 7, 1 }, true, null, true, TestName = "NoTimeOutWithDeferred")]
        [TestCase(new[] { GameLaunchState.IncompleteLaunch, GameLaunchState.RunningGame },
                  new[] { 3, 1 }, true, null, true, "external",
                  TestName = "RunWithExternalAccount")]
        public void WaitUntilGameLaunchedTest(GameLaunchState[] launchStates, int[] stateRepeat,
                                              bool launchResult, EndReason? endReason = null,
                                              bool isDevResumeOfferEnabled = false,
                                              string externalId = null)
        {
            _target = GetGameLaunch(isDevResumeOfferEnabled, externalId != null);
            Func<ICancelable, Task> currentTask = null;
            var action = Substitute.For<IAction>();
            DeveloperLogEvent devEvent = SetupUpdateEvent(action);
            _actionRecorder.CreateToolAction(ActionType.GameLaunchWaitForStart).Returns(action);
            var cancelable = Substitute.For<ICancelableTask>();
            action.Record(Arg.Any<Func<bool>>()).Returns(callInfo =>
            {
                new JoinableTaskFactory(new JoinableTaskContext()).Run(
                    () => currentTask(new NothingToCancel()));
                return true;
            });
            if (isDevResumeOfferEnabled)
            {
                string message = externalId == null
                    ? TaskMessages.LaunchingDeferredGame
                    : TaskMessages.LaunchingDeferredGameWithExternalId(_appId);
                _cancelableTaskFactory.Create(
                    message, TaskMessages.LaunchingDeferredGameTitle,
                    Arg.Any<Func<ICancelable, Task>>()).Returns(callInfo =>
                {
                    currentTask = callInfo.Arg<Func<ICancelable, Task>>();
                    return cancelable;
                });
            }
            else
            {
                _cancelableTaskFactory.Create(
                TaskMessages.LaunchingGame, Arg.Any<Func<ICancelable, Task>>()).Returns(callInfo =>
                {
                    currentTask = callInfo.Arg<Func<ICancelable, Task>>();
                    return cancelable;
                });
            }
            
            List<GameLaunchState> statusSequence = launchStates
                .Select((state, i) => Enumerable.Repeat(state, stateRepeat[i]))
                .SelectMany(states => states).ToList();
            Task<GgpGrpc.Models.GameLaunch>[] launches = statusSequence.Select(
                (state, i) => Task.FromResult(GetGameLaunch(state, i == statusSequence.Count - 1
                                                                ? endReason
                                                                : null))).ToArray();
            _gameletClient.GetGameLaunchStateAsync(_launchName, action)
                .Returns(launches[0], launches.Skip(1).ToArray());

            bool launched = _target.WaitUntilGameLaunched();

            Assert.That(launched, Is.EqualTo(launchResult));
            if (!launchResult)
            {
                _dialogUtil.Received(1).ShowError(Arg.Any<string>());
            }

            Assert.That(devEvent.GameLaunchData.LaunchId, Is.EqualTo(_launchId));
            Assert.That(devEvent.GameLaunchData.EndReason, Is.EqualTo((int?) endReason));
            action.Received(1).Record(Arg.Any<Func<bool>>());
        }

        GgpGrpc.Models.GameLaunch GetGameLaunch(GameLaunchState state = GameLaunchState.RunningGame,
                                                EndReason? endReason = null) =>
            new GgpGrpc.Models.GameLaunch
            {
                GameletName = "gamelet/123",
                Name = _launchName,
                GameLaunchState = state,
                GameLaunchEnded = endReason.HasValue ? new GameLaunchEnded(endReason.Value) : null
            };

        DeveloperLogEvent SetupUpdateEvent(IAction action)
        {
            var devEvent = new DeveloperLogEvent();
            action.When(a => a.UpdateEvent(Arg.Any<DeveloperLogEvent>())).Do(callInfo =>
            {
                var evt = callInfo.Arg<DeveloperLogEvent>();
                devEvent.MergeFrom(evt);
            });
            return devEvent;
        }

        VsiGameLaunch GetGameLaunch(bool isDeveloperResumeOfferEnabled, bool isExternalAccount) =>
            new VsiGameLaunch(_launchName, isDeveloperResumeOfferEnabled, isExternalAccount, _appId,
                              _gameletClient, _cancelableTaskFactory, _gameLaunchBeHelper,
                              _actionRecorder, _dialogUtil, pollingTimeoutMs: 500,
                              pollingTimeoutResumeOfferMs: 1000, pollDelayMs: 100);
    }
}
