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

namespace Google.VisualStudioFake.Internal.Interop
{
    public abstract class Port : IDebugPort2
    {
        IDebugProcess2 process;

        public void SetProcess(IDebugProcess2 process)
        {
            this.process = process;
        }

        public int GetProcess(AD_PROCESS_ID processId, out IDebugProcess2 ppProcess)
        {
            ppProcess = process;
            return VSConstants.S_OK;
        }

        #region Not Implemented

        public int GetPortName(out string pbstrName)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetPortId(out Guid pguidPort)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetPortRequest(out IDebugPortRequest2 ppRequest)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int GetPortSupplier(out IDebugPortSupplier2 ppSupplier)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int EnumProcesses(out IEnumDebugProcesses2 ppEnum)
        {
            throw new NotImplementedTestDoubleException();
        }

        #endregion
    }
}
