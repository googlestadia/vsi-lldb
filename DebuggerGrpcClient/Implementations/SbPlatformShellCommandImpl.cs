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

namespace DebuggerGrpcClient
{
    // Creates SBPlatformShellCommand objects.
    public class GrpcPlatformShellCommandFactory
    {
        public SbPlatformShellCommand Create(string command)
        {
            return new SbPlatformShellCommandImpl(command);
        }
    }

    // Implementation of the SBPlatformShellCommand interface that uses GRPC to make RPCs to a
    // remote endpoint.
    class SbPlatformShellCommandImpl : SbPlatformShellCommand
    {
        readonly string command;
        string output;
        int signal;
        int status;

        internal SbPlatformShellCommandImpl(string command)
        {
            this.command = command;
        }

        #region SbPlatformConnectOptions

        public string GetCommand()
        {
            return command;
        }

        public void SetOutput(string output)
        {
            this.output = output;
        }

        public string GetOutput()
        {
            return output;
        }

        public void SetSignal(int signal)
        {
            this.signal = signal;
        }

        public int GetSignal()
        {
            return signal;
        }

        public void SetStatus(int status)
        {
            this.status = status;
        }

        public int GetStatus()
        {
            return status;
        }

        #endregion
    }
}
