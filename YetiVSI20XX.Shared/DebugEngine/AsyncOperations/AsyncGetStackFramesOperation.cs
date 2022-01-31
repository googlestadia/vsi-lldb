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

namespace YetiVSI.DebugEngine.AsyncOperations
{
    public class AsyncGetStackFramesOperation : IAsyncDebugEngineOperation
    {
        readonly StackFramesProvider _stackFramesProvider;
        readonly enum_FRAMEINFO_FLAGS _fieldSpec;
        readonly IAsyncDebugGetFramesCompletionHandler _completionHandler;
        readonly ITaskExecutor _taskExecutor;
        readonly IDebugThread _debugThread;

        CancellationTokenSource _cancelSource;

        public AsyncGetStackFramesOperation(IDebugThread debugThread,
            StackFramesProvider stackFramesProvider, enum_FRAMEINFO_FLAGS fieldSpec,
            IAsyncDebugGetFramesCompletionHandler completionHandler, ITaskExecutor taskExecutor)
        {
            _debugThread = debugThread;
            _stackFramesProvider = stackFramesProvider;
            _fieldSpec = fieldSpec;
            _completionHandler = completionHandler;
            _taskExecutor = taskExecutor;
        }

        public int BeginExecute()
        {
            _cancelSource = new CancellationTokenSource();
            SafeErrorUtil.SafelyLogErrorAndForget(
                ExecuteAsync,
                "Error processing async get frames request");
            return VSConstants.S_OK;
        }

        public int Cancel()
        {
            _cancelSource?.Cancel();
            _completionHandler.OnComplete(VSConstants.E_ABORT, null);
            return VSConstants.S_OK;
        }

        async Task ExecuteAsync()
        {
            try
            {
                IListDebugFrameInfo frames = await _taskExecutor.SubmitAsync(
                    GetFramesAsync, _cancelSource.Token, nameof(ExecuteAsync),
                    typeof(AsyncGetStackFramesOperation));

                int status = frames != null
                    ? VSConstants.S_OK
                    : VSConstants.E_FAIL;
                _completionHandler.OnComplete(status, frames);
            }
            catch (OperationCanceledException)
            {
                // OnComplete has already been sent from Cancel()
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Error processing async get frames request: {e.Demystify()}");
                _completionHandler.OnComplete(e.HResult, null);
            }
            finally
            {
                _cancelSource.Dispose();
                _cancelSource = null;
            }
        }

        async Task<IListDebugFrameInfo> GetFramesAsync()
        {
            IList<FRAMEINFO> frameInfos =
                await _stackFramesProvider.GetAllAsync(_fieldSpec, _debugThread);
            return frameInfos == null ? null : new ListDebugFrameInfo(frameInfos.ToArray());
        }
    }
}
