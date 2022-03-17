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

using GgpGrpc.Models;
using NSubstitute;
using NUnit.Framework;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using Metrics.Shared;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.Metrics;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiVSI.Test
{
    [TestFixture]
    class RemoteDeployTests
    {
        readonly string _binaryName = "Player.so";
        readonly string _chmodFailed = "Error running chmod a+x";
        readonly string _rsyncFailed = "Error copying files with ggp_rsync";

        [Test]
        public async Task DeployGameExecutableDeploysBinaryAndSetsExecutableBitAsync(
            [Values(DeployOnLaunchSetting.DELTA, DeployOnLaunchSetting.ALWAYS)]
            DeployOnLaunchSetting value)
        {
            string localPath = GetLocalPath();
            IAsyncProject project = GetProjectWithLocalPathAndDeployMode(localPath, value);
            (SshTarget target, IAction action, ICancelable cancelable) = GetDeploymentArguments();
            (IRemoteFile file, IRemoteCommand command, IRemoteDeploy deploy, _) = GetTestObjects();

            await deploy.DeployGameExecutableAsync(project, target, cancelable, action);

            Assert.Multiple(async () =>
            {
                await file.Received(1).SyncAsync(target, localPath,
                                                        YetiConstants.RemoteDeployPath, cancelable,
                                                        Arg.Any<bool>());

                await command.Received(1)
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
            (_, _, IRemoteDeploy deploy, _) = GetTestObjects();
            await deploy.DeployGameExecutableAsync(project, target, cancelable, action);

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
            (IRemoteFile file, _, IRemoteDeploy deploy, _) = GetTestObjects();
            file
                .SyncAsync(Arg.Any<SshTarget>(), Arg.Any<string>(), Arg.Any<string>(),
                           Arg.Any<ICancelable>(), Arg.Any<bool>())
                .Returns<Task>(_ => throw new ProcessException(_rsyncFailed));

            var error = Assert.ThrowsAsync<DeployException>(
                async () =>
                    await deploy.DeployGameExecutableAsync(
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
            (_, IRemoteCommand command, IRemoteDeploy deploy, _) = GetTestObjects();
            command.RunWithSuccessAsync(Arg.Any<SshTarget>(), Arg.Any<string>())
                .Returns(_ => throw new ProcessException(_chmodFailed));

            var error = Assert.ThrowsAsync<DeployException>(
                async () => await deploy
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
            (IRemoteFile file, IRemoteCommand command, IRemoteDeploy deploy, _) = GetTestObjects();
            await deploy.DeployGameExecutableAsync(project, target, cancelable, action);

            Assert.Multiple(async () =>
            {
                await file.Received(0).SyncAsync(Arg.Any<SshTarget>(), Arg.Any<string>(),
                                                        Arg.Any<string>(), Arg.Any<ICancelable>());

                await command.Received(0)
                    .RunWithSuccessAsync(Arg.Any<SshTarget>(), Arg.Any<string>());

                Assert.IsFalse(action.GetEvent().CopyExecutable.CopyAttempted);
            });
        }


        [Test]
        public async Task DeployGamePopulatesDeploymentModeAsync(
            [Values(DeployOnLaunchSetting.DELTA, DeployOnLaunchSetting.ALWAYS)]
            DeployOnLaunchSetting value, [Values] bool commandSucceeds)
        {
            string localPath = GetLocalPath();
            IAsyncProject project = GetProjectWithLocalPathAndDeployMode(localPath, value);
            (SshTarget target, IAction action, ICancelable cancelable) = GetDeploymentArguments();
            (_, IRemoteCommand command, IRemoteDeploy deploy, _) = GetTestObjects();

            if (!commandSucceeds)
            {
                command.RunWithSuccessAsync(Arg.Any<SshTarget>(), Arg.Any<string>())
                    .Returns(_ => throw new ProcessException(_chmodFailed));
            }

            try
            {
                await deploy.DeployGameExecutableAsync(project, target, cancelable, action);
            }
            catch (DeployException)
            {
                Assert.IsFalse(commandSucceeds);
            }

            CopyBinaryData actionEvent = action.GetEvent().CopyExecutable;
            Assert.That(actionEvent.DeploymentMode,
                Is.EqualTo(CopyBinaryType.Types.DeploymentMode.GgpRsync));
        }

        [Test]
        public async Task DeployGameSetToNeverDoesNotPopulateDeploymentModeAsync()
        {
            string localPath = GetLocalPath();
            IAsyncProject project = GetProjectWithLocalPathAndDeployMode(localPath,
                DeployOnLaunchSetting.FALSE);
            (SshTarget target, IAction action, ICancelable cancelable) = GetDeploymentArguments();
            (_, _, IRemoteDeploy deploy, _) = GetTestObjects();

            await deploy.DeployGameExecutableAsync(project, target, cancelable, action);

            CopyBinaryData actionEvent = action.GetEvent().CopyExecutable;
            Assert.That(actionEvent.DeploymentMode, Is.Null);
        }

        [TestCase(DeployOnLaunchSetting.ALWAYS, BinarySignatureCheck.Types.Result.AlwaysCopy)]
        [TestCase(DeployOnLaunchSetting.FALSE, BinarySignatureCheck.Types.Result.NoCopy)]
        [TestCase(DeployOnLaunchSetting.DELTA, BinarySignatureCheck.Types.Result.YesCopy)]
        public async Task DeployGamePopulatesSignatureCheckModeAsync(
            DeployOnLaunchSetting deploySetting,
            BinarySignatureCheck.Types.Result signatureCheck)
        {
            string localPath = GetLocalPath();
            IAsyncProject project = GetProjectWithLocalPathAndDeployMode(localPath, deploySetting);
            (SshTarget target, IAction action, ICancelable cancelable) = GetDeploymentArguments();
            (_, _, IRemoteDeploy deploy, _) = GetTestObjects();

            await deploy.DeployGameExecutableAsync(project, target, cancelable, action);

            CopyBinaryData actionEvent = action.GetEvent().CopyExecutable;
            Assert.That(actionEvent.SignatureCheckResult, Is.EqualTo(signatureCheck));
        }

        [Test]
        public async Task DeployLldbServerDeploysBinaryAndSetsExecutableBitAsync()
        {
            string remotePath = YetiConstants.LldbServerLinuxPath;
            (IRemoteFile file, IRemoteCommand command, RemoteDeploy deploy, _) =
                GetTestObjects();
            string localPath = deploy.GetLldbServerPath();
            (SshTarget target, IAction action, ICancelable _) = GetDeploymentArguments();

            await deploy.DeployLldbServerAsync(target, action);

            Assert.Multiple(async () =>
            {
                await file.Received(1)
                    .SyncAsync(target, localPath, remotePath, Arg.Any<ICancelable>());
                await command.Received(1).RunWithSuccessAsync(
                    target, $"chmod a+x {remotePath}{YetiConstants.LldbServerLinuxExecutable}");
            });
        }

        [Test]
        public async Task DeployLldbServerPopulatesActionEventOnSuccessAsync()
        {
            (SshTarget target, IAction action, var _) = GetDeploymentArguments();
            (_, _, RemoteDeploy deploy, _) = GetTestObjects();
            
            await deploy.DeployLldbServerAsync(target, action);

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
            (_, IRemoteCommand command, RemoteDeploy deploy, _) = GetTestObjects();
            command.RunWithSuccessAsync(Arg.Any<SshTarget>(), Arg.Any<string>())
                .Returns(_ => throw new ProcessException(_chmodFailed));

            var error = Assert.ThrowsAsync<DeployException>(
                async () => await deploy.DeployLldbServerAsync(target, action));

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
            (IRemoteFile file, _, RemoteDeploy deploy, _) = GetTestObjects();
            file
                .SyncAsync(Arg.Any<SshTarget>(), Arg.Any<string>(), Arg.Any<string>(),
                           Arg.Any<ICancelable>(), Arg.Any<bool>())
                .Returns(_ => throw new ProcessException(_rsyncFailed));

            var error = Assert.ThrowsAsync<DeployException>(
                async () => await deploy.DeployLldbServerAsync(target, action));

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
        public async Task DeployVulkanLayerDeploysExecutableAndManifestAsync()
        {
            string remotePath = YetiConstants.OrbitVulkanLayerLinuxPath;
            (IRemoteFile file, IRemoteCommand command, RemoteDeploy deploy, _) = GetTestObjects();
            string localOrbitCollectorDir =
                Path.Combine(SDKUtil.GetSDKPath(), YetiConstants.OrbitCollectorDir);
            string localManifestPath =
                Path.Combine(localOrbitCollectorDir, YetiConstants.OrbitVulkanLayerManifest);
            string localLayerPath =
                Path.Combine(localOrbitCollectorDir, YetiConstants.OrbitVulkanLayerExecutable);
            (SshTarget target, IAction _, ICancelable task) = GetDeploymentArguments();

            var project = GetProjectWithOrbitVulkanLayerDeployMode(true);
            await deploy.DeployOrbitVulkanLayerAsync(project, target, task);

            Assert.Multiple(async () => {
                await file.Received(1).SyncAsync(target, localManifestPath, remotePath, task);
                await file.Received(1).SyncAsync(target, localLayerPath, remotePath, task);
            });
        }

        [Test]
        public async Task DeployVulkanLayerDoesNotDeploysExecutableAndManifestIfDisabledAsync()
        {
            (IRemoteFile file, IRemoteCommand command, RemoteDeploy deploy, _) = GetTestObjects();
            (SshTarget target, IAction _, ICancelable task) = GetDeploymentArguments();

            var project = GetProjectWithOrbitVulkanLayerDeployMode(false);
            await deploy.DeployOrbitVulkanLayerAsync(project, target, task);

            Assert.Multiple(() => { file.Received(0); });
        }

        [Test]
        public void DeployVulkanLAyerThrowsExceptionOnFailureInManifestDeployment()
        {
            string localManifestPath =
                Path.Combine(SDKUtil.GetSDKPath(), YetiConstants.OrbitCollectorDir,
                             YetiConstants.OrbitVulkanLayerManifest);
            (SshTarget target, IAction _, ICancelable task) = GetDeploymentArguments();
            (IRemoteFile file, _, RemoteDeploy deploy, _) = GetTestObjects();
            file.SyncAsync(target, localManifestPath, Arg.Any<string>(), task, Arg.Any<bool>())
                .Returns(_ => throw new ProcessException(_rsyncFailed));

            var project = GetProjectWithOrbitVulkanLayerDeployMode(true);
            var error = Assert.ThrowsAsync<DeployException>(
                async () => await deploy.DeployOrbitVulkanLayerAsync(project, target, task));

            string expectedError = ErrorStrings.FailedToDeployExecutable(_rsyncFailed);
            Assert.That(error.Message, Is.EqualTo(expectedError));
        }

        [Test]
        public void DeployVulkanLAyerThrowsExceptionOnFailureInExecutableDeployment()
        {
            string localLayerPath =
                Path.Combine(SDKUtil.GetSDKPath(), YetiConstants.OrbitCollectorDir,
                             YetiConstants.OrbitVulkanLayerExecutable);
            (SshTarget target, IAction _, ICancelable task) = GetDeploymentArguments();
            (IRemoteFile file, _, RemoteDeploy deploy, _) = GetTestObjects();
            file.SyncAsync(target, localLayerPath, Arg.Any<string>(), task, Arg.Any<bool>())
                .Returns(_ => throw new ProcessException(_rsyncFailed));

            var project = GetProjectWithOrbitVulkanLayerDeployMode(true);
            var error = Assert.ThrowsAsync<DeployException>(
                async () => await deploy.DeployOrbitVulkanLayerAsync(project, target, task));

            string expectedError = ErrorStrings.FailedToDeployExecutable(_rsyncFailed);
            Assert.That(error.Message, Is.EqualTo(expectedError));
        }

        [Test]
        public async Task ExecuteCustomCommandWhenRunsSuccessfullyAsync()
        {
            string command = "ls '/mnt/developer'";
            string rootPath = @"c:\src\game\bin";
            (SshTarget _, IAction action, ICancelable _) = GetDeploymentArguments();
            (_, _, RemoteDeploy deploy, ManagedProcess.Factory factory) = GetTestObjects();
            IProcess process = GetCustomDeployProcess(factory);
            IAsyncProject project = GetProject();
            var gamelet = new Gamelet();

            await deploy.ExecuteCustomCommandAsync(project, gamelet, action);

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

            IProcess GetCustomDeployProcess(ManagedProcess.Factory managedProcessFactory)
            {
                var local = Substitute.For<IProcess>();
                managedProcessFactory.CreateVisible(
                        Arg.Is<ProcessStartInfo>(x => x.FileName.EndsWith(YetiConstants.Command) &&
                                                     x.Arguments.Equals($"/C \"{command}\"") &&
                                                     x.WorkingDirectory == rootPath), int.MaxValue)
                    .Returns(local);
                return local;
            }
        }


        public (IRemoteFile file, IRemoteCommand command, RemoteDeploy deploy,
            ManagedProcess.Factory factory) GetTestObjects()
        {
            var remoteFile = Substitute.For<IRemoteFile>();
            var remoteCommand = Substitute.For<IRemoteCommand>();
            var managedProcessFactory = Substitute.For<ManagedProcess.Factory>();

            var remoteDeploy = new RemoteDeploy(remoteCommand, remoteFile, managedProcessFactory,
                                             new MockFileSystem());

            return (remoteFile, remoteCommand, remoteDeploy, managedProcessFactory);
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

        IAsyncProject GetProjectWithOrbitVulkanLayerDeployMode(bool deployOrbitVulkanLayerOnLaunch)
        {
            var project = Substitute.For<IAsyncProject>();
            project.GetDeployOrbitVulkanLayerOnLaunch().Returns(deployOrbitVulkanLayerOnLaunch);
            return project;
        }

        (SshTarget target, IAction action, ICancelable task) GetDeploymentArguments()
        {
            var target = new SshTarget(new Gamelet { Id = "instance_id", IpAddr = "1.2.3.4" });
            var cancelable = Substitute.For<ICancelableTask>();
            IAction action = new Action(DeveloperEventType.Types.Type.VsiDeployBinary,
                                        Substitute.For<Timer.Factory>(),
                                        Substitute.For<IVsiMetrics>());

            return (target, action, cancelable);
        }
    }
}