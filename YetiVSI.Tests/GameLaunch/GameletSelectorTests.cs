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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Metrics.Shared;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using YetiCommon;
using YetiCommon.Cloud;
using YetiCommon.SSH;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;
using YetiVSI.ProjectSystem.Abstractions;
using YetiVSITestsCommon;

namespace YetiVSI.Test.GameLaunch
{
    [TestFixture]
    class GameletSelectorTests
    {
        const string _testDebugSessionId = "sessiondebugid";
        const string _testAccount = "test account";
        const string _devAccount = "dev_account";
        const string _launchName = "test/launch/name";

        // /proc/mounts result after 'ggp instance unmount'.
        readonly List<string> _unmounted = new List<string>
        {
            "overlay /srv/game/assets overlay ro,relatime,lowerdir=/mnt/developer:/mnt/localssd/var/empty 0 0",
            "/dev/nvme0n1p6 /mnt/developer ext4 rw,relatime 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
        };

        // /proc/mounts result after 'ggp instance mount --package'.
        readonly List<string> _mountedPackage = new List<string>
        {
            "/dev/nvme0n1p6 /mnt/developer ext4 rw,relatime 0 0",
            "/dev/mapper/cryptfs-content-asset-1-0-0-96394ebed9b4 /mnt/assets-content-asset-1-0-0-96394ebed9b4 ext4 ro,relatime,norecovery 0 0",
            "overlay /srv/game/assets overlay ro,relatime,lowerdir=/mnt/assets-content-asset-1-0-0-0cd87f2db855:/mnt/localssd/var/empty 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
        };

        // /proc/mounts result after 'ggp instance mount --package <id> --overlay-instance-storage'
        readonly List<string> _mountedPackageWithOverlay = new List<string>
        {
            "/dev/nvme0n1p6 /mnt/developer ext4 rw,relatime 0 0",
            "/dev/mapper/cryptfs-content-asset-1-0-0-96394ebed9b4 /mnt/assets-content-asset-1-0-0-96394ebed9b4 ext4 ro,relatime,norecovery 0 0",
            "overlay /srv/game/assets overlay ro,relatime,lowerdir=/mnt/developer:/mnt/assets-content-asset-1-0-0-48f27cee9049 0 0",
            "tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1048576k,mode=700,uid=1000,gid=1000,inode64 0 0"
        };

        GameletSelector _gameletSelector;

        IVsiMetrics _metrics;
        IDialogUtil _dialogUtil;
        IInstanceSelectionWindow _instanceSelectionWindow;
        IGameletClient _gameletClient;
        ISshManager _sshManager;
        IGameLaunchBeHelper _gameLaunchBeHelper;

        Gamelet _gamelet1;
        Gamelet _gamelet2;
        ActionRecorder _actionRecorder;
        IRemoteCommand _remoteCommand;

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

            _metrics = Substitute.For<IVsiMetrics>();
            _metrics.NewDebugSessionId().Returns(_testDebugSessionId);

            _dialogUtil = Substitute.For<IDialogUtil>();

            var sdkConfigFactory = Substitute.For<SdkConfig.Factory>();
            var sdkConfig = new SdkConfig();
            sdkConfigFactory.LoadOrDefault().Returns(sdkConfig);

            var credentialManager = Substitute.For<ICredentialManager>();
            credentialManager.LoadAccount().Returns(_testAccount);

            _instanceSelectionWindow = Substitute.For<IInstanceSelectionWindow>();
            var gameletSelectionWindowFactory = Substitute.For<InstanceSelectionWindow.Factory>();
            gameletSelectionWindowFactory.Create(Arg.Any<List<Gamelet>>())
                .Returns(_instanceSelectionWindow);
            _gameLaunchBeHelper = Substitute.For<IGameLaunchBeHelper>();

            var cloudRunner = new CloudRunner(sdkConfigFactory, credentialManager,
                                              new CloudConnection(), new GgpSDKUtil());

            CancelableTask.Factory cancelableTaskFactory =
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
                FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false);
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

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

            _gameletSelector = new GameletSelector(_dialogUtil, cloudRunner,
                                                   gameletSelectionWindowFactory,
                                                   cancelableTaskFactory, gameletClientFactory,
                                                   _sshManager, _remoteCommand, _gameLaunchBeHelper,
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
                                                   new JoinableTaskContext(), _actionRecorder);
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
        }

        [Test]
        public void TestCanSelectFromMultipleGamelets()
        {
            var gamelets = new List<Gamelet> { _gamelet1, _gamelet2 };
            _instanceSelectionWindow.Run().Returns(_gamelet2);
            SetupGameletClientApi(_gamelet2);

            var result = _gameletSelector.TrySelectAndPrepareGamelet(
                _deploy, gamelets, null, _devAccount, out Gamelet gamelet,
                out MountConfiguration _);

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
            SetupGameletClientApi(_gamelet1);

            var result = _gameletSelector.TrySelectAndPrepareGamelet(
                _deploy, gamelets, null, _devAccount, out Gamelet gamelet,
                out MountConfiguration _);

            Assert.That(result, Is.True);
            Assert.That(gamelet.Id, Is.EqualTo(_gamelet1.Id));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsSelect,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsClearLogs,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public void TestNoGameletsThrowsException()
        {
            var result = Assert.Throws<ConfigurationException>(
                () => _gameletSelector.TrySelectAndPrepareGamelet(
                    _deploy, new List<Gamelet>(), null, _devAccount, out Gamelet _,
                    out MountConfiguration _));

            Assert.That(result.Message, Does.Contain(ErrorStrings.NoGameletsFound));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsSelect,
                                 DeveloperEventStatus.Types.Code.InvalidConfiguration);
        }

        [Test]
        public void TestGameletIsInvalidStateThrowsException()
        {
            _gamelet1.State = GameletState.Unhealthy;

            var result = Assert.Throws<InvalidStateException>(
                () => _gameletSelector.TrySelectAndPrepareGamelet(
                    _deploy, new List<Gamelet> { _gamelet1 }, null, _devAccount, out Gamelet _,
                    out MountConfiguration _));

            Assert.That(result.Message,
                        Is.EqualTo(ErrorStrings.GameletInUnexpectedState(_gamelet1)));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsSelect,
                                 DeveloperEventStatus.Types.Code.InvalidObjectState);
        }

        [Test]
        public void TestEnableSshFailsReturnsFalse()
        {
            _sshManager
                .When(m => m.EnableSshAsync(Arg.Is<Gamelet>(g => g.Name == _gamelet1.Name),
                                            Arg.Any<IAction>()))
                .Do(c => throw new CloudException("Oops!"));
            SetupGameletClientApi(_gamelet1);

            var result = _gameletSelector.TrySelectAndPrepareGamelet(
                _deploy, new List<Gamelet> { _gamelet1 }, null, _devAccount, out Gamelet _,
                out MountConfiguration _);

            Assert.That(result, Is.False);
            _dialogUtil.Received(1).ShowError(Arg.Any<string>(), Arg.Any<CloudException>());
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsEnableSsh,
                                 DeveloperEventStatus.Types.Code.InternalError);
        }

        [TestCase(true, TestName = "TestInvalidMountSetupDeployingButDetachedYesContinue")]
        [TestCase(false, TestName = "TestInvalidMountSetupDeployingButDetachedNoContinue")]
        public void TestInvalidMountSetupDeployingButDetached(bool confirmContinue)
        {
            SetupGetGameletApi(_gamelet1, GameletState.Reserved);
            SetupProcMountsContent(_gamelet1, _mountedPackage, _unmounted);
            SetupNoInstanceStorageOverlayDialog(confirmContinue);

            var result = _gameletSelector.TrySelectAndPrepareGamelet(
                _deploy, new List<Gamelet> { _gamelet1 }, null, _devAccount, out Gamelet _,
                out MountConfiguration config);

            Assert.That(result, Is.EqualTo(confirmContinue));

            // Check whether the mount config was refreshed.
            MountFlags flags = confirmContinue
                ? MountFlags.InstanceStorageOverlay
                : MountFlags.PackageMounted;
            Assert.That(config.Flags, Is.EqualTo(flags));
        }

        [TestCase(true, TestName = "TestInvalidMountSetupNotStreamingButDetachedYesContinue")]
        [TestCase(false, TestName = "TestInvalidMountSetupNotStreamingButDetachedNoContinue")]
        public void TestInvalidMountSetupNotStreamingButDetached(bool confirmContinue)
        {
            SetupGetGameletApi(_gamelet1, GameletState.Reserved);
            SetupProcMountsContent(_gamelet1, _mountedPackage, _unmounted);
            SetupNoAssetStreamingDialog(confirmContinue);

            var result = _gameletSelector.TrySelectAndPrepareGamelet(
                DeployOnLaunchSetting.FALSE, new List<Gamelet> { _gamelet1 }, null, _devAccount,
                out Gamelet _, out MountConfiguration config);

            Assert.That(result, Is.EqualTo(confirmContinue));

            // Check whether the mount config was refreshed.
            MountFlags flags = confirmContinue
                ? MountFlags.InstanceStorageOverlay
                : MountFlags.PackageMounted;
            Assert.That(config.Flags, Is.EqualTo(flags));
        }

        [Test]
        public void TestValidMountSetup()
        {
            SetupGetGameletApi(_gamelet1, GameletState.Reserved);
            SetupProcMountsContent(_gamelet1, _mountedPackageWithOverlay, _unmounted);

            var result = _gameletSelector.TrySelectAndPrepareGamelet(
                _deploy, new List<Gamelet> { _gamelet1 }, null, _devAccount, out Gamelet _,
                out MountConfiguration config);

            Assert.That(result, Is.True);

            // Check whether the mount config was NOT refreshed.
            MountFlags flags = MountFlags.InstanceStorageOverlay | MountFlags.PackageMounted;
            Assert.That(config.Flags, Is.EqualTo(flags));
        }

        [Test]
        public void TestClearLogsFailsReturnsFalse()
        {
            _remoteCommand
                .When(m => m.RunWithSuccessAsync(new SshTarget(_gamelet1),
                                                 GameletSelector.ClearLogsCmd))
                .Do(c => { throw new ProcessException("Oops!"); });
            SetupGameletClientApi(_gamelet1);

            var result = _gameletSelector.TrySelectAndPrepareGamelet(
                _deploy, new List<Gamelet> { _gamelet1 }, null, _devAccount, out Gamelet _,
                out MountConfiguration _);

            Assert.That(result, Is.False);
            _dialogUtil.ShowError(Arg.Any<string>(), Arg.Any<ProcessException>());
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsClearLogs,
                                 DeveloperEventStatus.Types.Code.ExternalToolUnavailable);
        }

        [TestCase(true, TestName = "GameLaunchExistsForAccountYesStop")]
        [TestCase(false, TestName = "GameLaunchExistsForAccountNoStop")]
        public void GameLaunchExistsForAccount(bool confirmStop)
        {
            var gamelets = new List<Gamelet> { _gamelet1, _gamelet2 };
            _instanceSelectionWindow.Run().Returns(_gamelet2);
            SetupGetGameletApi(_gamelet2, GameletState.Reserved);
            SetupGetGameletApi(_gamelet1, GameletState.InUse);
            SetupGetCurrentGameLaunch(null, _gamelet1.Name);
            SetupStopGameLaunchDialog(confirmStop);
            SetupDeleteGameLaunch(_gamelet1.Name, GameLaunchState.GameLaunchEnded, true);

            bool result = _gameletSelector.TrySelectAndPrepareGamelet(
                _deploy, gamelets, null, _devAccount, out Gamelet gamelet,
                out MountConfiguration _);

            Assert.That(result, Is.EqualTo(confirmStop));
            if (confirmStop)
            {
                Assert.That(gamelet.Id, Is.EqualTo(_gamelet2.Id));
                AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameLaunchDeleteExisting,
                                     DeveloperEventStatus.Types.Code.Success);
            }

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameLaunchGetExisting,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameLaunchStopPrompt, confirmStop
                                     ? DeveloperEventStatus.Types.Code.Success
                                     : DeveloperEventStatus.Types.Code.Cancelled);
        }

        [Test]
        public void GameLaunchExistsForAccountOnNonDevGamelet()
        {
            var gamelets = new List<Gamelet> { _gamelet1, _gamelet2 };
            _instanceSelectionWindow.Run().Returns(_gamelet2);
            SetupGetGameletApi(_gamelet2, GameletState.Reserved);
            SetupGetGameletApi(_gamelet1, GameletState.InUse);
            SetupGetCurrentGameLaunch(null, "non/dev/gamelet");
            SetupStopGameLaunchDialog(true);
            SetupDeleteGameLaunch(_gamelet1.Name, GameLaunchState.GameLaunchEnded, true);

            bool result = _gameletSelector.TrySelectAndPrepareGamelet(
                _deploy, gamelets, null, _devAccount, out Gamelet gamelet,
                out MountConfiguration _);

            Assert.That(result, Is.EqualTo(true));
            Assert.That(gamelet.Id, Is.EqualTo(_gamelet2.Id));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameLaunchDeleteExisting,
                                 DeveloperEventStatus.Types.Code.Success);

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameLaunchGetExisting,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameLaunchStopPrompt,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [TestCase(true, TestName = "GameLaunchExistsOnGameletYesStop")]
        [TestCase(false, TestName = "GameLaunchExistsOnGameletNoStop")]
        public void GameLaunchExistsOnGamelet(bool confirmStop)
        {
            var gamelets = new List<Gamelet> { _gamelet1, _gamelet2 };
            _instanceSelectionWindow.Run().Returns(_gamelet1);
            SetupGetGameletApi(_gamelet2, GameletState.Reserved);
            SetupGetGameletApi(_gamelet1, GameletState.InUse, GameletState.InUse,
                               GameletState.Reserved);
            SetupGetCurrentGameLaunch(_testAccount, _gamelet1.Name);
            SetupStopGameletDialog(confirmStop);

            bool result = _gameletSelector.TrySelectAndPrepareGamelet(
                _deploy, gamelets, null, _devAccount, out Gamelet gamelet,
                out MountConfiguration _);

            Assert.That(result, Is.EqualTo(confirmStop));
            if (confirmStop)
            {
                Assert.That(gamelet.Id, Is.EqualTo(_gamelet1.Id));
                AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsStop,
                                     DeveloperEventStatus.Types.Code.Success);
            }

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameLaunchGetExisting,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsPrepare, confirmStop
                                     ? DeveloperEventStatus.Types.Code.Success
                                     : DeveloperEventStatus.Types.Code.Cancelled);
        }

        [Test]
        public void GameLaunchExistsForAnotherAccount()
        {
            var gamelets = new List<Gamelet> { _gamelet1, _gamelet2 };
            _instanceSelectionWindow.Run().Returns(_gamelet2);
            SetupGetGameletApi(_gamelet2, GameletState.Reserved);
            SetupGetGameletApi(_gamelet1, GameletState.InUse);
            SetupGetCurrentGameLaunch(_testAccount, _gamelet1.Name);
            SetupDeleteGameLaunch(_gamelet1.Name, GameLaunchState.GameLaunchEnded, true);

            bool result = _gameletSelector.TrySelectAndPrepareGamelet(
                _deploy, gamelets, null, _devAccount, out Gamelet gamelet,
                out MountConfiguration _);

            Assert.That(result, Is.EqualTo(true));
            Assert.That(gamelet.Id, Is.EqualTo(_gamelet2.Id));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameLaunchGetExisting,
                                 DeveloperEventStatus.Types.Code.Success);
            _dialogUtil.DidNotReceive().ShowYesNo(Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public void GameLaunchExistsOnGameletAndAccount()
        {
            var gamelets = new List<Gamelet> { _gamelet1, _gamelet2 };
            _instanceSelectionWindow.Run().Returns(_gamelet1);
            SetupGetGameletApi(_gamelet2, GameletState.InUse, GameletState.InUse,
                               GameletState.Reserved);
            SetupGetGameletApi(_gamelet1, GameletState.InUse, GameletState.InUse,
                               GameletState.Reserved);
            SetupGetCurrentGameLaunch(null, _gamelet2.Name);
            SetupDeleteGameLaunch(_gamelet2.Name, GameLaunchState.GameLaunchEnded, true);
            SetupStopGameletDialog(true);
            SetupStopGameLaunchDialog(true);

            bool result = _gameletSelector.TrySelectAndPrepareGamelet(
                _deploy, gamelets, null, _devAccount, out Gamelet gamelet,
                out MountConfiguration _);

            Assert.That(result, Is.True);
            Assert.That(gamelet.Id, Is.EqualTo(_gamelet1.Id));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsStop,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameLaunchGetExisting,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameletsPrepare,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameLaunchDeleteExisting,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiGameLaunchStopPrompt,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        // GameLaunch present for the same and another account on gamelet: yes stop

        void SetupStopGameLaunchDialog(bool confirm)
        {
            _dialogUtil
                .ShowYesNo(
                    Arg.Is<string>(m => m.Contains("An account can only play one game at a time")),
                    ErrorStrings.StopRunningGame).Returns(confirm);
        }

        void SetupStopGameletDialog(bool confirm)
        {
            _dialogUtil
                .ShowYesNo(
                    Arg.Is<string>(
                        m => m.Contains(
                            "Another account is already playing a game on this instance")),
                    ErrorStrings.StopRunningGame).Returns(confirm);
        }

        void SetupNoInstanceStorageOverlayDialog(bool confirm)
        {
            _dialogUtil
                .ShowYesNo(
                    Arg.Is<string>(m => m.Contains(
                                       "A package or local workstation directory has been " +
                                       "mounted on the instance, but no instance storage overlay " +
                                       "has been specified")), ErrorStrings.MountConfiguration)
                .Returns(confirm);
        }

        void SetupNoAssetStreamingDialog(bool confirm)
        {
            _dialogUtil
                .ShowYesNo(
                    Arg.Is<string>(m => m.Contains(
                                       "A package has been mounted on the instance, but neither " +
                                       "a local workstation directory is streamed nor an " +
                                       "instance storage overlay has been specified.")),
                    ErrorStrings.MountConfiguration).Returns(confirm);
        }

        void SetupDeleteGameLaunch(string gameletName, GameLaunchState? deletedLaunchState,
                                   bool success)
        {
            GgpGrpc.Models.GameLaunch gameLaunch = deletedLaunchState.HasValue
                ? new GgpGrpc.Models.GameLaunch
                {
                    GameLaunchState = deletedLaunchState.Value,
                    GameletName = gameletName,
                    Name = _launchName
                }
                : null;
            _gameLaunchBeHelper
                .DeleteLaunchAsync(_launchName, Arg.Any<ICancelable>(), Arg.Any<IAction>()).Returns(
                    new DeleteLaunchResult(gameLaunch, success));
        }

        void SetupGetCurrentGameLaunch(string testAccount, string gameletName)
        {
            _gameLaunchBeHelper.GetCurrentGameLaunchAsync(testAccount, Arg.Any<IAction>()).Returns(
                Task.FromResult(new GgpGrpc.Models.GameLaunch
                {
                    GameLaunchState = GameLaunchState.RunningGame,
                    GameletName = gameletName,
                    Name = _launchName
                }));
        }

        void SetupGetGameletApi(Gamelet gamelet, params GameletState[] states)
        {
            IEnumerable<Task<Gamelet>> gameletCopies = states.Select(state =>
            {
                Gamelet gameletCopy = gamelet.Clone();
                gameletCopy.State = state;
                return Task.FromResult(gameletCopy);
            }).ToList();
            _gameletClient.GetGameletByNameAsync(gamelet.Name)
                .Returns(gameletCopies.First(), gameletCopies.Skip(1).ToArray());
        }

        void SetupProcMountsContent(Gamelet gamelet, List<string> procMountsContent1,
                                    List<string> procMountsContent2)
        {
            _remoteCommand
                .RunWithSuccessCapturingOutputAsync(new SshTarget(gamelet),
                                                    GameletMountChecker.ReadMountsCmd)
                .Returns(Task.FromResult(procMountsContent1), Task.FromResult(procMountsContent2));
        }

        void AssertMetricRecorded(DeveloperEventType.Types.Type type,
                                  DeveloperEventStatus.Types.Code status)
        {
            _metrics.Received()
                .RecordEvent(
                    type,
                    Arg.Is<DeveloperLogEvent>(p => p.StatusCode == status &&
                                                  p.DebugSessionIdStr == _testDebugSessionId));
        }

        void SetupGameletClientApi(Gamelet gamelet)
        {
            SetupGetGameletApi(gamelet, GameletState.Reserved);
            _gameLaunchBeHelper.GetCurrentGameLaunchAsync(Arg.Any<string>(), Arg.Any<IAction>())
                .Returns(Task.FromResult((GgpGrpc.Models.GameLaunch) null));
        }
    }
}