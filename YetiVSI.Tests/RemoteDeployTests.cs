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
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using YetiCommon.VSProject;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.Test
{
    [TestFixture]
    class RemoteDeployTests
    {
        const string TEST_GAMELET_ID = "gameletid";
        const string TEST_GAMELET_IP = "1.2.3.4";
        const string TEST_TARGET_DIR = "C:\\testtargetdir";
        const string TEST_TARGET_FILE_NAME = "testtargetfilename";

        static readonly string TEST_TARGET_PREVIOUS_FILE_NAME = "testtargetfilename" +
            YetiConstants.PreviousExecutableSuffix;

        static readonly string TEST_TARGET_DELTA_FILE_NAME = "testtargetfilename" +
            YetiConstants.ExecutableDeltaSuffix;

        const string TEST_TARGET_PATH = TEST_TARGET_DIR + "\\" + TEST_TARGET_FILE_NAME;

        static readonly string TEST_TARGET_PREVIOUS_PATH = TEST_TARGET_DIR + "\\" +
            TEST_TARGET_PREVIOUS_FILE_NAME;

        static readonly string TEST_TARGET_DELTA_PATH = TEST_TARGET_DIR + "\\" +
            TEST_TARGET_DELTA_FILE_NAME;

        const string TEST_CUSTOM_COMMAND = "custom deploy command";
        const string TEST_ABSOLUTE_ROOT_PATH = "absolute\\root\\path";
        string TEST_LLDB_SERVER_PATH;
        string TEST_XDELTA_FOR_GAMELET_PATH;
        string TEST_TARGET_DIR_REMOTE = YetiConstants.RemoteDeployPath;

        string TEST_TARGET_PATH_REMOTE = Path.Combine(YetiConstants.RemoteDeployPath,
                                                      TEST_TARGET_FILE_NAME);

        readonly string TEST_LLDB_SERVER_PATH_REMOTE = Path.Combine(
            YetiConstants.LldbServerLinuxPath, YetiConstants.LldbServerLinuxExecutable);

        readonly string TEST_XDELTA_FOR_GAMELET_PATH_REMOTE = Path.Combine(
            YetiConstants.RemoteToolsBinDir, YetiConstants.XDeltaLinuxExecutable);

        readonly string TEST_TARGET_PATH_SIGNATURE =
            TEST_TARGET_PATH + YetiConstants.BinarySignatureSuffix;

        readonly MockFileData MockDeltaExeFileData = new MockFileData("3");

        readonly MockFileData MockPreviousExeFileData = new MockFileData("abc12");

        readonly MockFileData MockExeFileData = new MockFileData("abc123");
        const long MockExeFileLength = 6;

        readonly MockFileData MockLldbServerFileData = new MockFileData("abcde12345");
        const int MockLldbServerFileLength = 10;

        readonly MockFileData MockXDeltaForGameletFileData = new MockFileData("xdelta123");
        const int MockXDeltaForGameletFileLength = 9;

        readonly MockFileData MockXDeltaWindowsExecutable = new MockFileData("xdelta windows");
        readonly MockFileData MockXDeltaLinuxExecutable = new MockFileData("xdelta linux");
        readonly MockFileData MockPigzWindowsExecutable = new MockFileData("pigz");

        IRemoteFile remoteFile;
        IRemoteCommand remoteCommand;
        ManagedProcess.Factory managedProcessFactory;
        IBinaryFileUtil binaryFileUtil;
        IAsyncProject project;
        IProgress<string> progress;
        ICancelableTask task;

        MockFileSystem fileSystem;

        Gamelet gamelet = new Gamelet { Id = TEST_GAMELET_ID, IpAddr = TEST_GAMELET_IP };
        SshTarget target;
        BuildId buildId = new BuildId("2690EE47B4594F0628340990A2FEAA42");
        BuildId previousBuildId = new BuildId("2690EE47B4594F0628340990A2FEAA41");

        IAction action = new YetiVSI.Metrics.Action(DeveloperEventType.Types.Type.VsiDeployBinary,
                                                    Substitute.For<Timer.Factory>(),
                                                    Substitute.For<IMetrics>());

        RemoteDeploy remoteDeploy;

        [SetUp]
        public void SetUp()
        {
            remoteFile = Substitute.For<IRemoteFile>();
            remoteCommand = Substitute.For<IRemoteCommand>();
            managedProcessFactory = Substitute.For<ManagedProcess.Factory>();
            binaryFileUtil = Substitute.For<IBinaryFileUtil>();

            project = Substitute.For<IAsyncProject>();
            project.GetTargetDirectoryAsync().Returns(TEST_TARGET_DIR);
            project.GetTargetPathAsync().Returns(TEST_TARGET_PATH);
            project.GetTargetFileNameAsync().Returns(TEST_TARGET_FILE_NAME);
            project.GetAbsoluteRootPathAsync().Returns(TEST_ABSOLUTE_ROOT_PATH);

            fileSystem = new MockFileSystem();
            fileSystem.AddFile(TEST_TARGET_PATH, MockExeFileData);
            fileSystem.AddFile(YetiConstants.PigzWinExecutablePath, MockPigzWindowsExecutable);
            fileSystem.AddFile(YetiConstants.XDeltaWinExecutablePath, MockXDeltaWindowsExecutable);
            fileSystem.AddFile(YetiConstants.XDeltaLinuxExecutablePath, MockXDeltaLinuxExecutable);

            target = new SshTarget(gamelet);

            remoteDeploy = new RemoteDeploy(remoteCommand, remoteFile, managedProcessFactory,
                                            fileSystem, binaryFileUtil);
            TEST_LLDB_SERVER_PATH = remoteDeploy.GetLldbServerPath();
            fileSystem.AddFile(TEST_LLDB_SERVER_PATH, MockLldbServerFileData);

            TEST_XDELTA_FOR_GAMELET_PATH =
                Path.Combine(YetiConstants.XDeltaLinuxDir, YetiConstants.XDeltaLinuxExecutable);
            fileSystem.AddFile(TEST_XDELTA_FOR_GAMELET_PATH, MockXDeltaForGameletFileData);

            progress = new Progress<string>();
            task = Substitute.For<ICancelableTask>();
            task.Progress.Returns(progress);
        }

        [Test]
        public async Task DeployNothingAsync()
        {
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.FALSE);
            project.GetCustomDeployOnLaunchAsync().Returns("");
            // local and remote lldb-server files have the same build ids
            binaryFileUtil.ReadBuildIdAsync(TEST_LLDB_SERVER_PATH).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_LLDB_SERVER_PATH_REMOTE, target).Returns(buildId);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);
            await remoteDeploy.DeployLldbServerAsync(target, action);
            await remoteDeploy.ExecuteCustomCommandAsync(project, gamelet, action);

            await remoteFile.DidNotReceive().PutAsync(
                Arg.Any<SshTarget>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<DeployCompression>(), Arg.Any<IIncrementalProgress>(),
                Arg.Any<ICancelableTask>());
            await remoteCommand.Received().RunWithSuccessAsync(
                target, $"chmod a+x {YetiConstants.LldbServerLinuxPath}" +
                            $"{YetiConstants.LldbServerLinuxExecutable}");
            managedProcessFactory.DidNotReceive().Create(Arg.Any<ProcessStartInfo>(),
                                                         Arg.Any<int>());

            // Always record gamelet data. Only need to verify this once.
            Assert.AreEqual(GameletData.FromGamelet(gamelet), action.GetEvent().GameletData);
            Assert.IsFalse(action.GetEvent().CopyExecutable.CopyAttempted);
            Assert.IsFalse(action.GetEvent().CustomCommand.CustomCommandAttempted);
            Assert.IsTrue(action.GetEvent().CopyLldbServer.CopyAttempted);
        }

        [TestCase(DeployCompressionSetting.Compressed, DeployCompression.Compressed,
                  CopyBinaryType.Types.DeploymentMode.Compressed)]
        [TestCase(DeployCompressionSetting.Uncompressed, DeployCompression.Uncompressed,
                  CopyBinaryType.Types.DeploymentMode.Uncompressed)]
        public async Task DeployExeAsync(DeployCompressionSetting compressionSetting,
                                         DeployCompression compression,
                                         CopyBinaryType.Types.DeploymentMode deploymentMode)
        {
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.ALWAYS);
            project.GetDeployCompressionAsync().Returns(compressionSetting);

            remoteFile
                .PutAsync(target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE, compression,
                          Arg.Any<IIncrementalProgress>(), Arg.Any<ICancelableTask>())
                .Returns(MockExeFileLength);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);

            await remoteFile.Received().PutAsync(target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE,
                                                 compression, Arg.Any<IIncrementalProgress>(),
                                                 Arg.Any<ICancelableTask>());
            await remoteCommand.Received().RunWithSuccessAsync(
                target, $"chmod a+x {YetiConstants.RemoteDeployPath}{TEST_TARGET_FILE_NAME}");

            managedProcessFactory.DidNotReceive().Create(Arg.Any<ProcessStartInfo>(),
                                                         Arg.Any<int>());

            Assert.IsTrue(action.GetEvent().CopyExecutable.CopyAttempted);
            Assert.AreEqual(MockExeFileLength, action.GetEvent().CopyExecutable.CopyBinaryBytes);
            Assert.AreEqual(0, action.GetEvent().CopyExecutable.CopyExitCode);
            Assert.AreEqual(0, action.GetEvent().CopyExecutable.SshChmodExitCode);
            Assert.AreEqual(deploymentMode, action.GetEvent().CopyExecutable.DeploymentMode);
            Assert.AreEqual(MockExeFileLength,
                            action.GetEvent().CopyExecutable.TransferredBinaryBytes);
        }

        [Test]
        public void DeployExeCompressionFails()
        {
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.ALWAYS);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Compressed);

            string exceptionString = "test exception";
            remoteFile
                .PutAsync(target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE,
                          DeployCompression.Compressed, Arg.Any<IIncrementalProgress>(),
                          Arg.Any<ICancelableTask>())
                .Returns<Task<long>>(x => { throw new CompressedCopyException(exceptionString); });

            DeployException ex = Assert.ThrowsAsync<DeployException>(
                () => remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action));

            Assert.IsTrue(action.GetEvent().CopyExecutable.CopyAttempted);
            Assert.AreEqual(MockExeFileLength, action.GetEvent().CopyExecutable.CopyBinaryBytes);
            Assert.AreEqual(-1, action.GetEvent().CopyExecutable.CopyExitCode);
            Assert.AreEqual(ex.Message, ErrorStrings.FailedToDeployExecutable(exceptionString));
        }

        [TestCase(DeployCompressionSetting.Compressed)]
        [TestCase(DeployCompressionSetting.Uncompressed)]
        public void DeployExeNoDevShape(DeployCompressionSetting compressionSetting)
        {
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.ALWAYS);
            project.GetDeployCompressionAsync().Returns(compressionSetting);

            BuildMockCustomTestMntDeveloperProcess(new string[] { "MISSING" });

            remoteFile
                .PutAsync(target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE,
                          Arg.Any<DeployCompression>(), Arg.Any<IIncrementalProgress>(),
                          Arg.Any<ICancelableTask>())
                .Returns<Task<long>>(x => { throw new CompressedCopyException("test exception"); });

            DeployException ex = Assert.ThrowsAsync<DeployException>(
                () => remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action));

            Assert.That(ex.Message,
                        Is.EqualTo(ErrorStrings.FailedToDeployExecutableMissingUploadDir(
                            YetiConstants.RemoteDeployPath)));
        }

        [Test]
        public void DeployExeFails()
        {
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.ALWAYS);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Uncompressed);

            string exceptionString = "test exception";
            remoteFile
                .PutAsync(target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE,
                          DeployCompression.Uncompressed, Arg.Any<IIncrementalProgress>(),
                          Arg.Any<ICancelableTask>())
                .Returns<Task<long>>(
                    x => { throw new ProcessExecutionException(exceptionString, 1); });

            DeployException ex = Assert.ThrowsAsync<DeployException>(
                () => remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action));

            Assert.IsTrue(action.GetEvent().CopyExecutable.CopyAttempted);
            Assert.AreEqual(MockExeFileLength, action.GetEvent().CopyExecutable.CopyBinaryBytes);
            Assert.AreEqual(1, action.GetEvent().CopyExecutable.CopyExitCode);
            Assert.AreEqual(ex.Message, ErrorStrings.FailedToDeployExecutable(exceptionString));
        }

        [Test]
        public async Task DeployLldbServerAsync()
        {
            // read request for remote lldb-server build id throws exception
            binaryFileUtil.ReadBuildIdAsync(TEST_LLDB_SERVER_PATH_REMOTE, target)
                .Returns<Task<BuildId>>(
                    x => throw SignatureCheckExceptionForErrorType(
                        SignatureErrorType.BINARY_ERROR));

            await remoteDeploy.DeployLldbServerAsync(target, action);

            await remoteFile.Received().PutAsync(
                target, TEST_LLDB_SERVER_PATH, TEST_LLDB_SERVER_PATH_REMOTE,
                DeployCompression.Uncompressed, Arg.Any<IIncrementalProgress>(),
                Arg.Any<ICancelable>());
            await remoteCommand.Received().RunWithSuccessAsync(
                target, $"chmod a+x {YetiConstants.LldbServerLinuxPath}" +
                            $"{YetiConstants.LldbServerLinuxExecutable}");
            managedProcessFactory.DidNotReceive().Create(Arg.Any<ProcessStartInfo>(),
                                                         Arg.Any<int>());

            Assert.IsTrue(action.GetEvent().CopyLldbServer.CopyAttempted);
            Assert.AreEqual(MockLldbServerFileLength,
                            action.GetEvent().CopyLldbServer.CopyBinaryBytes);
            Assert.AreEqual(0, action.GetEvent().CopyLldbServer.CopyExitCode);
            Assert.AreEqual(0, action.GetEvent().CopyLldbServer.SshChmodExitCode);
        }

        [Test]
        public void DeployLldbServerFails()
        {
            binaryFileUtil.ReadBuildIdAsync(TEST_LLDB_SERVER_PATH_REMOTE, target).Throws(
                SignatureCheckExceptionForErrorType(SignatureErrorType.BINARY_ERROR));

            // copying lldb-file to gamelet throws exception
            remoteFile
                .PutAsync(target, TEST_LLDB_SERVER_PATH, TEST_LLDB_SERVER_PATH_REMOTE,
                          DeployCompression.Uncompressed, Arg.Any<IIncrementalProgress>(),
                          Arg.Any<ICancelable>())
                .Returns<Task<long>>(
                    x => { throw new ProcessExecutionException("test exception", 1); });

            Assert.ThrowsAsync<DeployException>(
                () => remoteDeploy.DeployLldbServerAsync(target, action));

            Assert.IsTrue(action.GetEvent().CopyLldbServer.CopyAttempted);
            Assert.AreEqual(MockLldbServerFileLength,
                            action.GetEvent().CopyLldbServer.CopyBinaryBytes);
            Assert.AreEqual(1, action.GetEvent().CopyLldbServer.CopyExitCode);
        }

        [Test]
        public void DeployLldbServerFailToSetPermissions()
        {
            // read request for remote lldb-server build id throws exception
            binaryFileUtil.ReadBuildIdAsync(TEST_LLDB_SERVER_PATH_REMOTE, target).Throws(
                SignatureCheckExceptionForErrorType(SignatureErrorType.BINARY_ERROR));

            remoteCommand
                .RunWithSuccessAsync(target, $"chmod a+x {YetiConstants.LldbServerLinuxPath}" +
                                                 $"{YetiConstants.LldbServerLinuxExecutable}")
                .Returns(x => { throw new ProcessExecutionException("test exception", 1); });

            Assert.ThrowsAsync<DeployException>(
                () => remoteDeploy.DeployLldbServerAsync(target, action));

            Assert.IsTrue(action.GetEvent().CopyLldbServer.CopyAttempted);
            Assert.AreEqual(MockLldbServerFileLength,
                            action.GetEvent().CopyLldbServer.CopyBinaryBytes);
            Assert.AreEqual(0, action.GetEvent().CopyLldbServer.CopyExitCode);
            Assert.AreEqual(1, action.GetEvent().CopyLldbServer.SshChmodExitCode);
        }

        [Test]
        public void DeployExeFailToSetPermissions()
        {
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.ALWAYS);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Uncompressed);

            remoteCommand
                .RunWithSuccessAsync(
                    target, $"chmod a+x {YetiConstants.RemoteDeployPath}{TEST_TARGET_FILE_NAME}")
                .Returns(x => { throw new ProcessExecutionException("test exception", 1); });

            Assert.ThrowsAsync<DeployException>(
                () => remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action));

            Assert.IsTrue(action.GetEvent().CopyExecutable.CopyAttempted);
            Assert.AreEqual(MockExeFileLength, action.GetEvent().CopyExecutable.CopyBinaryBytes);
            Assert.AreEqual(0, action.GetEvent().CopyExecutable.CopyExitCode);
            Assert.AreEqual(1, action.GetEvent().CopyExecutable.SshChmodExitCode);
            Assert.AreEqual(CopyBinaryType.Types.DeploymentMode.Uncompressed,
                            action.GetEvent().CopyExecutable.DeploymentMode);
        }

        [Test]
        public async Task ExecuteCustomAsync()
        {
            project.GetCustomDeployOnLaunchAsync().Returns(TEST_CUSTOM_COMMAND);

            var process = MockCustomDeployProcess();
            await remoteDeploy.ExecuteCustomCommandAsync(project, gamelet, action);

            await remoteFile.DidNotReceive().PutAsync(
                Arg.Any<SshTarget>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<DeployCompression>(), Arg.Any<IIncrementalProgress>(),
                Arg.Any<ICancelableTask>());
            await remoteCommand.DidNotReceive().RunWithSuccessAsync(
                Arg.Any<SshTarget>(), Arg.Any<string>());

            await process.Received().RunToExitAsync();

            Assert.IsTrue(action.GetEvent().CustomCommand.CustomCommandAttempted);
            Assert.IsTrue(action.GetEvent().CustomCommand.CustomCommandSucceeded);
        }

        [Test]
        public void ExecuteCustomFails()
        {
            project.GetCustomDeployOnLaunchAsync().Returns(TEST_CUSTOM_COMMAND);

            var process = MockCustomDeployProcess();
            process.When(x => x.RunToExitAsync())
                .Do(x => { throw new ProcessException("test exception"); });
            Assert.ThrowsAsync<DeployException>(
                () => remoteDeploy.ExecuteCustomCommandAsync(project, gamelet, action));

            Assert.IsTrue(action.GetEvent().CustomCommand.CustomCommandAttempted);
            Assert.IsFalse(action.GetEvent().CustomCommand.CustomCommandSucceeded);
        }

        [Test]
        public async Task DeployExeDeltaAsync()
        {
            fileSystem.AddFile(TEST_TARGET_PREVIOUS_PATH, MockPreviousExeFileData);
            fileSystem.AddFile(TEST_TARGET_DELTA_PATH, MockDeltaExeFileData);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PREVIOUS_PATH, null)
                .Returns(previousBuildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH_REMOTE, target)
                .Returns(previousBuildId);
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.DELTA);

            remoteFile
                .PutAsync(target, TEST_XDELTA_FOR_GAMELET_PATH, TEST_XDELTA_FOR_GAMELET_PATH_REMOTE,
                          DeployCompression.Uncompressed, Arg.Any<IIncrementalProgress>(),
                          Arg.Any<ICancelableTask>())
                .Returns(MockXDeltaForGameletFileLength);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);

            await remoteCommand.Received().RunWithSuccessAsync(
                target,
                $"chmod a+x {YetiConstants.RemoteToolsBinDir}" +
                $"{YetiConstants.XDeltaLinuxExecutable}");

            await remoteFile.Received().PutAsync(
                target, TEST_TARGET_DELTA_PATH, TEST_TARGET_DIR_REMOTE,
                DeployCompression.Uncompressed, Arg.Any<IIncrementalProgress>(),
                Arg.Any<ICancelableTask>());
            await remoteFile.Received().PutAsync(
                target, TEST_XDELTA_FOR_GAMELET_PATH, TEST_XDELTA_FOR_GAMELET_PATH_REMOTE,
                DeployCompression.Uncompressed, Arg.Any<IIncrementalProgress>(),
                Arg.Any<ICancelableTask>());

            await remoteCommand.Received().RunWithSuccessAsync(
                target, $"chmod a+x {YetiConstants.RemoteDeployPath}{TEST_TARGET_FILE_NAME}");

            Assert.AreEqual(MockExeFileLength, action.GetEvent().CopyExecutable.CopyBinaryBytes);
            Assert.AreEqual(MockXDeltaForGameletFileLength,
                            action.GetEvent().CopyExecutable.TransferredBinaryBytes);
        }

        [Test]
        public async Task DeployExeDeltaCanceledAsync()
        {
            fileSystem.AddFile(TEST_TARGET_PREVIOUS_PATH, MockPreviousExeFileData);
            fileSystem.AddFile(TEST_TARGET_DELTA_PATH, MockDeltaExeFileData);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PREVIOUS_PATH, null)
                .Returns(previousBuildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH_REMOTE, target)
                .Returns(previousBuildId);
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.DELTA);

            remoteFile
                .PutAsync(target, TEST_XDELTA_FOR_GAMELET_PATH, TEST_XDELTA_FOR_GAMELET_PATH_REMOTE,
                          DeployCompression.Uncompressed, Arg.Any<IIncrementalProgress>(),
                          Arg.Any<ICancelableTask>())
                .Returns<Task<long>>(_ => throw new OperationCanceledException());

            Assert.ThrowsAsync<OperationCanceledException>(
                () => remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action));

            await remoteFile.Received().PutAsync(
                target, TEST_TARGET_DELTA_PATH, TEST_TARGET_DIR_REMOTE,
                DeployCompression.Uncompressed, Arg.Any<IIncrementalProgress>(),
                Arg.Any<ICancelableTask>());
            await remoteFile.Received().PutAsync(
                target, TEST_XDELTA_FOR_GAMELET_PATH, TEST_XDELTA_FOR_GAMELET_PATH_REMOTE,
                DeployCompression.Uncompressed, Arg.Any<IIncrementalProgress>(),
                Arg.Any<ICancelableTask>());

            await remoteCommand.DidNotReceive().RunWithSuccessAsync(
                target, Arg.Is<string>(cmd => cmd.Contains("chmod")));
        }

        [TestCase(DeployCompressionSetting.Compressed, DeployCompression.Compressed,
                  CopyBinaryType.Types.DeploymentMode.Compressed)]
        [TestCase(DeployCompressionSetting.Uncompressed, DeployCompression.Uncompressed,
                  CopyBinaryType.Types.DeploymentMode.Uncompressed)]
        public async Task
            DeployExeDeltaPreviousAndCurrentSignaturesMatchRemoteSignatureMismatchAsync(
                DeployCompressionSetting compressionSetting, DeployCompression deployCompression,
                CopyBinaryType.Types.DeploymentMode deploymentMode)
        {
            project.GetDeployCompressionAsync().Returns(compressionSetting);

            remoteFile
                .PutAsync(target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE, deployCompression,
                          Arg.Any<IIncrementalProgress>(), Arg.Any<ICancelableTask>())
                .Returns(MockExeFileLength);

            fileSystem.AddFile(TEST_TARGET_PREVIOUS_PATH, MockPreviousExeFileData);

            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH, null).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PREVIOUS_PATH, null).Returns(buildId);

            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.DELTA);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);

            Assert.AreEqual(BinarySignatureCheck.Types.Result.YesCopy,
                            action.GetEvent().CopyExecutable.SignatureCheckResult);

            Assert.AreEqual(deploymentMode, action.GetEvent().CopyExecutable.DeploymentMode);
            Assert.AreEqual(MockExeFileLength,
                            action.GetEvent().CopyExecutable.TransferredBinaryBytes);
        }

        [Test]
        public async Task DeployExeDeltaLocalAndRemoteSignaturesMatchAsync()
        {
            fileSystem.AddFile(TEST_TARGET_PREVIOUS_PATH, MockPreviousExeFileData);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PREVIOUS_PATH, null).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH_REMOTE, target).Returns(buildId);
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.DELTA);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);

            Assert.AreEqual(BinarySignatureCheck.Types.Result.NoCopy,
                            action.GetEvent().CopyExecutable.SignatureCheckResult);
            Assert.AreEqual(CopyBinaryType.Types.DeploymentMode.BinaryDiff,
                            action.GetEvent().CopyExecutable.DeploymentMode);
        }

        [Test]
        public async Task DeployExeDeltaRemoteSignatureMismatchAsync()
        {
            fileSystem.AddFile(TEST_TARGET_PREVIOUS_PATH, MockPreviousExeFileData);
            fileSystem.AddFile(TEST_TARGET_DELTA_PATH, MockDeltaExeFileData);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH, null).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PREVIOUS_PATH, null).Returns(previousBuildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH_REMOTE, target).Returns(BuildId.Empty);
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.DELTA);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);

            await remoteFile.Received().PutAsync(
                target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE, DeployCompression.Uncompressed,
                Arg.Any<IIncrementalProgress>(), Arg.Any<ICancelableTask>());

            await remoteCommand.Received().RunWithSuccessAsync(
                target, $"chmod a+x {YetiConstants.RemoteDeployPath}{TEST_TARGET_FILE_NAME}");

            Assert.AreEqual(BinarySignatureCheck.Types.Result.YesCopy,
                            action.GetEvent().CopyExecutable.SignatureCheckResult);
            Assert.AreEqual(MockExeFileLength, action.GetEvent().CopyExecutable.CopyBinaryBytes);
            Assert.AreEqual(CopyBinaryType.Types.DeploymentMode.Uncompressed,
                            action.GetEvent().CopyExecutable.DeploymentMode);
        }

        [Test]
        public async Task DeployExeDeltaRemoteToolExceptionAsync()
        {
            fileSystem.AddFile(TEST_TARGET_PREVIOUS_PATH, MockPreviousExeFileData);
            fileSystem.AddFile(TEST_TARGET_DELTA_PATH, MockDeltaExeFileData);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH_REMOTE, target)
                .Returns(previousBuildId);
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.DELTA);

            var process = Substitute.For<IProcess>();
            const int ErrorCode = 255;
            process.RunToExitAsync().Returns(x => ErrorCode);

            managedProcessFactory
                .CreateVisible(
                    Arg.Is<ProcessStartInfo>(
                        x => x.FileName.Contains(YetiConstants.XDeltaWinExecutable)), int.MaxValue)
                .Returns(process);
            remoteDeploy = new RemoteDeploy(remoteCommand, remoteFile, managedProcessFactory,
                                            fileSystem, binaryFileUtil);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);

            await remoteFile.Received().PutAsync(
                target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE, DeployCompression.Uncompressed,
                Arg.Any<IIncrementalProgress>(), Arg.Any<ICancelableTask>());

            await remoteCommand.Received().RunWithSuccessAsync(
                target, $"chmod a+x {YetiConstants.RemoteDeployPath}{TEST_TARGET_FILE_NAME}");

            Assert.AreEqual(BinarySignatureCheck.Types.Result.YesCopy,
                            action.GetEvent().CopyExecutable.SignatureCheckResult);
            Assert.AreEqual(MockExeFileLength, action.GetEvent().CopyExecutable.CopyBinaryBytes);
            Assert.AreEqual(CopyBinaryType.Types.DeploymentMode.Uncompressed,
                            action.GetEvent().CopyExecutable.DeploymentMode);
        }

        [Test]
        public async Task DeployExeLldbServerAndExecuteCustomAsync()
        {
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.ALWAYS);
            project.GetCustomDeployOnLaunchAsync().Returns(TEST_CUSTOM_COMMAND);
            binaryFileUtil.ReadBuildIdAsync(TEST_LLDB_SERVER_PATH_REMOTE, target).Throws(
                SignatureCheckExceptionForErrorType(SignatureErrorType.BINARY_ERROR));

            var process = MockCustomDeployProcess();
            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);
            await remoteDeploy.DeployLldbServerAsync(target, action);
            await remoteDeploy.ExecuteCustomCommandAsync(project, gamelet, action);

            Received.InOrder(() =>
            {
                remoteFile
                    .PutAsync(target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE,
                              DeployCompression.Uncompressed, Arg.Any<IIncrementalProgress>(),
                              Arg.Any<ICancelableTask>())
                    .Wait();
                remoteCommand.RunWithSuccessAsync(
                        target,
                        $"chmod a+x {YetiConstants.RemoteDeployPath}{TEST_TARGET_FILE_NAME}")
                    .Wait();
                process.RunToExitAsync().Wait();
            });

            Assert.IsTrue(action.GetEvent().CopyExecutable.CopyAttempted);
            Assert.IsTrue(action.GetEvent().CopyLldbServer.CopyAttempted);
            Assert.IsTrue(action.GetEvent().CustomCommand.CustomCommandAttempted);

            Assert.AreEqual(MockExeFileLength, action.GetEvent().CopyExecutable.CopyBinaryBytes);
            Assert.AreEqual(0, action.GetEvent().CopyExecutable.CopyExitCode);
            Assert.AreEqual(0, action.GetEvent().CopyExecutable.SshChmodExitCode);

            Assert.AreEqual(MockLldbServerFileLength,
                            action.GetEvent().CopyLldbServer.CopyBinaryBytes);
            Assert.AreEqual(0, action.GetEvent().CopyLldbServer.CopyExitCode);
            Assert.AreEqual(0, action.GetEvent().CopyLldbServer.SshChmodExitCode);

            Assert.IsTrue(action.GetEvent().CustomCommand.CustomCommandSucceeded);
            Assert.AreEqual(CopyBinaryType.Types.DeploymentMode.Uncompressed,
                            action.GetEvent().CopyExecutable.DeploymentMode);
        }

        [TestCase(SignatureErrorType.MISSING_SIGNATURE)]
        [TestCase(SignatureErrorType.BINARY_ERROR)]
        [TestCase(SignatureErrorType.COMMAND_ERROR)]
        public async Task DeployExeErrorReadingLocalSignatureAsync(SignatureErrorType errorType)
        {
            binaryFileUtil.ReadBuildIdAsync(null, null)
                .ReturnsForAnyArgs<Task<BuildId>>(
                    x => throw SignatureCheckExceptionForErrorType(errorType));
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.TRUE);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Uncompressed);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);
            await remoteFile.Received().PutAsync(
                target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE, DeployCompression.Uncompressed,
                Arg.Any<IIncrementalProgress>(), Arg.Any<ICancelableTask>());

            // If the local signature could not be read, we don't write the file after.
            VerifyNoLocalSignatureFile();

            Assert.IsTrue(action.GetEvent().CopyExecutable.CopyAttempted);
            Assert.AreEqual(SignatureErrorCodeForErrorType(errorType, false /* local */),
                            action.GetEvent().CopyExecutable.SignatureCheckErrorCode);
        }

        [Test]
        public async Task DeployExeNoSignatureFileWithRemoteMismatchAsync()
        {
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH_REMOTE, target)
                .Returns(previousBuildId);
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.TRUE);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Uncompressed);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);
            await remoteFile.Received().PutAsync(
                target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE, DeployCompression.Uncompressed,
                Arg.Any<IIncrementalProgress>(), Arg.Any<ICancelableTask>());
            VerifyLocalSignatureFile();

            Assert.AreEqual(BinarySignatureCheck.Types.Result.YesCopy,
                            action.GetEvent().CopyExecutable.SignatureCheckResult);
            Assert.AreEqual(BinarySignatureCheck.Types.ErrorCode.RemoteBinaryMismatch,
                            action.GetEvent().CopyExecutable.SignatureCheckErrorCode);
            Assert.AreEqual(CopyBinaryType.Types.DeploymentMode.Uncompressed,
                            action.GetEvent().CopyExecutable.DeploymentMode);
        }

        [Test]
        public async Task DeployExeNoSignatureFileWithRemoteMatchAsync()
        {
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH_REMOTE, target).Returns(buildId);
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.TRUE);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Uncompressed);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);
            await remoteFile.DidNotReceiveWithAnyArgs().PutAsync(
                null, null, null, DeployCompression.Uncompressed, Arg.Any<IIncrementalProgress>(),
                Arg.Any<ICancelableTask>());
            VerifyLocalSignatureFile();

            Assert.AreEqual(BinarySignatureCheck.Types.Result.NoCopy,
                            action.GetEvent().CopyExecutable.SignatureCheckResult);
            Assert.AreEqual(CopyBinaryType.Types.DeploymentMode.Uncompressed,
                            action.GetEvent().CopyExecutable.DeploymentMode);
        }

        [Test]
        public async Task DeployExeLocalSignatureMismatchAsync()
        {
            fileSystem.AddFile(TEST_TARGET_PATH_SIGNATURE, new MockFileData(""));
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH).Returns(buildId);
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.TRUE);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Uncompressed);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);
            await remoteFile.Received().PutAsync(
                target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE, DeployCompression.Uncompressed,
                Arg.Any<IIncrementalProgress>(), Arg.Any<ICancelableTask>());
            VerifyLocalSignatureFile();

            Assert.AreEqual(BinarySignatureCheck.Types.Result.YesCopy,
                            action.GetEvent().CopyExecutable.SignatureCheckResult);
            Assert.AreEqual(BinarySignatureCheck.Types.ErrorCode.LocalSignatureMismatch,
                            action.GetEvent().CopyExecutable.SignatureCheckErrorCode);
        }

        [TestCase(SignatureErrorType.MISSING_SIGNATURE)]
        [TestCase(SignatureErrorType.BINARY_ERROR)]
        [TestCase(SignatureErrorType.COMMAND_ERROR)]
        public async Task DeployExeErrorReadingRemoteSignatureAsync(SignatureErrorType errorType)
        {
            fileSystem.AddFile(TEST_TARGET_PATH_SIGNATURE,
                               new MockFileData(buildId.Bytes.ToArray()));
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH_REMOTE, target)
                .Returns<Task<BuildId>>(x =>
                {
                    throw SignatureCheckExceptionForErrorType(errorType);
                });

            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.TRUE);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Uncompressed);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);
            await remoteFile.Received().PutAsync(
                target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE, DeployCompression.Uncompressed,
                Arg.Any<IIncrementalProgress>(), Arg.Any<ICancelableTask>());
            VerifyLocalSignatureFile();

            Assert.AreEqual(BinarySignatureCheck.Types.Result.YesCopy,
                            action.GetEvent().CopyExecutable.SignatureCheckResult);
            Assert.AreEqual(SignatureErrorCodeForErrorType(errorType, true /* remote */),
                            action.GetEvent().CopyExecutable.SignatureCheckErrorCode);
        }

        [Test]
        public async Task DeployExeRemoteSignatureMismatchAsync()
        {
            fileSystem.AddFile(TEST_TARGET_PATH_SIGNATURE,
                               new MockFileData(buildId.Bytes.ToArray()));
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH_REMOTE, target)
                .Returns(previousBuildId);
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.TRUE);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Uncompressed);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);
            await remoteFile.Received().PutAsync(
                target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE, DeployCompression.Uncompressed,
                Arg.Any<IIncrementalProgress>(), Arg.Any<ICancelableTask>());
            VerifyLocalSignatureFile();

            Assert.AreEqual(BinarySignatureCheck.Types.Result.YesCopy,
                            action.GetEvent().CopyExecutable.SignatureCheckResult);
            Assert.AreEqual(BinarySignatureCheck.Types.ErrorCode.RemoteBinaryMismatch,
                            action.GetEvent().CopyExecutable.SignatureCheckErrorCode);
        }

        [Test]
        public void DeployExeCanceled()
        {
            remoteFile
                .PutAsync(target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE,
                          DeployCompression.Compressed, Arg.Any<IIncrementalProgress>(),
                          Arg.Any<ICancelableTask>())
                .Returns<Task<long>>(_ => throw new OperationCanceledException());

            fileSystem.AddFile(TEST_TARGET_PATH_SIGNATURE,
                               new MockFileData(buildId.Bytes.ToArray()));

            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH_REMOTE, target).Returns(buildId);
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.ALWAYS);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Compressed);

            Assert.ThrowsAsync<OperationCanceledException>(
                () => remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action));

            VerifyNoLocalSignatureFile();
        }

        [Test]
        public async Task DeployExeFailedAsync()
        {
            remoteFile
                .PutAsync(target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE,
                          DeployCompression.Compressed, Arg.Any<IIncrementalProgress>(),
                          Arg.Any<ICancelableTask>())
                .Returns<Task<long>>(_ => throw new CompressedCopyException("error"));

            fileSystem.AddFile(TEST_TARGET_PATH_SIGNATURE,
                               new MockFileData(buildId.Bytes.ToArray()));

            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH_REMOTE, target).Returns(buildId);
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.ALWAYS);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Compressed);

            Assert.ThrowsAsync<DeployException>(
                () => remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action));

            await remoteFile.Received(1).PutAsync(
                target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE, DeployCompression.Compressed,
                Arg.Any<IIncrementalProgress>(), Arg.Any<ICancelableTask>());
        }

        [TestCase(DeployOnLaunchSetting.TRUE)]
        [TestCase(DeployOnLaunchSetting.ALWAYS)]
        public async Task DeployExeSignaturesMatchAsync(DeployOnLaunchSetting setting)
        {
            // It's important that we test TRUE vs. ALWAYS under identical conditions.
            // TRUE must always NOT copy, and ALWAYS must always copy.
            fileSystem.AddFile(TEST_TARGET_PATH_SIGNATURE,
                               new MockFileData(buildId.Bytes.ToArray()));
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH).Returns(buildId);
            binaryFileUtil.ReadBuildIdAsync(TEST_TARGET_PATH_REMOTE, target).Returns(buildId);
            project.GetDeployOnLaunchAsync().Returns(setting);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Uncompressed);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);

            if (setting == DeployOnLaunchSetting.ALWAYS)
            {
                await remoteFile.Received().PutAsync(
                    target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE,
                    DeployCompression.Uncompressed, Arg.Any<IIncrementalProgress>(),
                    Arg.Any<ICancelableTask>());
                Assert.AreEqual(BinarySignatureCheck.Types.Result.AlwaysCopy,
                                action.GetEvent().CopyExecutable.SignatureCheckResult);
            }
            else
            {
                await remoteFile.DidNotReceiveWithAnyArgs().PutAsync(
                    null, null, null, DeployCompression.Uncompressed,
                    Arg.Any<IIncrementalProgress>(), Arg.Any<ICancelableTask>());
                Assert.AreEqual(BinarySignatureCheck.Types.Result.NoCopy,
                                action.GetEvent().CopyExecutable.SignatureCheckResult);
            }

            await remoteCommand.Received().RunWithSuccessAsync(
                target, $"chmod a+x {YetiConstants.RemoteDeployPath}{TEST_TARGET_FILE_NAME}");

            // Binary size is always recorded, even if we don't copy the binary.
            Assert.AreEqual(MockExeFileLength, action.GetEvent().CopyExecutable.CopyBinaryBytes);

            // Local signature should be left untouched in both cases.
            VerifyLocalSignatureFile();
        }

        [Test]
        public async Task DeployExeAlwaysDoesNotWriteSignatureAsync()
        {
            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.ALWAYS);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Uncompressed);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);
            await remoteFile.Received().PutAsync(
                target, TEST_TARGET_PATH, TEST_TARGET_PATH_REMOTE, DeployCompression.Uncompressed,
                Arg.Any<IIncrementalProgress>(), Arg.Any<ICancelableTask>());

            VerifyNoLocalSignatureFile();
        }

        [Test]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, true)]
        public async Task DeployExeWithoutDeltaOrCompressionUtilAsync(
            bool removeLinuxExecutable, bool removeWindowsExecutable, bool removePigzExecutable)
        {
            if (removeLinuxExecutable)
            {
                fileSystem.RemoveFile(YetiConstants.XDeltaLinuxExecutablePath);
            }

            if (removeWindowsExecutable)
            {
                fileSystem.RemoveFile(YetiConstants.XDeltaWinExecutablePath);
            }

            if (removePigzExecutable)
            {
                fileSystem.RemoveFile(YetiConstants.PigzWinExecutablePath);
            }

            project.GetDeployOnLaunchAsync().Returns(DeployOnLaunchSetting.DELTA);
            project.GetDeployCompressionAsync().Returns(DeployCompressionSetting.Compressed);

            await remoteDeploy.DeployGameExecutableAsync(project, gamelet, task, action);

            // Since we set up DeployCompressionSetting.Compressed at the beginning of this test
            // we should fallback to compressed deployment if pigz executable exists and to
            // uncompressed otherwise.
            var expectedMode = removePigzExecutable
                ? CopyBinaryType.Types.DeploymentMode.Uncompressed
                : CopyBinaryType.Types.DeploymentMode.Compressed;

            Assert.AreEqual(expectedMode, action.GetEvent().CopyExecutable.DeploymentMode);
        }

        IProcess MockCustomDeployProcess()
        {
            var process = Substitute.For<IProcess>();
            managedProcessFactory.CreateVisible(
                Arg.Is<ProcessStartInfo>(x => x.FileName.Contains(YetiConstants.Command) &&
                                             x.Arguments.Contains(
                                                 "\"" + TEST_CUSTOM_COMMAND + "\"") &&
                                             x.WorkingDirectory == TEST_ABSOLUTE_ROOT_PATH),
                int.MaxValue).Returns(process);
            return process;
        }

        void SetProcessOutput(IProcess process, string[] output)
        {
            Action<NSubstitute.Core.CallInfo> sendEvents = x =>
            {
                foreach (var s in output)
                {
                    process.OutputDataReceived +=
                        Raise.Event<TextReceivedEventHandler>(this, new TextReceivedEventArgs(s));
                }
            };
            process.When(x => x.Start(true)).Do(sendEvents);
            process.When(x => x.RunToExitAsync()).Do(sendEvents);
        }

        void BuildMockCustomTestMntDeveloperProcess(string[] output)
        {
            var process = Substitute.For<IProcess>();
            managedProcessFactory
                .Create(Arg.Is<ProcessStartInfo>(
                            x => x.FileName.Contains(YetiConstants.SshWinExecutable) &&
                                 x.Arguments.Contains("test -d /mnt/developer/ || echo MISSING")),
                        Arg.Any<int>())
                .Returns<IProcess>(process);
            SetProcessOutput(process, output);
        }

        void VerifyLocalSignatureFile()
        {
            var bytes = fileSystem.File.ReadAllBytes(TEST_TARGET_PATH_SIGNATURE);
            var signature = new BuildId(bytes);
            Assert.AreEqual(buildId, signature);
        }

        void VerifyNoLocalSignatureFile()
        {
            if (fileSystem.AllPaths.Contains(TEST_TARGET_PATH_SIGNATURE))
            {
                Assert.Fail($"Must not write signature file: {TEST_TARGET_PATH_SIGNATURE}");
            }
        }

        // This enum is used internally by the tests, but it must be public to be used as a
        // a test case parameter because test cases are public methods.
        public enum SignatureErrorType
        {
            MISSING_SIGNATURE,
            BINARY_ERROR,
            COMMAND_ERROR
        }

        BinarySignatureCheck.Types.ErrorCode SignatureErrorCodeForErrorType(
            SignatureErrorType type, bool remote)
        {
            switch (type)
            {
                case SignatureErrorType.MISSING_SIGNATURE:
                    return remote
                        ? BinarySignatureCheck.Types.ErrorCode.RemoteBinaryMismatch
                        : BinarySignatureCheck.Types.ErrorCode.LocalSignatureMissing;
                case SignatureErrorType.BINARY_ERROR:
                    return remote
                        ? BinarySignatureCheck.Types.ErrorCode.RemoteBinaryError
                        : BinarySignatureCheck.Types.ErrorCode.LocalBinaryError;
                case SignatureErrorType.COMMAND_ERROR:
                    return remote
                        ? BinarySignatureCheck.Types.ErrorCode.RemoteCommandError
                        : BinarySignatureCheck.Types.ErrorCode.LocalCommandError;
            }

            return BinarySignatureCheck.Types.ErrorCode.UnknownError;
        }

        BinaryFileUtilException SignatureCheckExceptionForErrorType(SignatureErrorType type)
        {
            switch (type)
            {
                case SignatureErrorType.MISSING_SIGNATURE:
                    return new BinaryFileUtilException("missing signature",
                                                       new ArgumentException());
                case SignatureErrorType.BINARY_ERROR:
                    return new BinaryFileUtilException("binary error",
                                                       new ProcessExecutionException(
                                                           "readelf failed", 1));
                case SignatureErrorType.COMMAND_ERROR:
                    return new BinaryFileUtilException("command error",
                                                       new ProcessException(
                                                           "failed to run readelf"));
            }

            return null;
        }
    }
}