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
using System.Collections.Generic;

namespace YetiVSI.Test.TestSupport.Lldb
{
    public class SbCommandInterpreterDummy : SbCommandInterpreter
    {
        HashSet<string> handledCommands = new HashSet<string>();

        public HashSet<string> HandledCommands => handledCommands;

        public ReturnStatus HandleCommand(string command, out SbCommandReturnObject result)
        {
            handledCommands.Add(command);
            result = new SbCommandReturnObjectStub(true);
            return ReturnStatus.SuccessFinishResult;
        }

        public void SourceInitFileInHomeDirectory()
        {
            InitFileSourced = true;
        }

        public bool InitFileSourced { get; set; }
    }
}
