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

using System.Collections.Generic;
using System.Threading.Tasks;
using YetiCommon.SSH;

namespace YetiCommon
{
    // Executes shell commands on a remote gamelet.
    public interface IRemoteCommand
    {
        // Runs the provided command remotely on the provided gamelet.  Throws a ProcessException
        // if the command fails to run, or if it returns a non-zero exit code.
        Task RunWithSuccessAsync(SshTarget target, string command);

        /// <summary>
        /// Runs the provided command remotely on the provided gamelet.
        /// </summary>
        /// <returns>Standard output of the process.</returns>
        Task<List<string>> RunWithSuccessCapturingOutputAsync(SshTarget target, string command);
    }

    public class RemoteCommand : IRemoteCommand
    {
        readonly ManagedProcess.Factory remoteProcessFactory;

        public RemoteCommand(ManagedProcess.Factory remoteProcessFactory)
        {
            this.remoteProcessFactory = remoteProcessFactory;
        }

        public async Task RunWithSuccessAsync(SshTarget target, string command)
        {
            var startInfo = ProcessStartInfoBuilder.BuildForSsh(command, target);
            using (var process = remoteProcessFactory.Create(startInfo))
            {
                await process.RunToExitWithSuccessAsync();
            }
        }

        /// <summary>
        /// Runs the provided command remotely on the provided gamelet.
        /// </summary>
        /// <returns>Standard output of the process.</returns>
        /// <exception cref="ProcessException">
        /// Thrown if the process cannot be started, or if it does not exit within the timeout
        /// period that was specified when the process was created.
        /// </exception>
        /// <exception cref="ProcessExecutionException">
        /// Thrown if the process exits with a non-zero exit code. Use the OutputLines and
        /// ErrorLines properties to get the process output and error text, respectively.
        /// </exception>
        public async Task<List<string>> RunWithSuccessCapturingOutputAsync(
                SshTarget target, string command)
        {
            var startInfo = ProcessStartInfoBuilder.BuildForSsh(command, target);
            using (var process = remoteProcessFactory.Create(startInfo))
            {
                return await process.RunToExitWithSuccessCapturingOutputAsync();
            }
        }
    }
}
