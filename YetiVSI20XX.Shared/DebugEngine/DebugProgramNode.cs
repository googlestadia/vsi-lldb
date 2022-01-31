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

using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio;
using System;
using YetiCommon;
using Microsoft.VisualStudio.Threading;
using YetiVSI.Util;

namespace YetiVSI.DebugEngine
{
    class DebugProgramNode : IDebugProgramNode2
    {
        private JoinableTaskContext taskContext;
        private IDebugProcess2 process;

        public DebugProgramNode(JoinableTaskContext taskContext, IDebugProcess2 process)
        {
            this.taskContext = taskContext;
            this.process = process;
        }

        public int GetEngineInfo(out string engine, out Guid engineGuid)
        {
            engine = YetiConstants.YetiTitle;
            engineGuid = YetiConstants.DebugEngineGuid;
            return VSConstants.S_OK;
        }

        public int GetProgramName(out string programName)
        {
            programName = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetHostName(enum_GETHOSTNAME_TYPE hostNameType, out string hostName)
        {
            hostName = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetHostPid(AD_PROCESS_ID[] hostProcessId)
        {
            taskContext.ThrowIfNotOnMainThread();
            return process.GetPhysicalProcessId(hostProcessId);
        }

        public int GetHostMachineName_V7(out string hostMachineName)
        {
            hostMachineName = null;
            return VSConstants.E_NOTIMPL;
        }

        public int Attach_V7(IDebugProgram2 program, IDebugEventCallback2 callback, uint reason)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int DetachDebugger_V7()
        {
            return VSConstants.E_NOTIMPL;
        }
    }
}
