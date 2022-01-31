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
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio;
using YetiCommon;

namespace YetiVSI.PortSupplier
{
    // DebugProgram contains execution information about a process.
    public class DebugProgram : IDebugProgram2
    {
        public class Factory
        {
            readonly DebugProperty.Factory debugPropertyFactory;
            readonly IExtensionOptions options;

            // For test substitution.
            public Factory() { }

            public Factory(DebugProperty.Factory debugPropertyFactory, IExtensionOptions options)
            {
                this.debugPropertyFactory = debugPropertyFactory;
                this.options = options;
            }

            public virtual IDebugProgram2 Create(DebugProcess process)
            {
                return new DebugProgram(debugPropertyFactory, options, process);
            }
        }

        readonly DebugProperty.Factory debugPropertyFactory;
        readonly DebugProcess process;
        readonly Guid guid;

        private DebugProgram(DebugProperty.Factory debugPropertyFactory, IExtensionOptions options,
            DebugProcess process)
        {
            this.debugPropertyFactory = debugPropertyFactory;
            this.process = process;
            this.guid = Guid.NewGuid();
        }

        public int Attach(IDebugEventCallback2 callback)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int CanDetach()
        {
            return VSConstants.E_NOTIMPL;
        }

        public int CauseBreak()
        {
            return VSConstants.E_NOTIMPL;
        }

        public int Continue(IDebugThread2 thread)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int Detach()
        {
            return VSConstants.E_NOTIMPL;
        }

        public int EnumCodeContexts(
            IDebugDocumentPosition2 docPos, out IEnumDebugCodeContexts2 contextsEnum)
        {
            contextsEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        public int EnumCodePaths(
            string hint, IDebugCodeContext2 start, IDebugStackFrame2 frame, int source,
            out IEnumCodePaths2 pathsEnum, out IDebugCodeContext2 safety)
        {
            pathsEnum = null;
            safety = null;
            return VSConstants.E_NOTIMPL;
        }

        public int EnumModules(out IEnumDebugModules2 modulesEnum)
        {
            modulesEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        public int EnumThreads(out IEnumDebugThreads2 threadsEnum)
        {
            threadsEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        public int Execute()
        {
            return VSConstants.E_NOTIMPL;
        }

        public int GetDebugProperty(out IDebugProperty2 property)
        {
            property = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetDisassemblyStream(
            enum_DISASSEMBLY_STREAM_SCOPE scope, IDebugCodeContext2 codeContext,
            out IDebugDisassemblyStream2 disassemblyStream)
        {
            disassemblyStream = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetENCUpdate(out object update)
        {
            update = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetEngineInfo(out string engine, out Guid engineGuid)
        {
            engine = YetiConstants.YetiTitle;
            engineGuid = YetiConstants.DebugEngineGuid;
            return VSConstants.S_OK;
        }

        public int GetMemoryBytes(out IDebugMemoryBytes2 memoryBytes)
        {
            memoryBytes = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetName(out string name)
        {
            return process.GgpGetName(enum_GETNAME_TYPE.GN_BASENAME, out name);
        }

        public int GetProcess(out IDebugProcess2 process)
        {
            process = this.process;
            return VSConstants.S_OK;
        }

        public int GetProgramId(out Guid guid)
        {
            guid = this.guid;
            return VSConstants.S_OK;
        }

        public int Step(IDebugThread2 thread, enum_STEPKIND sk, enum_STEPUNIT step)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int Terminate()
        {
            return VSConstants.E_NOTIMPL;
        }

        public int WriteDump(enum_DUMPTYPE dumpType, string dumpUrl)
        {
            return VSConstants.E_NOTIMPL;
        }
    }
}