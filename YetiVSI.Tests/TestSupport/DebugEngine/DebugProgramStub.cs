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

﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using TestsCommon.TestSupport;

namespace YetiVSI.Test.TestSupport.DebugEngine
{
    class DebugProgramStub : IDebugProgram2
    {
        readonly IDebugProcess2 debugProcess;
        readonly Guid programId;

        public DebugProgramStub(IDebugProcess2 debugProcess, Guid programId)
        {
            this.debugProcess = debugProcess;
            this.programId = programId;
        }

        public int GetProcess(out IDebugProcess2 ppProcess)
        {
            ppProcess = debugProcess;
            return VSConstants.S_OK;
        }

        public int GetProgramId(out Guid pguidProgramId)
        {
            pguidProgramId = programId;
            return VSConstants.S_OK;
        }

        #region Not Implemented

        public int Attach(IDebugEventCallback2 pCallback)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int CanDetach()
        {
            throw new NotImplementedTestDoubleException();
        }

        public int CauseBreak()
        {
            throw new NotImplementedTestDoubleException();
        }

        public int Continue(IDebugThread2 pThread)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int Detach()
        {
            throw new NotImplementedTestDoubleException();
        }

        public int EnumCodeContexts(IDebugDocumentPosition2 pDocPos,
            out IEnumDebugCodeContexts2 ppEnum)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int EnumCodePaths(string pszHint, IDebugCodeContext2 pStart,
            IDebugStackFrame2 pFrame, int fSource, out IEnumCodePaths2 ppEnum,
            out IDebugCodeContext2 ppSafety)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int EnumModules(out IEnumDebugModules2 ppEnum)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int EnumThreads(out IEnumDebugThreads2 ppEnum)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int Execute()
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetDebugProperty(out IDebugProperty2 ppProperty)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE dwScope,
            IDebugCodeContext2 pCodeContext, out IDebugDisassemblyStream2 ppDisassemblyStream)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetENCUpdate(out object ppUpdate)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetEngineInfo(out string pbstrEngine, out Guid pguidEngine)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetName(out string pbstrName)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int Step(IDebugThread2 pThread, enum_STEPKIND sk, enum_STEPUNIT Step)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int Terminate()
        {
            throw new NotImplementedTestDoubleException();
        }

        public int WriteDump(enum_DUMPTYPE DUMPTYPE, string pszDumpUrl)
        {
            throw new NotImplementedTestDoubleException();
        }

        #endregion
    }
}
