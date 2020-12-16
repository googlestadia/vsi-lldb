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

﻿using DebuggerApi;
using DebuggerGrpcClient;
using System;
using System.Collections.Generic;
using TestsCommon.TestSupport;

namespace YetiVSI.Test.TestSupport.Lldb
{
    public class GrpcDebuggerFactoryFake : GrpcDebuggerFactory
    {
        private readonly TimeSpan waitTime;


        public GrpcDebuggerFactoryFake(TimeSpan waitTime, bool stadiaPlatformAvailable)
        {
            this.waitTime = waitTime;

            Debugger = new SbDebuggerFake(stadiaPlatformAvailable);
        }

        public void SetTargetAttachError(string errorString)
        {
            Debugger.SetTargetAttachError(errorString);
        }

        public override SbDebugger Create(
            GrpcConnection connection, bool sourceInitFiles, TimeSpan retryWaitTime)
        {
            if (waitTime > retryWaitTime)
            {
                return null;
            }
            return Debugger;
        }

        public SbDebuggerFake Debugger { get; }
    }

    /// <summary>
    /// Supports adding and removing targets and enabling LLDB logs. Enabling logs for other
    /// channels will throw a NotSupportedException.
    /// </summary>
    public class SbDebuggerFake : SbDebugger
    {
        List<RemoteTarget> targets = new List<RemoteTarget>();
        SbPlatform selectedPlatform;
        SbCommandInterpreterDummy commandInterpreter = new SbCommandInterpreterDummy();
        readonly bool _stadiaPlatformAvailable;
        string _targetAttachErrorString = null;
        bool async;
        bool skipLLDBInitFiles;

        readonly Dictionary<string, List<string>> enabledLogs =
            new Dictionary<string, List<string>>()
        {
                { "lldb", new List<string>()},
        };

        public SbCommandInterpreterDummy CommandInterpreter { get; }

        public SbDebuggerFake(bool stadiaPlatformAvailable)
        {
            _stadiaPlatformAvailable = stadiaPlatformAvailable;
            CommandInterpreter = new SbCommandInterpreterDummy();
        }

        public bool IsInitFileSourced => commandInterpreter.InitFileSourced;

        public RemoteTarget CreateTarget(string filename)
        {
            var target = new RemoteTargetStub(filename, _targetAttachErrorString);
            targets.Add(target);
            return target;
        }

        public bool DeleteTarget(RemoteTarget target)
            => targets.Remove(target);

        public bool EnableLog(string channel, IEnumerable<string> types)
        {
            if (!enabledLogs.ContainsKey(channel))
            {
                throw new NotImplementedTestDoubleException(
                    $"Enabling logs for {channel} is not supported");
            }
            enabledLogs[channel].AddRange(types);
            return true;
        }

        public bool IsPlatformAvailable(string platformName)
        {
            return _stadiaPlatformAvailable &&
                (platformName.Equals("remote-stadia", StringComparison.OrdinalIgnoreCase));
        }

        public SbCommandInterpreter GetCommandInterpreter()
        {
            return commandInterpreter;
        }

        public SbPlatform GetSelectedPlatform()
        {
            return selectedPlatform;
        }

        public void SetAsync(bool async)
        {
            this.async = async;
        }

        public void SkipLLDBInitFiles(bool skip)
        {
            this.skipLLDBInitFiles = skip;
        }

        public void SetSelectedPlatform(SbPlatform platform)
        {
            this.selectedPlatform = platform;
        }

        public void SetTargetAttachError(string errorString)
        {
            _targetAttachErrorString = errorString;
        }
    }
}
