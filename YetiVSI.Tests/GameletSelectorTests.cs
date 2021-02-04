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

using GgpGrpc;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.Cloud;
using YetiCommon.SSH;
using YetiCommon.VSProject;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSITestsCommon;

namespace YetiVSI.Test
{
    [TestFixture]
    class GameletSelectorTests
    {
        const string _testDebugSessionId = "sessiondebugid";
        const string _testAccount = "test account";
        const string _devAccount = "dev_account";

        GameletSelectorLegacyFlow _gameletSelector;

        IMetrics _metrics;
        IDialogUtil _dialogUtil;
        IGameletSelectionWindow _gameletSelectionWindow;
        IGameletClient _gameletClient;
        ISshManager _sshManager;

        Gamelet _gamelet1;
        Gamelet _gamelet2;
        ActionRecorder _actionRecorder;
        IRemoteCommand _remoteCommand;

        readonly string _targetPath = "";
        readonly DeployOnLaunchSetting _deploy = DeployOnLaunchSetting.ALWAYS;

        [SetUp]
        public void SetUp()
        {
            _gamelet1 = new Gamelet
            {
                Id = "test_gamelet1",
                Name = "test_gamelet_name1",
                IpAddr = "1.2.3.4",
                State = GameletState.Reserved,
            };

            _gamelet2 = new Gamelet
            {
                Id = "test_gamelet2",
                Name = "test_gamelet_name2",
                IpAddr = "1.2.3.5",
                State = GameletState.Reserved,
            };

            _metrics = Substitute.For<IMetrics>();
            _metrics.NewDebugSessionId().Returns(_testDebugSessionId);

            _dialogUtil = Substitute.For<IDialogUtil>();

            var sdkConfigFactory = Substitute.For<SdkConfig.Factory>();
            var sdkConfig = new SdkConfig();
            sdkConfigFactory.LoadOrDefault().Returns(sdkConfig);

            var credentialManager = Substitute.For<YetiCommon.ICredentialManager>();
            credentialManager.LoadAccount().Returns(_testAccount);

            _gameletSelectionWindow = Substitute.For<IGameletSelectionWindow>();
            var gameletSelectionWindowFactory = Substitute.For<GameletSelectionWindow.Factory>();
            gameletSelectionWindowFactory.Create(Arg.Any<List<Gamelet>>())
                .Returns(_gameletSelectionWindow);

            var cloudRunner = new CloudRunner(sdkConfigFactory, credentialManager,
                                              new CloudConnection(), new GgpSDKUtil());

            CancelableTask.Factory cancelableTaskFactory =
                FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false);

            _gameletClient = Substitute.For<IGameletClient>();
            var gameletClientFactory = Substitute.For<GameletClient.Factory>();
            gameletClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(_gameletClient);

            _sshManager = Substitute.For<ISshManager>();

            _sshManager.EnableSshAsync(_gamelet1, Arg.Any<IAction>())
                .Returns(Task.FromResult(true));
            _sshManager.EnableSshAsync(_gamelet2, Arg.Any<IAction>())
                .Returns(Task.FromResult(true));

            _remoteCommand = Substitute.For<IRemoteCommand>();

            var debugSessionMetrics = new DebugSessionMetrics(_metrics);
            debugSessionMetrics.UseNewDebugSessionId();
            _actionRecorder = new ActionRecorder(debugSessionMetrics);

            _gameletSelector = new GameletSelectorLegacyFlow(_dialogUtil, cloudRunner,
                                                            gameletSelectionWindowFactory,
                                                            cancelableTaskFactory,
                                                            gameletClientFactory, _sshManager,
                                                            _remoteCommand, _actionRecorder);
        }

        [Test]
        public void TestCanSelectFromMultipleGamelets()
        {
            var gamelets = new List<Gamelet> { _gamelet1, _gamelet2 };

            _gameletSelectionWindow.Run().Returns(_gamelet2);

            Gamelet gamelet;
            var result = _gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                gamelets, null, _devAccount, out gamelet);

            Assert.That(result, Is.True);
            Assert.That(gamelet.Id, Is.EqualTo(_gamelet2.Id));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsSelect,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsClearLogs,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public void TestCanSelectOnlyGamelet()
        {
            var gamelets = new List<Gamelet> { _gamelet1 };

            Gamelet gamelet;
            var result = _gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                gamelets, null, _devAccount, out gamelet);

            Assert.That(result, Is.True);
            Assert.That(gamelet.Id, Is.EqualTo(_gamelet1.Id));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsSelect,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsClearLogs,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task TestGameletRunningStopReturnsTrueAsync()
        {
            _gamelet1.State = GameletState.InUse;

            var stoppedGamelet = _gamelet1.Clone();
            stoppedGamelet.State = GameletState.Reserved;
            _gameletClient.GetGameletByNameAsync(_gamelet1.Name)
                .Returns(Task.FromResult(stoppedGamelet));
            _dialogUtil.ShowYesNo(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            Gamelet gamelet;
            var result = _gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                new List<Gamelet> { _gamelet1 }, null, _devAccount, out gamelet);

            Assert.That(result, Is.True);
            Assert.That(gamelet.Id, Is.EqualTo(_gamelet1.Id));
            Assert.That(gamelet.State, Is.EqualTo(GameletState.Reserved));
            await _gameletClient.Received(1).StopGameAsync(_gamelet1.Id);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsSelect,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsClearLogs,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public void TestNoGameletsThrowsException()
        {
            Gamelet gamelet;
            var result = Assert.Throws<ConfigurationException>(
                () => _gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                    new List<Gamelet>(), null, _devAccount, out gamelet));

            Assert.That(result.Message, Does.Contain(ErrorStrings.NoGameletsFound));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsSelect,
                                 DeveloperEventStatus.Types.Code.InvalidConfiguration);
        }

        [Test]
        public void TestGameletRunningNoStopReturnsFalse()
        {
            _gamelet1.State = GameletState.InUse;

            _dialogUtil.ShowYesNo(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

            Gamelet gamelet;
            var result = _gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                new List<Gamelet> { _gamelet1 }, null, _devAccount, out gamelet);
            Assert.That(result, Is.False);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsPrepare,
                                 DeveloperEventStatus.Types.Code.Cancelled);
        }


        [Test]
        public void TestGameletIsInvalidStateThrowsException()
        {
            _gamelet1.State = GameletState.Unhealthy;

            Gamelet gamelet;
            var result = Assert.Throws<InvalidStateException>(
                () => _gameletSelector.TrySelectAndPrepareGamelet(
                    _targetPath, _deploy, new List<Gamelet> { _gamelet1 }, null, _devAccount,
                    out gamelet));

            Assert.That(result.Message,
                        Is.EqualTo(ErrorStrings.GameletInUnexpectedState(_gamelet1)));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsSelect,
                                 DeveloperEventStatus.Types.Code.InvalidObjectState);
        }

        [Test]
        public void TestEnableSshFailsReturnsFalse()
        {
            _sshManager.When(m => m.EnableSshAsync(_gamelet1, Arg.Any<YetiVSI.Metrics.IAction>()))
                .Do(c => throw new CloudException("Oops!"));

            Gamelet gamelet;
            var result = _gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                new List<Gamelet> { _gamelet1 }, null, _devAccount, out gamelet);

            Assert.That(result, Is.False);
            _dialogUtil.Received(1).ShowError(Arg.Any<string>(), Arg.Any<string>());
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsEnableSsh,
                                 DeveloperEventStatus.Types.Code.InternalError);
        }

        [Test]
        public void TestClearLogsFailsReturnsFalse()
        {
            _remoteCommand
                .When(m => m.RunWithSuccessAsync(new SshTarget(_gamelet1),
                    GameletSelectorLegacyFlow.ClearLogsCmd))
                .Do(c => { throw new ProcessException("Oops!"); });

            Gamelet gamelet;
            var result = _gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                new List<Gamelet> { _gamelet1 }, null, _devAccount, out gamelet);

            Assert.That(result, Is.False);
            _dialogUtil.ShowError(Arg.Any<string>(), Arg.Any<string>());
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsClearLogs,
                                 DeveloperEventStatus.Types.Code.ExternalToolUnavailable);
        }

        void AssertMetricRecorded(DeveloperEventType.Types.Type type,
                                  DeveloperEventStatus.Types.Code status)
        {
            _metrics.Received().RecordEvent(type, Arg.Is<DeveloperLogEvent>(
                p =>
                    p.StatusCode == status &&
                    p.DebugSessionIdStr == _testDebugSessionId
                ));
        }
    }
}