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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Grpc.Core;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using YetiCommon.Cloud;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;

namespace YetiVSI.Test.GameLaunch
{
    [TestFixture]
    public class GameLaunchBeHelperTests
    {
        const string _gameLaunchName = "some/game/launch";
        const string _fullLaunchName = "full/game/launch/name";
        const string _testAccount = "test/account";

        GameLaunchBeHelper _target;

        IGameletClient _gameletClient;
        ILaunchGameParamsConverter _paramsConverter;
        ICancelable _cancelable;
        IAction _action;

        [SetUp]
        public void Setup()
        {
            _cancelable = Substitute.For<ICancelable>();
            _action = Substitute.For<IAction>();

            _gameletClient = Substitute.For<IGameletClient>();
            _paramsConverter = Substitute.For<ILaunchGameParamsConverter>();

            _target = new GameLaunchBeHelper(_gameletClient, _paramsConverter, 1000, 100);
        }

        [Test]
        public async Task DeleteLaunchNoLaunchAsync()
        {
            _gameletClient.DeleteGameLaunchAsync(_gameLaunchName, _action).Throws(
                new CloudException(
                    "error", new RpcException(new Status(StatusCode.NotFound, "not found"))));

            DeleteLaunchResult result =
                await _target.DeleteLaunchAsync(_gameLaunchName, _cancelable, _action);

            Assert.That(result.IsSuccessful, Is.EqualTo(true));
            Assert.That(result.DeletedLaunch, Is.Null);
            await AssertActionNotRecordedAsync();
        }

        [Test]
        public async Task DeleteLaunchSuccessAsync()
        {
            _gameletClient.DeleteGameLaunchAsync(_gameLaunchName, _action).Returns(
                GetLaunch(_gameLaunchName, GameLaunchState.RunningGame));
            GgpGrpc.Models.GameLaunch lastLaunch =
                GetLaunch(_gameLaunchName, GameLaunchState.GameLaunchEnded,
                          EndReason.GameExitedWithSuccessfulCode);
            _gameletClient.GetGameLaunchStateAsync(_gameLaunchName, _action).Returns(
                GetLaunch(_gameLaunchName, GameLaunchState.RunningGame),
                GetLaunch(_gameLaunchName, GameLaunchState.RunningGame), lastLaunch);

            DeleteLaunchResult result =
                await _target.DeleteLaunchAsync(_gameLaunchName, _cancelable, _action);

            Assert.That(result.IsSuccessful, Is.EqualTo(true));
            Assert.That(result.DeletedLaunch, Is.EqualTo(lastLaunch));
            await AssertActionNotRecordedAsync();
        }

        [Test]
        public async Task DeleteLaunchThrowsAsync()
        {
            var exception = new CloudException(
                "error", new RpcException(new Status(StatusCode.Internal, "error")));
            _gameletClient.DeleteGameLaunchAsync(_gameLaunchName, _action).Throws(exception);

            var resultException = Assert.ThrowsAsync<CloudException>(
                () => _target.DeleteLaunchAsync(_gameLaunchName, _cancelable, _action));

            Assert.That(resultException, Is.EqualTo(exception));
            await AssertActionNotRecordedAsync();
        }

        [Test]
        public async Task DeleteLaunchCanceledAsync()
        {
            _gameletClient.DeleteGameLaunchAsync(_gameLaunchName, _action).Returns(
                GetLaunch(_gameLaunchName, GameLaunchState.RunningGame));
            GgpGrpc.Models.GameLaunch lastLaunch =
                GetLaunch(_gameLaunchName, GameLaunchState.GameLaunchEnded,
                          EndReason.GameExitedWithSuccessfulCode);
            _gameletClient.GetGameLaunchStateAsync(_gameLaunchName, _action).Returns(
                GetLaunch(_gameLaunchName, GameLaunchState.RunningGame),
                GetLaunch(_gameLaunchName, GameLaunchState.RunningGame), lastLaunch);
            _cancelable.When(c => c.ThrowIfCancellationRequested())
                .Do(callInfo => throw new TaskAbortedException(new Exception()));

            Assert.ThrowsAsync<TaskAbortedException>(
                () => _target.DeleteLaunchAsync(_gameLaunchName, _cancelable, _action));
            await AssertActionNotRecordedAsync();
        }

        [TestCase(new[] { GameLaunchState.RunningGame, GameLaunchState.GameLaunchEnded },
                  new[] { 4, 1 }, true, TestName = "EndedAfterWait")]
        [TestCase(new[] { GameLaunchState.RunningGame, GameLaunchState.GameLaunchEnded },
                  new[] { 30, 1 }, false, TestName = "Timeout")]
        public async Task WaitUntilGameLaunchEndedAsync(GameLaunchState[] launchStates,
                                                        int[] stateRepeat, bool isSuccessful)
        {
            List<GameLaunchState> statusSequence = launchStates
                .Select((state, i) => Enumerable.Repeat(state, stateRepeat[i]))
                .SelectMany(states => states).ToList();
            Task<GgpGrpc.Models.GameLaunch>[] launches = statusSequence
                .Select((state, i) => Task.FromResult(GetLaunch(_gameLaunchName, state))).ToArray();
            _gameletClient.GetGameLaunchStateAsync(_gameLaunchName, _action)
                .Returns(launches[0], launches.Skip(1).ToArray());

            DeleteLaunchResult result =
                await _target.WaitUntilGameLaunchEndedAsync(_gameLaunchName, _cancelable, _action);
            Assert.That(result.IsSuccessful, Is.EqualTo(isSuccessful));
            Assert.That(result.DeletedLaunch.Name, Is.EqualTo(_gameLaunchName));
            await AssertActionNotRecordedAsync();
        }

        [Test]
        public async Task GetCurrentGameLaunchSuccessAsync()
        {
            _paramsConverter.FullGameLaunchName(null, _testAccount).Returns(_fullLaunchName);
            GgpGrpc.Models.GameLaunch launch =
                GetLaunch(_gameLaunchName, GameLaunchState.ReadyToPlay);
            _gameletClient.GetGameLaunchStateAsync(_fullLaunchName, _action).Returns(launch);

            GgpGrpc.Models.GameLaunch resultLaunch =
                await _target.GetCurrentGameLaunchAsync(_testAccount, _action);
            Assert.That(resultLaunch, Is.EqualTo(launch));
            await AssertActionNotRecordedAsync();
        }

        GgpGrpc.Models.GameLaunch GetLaunch(string name, GameLaunchState state,
                                            EndReason? endReason = null) =>
            new GgpGrpc.Models.GameLaunch
            {
                Name = name, GameLaunchState = state,
                GameLaunchEnded = endReason.HasValue ? new GameLaunchEnded(endReason.Value) : null
            };

        async Task AssertActionNotRecordedAsync()
        {
            _action.DidNotReceiveWithAnyArgs().Record(Arg.Any<Func<bool>>());
            await _action.DidNotReceiveWithAnyArgs().RecordAsync(Arg.Any<Task>());
        }
    }
}
