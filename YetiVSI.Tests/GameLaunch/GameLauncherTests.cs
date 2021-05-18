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
using System.Linq;
using System.Threading.Tasks;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using YetiCommon;
using YetiCommon.Cloud;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.Test.GameLaunch
{
    [TestFixture]
    public class GameLauncherTests
    {
        const string _gameLaunchName = "test/game/launch-id";
        const string _gameLaunchId = "launch-id";
        const string _notCompatibleMessage = "Not compatible";
        const string _errorSdkCheckMessage = "Error on SDK check";
        const string _sdkVersion = "test.123.654";
        const string _gameletSdkVersion = "gamelet.test.765";
        const string _gameletName = "some/gamelet/name";

        readonly LaunchParams _launchParams = new LaunchParams
        {
            GameletName = _gameletName,
            SdkVersion = _sdkVersion,
            GameletSdkVersion = _gameletSdkVersion
        };

        GameLauncher _target;

        IGameletClient _gameletClient;
        ILaunchGameParamsConverter _paramsConverter;
        CancelableTask.Factory _cancelableTaskFactory;
        IYetiVSIService _yetiVsiService;
        IMetrics _metrics;
        ActionRecorder _actionRecorder;
        IDialogUtil _dialogUtil;
        IVsiGameLaunchFactory _vsiGameLaunchFactory;
        IVsiGameLaunch _vsiGameLaunch;
        ICancelableTask _cancelable;
        IAction _action;
        DeveloperLogEvent _devEvent;

        [SetUp]
        public void Setup()
        {
            _cancelable = Substitute.For<ICancelableTask>();
            _action = Substitute.For<IAction>();
            _gameletClient = Substitute.For<IGameletClient>();
            _paramsConverter = Substitute.For<ILaunchGameParamsConverter>();
            _cancelableTaskFactory = Substitute.For<CancelableTask.Factory>();
            _yetiVsiService = Substitute.For<IYetiVSIService>();
            _metrics = Substitute.For<IMetrics>();
            _actionRecorder = Substitute.For<ActionRecorder>(_metrics);
            _devEvent = SetupCreateLaunchEvent();
            _dialogUtil = Substitute.For<IDialogUtil>();
            _vsiGameLaunchFactory = Substitute.For<IVsiGameLaunchFactory>();
            _vsiGameLaunch = Substitute.For<IVsiGameLaunch>();
            _vsiGameLaunchFactory.Create(_gameLaunchName, Arg.Any<bool>()).Returns(_vsiGameLaunch);
            _vsiGameLaunch.LaunchName.Returns(_gameLaunchName);
            _vsiGameLaunch.LaunchId.Returns(_gameLaunchId);

            _target = new GameLauncher(_gameletClient, _yetiVsiService, _paramsConverter,
                                       _cancelableTaskFactory, _actionRecorder, _dialogUtil,
                                       _vsiGameLaunchFactory);

            SetupCancelableTask();
        }

        [TestCase(GameletSdkCompatibilityResult.Compatible, null, new string[0],
                  TestName = "Success")]
        [TestCase(GameletSdkCompatibilityResult.NotCompatibleOutsideOfRange, _notCompatibleMessage,
                  new string[0], TestName = "SdkNotCompatible")]
        [TestCase(null, ErrorStrings.ErrorWhileSdkCheck, new string[0], TestName = "SdkCheckFails")]
        [TestCase(GameletSdkCompatibilityResult.Compatible, null,
                  new[] { "warning 1", "Other Warning" }, TestName = "ParserWarning")]
        [TestCase(null, ErrorStrings.ErrorWhileSdkCheck,
                  new[] { "could not parse something!", "Other Warning", "123-987" },
                  TestName = "ParserWarningAndSdkCheckFails")]
        public void CreateLaunchWarningMessages(GameletSdkCompatibilityResult? sdkResult,
                                                string sdkWarning, string[] parserWarnings)
        {
            SetupSdkCompatibility(sdkResult);
            var launchRequest = new LaunchGameRequest();
            SetupParamsParser(launchRequest, GetStatus(parserWarnings, false));
            var launchGameResponse = new LaunchGameResponse
                { GameLaunchName = _gameLaunchName, RequestId = launchRequest.RequestId };
            SetupGameLaunchApi(launchRequest, launchGameResponse);
            SetupSdkWarningSetting();

            IVsiGameLaunch result = _target.CreateLaunch(_launchParams);

            Assert.That(result, Is.Not.Null);
            _dialogUtil.DidNotReceiveWithAnyArgs().ShowError(Arg.Any<string>(), Arg.Any<string>());

            if (string.IsNullOrEmpty(sdkWarning))
            {
                _dialogUtil.DidNotReceive()
                    .ShowOkNoMoreDisplayWarning(sdkWarning, Arg.Any<string[]>());
            }
            else
            {
                _dialogUtil.Received(1).ShowOkNoMoreDisplayWarning(sdkWarning, Arg.Any<string[]>());
            }

            if (parserWarnings.Any())
            {
                _dialogUtil.Received(1)
                    .ShowWarning(Arg.Is<string>(m => parserWarnings.All(m.Contains)));
            }

            Assert.That(_devEvent.GameLaunchData.RequestId, Is.EqualTo(launchRequest.RequestId));
            Assert.That(_devEvent.GameLaunchData.LaunchId, Is.EqualTo(_gameLaunchId));
            Assert.That(_devEvent.GameLaunchData.EndReason, Is.Null);
            _action.Received(1).Record(Arg.Any<Func<bool>>());
        }

        [TestCase(ShowOption.AlwaysShow, new string[0], true, TestName = "AlwaysShow")]
        [TestCase(ShowOption.NeverShow, new string[0], false, TestName = "NeverShow")]
        [TestCase(ShowOption.AskForEachDialog,
                  new[] { "other.version/" + _sdkVersion, _gameletSdkVersion + "/other.version" },
                  true, TestName = "AskForEachDialogVersionNotPresent")]
        [TestCase(ShowOption.AskForEachDialog, new[] { _gameletSdkVersion + "/" + _sdkVersion },
                  false, TestName = "AskForEachDialogVersionPresent")]
        public void CreateLaunchSdkWarningSetting(ShowOption sdkShowOption, string[] versionsToHide,
                                                  bool showSdkWarning)
        {
            SetupSdkCompatibility(GameletSdkCompatibilityResult.NotCompatibleOutsideOfRange);
            var launchRequest = new LaunchGameRequest();
            SetupParamsParser(launchRequest, ConfigStatus.OkStatus());
            var launchGameResponse = new LaunchGameResponse
                { GameLaunchName = _gameLaunchName, RequestId = launchRequest.RequestId };
            SetupGameLaunchApi(launchRequest, launchGameResponse);
            _yetiVsiService.Options.SdkCompatibilityWarningOption.Returns(sdkShowOption);
            _yetiVsiService.Options
                .SdkVersionsAreHidden(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(false);
            foreach (string versions in versionsToHide)
            {
                string gameletVersion = versions.Split('/')[0];
                string localVersion = versions.Split('/')[1];
                _yetiVsiService.Options
                    .SdkVersionsAreHidden(gameletVersion, localVersion, _gameletName).Returns(true);
            }

            IVsiGameLaunch result = _target.CreateLaunch(_launchParams);

            Assert.That(result, Is.Not.Null);
            _dialogUtil.DidNotReceiveWithAnyArgs().ShowError(default);

            if (!showSdkWarning)
            {
                _dialogUtil.DidNotReceiveWithAnyArgs().ShowOkNoMoreDisplayWarning(default, default);
            }
            else
            {
                _dialogUtil.ReceivedWithAnyArgs(1).ShowOkNoMoreDisplayWarning(default, default);
            }

            Assert.That(_devEvent.GameLaunchData.RequestId, Is.EqualTo(launchRequest.RequestId));
            Assert.That(_devEvent.GameLaunchData.LaunchId, Is.EqualTo(_gameLaunchId));
            Assert.That(_devEvent.GameLaunchData.EndReason, Is.Null);
            _action.Received(1).Record(Arg.Any<Func<bool>>());
        }

        [Test]
        public void CreateLaunchParserError()
        {
            SetupSdkCompatibility(GameletSdkCompatibilityResult.Compatible);
            var launchRequest = new LaunchGameRequest();
            var errors = new[] { "error 1", "second error" };
            SetupParamsParser(launchRequest, GetStatus(errors, true));
            var launchGameResponse = new LaunchGameResponse
                { GameLaunchName = _gameLaunchName, RequestId = launchRequest.RequestId };
            SetupGameLaunchApi(launchRequest, launchGameResponse);

            IVsiGameLaunch result = _target.CreateLaunch(_launchParams);

            Assert.That(result, Is.Null);
            _dialogUtil.DidNotReceive().ShowWarning(Arg.Any<string>());
            _dialogUtil.Received(1).ShowError(Arg.Is<string>(m => errors.All(m.Contains)));
            Assert.That(_devEvent.GameLaunchData, Is.Null);
            _action.Received(1).Record(Arg.Any<Func<bool>>());
        }

        [Test]
        public void CreateLaunchApiCallFails()
        {
            SetupSdkCompatibility(GameletSdkCompatibilityResult.Compatible);
            var launchRequest = new LaunchGameRequest();
            SetupParamsParser(launchRequest, ConfigStatus.OkStatus());
            _gameletClient.LaunchGameAsync(launchRequest, _action)
                .Throws(new CloudException("fail"));
            SetupSdkWarningSetting();

            IVsiGameLaunch result = _target.CreateLaunch(_launchParams);

            Assert.That(result, Is.Null);
            _dialogUtil.Received(1)
                .ShowError(Arg.Is<string>(m => m.Contains(ErrorStrings.CouldNotStartTheGame) &&
                                              m.Contains("fail")));
            _dialogUtil.DidNotReceive().ShowWarning(Arg.Any<string>());
            _dialogUtil.DidNotReceive()
                .ShowOkNoMoreDisplayWarning(Arg.Any<string>(), Arg.Any<string[]>());
            Assert.That(_devEvent.GameLaunchData.RequestId, Is.EqualTo(launchRequest.RequestId));
            Assert.That(_devEvent.GameLaunchData.LaunchId, Is.Null);
            Assert.That(_devEvent.GameLaunchData.EndReason, Is.Null);
            _action.Received(1).Record(Arg.Any<Func<bool>>());
        }

        [Test]
        public void CreateLaunchCancelled()
        {
            SetupSdkCompatibility(GameletSdkCompatibilityResult.Compatible);
            var launchRequest = new LaunchGameRequest();
            SetupParamsParser(launchRequest, ConfigStatus.OkStatus());
            _cancelable.When(c => c.ThrowIfCancellationRequested())
                .Throw(new OperationCanceledException("cancel!"));
            var launchGameResponse = new LaunchGameResponse
                { GameLaunchName = _gameLaunchName, RequestId = launchRequest.RequestId };
            SetupGameLaunchApi(launchRequest, launchGameResponse);

            IVsiGameLaunch result = _target.CreateLaunch(_launchParams);

            Assert.That(result, Is.Null);
            _dialogUtil.DidNotReceive().ShowError(Arg.Any<string>());
            _dialogUtil.DidNotReceive().ShowWarning(Arg.Any<string>());
            _action.Received(1).Record(Arg.Any<Func<bool>>());
        }

        ConfigStatus GetStatus(string[] parserMessages, bool areErrors)
        {
            ConfigStatus status = ConfigStatus.OkStatus();
            foreach (string parserMessage in parserMessages)
            {
                if (areErrors)
                {
                    status.AppendError(parserMessage);
                }
                else
                {
                    status.AppendWarning(parserMessage);
                }
            }

            return status;
        }

        void SetupSdkWarningSetting()
        {
            _yetiVsiService.Options.SdkCompatibilityWarningOption.Returns(
                ShowOption.AskForEachDialog);
            _yetiVsiService.Options
                .SdkVersionsAreHidden(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(false);
        }

        void SetupSdkCompatibility(GameletSdkCompatibilityResult? result)
        {
            if (result.HasValue)
            {
                _gameletClient
                    .CheckSdkCompatibilityAsync(_launchParams.GameletName, _launchParams.SdkVersion,
                                                _action).Returns(new GameletSdkCompatibility
                    {
                        CompatibilityResult = result.Value,
                        Message = _notCompatibleMessage
                    });
            }
            else
            {
                _gameletClient
                    .CheckSdkCompatibilityAsync(_launchParams.GameletName, _launchParams.SdkVersion,
                                                _action).Throws(
                        new CloudException(_errorSdkCheckMessage));
            }
        }

        DeveloperLogEvent SetupCreateLaunchEvent()
        {
            _actionRecorder.CreateToolAction(ActionType.GameLaunchCreate).Returns(_action);
            var devEvent = new DeveloperLogEvent();
            _action.When(a => a.UpdateEvent(Arg.Any<DeveloperLogEvent>())).Do(callInfo =>
            {
                var evt = callInfo.Arg<DeveloperLogEvent>();
                devEvent.MergeFrom(evt);
            });
            return devEvent;
        }

        void SetupCancelableTask()
        {
            Func<ICancelable, Task> currentTask = null;
            _action.Record(Arg.Any<Func<bool>>()).Returns(callInfo =>
            {
                try
                {
                    new JoinableTaskFactory(new JoinableTaskContext()).Run(
                        () => currentTask(_cancelable));
                    return true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            });
            _cancelableTaskFactory.Create(
                TaskMessages.LaunchingGame, Arg.Any<Func<ICancelable, Task>>()).Returns(callInfo =>
            {
                currentTask = callInfo.Arg<Func<ICancelable, Task>>();
                return _cancelable;
            });
        }

        void SetupParamsParser(LaunchGameRequest launchRequest, ConfigStatus status)
        {
            _paramsConverter.ToLaunchGameRequest(_launchParams, out LaunchGameRequest _).Returns(
                callInfo =>
                {
                    callInfo[1] = launchRequest;
                    return status;
                });
        }

        void SetupGameLaunchApi(LaunchGameRequest launchRequest, LaunchGameResponse response)
        {
            _gameletClient.LaunchGameAsync(launchRequest, _action)
                .Returns(Task.FromResult(response));
        }
    }
}
