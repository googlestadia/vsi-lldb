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
using TestsCommon.TestSupport;

namespace Google.VisualStudioFake.Internal.Interop
{
    public class DefaultPort : Port, IDebugDefaultPort2
    {
        public class Factory
        {
            public Port Create(IDebugPortNotify2 portNotify) =>
                new DefaultPort(portNotify);
        }

        readonly IDebugPortNotify2 portNotify;

        DefaultPort(IDebugPortNotify2 portNotify)
        {
            this.portNotify = portNotify;
        }

        public int GetPortNotify(out IDebugPortNotify2 ppPortNotify)
        {
            ppPortNotify = portNotify;
            return VSConstants.S_OK;
        }

        #region Not Implemented

        public int GetServer(out IDebugCoreServer3 ppServer)
        {
            throw new NotImplementedTestDoubleException();
        }

        public int QueryIsLocal()
        {
            throw new NotImplementedTestDoubleException();
        }

        #endregion
    }
}
