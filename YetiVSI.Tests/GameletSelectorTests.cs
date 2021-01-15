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
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSITestsCommon;

namespace YetiVSI.Test
{
    [TestFixture]
    class GameletSelectorTests
    {
        const string TEST_DEBUG_SESSION_ID = "sessiondebugid";
        const string TEST_ACCOUNT = "test account";

        GameletSelector gameletSelector;

        IMetrics metrics;
        IDialogUtil dialogUtil;
        IGameletSelectionWindow gameletSelectionWindow;
        IGameletClient gameletClient;
        ISshManager sshManager;

        Gamelet gamelet1;
        Gamelet gamelet2;
        ActionRecorder actionRecorder;
        IRemoteCommand remoteCommand;

        readonly string _targetPath = "";
        readonly DeployOnLaunchSetting _deploy = DeployOnLaunchSetting.ALWAYS;

        [SetUp]
        public void SetUp()
        {
            gamelet1 = new Gamelet
            {
                Id = "test_gamelet1",
                Name = "test_gamelet_name1",
                IpAddr = "1.2.3.4",
                State = GameletState.Reserved,
            };

            gamelet2 = new Gamelet
            {
                Id = "test_gamelet2",
                Name = "test_gamelet_name2",
                IpAddr = "1.2.3.5",
                State = GameletState.Reserved,
            };

            metrics = Substitute.For<IMetrics>();
            metrics.NewDebugSessionId().Returns(TEST_DEBUG_SESSION_ID);

            dialogUtil = Substitute.For<IDialogUtil>();

            var sdkConfigFactory = Substitute.For<SdkConfig.Factory>();
            var sdkConfig = new SdkConfig();
            sdkConfigFactory.LoadOrDefault().Returns(sdkConfig);

            var credentialManager = Substitute.For<YetiCommon.ICredentialManager>();
            credentialManager.LoadAccount().Returns(TEST_ACCOUNT);

            gameletSelectionWindow = Substitute.For<IGameletSelectionWindow>();
            var gameletSelectionWindowFactory = Substitute.For<GameletSelectionWindow.Factory>();
            gameletSelectionWindowFactory.Create(Arg.Any<List<Gamelet>>())
                .Returns(gameletSelectionWindow);

            var cloudRunner = new CloudRunner(sdkConfigFactory, credentialManager,
                                              new CloudConnection(), new GgpSDKUtil());

            var cancelableTaskFactory =
                FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false);

            gameletClient = Substitute.For<IGameletClient>();
            var gameletClientFactory = Substitute.For<GameletClient.Factory>();
            gameletClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(gameletClient);

            sshManager = Substitute.For<ISshManager>();

            sshManager.EnableSshAsync(gamelet1, Arg.Any<YetiVSI.Metrics.IAction>())
                .Returns(Task.FromResult(true));
            sshManager.EnableSshAsync(gamelet2, Arg.Any<YetiVSI.Metrics.IAction>())
                .Returns(Task.FromResult(true));

            remoteCommand = Substitute.For<IRemoteCommand>();

            var debugSessionMetrics = new DebugSessionMetrics(metrics);
            debugSessionMetrics.UseNewDebugSessionId();
            actionRecorder = new ActionRecorder(debugSessionMetrics);

            gameletSelector = new GameletSelector(
                dialogUtil, cloudRunner, gameletSelectionWindowFactory,
                cancelableTaskFactory, gameletClientFactory, sshManager, remoteCommand);
        }

        [Test]
        public void TestCanSelectFromMultipleGamelets()
        {
            var gamelets = new List<Gamelet> { gamelet1, gamelet2 };

            gameletSelectionWindow.Run().Returns(gamelet2);

            Gamelet gamelet;
            var result = gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                actionRecorder,
                gamelets, out gamelet);

            Assert.That(result, Is.True);
            Assert.That(gamelet.Id, Is.EqualTo(gamelet2.Id));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsSelect,
                DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsClearLogs,
                DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public void TestCanSelectOnlyGamelet()
        {
            var gamelets = new List<Gamelet> { gamelet1 };

            Gamelet gamelet;
            var result = gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                actionRecorder,
                gamelets, out gamelet);

            Assert.That(result, Is.True);
            Assert.That(gamelet.Id, Is.EqualTo(gamelet1.Id));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsSelect,
                DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsClearLogs,
                DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task TestGameletRunningStopReturnsTrueAsync()
        {
            gamelet1.State = GameletState.InUse;

            var stoppedGamelet = gamelet1.Clone();
            stoppedGamelet.State = GameletState.Reserved;
            gameletClient.GetGameletByNameAsync(gamelet1.Name)
                .Returns(Task.FromResult(stoppedGamelet));
            dialogUtil.ShowYesNo(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            Gamelet gamelet;
            var result = gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                actionRecorder,
                new List<Gamelet> { gamelet1 }, out gamelet);

            Assert.That(result, Is.True);
            Assert.That(gamelet.Id, Is.EqualTo(gamelet1.Id));
            Assert.That(gamelet.State, Is.EqualTo(GameletState.Reserved));
            await gameletClient.Received(1).StopGameAsync(gamelet1.Id);
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
                () => gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                    actionRecorder,
                new List<Gamelet>(), out gamelet));

            Assert.That(result.Message, Does.Contain(ErrorStrings.NoGameletsFound));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsSelect,
                DeveloperEventStatus.Types.Code.InvalidConfiguration);
        }

        [Test]
        public void TestGameletRunningNoStopReturnsFalse()
        {
            gamelet1.State = GameletState.InUse;

            dialogUtil.ShowYesNo(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

            Gamelet gamelet;
            var result = gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                actionRecorder,
                new List<Gamelet> { gamelet1 }, out gamelet);
            Assert.That(result, Is.False);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsPrepare,
                DeveloperEventStatus.Types.Code.Cancelled);
        }


        [Test]
        public void TestGameletIsInvalidStateThrowsException()
        {
            gamelet1.State = GameletState.Unhealthy;

            Gamelet gamelet;
            var result = Assert.Throws<InvalidStateException>(() =>
                gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                    actionRecorder,
                new List<Gamelet> { gamelet1 }, out gamelet));

            Assert.That(result.Message,
                Is.EqualTo(ErrorStrings.GameletInUnexpectedState(gamelet1)));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsPrepare,
                DeveloperEventStatus.Types.Code.InvalidObjectState);
        }

        [Test]
        public void TestEnableSshFailsReturnsFalse()
        {
            sshManager
                .When(m => m.EnableSshAsync(gamelet1, Arg.Any<YetiVSI.Metrics.IAction>()))
                .Do(c => { throw new CloudException("Oops!"); });

            Gamelet gamelet;
            var result = gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                actionRecorder,
                new List<Gamelet> { gamelet1 }, out gamelet);

            Assert.That(result, Is.False);
            dialogUtil.Received(1).ShowError(Arg.Any<string>(), Arg.Any<string>());
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsEnableSsh,
                DeveloperEventStatus.Types.Code.InternalError);
        }

        [Test]
        public void TestClearLogsFailsReturnsFalse()
        {
            remoteCommand
                .When(m => m.RunWithSuccessAsync(new SshTarget(gamelet1),
                    GameletSelector.CLEAR_LOGS_CMD))
                .Do(c => { throw new ProcessException("Oops!"); });

            Gamelet gamelet;
            var result = gameletSelector.TrySelectAndPrepareGamelet(_targetPath, _deploy,
                actionRecorder,
                new List<Gamelet> { gamelet1 }, out gamelet);

            Assert.That(result, Is.False);
            dialogUtil.ShowError(Arg.Any<string>(), Arg.Any<string>());
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsClearLogs,
                DeveloperEventStatus.Types.Code.ExternalToolUnavailable);
        }

        void AssertMetricRecorded(
            DeveloperEventType.Types.Type type, DeveloperEventStatus.Types.Code status)
        {
            metrics.Received().RecordEvent(type, Arg.Is<DeveloperLogEvent>(
                p =>
                    p.StatusCode == status &&
                    p.DebugSessionIdStr == TEST_DEBUG_SESSION_ID
                ));
        }
    }
}
