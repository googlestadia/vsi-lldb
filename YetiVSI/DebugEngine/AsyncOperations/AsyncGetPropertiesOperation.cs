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

﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiCommon;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.DebugEngine.AsyncOperations
{
    public class AsyncGetPropertiesOperation : IAsyncDebugEngineOperation
    {
        readonly ITaskExecutor _taskExecutor;
        readonly IAsyncDebugGetPropertiesCompletionHandler _completionHandler;

        readonly IChildrenProvider _childrenProvider;

        readonly int _fromIndex;
        readonly int _requestedCount;

        CancellationTokenSource _cancelSource;

        public AsyncGetPropertiesOperation(ITaskExecutor taskExecutor,
                                           IAsyncDebugGetPropertiesCompletionHandler
                                               completionHandler,
                                           IChildrenProvider childrenProvider, int fromIndex,
                                           int requestedCount)
        {
            _taskExecutor = taskExecutor;
            _completionHandler = completionHandler;
            _childrenProvider = childrenProvider;
            _fromIndex = fromIndex;
            _requestedCount = requestedCount;
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
                                                    typeof(AsyncGetPropertiesOperation));

                uint total = (uint) await _childrenProvider.GetChildrenCountAsync();
                _completionHandler.OnComplete(VSConstants.S_OK, total, propertiesList);
            }
            catch (OperationCanceledException)
            {
                // OnComplete has already been sent from Cancel()
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Error processing async get properties request: {e}");
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
            var propertyInfos = new DEBUG_PROPERTY_INFO[_requestedCount];
            int returned =
                await _childrenProvider.GetChildrenAsync(_fromIndex, _requestedCount,
                                                         propertyInfos);

            return new ListDebugPropertyInfo(propertyInfos.Take(returned).ToArray());
        }
    }
}