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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YetiCommon;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.AsyncOperations
{
    public class AsyncGetRootPropertiesOperation : IAsyncDebugEngineOperation
    {
        readonly FrameVariablesProvider _frameVariablesProvider;
        readonly ITaskExecutor _taskExecutor;
        readonly IAsyncDebugGetPropertiesCompletionHandler _completionHandler;
        readonly IChildrenProviderFactory _childrenProviderFactory;
        readonly enum_DEBUGPROP_INFO_FLAGS _fields;
        readonly uint _radix;
        readonly Guid _guidFilter;

        CancellationTokenSource _cancelSource;

        public AsyncGetRootPropertiesOperation(FrameVariablesProvider frameVariablesProvider,
                                               ITaskExecutor taskExecutor,
                                               IAsyncDebugGetPropertiesCompletionHandler
                                                   completionHandler,
                                               IChildrenProviderFactory childrenProviderFactory,
                                               enum_DEBUGPROP_INFO_FLAGS fields, uint radix,
                                               Guid guidFilter)
        {
            _frameVariablesProvider = frameVariablesProvider;
            _taskExecutor = taskExecutor;
            _completionHandler = completionHandler;
            _childrenProviderFactory = childrenProviderFactory;
            _fields = fields;
            _radix = radix;
            _guidFilter = guidFilter;
        }

        public int BeginExecute()
        {
            _cancelSource = new CancellationTokenSource();
            SafeErrorUtil.SafelyLogErrorAndForget(ExecuteAsync,
                                                  "Error processing async get property request");

            return VSConstants.S_OK;
        }

        public int Cancel()
        {
            _cancelSource?.Cancel();
            _completionHandler.OnComplete(VSConstants.E_ABORT, 0, null);
            return VSConstants.S_OK;
        }

        async Task ExecuteAsync()
        {
            try
            {
                IListDebugPropertyInfo propertiesList =
                    await _taskExecutor.SubmitAsync(GetPropertiesInfoAsync, _cancelSource.Token,
                                                    nameof(ExecuteAsync),
                                                    typeof(AsyncGetRootPropertiesOperation));

                int status = propertiesList != null ? VSConstants.S_OK : VSConstants.E_FAIL;
                _completionHandler.OnComplete(status, (uint) (propertiesList?.Count ?? 0),
                                              propertiesList);
            }
            catch (OperationCanceledException)
            {
                // OnComplete has already been sent from Cancel()
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Error processing async get root properties request: {e}");
                _completionHandler.OnComplete(e.HResult, 0, null);
            }
            finally
            {
                _cancelSource.Dispose();
                _cancelSource = null;
            }
        }

        async Task<IListDebugPropertyInfo> GetPropertiesInfoAsync()
        {
            ICollection<IVariableInformation> varInfos = _frameVariablesProvider.Get(_guidFilter);

            if (varInfos == null)
            {
                return null;
            }

            var childrenProvider = _childrenProviderFactory.Create(
                new ListChildAdapter.Factory().Create(varInfos.ToList()), _fields, _radix);

            var propertyInfos = new DEBUG_PROPERTY_INFO[varInfos.Count];
            int returned = await childrenProvider.GetChildrenAsync(
                0, varInfos.Count, propertyInfos);

            return new ListDebugPropertyInfo(propertyInfos.Take(returned).ToArray());
        }
    }
}