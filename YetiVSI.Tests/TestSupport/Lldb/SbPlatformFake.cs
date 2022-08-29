
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

using DebuggerApi;
using DebuggerGrpcClient;
using System;
using System.Collections.Generic;
using System.Linq;

namespace YetiVSI.Test.TestSupport.Lldb
{
    public class PlatformFactoryFakeConnectRecorder
    {
        public int InvocationCount { get; private set; } = 0;

        public List<SbPlatformConnectOptions> InvocationOptions { get; }
            = new List<SbPlatformConnectOptions>();


        public void Add(SbPlatformConnectOptions sbPlatformConnectOptions)
        {
            InvocationOptions.Add(sbPlatformConnectOptions);
            InvocationCount++;
        }
    }

    public class GrpcPlatformFactoryFake : GrpcPlatformFactory
    {
        readonly List<PlatformProcess> _platformProcesses = new List<PlatformProcess>();
        readonly Dictionary<string, string> _processCommandToOutput =
            new Dictionary<string, string>();

        readonly PlatformFactoryFakeConnectRecorder _connectRecorder;
        readonly Queue<bool> _connectRemoteStatuses = new Queue<bool>();
        readonly Queue<bool> _runStatuses = new Queue<bool>();

        public GrpcPlatformFactoryFake(PlatformFactoryFakeConnectRecorder connectRecorder)
        {
            _connectRecorder = connectRecorder;
        }

        public override SbPlatform Create(string platformName, GrpcConnection grpcConnection) =>
            new SbPlatformFake(platformName,
                               _platformProcesses.Where(p => p.PlatformName == platformName),
                               _connectRecorder, _processCommandToOutput, _connectRemoteStatuses,
                               _runStatuses);

        public void AddFakeProcess(string platformName, string processName, uint pid) =>
            _platformProcesses.Add(new PlatformProcess
            {
                PlatformName = platformName,
                Name = processName,
                Pid = pid
            });

        public void AddCommandOutput(string command, string output)
        {
            _processCommandToOutput[command] = output;
        }

        public void AddConnectRemoteStatuses(params bool[] statuses)
        {
            foreach (var status in statuses)
            {
                _connectRemoteStatuses.Enqueue(status);
            }
        }

        public void AddRunStatuses(params bool[] statuses)
        {
            foreach (var status in statuses)
            {
                _runStatuses.Enqueue(status);
            }
        }

        /// <summary>
        /// This fake supports seeding with process name / pid pairs and running a single command
        /// type (pidof) to fetch the pid for a particular process. Other types of commands will
        /// throw a NotSupportedException. ConnectRemote has been set up to always succeed.
        /// </summary>
        class SbPlatformFake : SbPlatform
        {
            readonly IEnumerable<PlatformProcess> _processes;
            readonly PlatformFactoryFakeConnectRecorder _connectRecorder;
            readonly Dictionary<string, string> _processCommandToOutput;
            readonly Queue<bool> _connectRemoteStatuses;
            readonly Queue<bool> _runStatuses;

            public SbPlatformFake(string name, IEnumerable<PlatformProcess> processes,
                                  PlatformFactoryFakeConnectRecorder connectRecorder,
                                  Dictionary<string, string> processCommandToOutput,
                                  Queue<bool> connectRemoteStatuses, Queue<bool> runStatuses)
            {
                Name = name;

                _processes = processes;
                _connectRecorder = connectRecorder;
                _processCommandToOutput = processCommandToOutput;
                _connectRemoteStatuses = connectRemoteStatuses;
                _runStatuses = runStatuses;
            }

            public string Name { get; }

            public SbError ConnectRemote(SbPlatformConnectOptions connectOptions)
            {
                _connectRecorder?.Add(connectOptions);
                return _connectRemoteStatuses.Count > 0
                    ? new SbErrorStub(_connectRemoteStatuses.Dequeue())
                    : new SbErrorStub(true);
            }

            public SbError Run(SbPlatformShellCommand command)
            {
                if (_runStatuses.Count > 0)
                {
                    return new SbErrorStub(_runStatuses.Dequeue());
                }

                var commandText = command.GetCommand();
                if (commandText.Contains("game_pid.current") ||
                    commandText.Contains("/proc/*/cmdline"))
                {
                    // game_pid.current file gives pid of the game executable
                    var processName = "myGame";
                    var process = _processes.FirstOrDefault(p => p.Name == processName);
                    if (process == null)
                    {
                        return new SbErrorStub(false, $"unknown process: {processName}");
                    }
                    command.SetOutput(process.Pid.ToString());
                    return new SbErrorStub(true);
                }
                if (_processCommandToOutput.TryGetValue(commandText, out string output))
                {
                    if (output == null)
                    {
                        command.SetStatus(1);
                        command.SetOutput("");
                    }
                    else
                    {
                        command.SetStatus(0);
                        command.SetOutput(output);
                    }
                    return new SbErrorStub(true);
                }
                throw new NotSupportedException(
                    $"The command is not supported: Command: {commandText}");
            }
        }

        class PlatformProcess
        {
            public string PlatformName { get; set; }

            public string Name { get; set; }

            public uint Pid { get; set; }
        }
    }
}
