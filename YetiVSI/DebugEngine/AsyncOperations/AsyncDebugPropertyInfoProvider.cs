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

﻿using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.DebugEngine.AsyncOperations
{
    public class AsyncDebugPropertyInfoProvider : IAsyncDebugPropertyInfoProvider
    {
        readonly ITaskExecutor _taskExecutor;
        readonly IChildrenProvider _childrenProvider;

        public AsyncDebugPropertyInfoProvider(IChildrenProvider childrenProvider,
                                              ITaskExecutor taskExecutor)
        {
            _childrenProvider = childrenProvider;
            _taskExecutor = taskExecutor;
        }

        public int GetPropertiesAsync(uint firstIndex, uint count,
                                      IAsyncDebugGetPropertiesCompletionHandler pCompletionHandler,
                                      out IAsyncDebugEngineOperation ppDebugOperation)
        {
            ppDebugOperation = new AsyncGetPropertiesOperation(_taskExecutor, pCompletionHandler,
                                                               _childrenProvider, (int) firstIndex,
                                                               (int) count);

            return VSConstants.S_OK;
        }
    }
}