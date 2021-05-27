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

using System.Diagnostics;
using GgpGrpc.Models;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.Metrics;
using YetiVSI.ProjectSystem.Abstractions;
using YetiVSI.Shared.Metrics;
using System.IO.Abstractions.TestingHelpers;

namespace YetiVSI.Test
{
    [TestFixture]
    class RemoteDeployTests
    {
        RemoteDeploy _remoteDeploy;
        IRemoteFile _remoteFile;
        IRemoteCommand _remoteCommand;
        ManagedProcess.Factory _managedProcessFactory;
        readonly string _binaryName = "Player.so";
        readonly string _chmodFailed = "Error running chmod a+x";
        readonly string _rsyncFailed = "Error copying files with ggp_rsync";

        [SetUp]
        public void SetUp()
        {
            _remoteFile = Substitute.For<IRemoteFile>();
            _remoteCommand = Substitute.For<IRemoteCommand>();
            _managedProcessFactory = Substitute.For<ManagedProcess.Factory>();
            
            _remoteDeploy = new RemoteDeploy(_remoteCommand, _remoteFile, _managedProcessFactory,
                                             new MockFileSystem());
        }

        [Test]
        public async Task DeployGameExecutableDeploysBinaryAndSetsExecutableBitAsync(
            [Values(DeployOnLaunchSetting.DELTA, DeployOnLaunchSetting.ALWAYS)]
            DeployOnLaunchSetting value)
        {
            string localPath = GetLocalPath();
            IAsyncProject project = GetProjectWithLocalPathAndDeployMode(localPath, value);
            (SshTarget target, IAction action, ICancelable cancelable) = GetDeploymentArguments();

            await _remoteDeploy.DeployGameExecutableAsync(project, target, cancelable, action);

            Assert.Multiple(async () =>
            {
                await _remoteFile.Received(1).SyncAsync(target, localPath,
                                                        YetiConstants.RemoteDeployPath, cancelable,
                                                        Arg.Any<bool>());

                await _remoteCommand.Received(1)
                    .RunWithSuccessAsync(
                        target, $"chmod a+x {YetiConstants.RemoteDeployPath}{_binaryName}");
            });
        }

        [Test]
        public async Task DeployGameExecutablePopulatesActionEventOnSuccessAsync(
            [Values(DeployOnLaunchSetting.DELTA, DeployOnLaunchSetting.ALWAYS)]
            DeployOnLaunchSetting value)
        {
            string localPath = GetLocalPath();
            IAsyncProject project = GetProjectWithLocalPathAndDeployMode(localPath, value);
            (SshTarget target, IAction action, ICancelable cancelable) = GetDeploymentArguments();

            await _remoteDeploy.DeployGameExecutableAsync(project, target, cancelable, action);

            Assert.Multiple(() =>
            {
                CopyBinaryData actionEvent = action.GetEvent().CopyExecutable;
                Assert.NotNull(actionEvent);
                Assert.NotNull(actionEvent.CopyBinaryBytes);
                Assert.IsTrue(actionEvent.CopyAttempted);
                Assert.That(actionEvent.CopyExitCode, Is.EqualTo(0));
                Assert.That(actionEvent.SshChmodExitCode, Is.EqualTo(0));
            });
        }

        [Test]
        public void DeployGameExecutablePopulatesActionEventOnFailureInDeployment(
            [Values(DeployOnLaunchSetting.DELTA, DeployOnLaunchSetting.ALWAYS)]
            DeployOnLaunchSetting value)
        {
            string localPath = GetLocalPath();
            IAsyncProject project = GetProjectWithLocalPathAndDeployMode(localPath, value);
            (SshTarget target, IAction action, ICancelable cancelable) = GetDeploymentArguments();

            _remoteFile
                .SyncAsync(Arg.Any<SshTarget>(), Arg.Any<string>(), Arg.Any<string>(),
                           Arg.Any<ICancelable>(), Arg.Any<bool>())
                .Returns<Task>(_ => throw new ProcessException(_rsyncFailed));

            var error = Assert.ThrowsAsync<DeployException>(
                async () =>
                    await _remoteDeploy.DeployGameExecutableAsync(
                        project, target, cancelable, action));
            Assert.Multiple(() =>
            {
                string expectedError = ErrorStrings.FailedToDeployExecutable(_rsyncFailed);
                Assert.That(error.Message, Is.EqualTo(expectedError));
                CopyBinaryData actionEvent = action.GetEvent().CopyExecutable;
                Assert.NotNull(actionEvent);
                Assert.NotNull(actionEvent.CopyBinaryBytes);
                Assert.IsTrue(actionEvent.CopyAttempted);
                Assert.That(actionEvent.CopyExitCode, Is.EqualTo(-1));
                Assert.IsNull(actionEvent.SshChmodExitCode);
            });
        }

        [Test]
        public void DeployGameExecutablePopulatesActionEventOnFailureInSettingExecutableBit(
            [Values(DeployOnLaunchSetting.DELTA, DeployOnLaunchSetting.ALWAYS)]
            DeployOnLaunchSetting value)
        {
            string localPath = GetLocalPath();
            IAsyncProject project = GetProjectWithLocalPathAndDeployMode(localPath, value);
            (SshTarget target, IAction action, ICancelable cancelable) = GetDeploymentArguments();

            _remoteCommand.RunWithSuccessAsync(Arg.Any<SshTarget>(), Arg.Any<string>())
                .Returns(_ => throw new ProcessException(_chmodFailed));

            var error = Assert.ThrowsAsync<DeployException>(
                async () => await _remoteDeploy
                    .DeployGameExecutableAsync(project, target, cancelable, action));
            Assert.Multiple(() =>
            {
                string expectedError =
                    ErrorStrings.FailedToSetExecutablePermissions(_chmodFailed);
                Assert.That(error.Message, Is.EqualTo(expectedError));
                CopyBinaryData actionEvent = action.GetEvent().CopyExecutable;
                Assert.NotNull(actionEvent);
                Assert.NotNull(actionEvent.CopyBinaryBytes);
                Assert.IsTrue(actionEvent.CopyAttempted);
                Assert.That(actionEvent.CopyExitCode, Is.EqualTo(0));
                Assert.That(actionEvent.SshChmodExitCode, Is.EqualTo(-1));
            });
        }

        [Test]
        public async Task DeployGameExecutableDoesNotCopyWhenSetToFalseAsync()
        {
            string localPath = GetLocalPath();
            IAsyncProject project =
                GetProjectWithLocalPathAndDeployMode(localPath, DeployOnLaunchSetting.FALSE);
            (SshTarget target, IAction action, ICancelable cancelable) = GetDeploymentArguments();

            await _remoteDeploy.DeployGameExecutableAsync(project, target, cancelable, action);

            Assert.Multiple(async () =>
            {
                await _remoteFile.Received(0).SyncAsync(Arg.Any<SshTarget>(), Arg.Any<string>(),
                                                        Arg.Any<string>(), Arg.Any<ICancelable>());

                await _remoteCommand.Received(0)
                    .RunWithSuccessAsync(Arg.Any<SshTarget>(), Arg.Any<string>());

                Assert.IsFalse(action.GetEvent().CopyExecutable.CopyAttempted);
            });
        }

        [Test]
        public async Task DeployLldbServerDeploysBinaryAndSetsExecutableBitAsync()
        {
            string remotePath = YetiConstants.LldbServerLinuxPath;
            string localPath = _remoteDeploy.GetLldbServerPath();
            (SshTarget target, IAction action, ICancelable _) = GetDeploymentArguments();

            await _remoteDeploy.DeployLldbServerAsync(target, action);

            Assert.Multiple(async () =>
            {
                await _remoteFile.Received(1)
                    .SyncAsync(target, localPath, remotePath, Arg.Any<ICancelable>());
                await _remoteCommand.Received(1).RunWithSuccessAsync(
                    target, $"chmod a+x {remotePath}{YetiConstants.LldbServerLinuxExecutable}");
            });
        }

        [Test]
        public async Task DeployLldbServerPopulatesActionEventOnSuccessAsync()
        {
            (SshTarget target, IAction action, var _) = GetDeploymentArguments();

            await _remoteDeploy.DeployLldbServerAsync(target, action);

            Assert.Multiple(() =>
            {
                CopyBinaryData actionEvent = action.GetEvent().CopyLldbServer;
                Assert.NotNull(actionEvent);
                Assert.NotNull(actionEvent.CopyBinaryBytes);
                Assert.IsTrue(actionEvent.CopyAttempted);
                Assert.That(actionEvent.CopyExitCode, Is.EqualTo(0));
                Assert.That(actionEvent.SshChmodExitCode, Is.EqualTo(0));
            });
        }

        [Test]
        public void DeployLldbServerPopulatesActionEventOnFailureInSettingExecutableBit()
        {
            (SshTarget target, IAction action, var _) = GetDeploymentArguments();

            _remoteCommand.RunWithSuccessAsync(Arg.Any<SshTarget>(), Arg.Any<string>())
                .Returns(_ => throw new ProcessException(_chmodFailed));

            var error = Assert.ThrowsAsync<DeployException>(
                async () => await _remoteDeploy.DeployLldbServerAsync(target, action));

            Assert.Multiple(() =>
            {
                string expectedError =
                    ErrorStrings.FailedToSetExecutablePermissions(_chmodFailed);
                Assert.That(error.Message, Is.EqualTo(expectedError));
                CopyBinaryData actionEvent = action.GetEvent().CopyLldbServer;
                Assert.NotNull(actionEvent);
                Assert.NotNull(actionEvent.CopyBinaryBytes);
                Assert.IsTrue(actionEvent.CopyAttempted);
                Assert.That(actionEvent.CopyExitCode, Is.EqualTo(0));
                Assert.That(actionEvent.SshChmodExitCode, Is.EqualTo(-1));
            });
        }

        [Test]
        public void DeployLldbServerPopulatesActionEventOnFailureInDeployment()
        {
            (SshTarget target, IAction action, var _) = GetDeploymentArguments();
            _remoteFile
                .SyncAsync(Arg.Any<SshTarget>(), Arg.Any<string>(), Arg.Any<string>(),
                           Arg.Any<ICancelable>(), Arg.Any<bool>())
                .Returns(_ => throw new ProcessException(_rsyncFailed));

            var error = Assert.ThrowsAsync<DeployException>(
                async () => await _remoteDeploy.DeployLldbServerAsync(target, action));

            Assert.Multiple(() =>
            {
                string expectedError = ErrorStrings.FailedToDeployExecutable(_rsyncFailed);
                Assert.That(error.Message, Is.EqualTo(expectedError));
                CopyBinaryData actionEvent = action.GetEvent().CopyLldbServer;
                Assert.NotNull(actionEvent);
                Assert.NotNull(actionEvent.CopyBinaryBytes);
                Assert.IsTrue(actionEvent.CopyAttempted);
                Assert.That(actionEvent.CopyExitCode, Is.EqualTo(-1));
                Assert.That(actionEvent.SshChmodExitCode, Is.EqualTo(null));
            });
        }

        [Test]
        public async Task ExecuteCustomCommandWhenRunsSuccessfullyAsync()
        {
            string command = "ls '/mnt/developer'";
            string rootPath = @"c:\src\game\bin";
            (SshTarget _, IAction action, ICancelable _) = GetDeploymentArguments();
            IProcess process = GetCustomDeployProcess();
            IAsyncProject project = GetProject();
            var gamelet = new Gamelet();

            await _remoteDeploy.ExecuteCustomCommandAsync(project, gamelet, action);

            Assert.Multiple(async () =>
            {
                await process.Received(1).RunToExitAsync();
                Assert.IsTrue(action.GetEvent().CustomCommand.CustomCommandAttempted);
                Assert.IsTrue(action.GetEvent().CustomCommand.CustomCommandSucceeded);
            });

            IAsyncProject GetProject()
            {
                var local = Substitute.For<IAsyncProject>();
                local.GetCustomDeployOnLaunchAsync().Returns(command);
                local.GetAbsoluteRootPathAsync().Returns(rootPath);
                return local;
            }

            IProcess GetCustomDeployProcess()
            {
                var local = Substitute.For<IProcess>();
                _managedProcessFactory.CreateVisible(
                        Arg.Is<ProcessStartInfo>(x => x.FileName.EndsWith(YetiConstants.Command) &&
                                                     x.Arguments.Equals($"/C \"{command}\"") &&
                                                     x.WorkingDirectory == rootPath), int.MaxValue)
                    .Returns(local);
                return local;
            }
        }

        string GetLocalPath() => $@"c:\src\game\bin\{_binaryName}";

        IAsyncProject GetProjectWithLocalPathAndDeployMode(string localPath,
                                                           DeployOnLaunchSetting
                                                               deployOnLaunchSetting)
        {
            var project = Substitute.For<IAsyncProject>();
            project.GetDeployOnLaunchAsync().Returns(deployOnLaunchSetting);
            project.GetTargetPathAsync().Returns(localPath);
            return project;
        }

        (SshTarget target, IAction action, ICancelable task) GetDeploymentArguments()
        {
            var target = new SshTarget(new Gamelet { Id = "instance_id", IpAddr = "1.2.3.4" });
            var cancelable = Substitute.For<ICancelableTask>();
            IAction action = new Action(DeveloperEventType.Types.Type.VsiDeployBinary,
                                        Substitute.For<Timer.Factory>(),
                                        Substitute.For<IMetrics>());

            return (target, action, cancelable);
        }
    }
}