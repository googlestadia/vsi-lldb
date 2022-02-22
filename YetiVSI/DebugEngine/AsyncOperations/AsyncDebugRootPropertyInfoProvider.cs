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
using DebuggerApi;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.DebugEngine.AsyncOperations
{
    public class AsyncDebugRootPropertyInfoProvider : IAsyncDebugPropertyInfoProvider
    {
        readonly RemoteTarget _target;
        readonly FrameVariablesProvider _frameVariablesProvider;
        readonly ITaskExecutor _taskExecutor;
        readonly IChildrenProviderFactory _childrenProviderFactory;
        readonly enum_DEBUGPROP_INFO_FLAGS _fields;
        readonly uint _radix;
        readonly Guid _guidFilter;

        public AsyncDebugRootPropertyInfoProvider(RemoteTarget target,
                                                  FrameVariablesProvider frameVariablesProvider,
                                                  ITaskExecutor taskExecutor,
                                                  IChildrenProviderFactory childrenProviderFactory,
                                                  enum_DEBUGPROP_INFO_FLAGS fields, uint radix,
                                                  Guid guidFilter)
        {
            _target = target;
            _frameVariablesProvider = frameVariablesProvider;
            _taskExecutor = taskExecutor;
            _childrenProviderFactory = childrenProviderFactory;
            _fields = fields;
            _radix = radix;
            _guidFilter = guidFilter;
        }

        public int GetPropertiesAsync(uint firstIndex, uint count,
                                      IAsyncDebugGetPropertiesCompletionHandler pCompletionHandler,
                                      out IAsyncDebugEngineOperation ppDebugOperation)
        {
            ppDebugOperation = new AsyncGetRootPropertiesOperation(_target,
                                                                   _frameVariablesProvider,
                                                                   _taskExecutor,
                                                                   pCompletionHandler,
                                                                   _childrenProviderFactory,
                                                                   _fields, _radix, _guidFilter);

            return VSConstants.S_OK;
        }
    }
}