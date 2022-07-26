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
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;

namespace YetiVSI.PortSupplier
{
    // Represents an item in the remote process list.
    public struct ProcessListEntry
    {
        public uint Pid;
        public uint Ppid;
        public string Title;
        public string Command;
    };

    public interface IProcessListRequest
    {
        Task<List<ProcessListEntry>> GetBySshAsync(SshTarget target, bool includeFromAllUsers);
    }

    public class ProcessListRequest : IProcessListRequest
    {
        public class Factory
        {
            readonly ManagedProcess.Factory remoteProcessFactory;

            public Factory(ManagedProcess.Factory remoteProcessFactory)
            {
                this.remoteProcessFactory = remoteProcessFactory;
            }

            // Default constructor for test substitution.
            public Factory() { }

            public virtual IProcessListRequest Create()
            {
                return new ProcessListRequest(remoteProcessFactory);
            }
        }

        readonly ManagedProcess.Factory remoteProcessFactory;

        private ProcessListRequest(ManagedProcess.Factory remoteProcessFactory)
        {
            this.remoteProcessFactory = remoteProcessFactory;
        }

        public async Task<List<ProcessListEntry>> GetBySshAsync(
            SshTarget target, bool includeFromAllUsers)
        {
            var command = "ps -o pid,ppid,comm,cmd -ww";

            if (includeFromAllUsers)
            {
                command += " --ppid 2 -p 2 --deselect";
            }
            else
            {
                command += " -x";
            }

            using (var process = remoteProcessFactory.Create(
                ProcessStartInfoBuilder.BuildForSsh(command, new List<string>(), target)))
            {
                return await GetByProcessAsync(process);
            }
        }

        private async Task<List<ProcessListEntry>> GetByProcessAsync(IProcess process)
        {
            var results = new SortedDictionary<uint, ProcessListEntry>();
            uint psPid = 0;

            TextReceivedEventHandler outHandler =
                delegate (object sender, TextReceivedEventArgs data)
            {
                string line = data.Text;
                if (line == null)
                {
                    return;
                }

                string[] words = line.Split(
                    default(string[]), StringSplitOptions.RemoveEmptyEntries);
                if (words.Length < 4)
                {
                    return;
                }
                uint pid;
                if (!UInt32.TryParse(words[0], out pid))
                {
                    return;
                }
                uint ppid;
                if (!UInt32.TryParse(words[1], out ppid))
                {
                    return;
                }
                // Grab the pid of the "ps" command so we can filter out parent processes.
                if (words[2] == "ps")
                {
                    psPid = pid;
                }
                results.Add(pid, new ProcessListEntry
                {
                    Pid = pid,
                    Ppid = ppid,
                    Title = words[2],
                    Command = String.Join(" ", words.Skip(3)),
                });
            };
            process.OutputDataReceived += outHandler;

            await process.RunToExitWithSuccessAsync();

            // Remove the chain of processes from the "ps" command.
            while (results.TryGetValue(psPid, out ProcessListEntry psResult))
            {
                psPid = psResult.Ppid;
                results.Remove(psResult.Pid);
            }

            return results.Values.ToList();
        }
    }
}
