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
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;

namespace YetiVSI.CoreAttach
{
    // Represents an item in the remote core file list.  |Name| and |Date| need to be properties
    // in order for CoreListEntry to be bound as UI's item source.
    public struct CoreListEntry
    {
        public string Name { get; set; }
        public DateTime Date { get; set; }
    }

    public interface ICoreListRequest
    {
        Task<List<CoreListEntry>> GetCoreListAsync(SshTarget sshTarget);
    }

    // Performs requests to a gamelet for a list of crash dumps while displaying a cancellable UI
    // dialog.
    public class CoreListRequest : ICoreListRequest
    {
        public class Factory
        {
            readonly ManagedProcess.Factory managedProcessFactory;

            public Factory()
            {
                managedProcessFactory = new ManagedProcess.Factory();
            }

            // Constructor for test substitution.
            public Factory(ManagedProcess.Factory managedProcessFactory)
            {
                this.managedProcessFactory = managedProcessFactory;
            }

            public virtual ICoreListRequest Create()
            {
                return new CoreListRequest(managedProcessFactory);
            }
        }

        readonly ManagedProcess.Factory managedProcessFactory;
        const string COMMAND =
                "cd /usr/local/cloudcast/core && (ls -lt --time-style='+%s' *.{core,dmp} || true)";

        private CoreListRequest(ManagedProcess.Factory managedProcessFactory)
        {
            this.managedProcessFactory = managedProcessFactory;
        }

        // Queries the provided port (gamelet) for a list of running cores.  On an error, a
        // TransportException will be thrown with the error code.
        public async Task<List<CoreListEntry>> GetCoreListAsync(SshTarget sshTarget)
        {
            // TODO: Find a more robust method of listing files on the gamelet.
            ProcessStartInfo processStartInfo = ProcessStartInfoBuilder.BuildForSsh(
                COMMAND, new List<string>(), sshTarget);
            return await GetCoreListFromProcessStartInfoAsync(processStartInfo);
        }

        private async Task<List<CoreListEntry>> GetCoreListFromProcessStartInfoAsync(
            ProcessStartInfo processStartInfo)
        {
            var results = new List<CoreListEntry>();
            using (var process = managedProcessFactory.Create(processStartInfo))
            {
                TextReceivedEventHandler outHandler =
                    delegate (object sender, TextReceivedEventArgs data)
                    {
                        string line = data.Text;
                        if (line == null) return;

                        // Split the line into a maximum of 7 columns. This preserves spaces in the
                        // 7th column, which contains the filename.
                        // Sample output line:
                        // "-rw-r--r-- 1 cloudcast cloudcast 421088 1568730925 hello world.core.dmp"
                        string[] words = line.Split(
                            default(string[]), 7, StringSplitOptions.RemoveEmptyEntries);
                        if (words.Length < 7)
                        {
                            return;
                        }
                        // Only show cores owned by 'cloudcast'.
                        if (words[2] != "cloudcast")
                        {
                            return;
                        }
                        results.Add(new CoreListEntry()
                        {
                            Name = words[6],
                            Date =
                            DateTimeOffset.FromUnixTimeSeconds(
                                long.Parse(words[5])).DateTime.ToLocalTime()
                        });
                    };
                process.OutputDataReceived += outHandler;
                await process.RunToExitWithSuccessAsync();
                return results;
            }
        }
    }
}