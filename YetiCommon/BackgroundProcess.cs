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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YetiCommon
{
    // A process that runs in the background, independent of the Visual Studio process.
    public interface IBackgroundProcess
    {
        // The ID of a process that has started.
        int Id { get; }

        // The name of a process that has started.
        string ProcessName { get; }

        // The starting information of the process.
        ProcessStartInfo StartInfo { get; }

        // Start running in the background.
        void Start();
    }

    // Represents a background process.
    public class BackgroundProcess : IBackgroundProcess
    {
        // Creates background processes.
        public class Factory
        {
            public virtual IBackgroundProcess Create(
                string fileName, string arguments, string workingDirectory)
            {
                return new BackgroundProcess(
                    new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        WorkingDirectory = workingDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });
            }
        }

        Process process;
        BackgroundProcess(ProcessStartInfo startInfo)
        {
            process = new Process
            {
                StartInfo = startInfo,
            };
        }

        public int Id => process.Id;
        public string ProcessName => process.ProcessName;
        public ProcessStartInfo StartInfo => process.StartInfo;

        public void Start()
        {
            try
            {
                process.Start();
                Trace.WriteLine(string.Format("Started background process {0} {1} with id {2}",
                    process.StartInfo.FileName, process.StartInfo.Arguments, process.Id));
            }
            catch (Exception e)
            {
                // Throw the same exception as ManagedProcess, to take advantage of its integration
                // with metrics recording. Semantically, both classes start a new process.
                //
                // Warning: don't access any property of Process other than StartInfo, because
                // they are not valid if the process could not be started.
                throw new ProcessException($"Error launching '{StartInfo.FileName}'", e);
            }
        }
    }
}
