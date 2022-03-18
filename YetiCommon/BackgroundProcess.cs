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
using System.ComponentModel;
using System.Diagnostics;

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

        // Terminates the process.
        void Kill();
    }

    // Represents a background process.
    public class BackgroundProcess : IBackgroundProcess
    {
        // Creates background processes.
        public class Factory
        {
            public virtual IBackgroundProcess Create(string fileName, string arguments,
                                                     string workingDirectory)
            {
                return new BackgroundProcess(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            }
        }

        Process _process;

        BackgroundProcess(ProcessStartInfo startInfo)
        {
            _process = new Process
            {
                StartInfo = startInfo,
            };
        }

        public int Id => _process.Id;
        public string ProcessName => _process.ProcessName;
        public ProcessStartInfo StartInfo => _process.StartInfo;

        public void Start()
        {
            try
            {
                _process.Start();
                Trace.WriteLine($"Started background process {_process.StartInfo.FileName} " +
                                $"{_process.StartInfo.Arguments} with id {_process.Id}");
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

        public void Kill()
        {
            try
            {
                // ProcessName and Id are not available anymore if the process already stopped.
                Trace.WriteLine($"Killing process {StartInfo.FileName}");
                _process.Kill();
            }
            catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
            {
                Trace.WriteLine($"Could not kill process {StartInfo.FileName}," +
                                " probably already stopping or stopped");
            }
        }
    }
}