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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.ProjectSystem.Abstractions;
using YetiVSI.Shared.Metrics;

namespace YetiVSI
{
    // Indicates an error that occurred while deploying files to the gamelet.
    public class DeployException : Exception, IUserVisibleError
    {
        public DeployException(string message) : base(message)
        {
        }

        public DeployException(string message, Exception e) : base(message, e)
        {
        }

        public string UserDetails
        {
            get { return ToString(); }
        }
    }

    // An interface for deploying files to a gamelet.
    public interface IRemoteDeploy
    {
        // Copies the target executable from the specified project to the specified gamelet. SSH
        // keys must already have been set up with the gamelet. On an error, DeployException will
        // be thrown. Records metrics about the deploy operation in the given action.
        Task DeployGameExecutableAsync(IAsyncProject project, Gamelet gamelet, ICancelable task,
                                       Metrics.IAction action);

        // Executes custom commands provided to the project.
        Task ExecuteCustomCommandAsync(IAsyncProject project, Gamelet gamelet,
                                       Metrics.IAction action);

        // Copies lldb-server file to the gamelet.
        Task DeployLldbServerAsync(SshTarget target, Metrics.IAction action);
    }

    public class RemoteDeploy : IRemoteDeploy
    {
        const string GgpInstanceIdName = "GGP_INSTANCE_ID";

        readonly IRemoteCommand remoteCommand;
        readonly IRemoteFile remoteFile;
        readonly ManagedProcess.Factory managedProcessFactory;
        readonly IFileSystem fileSystem;
        readonly IBinaryFileUtil binaryFileUtil;

        public RemoteDeploy(IRemoteCommand remoteCommand, IRemoteFile remoteFile,
                            ManagedProcess.Factory managedProcessFactory, IFileSystem fileSystem,
                            IBinaryFileUtil binaryFileUtil)
        {
            this.remoteCommand = remoteCommand;
            this.remoteFile = remoteFile;
            this.managedProcessFactory = managedProcessFactory;
            this.fileSystem = fileSystem;
            this.binaryFileUtil = binaryFileUtil;
        }

        public async Task DeployGameExecutableAsync(IAsyncProject project, Gamelet gamelet,
                                                    ICancelable task, Metrics.IAction action)
        {
            DataRecorder record = new DataRecorder(action, DataRecorder.File.GAME_EXECUTABLE);
            var deploySetting = await GetDeployOnLaynchSettingAsync(project);
            if (deploySetting != DeployOnLaunchSetting.FALSE)
            {
                record.SetCopyAttempted(true);
                var compression = await GetDeployCompressionAsync(project);
                var localPath = await project.GetTargetPathAsync();
                var targetName = Path.GetFileName(localPath);
                var remotePath = Path.Combine(YetiConstants.RemoteDeployPath, targetName);

                var sshTarget = new SshTarget(gamelet);
                try
                {
                    switch (deploySetting)
                    {
                        case DeployOnLaunchSetting.DELTA:
                            await DeployGameExecutableDeltaAsync(sshTarget, record, compression,
                                                                 localPath, remotePath,
                                                                 task.Progress, task);
                            break;
                        case DeployOnLaunchSetting.ALWAYS:
                            await DeployToTargetAsync(sshTarget, record, compression,
                                                      task.Progress, task,
                                                      new DeployPaths(localPath, remotePath));
                            break;
                        case DeployOnLaunchSetting.TRUE:
                            await DeployToTargetIfChangedAsync(sshTarget, record, compression,
                                                               localPath, remotePath,
                                                               cacheSignature: true,
                                                               progress: task.Progress,
                                                               task: task);
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    var localSignatureFileName = localPath + YetiConstants.BinarySignatureSuffix;
                    fileSystem.File.Delete(localSignatureFileName);
                    throw;
                }

                await SetRemoteExecutableBitAsync(gamelet, remotePath, record);
            }
            else
            {
                record.SetCopyAttempted(false);
            }
        }

        async Task<DeployOnLaunchSetting> GetDeployOnLaynchSettingAsync(IAsyncProject project)
        {
            DeployOnLaunchSetting deploySetting = await project.GetDeployOnLaunchAsync();
            if (deploySetting == DeployOnLaunchSetting.DELTA)
            {
                if (!fileSystem.File.Exists(YetiConstants.XDeltaWinExecutablePath))
                {
                    Trace.WriteLine("Binary delta util could not be found by path " +
                                    $"{YetiConstants.XDeltaWinExecutablePath}. " +
                                    "Fallback to deploy when executable changed.");
                    deploySetting = DeployOnLaunchSetting.TRUE;
                }

                if (!fileSystem.File.Exists(YetiConstants.XDeltaLinuxExecutablePath))
                {
                    Trace.WriteLine("Binary delta util could not be found by path " +
                                    $"{YetiConstants.XDeltaLinuxExecutablePath}. " +
                                    "Fallback to deploy when executable changed.");
                    deploySetting = DeployOnLaunchSetting.TRUE;
                }
            }

            return deploySetting;
        }

        async Task<DeployCompression> GetDeployCompressionAsync(IAsyncProject project)
        {
            DeployCompression compression = DeployCompression.Uncompressed;
            switch (await project.GetDeployCompressionAsync())
            {
                case DeployCompressionSetting.Uncompressed:
                    compression = DeployCompression.Uncompressed;
                    break;
                case DeployCompressionSetting.Compressed:
                    compression = DeployCompression.Compressed;
                    break;
            }

            if (compression == DeployCompression.Compressed &&
                !fileSystem.File.Exists(YetiConstants.PigzWinExecutablePath))
            {
                Trace.WriteLine("Compression util could not be found by path " +
                                $"{YetiConstants.PigzWinExecutablePath}. " +
                                "Fallback to uncompressed deploy.");
                compression = DeployCompression.Uncompressed;
            }

            return compression;
        }

        async Task DeployGameExecutableDeltaAsync(SshTarget target, DataRecorder record,
                                                  DeployCompression compression, string localPath,
                                                  string remotePath, IProgress<string> progress,
                                                  ICancelable task)
        {
            var previousLocalPath = localPath + YetiConstants.PreviousExecutableSuffix;
            DeltaDeployStrategy deployStrategy = await GetDeltaDeployStrategyAsync(
                localPath, previousLocalPath, remotePath, target, record);

            if (deployStrategy == DeltaDeployStrategy.NoDeploy)
            {
                record.WillCopy(false);
                record.RecordBinaryDiffMode();

                if (!fileSystem.File.Exists(previousLocalPath))
                {
                    fileSystem.File.Copy(localPath, previousLocalPath, overwrite: true);
                }

                return;
            }

            if (deployStrategy == DeltaDeployStrategy.BinaryDeltaDeploy)
            {
                try
                {
                    ProcessManager processManager = ProcessManager.CreateForCancelableTask(task);
                    var executableDeltaLocalPath = localPath + YetiConstants.ExecutableDeltaSuffix;

                    progress.Report(TaskMessages.DeltaDeployEncode);
                    await XDeltaEncodeAsync(previousLocalPath, localPath, executableDeltaLocalPath,
                                            processManager, record);

                    DeployPaths executableDeltaPath =
                        new DeployPaths(executableDeltaLocalPath, YetiConstants.RemoteDeployPath);
                    DeployPaths xdeltaPath = new DeployPaths(
                        YetiConstants.XDeltaLinuxExecutablePath,
                        Path.Combine(YetiConstants.RemoteToolsBinDir,
                                     YetiConstants.XDeltaLinuxExecutable));

                    progress.Report(TaskMessages.DeltaDeployCommand);
                    await DeployToTargetAsync(target, record, DeployCompression.Uncompressed,
                                              progress, task, executableDeltaPath, xdeltaPath);

                    await SetRemoteExecutableBitAsync(target, xdeltaPath.Remote, record);

                    var executableDeltaName = Path.GetFileName(executableDeltaLocalPath);
                    var remoteDeltaPath = Path.Combine(
                        YetiConstants.RemoteDeployPath, executableDeltaName);

                    progress.Report(TaskMessages.DeltaDeployDecode);
                    await XDeltaDecodeAsync(remotePath, remoteDeltaPath, target, processManager,
                                            record);

                    record.BinarySize(FileUtil.GetFileSize(localPath, fileSystem));
                    record.RecordBinaryDiffMode();
                }
                catch (ProcessExecutionException exception)
                {
                    Trace.WriteLine($"Failed to deploy binary diff: {exception}");
                    deployStrategy = DeltaDeployStrategy.FullDeploy;
                }

                task.ThrowIfCancellationRequested();
            }

            // Fallback to deploy the full binary in case if pre checks failed or
            // ProcessExecutionException was caught.
            if (deployStrategy == DeltaDeployStrategy.FullDeploy)
            {
                await DeployToTargetAsync(target, record, compression, progress, task,
                                          new DeployPaths(localPath, remotePath));
            }

            record.WillCopy(true);
            fileSystem.File.Copy(localPath, previousLocalPath, overwrite: true);
        }

        private async Task<DeltaDeployStrategy> GetDeltaDeployStrategyAsync(string localPath,
                                                                            string
                                                                                previousLocalPath,
                                                                            string remotePath,
                                                                            SshTarget target,
                                                                            DataRecorder record)
        {
            bool previousExecutableExists = fileSystem.File.Exists(previousLocalPath);
            if (!previousExecutableExists)
            {
                return DeltaDeployStrategy.FullDeploy;
            }

            BuildId executableId = await GetLocalBuildIdAsync(localPath, record);
            if (executableId == BuildId.Empty)
            {
                return DeltaDeployStrategy.FullDeploy;
            }

            BuildId remoteExecutableId = await GetRemoteBuildIdAsync(remotePath, target, record);
            if (remoteExecutableId == BuildId.Empty)
            {
                return DeltaDeployStrategy.FullDeploy;
            }

            if (executableId == remoteExecutableId)
            {
                return DeltaDeployStrategy.NoDeploy;
            }

            BuildId previousExecutableId = await GetLocalBuildIdAsync(previousLocalPath, record);
            if (previousExecutableId == BuildId.Empty)
            {
                return DeltaDeployStrategy.FullDeploy;
            }

            if (previousExecutableId == remoteExecutableId)
            {
                return DeltaDeployStrategy.BinaryDeltaDeploy;
            }

            return DeltaDeployStrategy.FullDeploy;
        }

        async Task XDeltaEncodeAsync(string previousLocalPath, string localPath,
                                     string deltaLocalPath, ProcessManager processManager,
                                     DataRecorder recorder)
        {
            var args = "-e -f -s " + $"{ProcessUtil.QuoteArgument(previousLocalPath)} " +
                $"{ProcessUtil.QuoteArgument(localPath)} " +
                $"{ProcessUtil.QuoteArgument(deltaLocalPath)}";

            ProcessStartInfo startInfo =
                new ProcessStartInfo(YetiConstants.XDeltaWinExecutablePath, args);

            var stopwatch = Stopwatch.StartNew();
            using (var process = managedProcessFactory.Create(startInfo, int.MaxValue))
            {
                processManager.AddProcess(process);
                await process.RunToExitWithSuccessAsync();
            }

            recorder.BinaryDiffEncoding(stopwatch.ElapsedMilliseconds);
        }

        async Task XDeltaDecodeAsync(string remotePath, string deltaRemotePath, SshTarget target,
                                     ProcessManager processManager, DataRecorder recorder)
        {
            var restoredPath = remotePath + YetiConstants.RestoredFromDeltaExecutableSuffix;

            var args = "-d -f -s " + $"{ProcessUtil.QuoteAndEscapeArgumentForSsh(remotePath)} " +
                $"{ProcessUtil.QuoteAndEscapeArgumentForSsh(deltaRemotePath)} " +
                $"{ProcessUtil.QuoteAndEscapeArgumentForSsh(restoredPath)}";

            var remoteXDeltaPath = Path.Combine(YetiConstants.RemoteToolsBinDir,
                                                YetiConstants.XDeltaLinuxExecutable);
            ProcessStartInfo startInfo = ProcessStartInfoBuilder.BuildForSsh(
                $"{remoteXDeltaPath} {args}", new List<string>(), target);

            var stopwatch = Stopwatch.StartNew();
            using (var process = managedProcessFactory.Create(startInfo))
            {
                processManager.AddProcess(process);
                await process.RunToExitWithSuccessAsync();
            }

            args = $"{ProcessUtil.QuoteArgument(restoredPath)} " +
                $"{ProcessUtil.QuoteArgument(remotePath)}";

            startInfo = ProcessStartInfoBuilder.BuildForSsh(
                $"cp {args}", new List<string>(), target);

            using (var process = managedProcessFactory.Create(startInfo))
            {
                processManager.AddProcess(process);
                await process.RunToExitWithSuccessAsync();
            }

            recorder.BinaryDiffDecoding(stopwatch.ElapsedMilliseconds);
        }

        public async Task DeployLldbServerAsync(SshTarget target, Metrics.IAction action)
        {
            DataRecorder record = new DataRecorder(action, DataRecorder.File.LLDB_SERVER);

            record.SetCopyAttempted(true);
            var localLldbServerPath = GetLldbServerPath();
            var remotePath = Path.Combine(YetiConstants.LldbServerLinuxPath,
                                          YetiConstants.LldbServerLinuxExecutable);

            await DeployToTargetIfChangedAsync(target, record, DeployCompression.Uncompressed,
                                               localLldbServerPath, remotePath,
                                               cacheSignature: false,
                                               progress: new Progress<string>(),
                                               task: new NothingToCancel());

            await SetRemoteExecutableBitAsync(target, remotePath, record);
        }

        public async Task ExecuteCustomCommandAsync(IAsyncProject project, Gamelet gamelet,
                                                    Metrics.IAction action)
        {
            DataRecorder record = new DataRecorder(action, DataRecorder.File.GAME_EXECUTABLE);
            record.Gamelet(gamelet);

            bool attempCustomDeploy = !string.IsNullOrEmpty(
                await project.GetCustomDeployOnLaunchAsync());
            record.SetAttemptedCustomCommand(attempCustomDeploy);
            if (attempCustomDeploy)
            {
                await CustomCommandAsync(project, gamelet, record);
            }
        }

        async Task DeployToTargetIfChangedAsync(SshTarget target, DataRecorder record,
                                                DeployCompression compression, string localPath,
                                                string remotePath, bool cacheSignature,
                                                IProgress<string> progress, ICancelable task)
        {
            record.BinarySize(FileUtil.GetFileSize(localPath, fileSystem));
            record.RecordBinaryDeployMode(compression);

            BuildId localBuildId = await GetLocalBuildIdAsync(localPath, record);
            if (await CheckBinarySignatureAsync(localBuildId, localPath, remotePath, target, record,
                                                cacheSignature))
            {
                Trace.WriteLine("Binary signature check passed; skipping deploying binary");
                record.WillCopy(false);
            }
            else
            {
                Trace.WriteLine("Binary signature check failed; deploying binary");
                record.WillCopy(true);
                await CopyBinariesAsync(target, record, compression, progress, task,
                                        new DeployPaths(localPath, remotePath));
            }

            if (cacheSignature)
            {
                WriteLocalBinarySignature(localPath, localBuildId);
            }
        }

        async Task DeployToTargetAsync(SshTarget target, DataRecorder record,
                                       DeployCompression compression, IProgress<string> progress,
                                       ICancelable task, params DeployPaths[] paths)
        {
            record.BinarySize(paths.Sum(path => FileUtil.GetFileSize(path.Local, fileSystem)));
            record.RecordBinaryDeployMode(compression);

            Trace.WriteLine("Unconditionally deploying binary");
            record.AlwaysCopy();
            await CopyBinariesAsync(target, record, compression, progress, task, paths);
        }

        async Task CustomCommandAsync(IAsyncProject project, Gamelet gamelet, DataRecorder record)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, YetiConstants.Command),
                Arguments = string.Format("/C \"{0}\"",
                                          await project.GetCustomDeployOnLaunchAsync()),
                WorkingDirectory = await project.GetAbsoluteRootPathAsync(),
            };
            startInfo.EnvironmentVariables[GgpInstanceIdName] = gamelet.Id;
            var stopwatch = Stopwatch.StartNew();
            using (var process = managedProcessFactory.CreateVisible(startInfo, int.MaxValue))
            {
                try
                {
                    await process.RunToExitWithSuccessAsync();
                    record.CustomDeploy(stopwatch.ElapsedMilliseconds, DataRecorder.NoError);
                }
                catch (ProcessException e)
                {
                    Trace.WriteLine("Error running custom deploy comamnd: " + e.ToString());
                    record.CustomDeploy(stopwatch.ElapsedMilliseconds, e);
                    throw new DeployException(
                        ErrorStrings.ErrorRunningCustomDeployCommand(e.Message), e);
                }
            }
        }

        // Copy the specified local paths to the remote targets.
        // Throws DeployException if there was an error copying the binary.
        async Task CopyBinariesAsync(SshTarget remoteTarget, DataRecorder record,
                                     DeployCompression compression, IProgress<string> progress,
                                     ICancelable task, params DeployPaths[] paths)
        {
            var stopwatch = Stopwatch.StartNew();
            var parallelProgress = new ParallelProgressSumAggregator(progress);
            var putTasks = paths.Select(async path =>
                                            await remoteFile.PutAsync(
                                                remoteTarget, path.Local, path.Remote, compression,
                                                parallelProgress, task));

            long transferredDataSize = 0;
            var allTasks = Task.WhenAll(putTasks.ToArray());
            try
            {
                transferredDataSize = (await allTasks).Sum();
            }
            catch (Exception e) when (e is ProcessException || e is CompressedCopyException)
            {
                string allPaths = string.Join(", ", paths.Select(path => path.Local));
                Trace.WriteLine($"Error deploying executables {allPaths}:");

                foreach(var exception in allTasks.Exception.Flatten().InnerExceptions)
                {
                    Trace.WriteLine(exception.ToString());
                }

                record.CopyBinary(stopwatch.ElapsedMilliseconds, e);

                // Try detect if the /mnt/developer is missing and give actionable feedback
                // if that's the case.
                string errorMessage = await ComputeErrorStringForFailedDeployAsync(e, remoteTarget);

                throw new DeployException(errorMessage, e);
            }

            record.TransferredDataSize(transferredDataSize);
            record.CopyBinary(stopwatch.ElapsedMilliseconds, DataRecorder.NoError);
        }

        async Task<string> ComputeErrorStringForFailedDeployAsync(Exception e,
                                                                  SshTarget remoteTarget)
        {
            string missingLabel = "MISSING";
            string deployPath = YetiConstants.RemoteDeployPath;
            ProcessStartInfo startInfo = ProcessStartInfoBuilder.BuildForSsh(
                $"test -d {YetiConstants.RemoteDeployPath} || echo {missingLabel}",
                new List<string>(), remoteTarget);

            try
            {
                List<string> output;
                using (var process = managedProcessFactory.Create(startInfo))
                {
                    output = await process.RunToExitWithSuccessCapturingOutputAsync();
                    if (output.Count > 0 && output[0] == missingLabel)
                    {
                        return ErrorStrings.FailedToDeployExecutableMissingUploadDir(
                            YetiConstants.RemoteDeployPath);
                    }
                }
            }
            catch (ProcessExecutionException)
            {
                // Fall through to the default error message.
            }
            return ErrorStrings.FailedToDeployExecutable(e.Message);
        }

        private async Task SetRemoteExecutableBitAsync(Gamelet gamelet, string remoteTargetPath,
                                                       DataRecorder record)
        {
            var target = new SshTarget(gamelet);
            await SetRemoteExecutableBitAsync(target, remoteTargetPath, record);
        }

        private async Task SetRemoteExecutableBitAsync(SshTarget target, string remoteTargetPath,
                                                       DataRecorder record)
        {
            try
            {
                await remoteCommand.RunWithSuccessAsync(target, "chmod a+x " + remoteTargetPath);
                record.Chmod(DataRecorder.NoError);
            }
            catch (ProcessException e)
            {
                Trace.WriteLine("Error setting executable permissions: " + e.ToString());
                record.Chmod(e);
                throw new DeployException(ErrorStrings.FailedToSetExecutablePermissions(e.Message),
                                          e);
            }
        }

        // Compares the BuildId from the local binary against the remote binary.
        // As an optimization, if a local signature file exists, and if checkLocalSignatureCache is
        // true, and it contains a different BuildId, then skip reading the remote build id.
        //
        // Returns true if the local and remote binaries have the same id or false otherwise.
        // This method logs common errors and does not throw any known exception types.
        async Task<bool> CheckBinarySignatureAsync(BuildId localBuildId, string localTargetPath,
                                                   string remoteTargetPath, SshTarget target,
                                                   DataRecorder record,
                                                   bool checkLocalSignatureCache)
        {
            if (localBuildId == BuildId.Empty)
            {
                return false;
            }

            if (checkLocalSignatureCache)
            {
                var localSignatureFileName = localTargetPath + YetiConstants.BinarySignatureSuffix;
                try
                {
                    var bytes = fileSystem.File.ReadAllBytes(localSignatureFileName);
                    var signature = new BuildId(bytes);
                    if (localBuildId != signature)
                    {
                        Trace.WriteLine(
                            $"Local binary '{localTargetPath}' with signature '{localBuildId}' " +
                            $"does not match local signature file containing '{signature}'");
                        record.SignatureCheckError(
                            BinarySignatureCheck.Types.ErrorCode.LocalSignatureMismatch);
                        return false;
                    }
                }
                catch (FileNotFoundException)
                {
                    Trace.WriteLine($"Signature file not found {localSignatureFileName}; " +
                                    "checking remote signature");
                    // NOTE: this is not recorded in the metrics. We will record the outcome of the
                    // remote check instead.
                }
                catch (Exception e) when (IsFileIoException(e))
                {
                    Trace.WriteLine("Error reading local signature file " +
                                    $"'{localSignatureFileName}': {e.ToString()}");
                    record.SignatureCheckError(BinarySignatureCheck.Types.ErrorCode.UnknownError);
                    return false;
                }
            }

            var remoteBuildId = await GetRemoteBuildIdAsync(remoteTargetPath, target, record);
            if (remoteBuildId == BuildId.Empty)
            {
                return false;
            }

            if (remoteBuildId != localBuildId)
            {
                Trace.WriteLine(
                    $"Remote binary '{remoteTargetPath}' with signature '{remoteBuildId}' " +
                    $"does not match local binary with signature '{localBuildId}'");
                record.SignatureCheckError(
                    BinarySignatureCheck.Types.ErrorCode.RemoteBinaryMismatch);
                return false;
            }

            Trace.WriteLine($"Local binary '{localTargetPath}' " +
                            $"matches remote binary '{remoteTargetPath}'");
            return true;
        }

        async Task<BuildId> GetRemoteBuildIdAsync(string remotePath, SshTarget target,
                                                  DataRecorder record)
        {
            try
            {
                return await binaryFileUtil.ReadBuildIdAsync(remotePath, target);
            }
            catch (BinaryFileUtilException e) when (e.InnerException is ProcessExecutionException)
            {
                Trace.WriteLine($"Remote binary doesn't exist or unreadable: {remotePath}");
                record.SignatureCheckError(BinarySignatureCheck.Types.ErrorCode.RemoteBinaryError);
            }
            catch (BinaryFileUtilException e) when (e.InnerException is ProcessException)
            {
                Trace.WriteLine($"Error running tool to read remote binary: {e}");
                record.SignatureCheckError(BinarySignatureCheck.Types.ErrorCode.RemoteCommandError);
            }
            catch (BinaryFileUtilException e)
            {
                // At this point the local binary has a valid ID, but the remote binary does not,
                // so we consider it a mismatch.
                Trace.WriteLine($"Remote binary {remotePath} has invalid or missing build ID: {e}");
                record.SignatureCheckError(
                    BinarySignatureCheck.Types.ErrorCode.RemoteBinaryMismatch);
            }

            return BuildId.Empty;
        }

        async Task<BuildId> GetLocalBuildIdAsync(string localTargetPath, DataRecorder record)
        {
            try
            {
                return await binaryFileUtil.ReadBuildIdAsync(localTargetPath);
            }
            catch (BinaryFileUtilException e) when (e.InnerException is ProcessExecutionException)
            {
                Trace.WriteLine($"Error reading signature from local binary {localTargetPath}: " +
                                e.ToString());
                record.SignatureCheckError(BinarySignatureCheck.Types.ErrorCode.LocalBinaryError);
            }
            catch (BinaryFileUtilException e) when (e.InnerException is ProcessException)
            {
                Trace.WriteLine("Error running tool to read signature from binary: {e}");
                record.SignatureCheckError(BinarySignatureCheck.Types.ErrorCode.LocalCommandError);
            }
            catch (BinaryFileUtilException e)
            {
                Trace.WriteLine(
                    $"Local binary {localTargetPath} has invalid or missing build ID: {e}");
                record.SignatureCheckError(BinarySignatureCheck.Types.ErrorCode
                                               .LocalSignatureMissing);
            }

            return BuildId.Empty;
        }

        // Writes the given signature for the local target binary.
        void WriteLocalBinarySignature(string localTargetPath, BuildId signature)
        {
            if (signature == BuildId.Empty)
            {
                // Don't write an empty/invalid signature.
                return;
            }

            var localSignatureFileName = localTargetPath + YetiConstants.BinarySignatureSuffix;
            try
            {
                fileSystem.File.WriteAllBytes(localSignatureFileName, signature.Bytes.ToArray());
            }
            catch (Exception e) when (IsFileIoException(e))
            {
                Trace.WriteLine($"Error writing local signature " +
                                $"file '{localSignatureFileName}': {e}");
            }
        }

        // Returns true if this is an exception thrown by file I/O operations.
        // Excludes exceptions caused by invalid arguments, including malformed paths.
        static bool IsFileIoException(Exception e)
        {
            return e is IOException || e is UnauthorizedAccessException ||
                e is System.Security.SecurityException;
        }

        public string GetLldbServerPath()
        {
#if USE_LOCAL_PYTHON_AND_TOOLCHAIN
            // This is gated by the <DeployPythonAndToolchainDependencies> project setting to speed
            // up the build.
            string toolchainDir = File.ReadAllText(
            Path.Combine(YetiConstants.RootDir, "local_toolchain_dir.txt")).Trim();
            string localLldbServerPath =
                Path.Combine(toolchainDir, "runtime", "bin", "lldb-server");

            // Quick sanity check that the file exist.
            if (!File.Exists(localLldbServerPath))
            {
                // Note: This error is only shown to internal VSI devs, not to external devs.
                throw new DeployException(
                    "You have set the <DeployPythonAndToolchainDependencies> project setting to " +
                    "False to speed up deployment, but the LLDB server file " +
                    $"{localLldbServerPath} moved. Either fix the wrong file path (preferred) or " +
                    "set <DeployPythonAndToolchainDependencies> to False.");
            }
#else
            string localLldbServerPath =
                Path.Combine(YetiConstants.LldbDir, "bin", YetiConstants.LldbServerLinuxExecutable);
#endif
            return localLldbServerPath;
        }

        public class ParallelProgressSumAggregator : IIncrementalProgress
        {
            IProgress<string> _progress;
            long _totalValue = 0;

            public ParallelProgressSumAggregator(IProgress<string> progress)
            {
                _progress = progress;
            }

            public void ReportProgressDelta(long value)
            {
                _progress.Report(
                    TaskMessages.GetDeployingProgress(Interlocked.Add(ref _totalValue, value)));
            }
        }

        enum DeltaDeployStrategy
        {
            NoDeploy,
            FullDeploy,
            BinaryDeltaDeploy
        }

        class DeployPaths
        {
            public DeployPaths(string local, string remote)
            {
                Local = local;
                Remote = remote;
            }

            public string Local { get; }

            public string Remote { get; }
        }

        // Helper class to record various metrics about the remote deploy operation. This helps to
        // abstract away some proto-handling details from the main workflow.
        class DataRecorder
        {
            // Used in place of an exception to indicate successful execution.
            public static ProcessException NoError = null;
            private File file;

            public enum File
            {
                GAME_EXECUTABLE = 0,
                LLDB_SERVER = 1,
            }

            Metrics.IAction action;

            public DataRecorder(Metrics.IAction action, File file)
            {
                this.action = action;
                this.file = file;
            }

            public void SetCopyAttempted(bool attempted)
            {
                RecordData(new CopyBinaryData {CopyAttempted = attempted});
            }

            public void SetAttemptedCustomCommand(bool attempted)
            {
                RecordCustomCommandData(new CustomCommandData
                {
                    CustomCommandAttempted = attempted
                });
            }

            public void AlwaysCopy()
            {
                RecordData(new CopyBinaryData
                {
                    SignatureCheckResult = BinarySignatureCheck.Types.Result.AlwaysCopy
                });
            }

            public void WillCopy(bool willCopy)
            {
                RecordData(new CopyBinaryData
                {
                    SignatureCheckResult = willCopy
                        ? BinarySignatureCheck.Types.Result.YesCopy
                        : BinarySignatureCheck.Types.Result.NoCopy
                });
            }

            public void SignatureCheckError(BinarySignatureCheck.Types.ErrorCode error)
            {
                RecordData(new CopyBinaryData {SignatureCheckErrorCode = error});
            }

            public void CopyBinary(double latencyMs, Exception e)
            {
                RecordData(new CopyBinaryData
                {
                    CopyExitCode = ExitCodeFromError(e),
                    CopyLatencyMs = latencyMs
                });
            }

            public void RecordBinaryDiffMode()
            {
                RecordData(new CopyBinaryData
                {
                    DeploymentMode = CopyBinaryType.Types.DeploymentMode.BinaryDiff
                });
            }

            public void BinaryDiffEncoding(double latencyMs)
            {
                RecordData(new CopyBinaryData
                {
                    BinaryDiffEncodingLatencyMs = latencyMs
                });
            }

            public void BinaryDiffDecoding(double latencyMs)
            {
                RecordData(new CopyBinaryData
                {
                    BinaryDiffDecodingLatencyMs = latencyMs
                });
            }

            public void RecordBinaryDeployMode(DeployCompression compression)
            {
                CopyBinaryType.Types.DeploymentMode mode = CopyBinaryType.Types.DeploymentMode
                    .Uncompressed;
                switch (compression)
                {
                    case DeployCompression.Compressed:
                        mode = CopyBinaryType.Types.DeploymentMode.Compressed;
                        break;
                    case DeployCompression.Uncompressed:
                        mode = CopyBinaryType.Types.DeploymentMode.Uncompressed;
                        break;
                }

                RecordData(new CopyBinaryData
                {
                    DeploymentMode = mode
                });
            }

            public void CustomDeploy(double latencyMs, ProcessException e)
            {
                RecordCustomCommandData(new CustomCommandData
                {
                    CustomCommandSucceeded = e == null,
                    CustomCommandLatencyMs = latencyMs
                });
            }

            public void Chmod(ProcessException e)
            {
                // When recording the exit code, use 0 for success, process exit code for execution
                // errors, or -1 for all other errors.
                RecordData(new CopyBinaryData {SshChmodExitCode = ExitCodeFromError(e)});
            }

            public void Gamelet(Gamelet gamelet)
            {
                action.UpdateEvent(new DeveloperLogEvent
                {
                    GameletData = Metrics.GameletData.FromGamelet(gamelet)
                });
            }

            public void BinarySize(long bytes)
            {
                RecordData(new CopyBinaryData {CopyBinaryBytes = bytes});
            }

            public void TransferredDataSize(long bytes)
            {
                RecordData(new CopyBinaryData {TransferredBinaryBytes = bytes});
            }

            void RecordData(CopyBinaryData copyBinaryData)
            {
                switch (file)
                {
                    case File.GAME_EXECUTABLE:
                        action.UpdateEvent(new DeveloperLogEvent
                        {
                            CopyExecutable = copyBinaryData
                        });
                        break;
                    case File.LLDB_SERVER:
                        action.UpdateEvent(new DeveloperLogEvent
                        {
                            CopyLldbServer = copyBinaryData
                        });
                        break;
                }
            }

            void RecordCustomCommandData(CustomCommandData data)
            {
                action.UpdateEvent(new DeveloperLogEvent {CustomCommand = data});
            }

            static int ExitCodeFromError(Exception e)
            {
                // When recording the exit code, use 0 for success, process exit code for execution
                // errors, or -1 for all other errors.
                return e == null ? 0 : (e as ProcessExecutionException)?.ExitCode ?? -1;
            }
        }
    }
}