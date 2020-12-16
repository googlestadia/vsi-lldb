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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using YetiCommon;
using YetiVSI.DebugEngine.Interfaces;
using Microsoft.VisualStudio.Debugger.Interop;

namespace YetiVSI.DebugEngine.AsyncOperations
{
    // This is a temporary implementation and will be changed in the context of (internal).
    // Task will be submitted to the queue instead, tests will also follow.
    public class AsyncEvaluateExpressionOperation : IAsyncDebugEngineOperation
    {
        readonly IAsyncDebugEvaluateCompletionHandler _completionHandler;
        readonly IAsyncExpressionEvaluator _asyncEvaluator;
        readonly ITaskExecutor _taskExecutor;

        CancellationTokenSource _cancelSource;

        public AsyncEvaluateExpressionOperation(
            IAsyncDebugEvaluateCompletionHandler completionHandler,
            IAsyncExpressionEvaluator asyncEvaluator, ITaskExecutor taskExecutor)
        {
            _completionHandler = completionHandler;
            _asyncEvaluator = asyncEvaluator;
            _taskExecutor = taskExecutor;
        }

        public int BeginExecute()
        {
            _cancelSource = new CancellationTokenSource();
            SafeErrorUtil.SafelyLogErrorAndForget(ExecuteAsync,
                                                  "Error processing async evaluate request");
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
                EvaluationResult result = await _taskExecutor.SubmitAsync(
                    _asyncEvaluator.EvaluateExpressionAsync, _cancelSource.Token,
                    nameof(ExecuteAsync), typeof(AsyncEvaluateExpressionOperation));
                _completionHandler.OnComplete(result.Status, result.Result);
            }
            catch (OperationCanceledException)
            {
                // OnComplete has already been sent from Cancel()
            }
            catch (Exception e)
            {
                Trace.WriteLine("Error processing async evaluate request: " + e);
                _completionHandler.OnComplete(e.HResult, null);
            }
            finally
            {
                _cancelSource.Dispose();
                _cancelSource = null;
            }
        }
    }
}