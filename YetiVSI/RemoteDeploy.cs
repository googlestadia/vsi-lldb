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
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
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
        public DeployException(string message, Exception e) : base(message, e)
        {
        }

        public string UserDetails => ToString();
    }

    // An interface for deploying files to a gamelet.
    public interface IRemoteDeploy
    {
        /// <summary>
        /// Copies the target executable from the specified project to the specified gamelet. SSH
        /// keys must already have been set up with the gamelet. On an error, DeployException will
        /// be thrown. Records metrics about  in the given action.
        /// </summary>
        /// <param name="project">Project used.</param>
        /// <param name="target">SSH target.</param>
        /// <param name="task">Task to associate this operation with.</param>
        /// <param name="action">To record metrics about the deploy operation.</param>
        /// <exception cref="DeployException">If any error occurs it will be re-thrown
        /// wrapped into DeployException</exception>
        Task DeployGameExecutableAsync(IAsyncProject project, SshTarget target, ICancelable task,
                                       Metrics.IAction action);
        /// <summary>
        /// Executes custom commands provided to the project.
        /// </summary>
        Task ExecuteCustomCommandAsync(IAsyncProject project, Gamelet gamelet,
                                       Metrics.IAction action);

        /// <summary>
        /// Copies lldb-server file to the gamelet.
        /// </summary>
        Task DeployLldbServerAsync(SshTarget target, Metrics.IAction action);
    }

    public class RemoteDeploy : IRemoteDeploy
    {
        const string GgpInstanceIdName = "GGP_INSTANCE_ID";

        readonly IRemoteCommand _remoteCommand;
        readonly IRemoteFile _remoteFile;
        readonly ManagedProcess.Factory _managedProcessFactory;
        readonly IFileSystem _fileSystem;

        public RemoteDeploy(IRemoteCommand remoteCommand, IRemoteFile remoteFile,
                            ManagedProcess.Factory managedProcessFactory,
                            IFileSystem fileSystem)
        {
            _remoteCommand = remoteCommand;
            _remoteFile = remoteFile;
            _managedProcessFactory = managedProcessFactory;
            _fileSystem = fileSystem;
        }

        public async Task DeployGameExecutableAsync(IAsyncProject project, SshTarget target,
                                                    ICancelable task, Metrics.IAction action)
        {
            DataRecorder record = new DataRecorder(action, DataRecorder.File.GAME_EXECUTABLE);
            DeployOnLaunchSetting deploySetting = await GetDeployOnLaunchSettingAsync(project);
            if (deploySetting != DeployOnLaunchSetting.FALSE)
            {
                string localPath = await project.GetTargetPathAsync();
                bool force = (deploySetting == DeployOnLaunchSetting.ALWAYS);
                await DeployToTargetAsync(record, task, target, localPath,
                                          YetiConstants.RemoteDeployPath, force);

                string targetName = Path.GetFileName(localPath);
                string remotePath = Path.Combine(YetiConstants.RemoteDeployPath, targetName);
                await SetRemoteExecutableBitAsync(target, remotePath, record);
            }
            else
            {
                record.SetCopyAttempted(false);
                record.SignatureCheckResult(BinarySignatureCheck.Types.Result.NoCopy);
            }
        }

        async Task<DeployOnLaunchSetting> GetDeployOnLaunchSettingAsync(IAsyncProject project)
        {
            return await project.GetDeployOnLaunchAsync();
        }

        public async Task DeployLldbServerAsync(SshTarget target, Metrics.IAction action)
        {
            DataRecorder record = new DataRecorder(action, DataRecorder.File.LLDB_SERVER);
            record.SetCopyAttempted(true);

            string localLldbServerPath = GetLldbServerPath();
            string remotePath = Path.Combine(YetiConstants.LldbServerLinuxPath,
                                             YetiConstants.LldbServerLinuxExecutable);

            await DeployToTargetAsync(record, new NothingToCancel(), target, localLldbServerPath,
                                      YetiConstants.LldbServerLinuxPath);
            await SetRemoteExecutableBitAsync(target, remotePath, record);
        }

        async Task DeployToTargetAsync(DataRecorder record, ICancelable task, SshTarget target,
                                       string localPath, string remotePath, bool force = false)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                BinarySignatureCheck.Types.Result signatureCheck = force
                    ? BinarySignatureCheck.Types.Result.AlwaysCopy
                    : BinarySignatureCheck.Types.Result.YesCopy;

                record.SetCopyAttempted(true);
                record.BinarySize(FileUtil.GetFileSize(localPath, _fileSystem));
                record.SignatureCheckResult(signatureCheck);
                record.DeploymentMode();

                await _remoteFile.SyncAsync(target, localPath, remotePath, task, force);

                record.CopyBinary(stopwatch.ElapsedMilliseconds, DataRecorder.NoError);
            }
            catch (ProcessException exception)
            {
                record.CopyBinary(stopwatch.ElapsedMilliseconds, exception);
                throw new DeployException(
                    ErrorStrings.FailedToDeployExecutable(exception.Message),
                    exception);
            }
        }

        public async Task ExecuteCustomCommandAsync(IAsyncProject project, Gamelet gamelet,
                                                    Metrics.IAction action)
        {
            DataRecorder record = new DataRecorder(action, DataRecorder.File.GAME_EXECUTABLE);
            record.Gamelet(gamelet);
            string customDeployCommand = await project.GetCustomDeployOnLaunchAsync();
            bool attemptedCustomDeploy = !string.IsNullOrEmpty(customDeployCommand);
            record.SetAttemptedCustomCommand(attemptedCustomDeploy);
            if (attemptedCustomDeploy)
            {
                await CustomCommandAsync(project, gamelet, record, customDeployCommand);
            }
        }

        async Task CustomCommandAsync(IAsyncProject project, Gamelet gamelet, DataRecorder record,
                                      string command)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, YetiConstants.Command),
                Arguments = $"/C \"{command}\"",
                WorkingDirectory = await project.GetAbsoluteRootPathAsync(),
            };

            startInfo.EnvironmentVariables[GgpInstanceIdName] = gamelet.Id;
            Stopwatch stopwatch = Stopwatch.StartNew();
            using (IProcess process = _managedProcessFactory.CreateVisible(startInfo, int.MaxValue))
            {
                try
                {
                    await process.RunToExitWithSuccessAsync();
                    record.CustomDeploy(stopwatch.ElapsedMilliseconds, DataRecorder.NoError);
                }
                catch (ProcessException e)
                {
                    Trace.WriteLine("Error running custom deploy command: " + e);
                    record.CustomDeploy(stopwatch.ElapsedMilliseconds, e);
                    throw new DeployException(
                        ErrorStrings.ErrorRunningCustomDeployCommand(e.Message), e);
                }
            }
        }

        async Task SetRemoteExecutableBitAsync(SshTarget target, string remoteTargetPath,
                                               DataRecorder record)
        {
            try
            {
                await _remoteCommand.RunWithSuccessAsync(target, "chmod a+x " + remoteTargetPath);
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

        public string GetLldbServerPath()
        {
#if USE_LOCAL_PYTHON_AND_TOOLCHAIN
            // This is gated by the <DeployPythonAndToolchainDependencies> project setting to speed
            // up the build.
            string toolchainDir =
                File.ReadAllText(Path.Combine(YetiConstants.RootDir, "local_toolchain_dir.txt"))
                    .Trim();
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

        // Helper class to record various metrics about the remote deploy operation. This helps to
        // abstract away some proto-handling details from the main workflow.
        class DataRecorder
        {
            // Used in place of an exception to indicate successful execution.
            public static ProcessException NoError = null;
            readonly File _file;
            readonly Metrics.IAction _action;

            public enum File
            {
                GAME_EXECUTABLE = 0,
                LLDB_SERVER = 1,
            }

            public DataRecorder(Metrics.IAction action, File file)
            {
                _action = action;
                _file = file;
            }

            public void SetCopyAttempted(bool attempted)
            {
                RecordData(new CopyBinaryData { CopyAttempted = attempted });
            }

            public void SetAttemptedCustomCommand(bool attempted)
            {
                var customCommand = new CustomCommandData { CustomCommandAttempted = attempted };
                RecordCustomCommandData(customCommand);
            }

            public void CopyBinary(double latencyMs, Exception e)
            {
                RecordData(new CopyBinaryData
                {
                    CopyExitCode = ExitCodeFromError(e),
                    CopyLatencyMs = latencyMs
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
                RecordData(new CopyBinaryData { SshChmodExitCode = ExitCodeFromError(e) });
            }

            public void Gamelet(Gamelet gamelet)
            {
                _action.UpdateEvent(new DeveloperLogEvent
                {
                    GameletData = Metrics.GameletData.FromGamelet(gamelet)
                });
            }

            public void BinarySize(long bytes)
            {
                RecordData(new CopyBinaryData { CopyBinaryBytes = bytes });
            }

            public void SignatureCheckResult(BinarySignatureCheck.Types.Result signatureCheck)
            {
                RecordData(new CopyBinaryData
                {
                    SignatureCheckResult = signatureCheck
                });
            }

            public void DeploymentMode()
            {
                RecordData(new CopyBinaryData
                {
                    DeploymentMode = CopyBinaryType.Types.DeploymentMode.GgpRsync
                });
            }

            void RecordData(CopyBinaryData copyBinaryData)
            {
                switch (_file)
                {
                    case File.GAME_EXECUTABLE:
                        _action.UpdateEvent(new DeveloperLogEvent
                        {
                            CopyExecutable = copyBinaryData
                        });
                        break;
                    case File.LLDB_SERVER:
                        _action.UpdateEvent(new DeveloperLogEvent
                        {
                            CopyLldbServer = copyBinaryData
                        });
                        break;
                }
            }

            void RecordCustomCommandData(CustomCommandData data)
            {
                _action.UpdateEvent(new DeveloperLogEvent { CustomCommand = data });
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