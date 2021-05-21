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
using System.Diagnostics;
using System.IO;
using System.Linq;
using YetiCommon.SSH;

namespace YetiCommon
{
    // RemoteUtil provides ProcessStartInfo instances for running processes on a remote gamelet.
    public class ProcessStartInfoBuilder
    {
        /// <summary>
        /// Represents the config for a forwarded port.
        /// </summary>
        public struct PortForwardEntry
        {
            public int LocalPort;
            public int RemotePort;
        }

        /// <summary>
        /// Returns ProcessStartInfo for running a command on a remote gamelet using SSH.
        /// </summary>
        public static ProcessStartInfo BuildForSsh(
            string command, IEnumerable<string> environment, SshTarget target)
        {
            string envArgs = environment.Aggregate("", (acc, entry) => acc + entry + " ");
            return new ProcessStartInfo()
            {
                FileName = Path.Combine(
                    SDKUtil.GetSshPath(), YetiConstants.SshWinExecutable),
                // (internal): Without -tt, GGP processes leak if ssh.exe is killed.
                Arguments = string.Format(
                    "-tt -i \"{0}\" -F NUL " +
                    "-oStrictHostKeyChecking=yes -oUserKnownHostsFile=\"\"\"{1}\"\"\" " +
                    "cloudcast@{2} -p {3} -- \"{4}{5}\"",
                    SDKUtil.GetSshKeyFilePath(), SDKUtil.GetSshKnownHostsFilePath(),
                    target.IpAddress, target.Port, envArgs, command),
            };
        }

        /// <summary>
        /// Returns ProcessStartInfo for forwarding a local port to a remote gamelet using SSH.
        /// </summary>
        public static ProcessStartInfo BuildForSshPortForward(
            IEnumerable<PortForwardEntry> ports, SshTarget target)
        {
            var portsArgument = string.Join(" ",
                ports.Select(e => $"-L{e.LocalPort}:localhost:{e.RemotePort}"));
            return new ProcessStartInfo()
            {
                FileName = Path.Combine(SDKUtil.GetSshPath(), YetiConstants.SshWinExecutable),
                Arguments = string.Format(
                    "-nNT -i \"{0}\" -F NUL " +
                    "-oStrictHostKeyChecking=yes -oUserKnownHostsFile=\"\"\"{1}\"\"\" " +
                    "{2} cloudcast@{3} -p {4}",
                    SDKUtil.GetSshKeyFilePath(), SDKUtil.GetSshKnownHostsFilePath(), portsArgument,
                    target.IpAddress, target.Port),
            };
        }

        /// <summary>
        /// Returns ProcessStartInfo for transferring a file from a remote gamelet using scp.
        /// </summary>
        public static ProcessStartInfo BuildForScpGet(
            string file, SshTarget target, string destination)
        {
            return new ProcessStartInfo
            {
                FileName = Path.Combine(SDKUtil.GetSshPath(), YetiConstants.ScpWinExecutable),
                // Note that remote file names must be escaped twice, once for local command
                // parsing and once for remote shell parsing. Linux file systems also allow double
                // quotes in file names, so those params must be escaped, where as the Windows
                // paths can simply be quoted.
                Arguments = string.Format(
                    "-T -i \"{0}\" -F NUL -P {1} " +
                    "-oStrictHostKeyChecking=yes -oUserKnownHostsFile=\"\"\"{2}\"\"\" " +
                    "cloudcast@{3}:{4} {5}",
                    SDKUtil.GetSshKeyFilePath(), target.Port, SDKUtil.GetSshKnownHostsFilePath(),
                    target.IpAddress, ProcessUtil.QuoteArgument($"'{file}'"),
                    ProcessUtil.QuoteArgument(destination)),
            };
        }

    }
}
