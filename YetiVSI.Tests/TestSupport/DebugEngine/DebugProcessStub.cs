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
    public class DebugProcessStub : IDebugProcess2
    {
        private readonly enum_AD_PROCESS_ID processIdType;
        private uint processId;

        public DebugProcessStub(enum_AD_PROCESS_ID processIdType, uint processId)
        {
            this.processIdType = processIdType;
            this.processId = processId;
        }
        public int GetPhysicalProcessId(AD_PROCESS_ID[] pProcessId)
        {
            pProcessId[0] = new AD_PROCESS_ID
            {
                dwProcessId = processId,
                ProcessIdType = (uint)processIdType
            };
            return VSConstants.S_OK;
        }

        #region Not Implemented

        public int Attach(IDebugEventCallback2 pCallback, Guid[] rgguidSpecificEngines,
            uint celtSpecificEngines, int[] rghrEngineAttach)
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

        public int Detach()
        {
            throw new NotImplementedTestDoubleException();
        }

        public int EnumPrograms(out IEnumDebugPrograms2 ppEnum)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int EnumThreads(out IEnumDebugThreads2 ppEnum)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetAttachedSessionName(out string pbstrSessionName)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetInfo(enum_PROCESS_INFO_FIELDS Fields, PROCESS_INFO[] pProcessInfo)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetName(enum_GETNAME_TYPE gnType, out string pbstrName)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetPort(out IDebugPort2 ppPort)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetProcessId(out Guid pguidProcessId)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetServer(out IDebugCoreServer2 ppServer)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int Terminate()
        {
            throw new NotImplementedTestDoubleException();
        }

        #endregion
    }
}
