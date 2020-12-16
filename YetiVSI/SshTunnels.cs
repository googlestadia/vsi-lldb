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

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace YetiVSI
{
    public class SshTunnels
    {
        const string _chrootDir = "ChrootDirectory";
        readonly string _managementClassPath = "Win32_Process";
        readonly string _nameField = "Name";
        readonly string _commandLineField = "CommandLine";
        readonly string _processName = "sshd.exe";
        readonly StringComparison _comparisonType = StringComparison.OrdinalIgnoreCase;
        readonly int _chrootDirLen = _chrootDir.Length;

        /// <summary>
        /// Loops through `sshd.exe` processes and yields their commandLines if available.
        /// </summary>
        public IEnumerable<string> GetSshCommandLines()
        {
            var managementClass = new ManagementClass(_managementClassPath);
            Trace.WriteLine($"GetInstances of local processes {_processName}:");
            foreach (ManagementBaseObject baseObject in managementClass.GetInstances())
            {
                if ((baseObject[_nameField] == null)
                    || (baseObject[_commandLineField] == null)
                    || !baseObject[_nameField].ToString().Equals(_processName, _comparisonType))
                {
                    continue;
                }

                yield return baseObject[_commandLineField].ToString();
                Trace.WriteLine($"- commandLine: {baseObject[_commandLineField]}");
            }
        }

        /// <summary>
        /// If the SSH process created a tunnel to the gamelet, extract the ChrootDirectory
        /// value from the command.
        /// </summary>
        /// <param name="commandLines">Ssh commandLines.</param>
        /// <returns>SSH mounting points as set in `ggp mount --local-dir`.</returns>
        public IEnumerable<string> ExtractMountingPoints(IEnumerable<string> commandLines)
        {
            string argumentAssignment = $"{_chrootDir}=";
            foreach (string commandLine in commandLines)
            {
                if (!commandLine.Contains("ForceCommand=internal-sftp -P symlink,hardlink"))
                {
                    continue;
                }

                string argument =
                    commandLine
                        .Split(' ')
                        .FirstOrDefault(x => x.StartsWith(argumentAssignment));

                string value = argument?.Substring(_chrootDirLen + 1);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                yield return value;
                Trace.WriteLine($"Parsed mounting point {value} from commandLine {commandLine}");
            }
        }
    }
}