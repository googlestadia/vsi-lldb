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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;

namespace YetiVSI
{
    /// <summary>
    /// Performs file operations on a remote gamelet.
    /// </summary>
    public interface IRemoteFile
    {
        /// <summary>
        /// Transfers the specified local file(s) to the gamelet, under remotePath.
        /// </summary>
        /// <param name="target">Ssh target (reserved instance).</param>
        /// <param name="localPath">Local file or folder to be copied.</param>
        /// <param name="remotePath">Destination folder on the gamelet.</param>
        /// <param name="task">Long-running operation associated with the process.</param>
        /// <param name="force">Copy files whole, do not apply delta-transfer algorithm.</param>
        /// <exception cref="ProcessException">Thrown if the copy operation fails.</exception>
        Task SyncAsync(SshTarget target, string localPath, string remotePath, ICancelable task,
                       bool force = false);

        /// <summary>
        /// Retrieve one or more files from the instance.
        /// </summary>
        /// <param name="target">Gamelet</param>
        /// <param name="file">Path to file on the gamelet.</param>
        /// <param name="destination">Target path for the operation.</param>
        /// <param name="task">Long-running operation associated with the process.</param>
        /// <returns></returns>
        Task GetAsync(SshTarget target, string file, string destination, ICancelable task);
    }

    public class RemoteFile : IRemoteFile
    {
        readonly ManagedProcess.Factory _remoteProcessFactory;

        public RemoteFile(ManagedProcess.Factory remoteProcessFactory)
        {
            _remoteProcessFactory = remoteProcessFactory;
        }

        public async Task GetAsync(SshTarget target, string file, string destination,
                                   ICancelable task)
        {
            await ScpAsync(ProcessStartInfoBuilder.BuildForScpGet(file, target, destination),
                           ProcessManager.CreateForCancelableTask(task));

            // Notify client if operation was cancelled.
            task.ThrowIfCancellationRequested();
        }

        public async Task SyncAsync(SshTarget target, string localPath, string remotePath,
                                    ICancelable task, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(localPath))
            {
                throw new ArgumentNullException(nameof(localPath), "Local path should be specified when running ggp_rsync");
            }

            if (string.IsNullOrWhiteSpace(remotePath))
            {
                throw new ArgumentNullException(nameof(remotePath), "Remote path should be specified when running ggp_rsync");
            }

            ProcessManager processManager = ProcessManager.CreateForCancelableTask(task);
            ProcessStartInfo startInfo = BuildForGgpSync(target, localPath, remotePath, force);
            using (IProcess process = _remoteProcessFactory.Create(startInfo, int.MaxValue))
            {

                processManager.AddProcess(process);
                process.OutputDataReceived += (sender, args) => {
                    task.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(args.Text))
                    {
                        return;
                    }

                    string data = args.Text.Trim();
                    task.Progress.Report(data);
                };

                List<string> errors = new List<string>();
                process.ErrorDataReceived += (sender, args) => {
                    task.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(args.Text))
                    {
                        return;
                    }

                    string data = args.Text;
                    errors.Add(data);
                };

                await process.RunToExitWithSuccessAsync();
                if (errors.Count > 0)
                {
                    throw new ProcessException(String.Join("\n", errors));
                }
            }

            // Notify client if operation was cancelled.
            task.ThrowIfCancellationRequested();
        }

        ProcessStartInfo BuildForGgpSync(SshTarget target, string localPath, string remotePath,
                                         bool force)
        {
            string tunnelSetup = $"--port {target.Port} --ip {target.IpAddress} --compress";
            string quotedLocalPath = ProcessUtil.QuoteArgument(localPath);
            string quotedRemotePath = ProcessUtil.QuoteArgument(remotePath);
            string copyWholeFiles = force ? "--whole-file --checksum" : "";

            return new ProcessStartInfo
            {
                FileName = Path.Combine(SDKUtil.GetSDKToolsPath(), "ggp_rsync.exe"),
                Arguments =
                    $"{tunnelSetup} {copyWholeFiles} {quotedLocalPath} {quotedRemotePath}",
            };
        }

        async Task ScpAsync(ProcessStartInfo startInfo, ProcessManager processManager)
        {
            // TODO ((internal)) : Instead of showing the command window, we should find someway to
            // parse stdout, or use an SSH library, and update the dialog window progress bar.
            using (IProcess process = _remoteProcessFactory.CreateVisible(startInfo, int.MaxValue))
            {
                processManager.AddProcess(process);
                await process.RunToExitWithSuccessAsync();
            }
        }
    }
}
