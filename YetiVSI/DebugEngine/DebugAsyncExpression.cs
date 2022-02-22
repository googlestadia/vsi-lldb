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

using DebuggerApi;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Threading;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.CastleAspects;
using YetiVSI.DebugEngine.AsyncOperations;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.DebugEngine
{
    public interface IDebugExpression : IDebugExpression2, IDebugExpression157
    {
    }

    public interface IDebugExpressionFactory
    {
        IDebugExpression Create(RemoteFrame frame, string text,
                                IDebugEngineHandler debugEngineHandler,
                                IGgpDebugProgram debugProgram, IDebugThread2 thread);
    }

    public class DebugAsyncExpression : SimpleDecoratorSelf<IDebugExpression>, IDebugExpression
    {
        public class Factory : IDebugExpressionFactory
        {
            readonly AsyncExpressionEvaluator.Factory _asyncEvaluatorFactory;
            readonly ITaskExecutor _taskExecutor;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            public Factory(AsyncExpressionEvaluator.Factory asyncEvaluatorFactory,
                           ITaskExecutor taskExecutor)
            {
                _asyncEvaluatorFactory = asyncEvaluatorFactory;
                _taskExecutor = taskExecutor;
            }

            public virtual IDebugExpression Create(RemoteFrame frame, string text,
                                                   IDebugEngineHandler debugEngineHandler,
                                                   IGgpDebugProgram debugProgram,
                                                   IDebugThread2 thread)
            {
                var evaluator = _asyncEvaluatorFactory.Create(debugProgram.Target, frame, text);
                return new DebugAsyncExpression(debugEngineHandler, evaluator, _taskExecutor,
                                                debugProgram, thread);
            }
        }

        readonly IGgpDebugProgram _program;
        readonly IDebugThread2 _thread;

        readonly IDebugEngineHandler _debugEngineHandler;
        readonly IAsyncExpressionEvaluator _asyncEvaluator;
        readonly ITaskExecutor _taskExecutor;

        DebugAsyncExpression(IDebugEngineHandler debugEngineHandler,
                             IAsyncExpressionEvaluator asyncEvaluator, ITaskExecutor taskExecutor,
                             IGgpDebugProgram program, IDebugThread2 thread)
        {
            _debugEngineHandler = debugEngineHandler;
            _asyncEvaluator = asyncEvaluator;
            _taskExecutor = taskExecutor;
            _program = program;
            _thread = thread;
        }

        public int Abort() => VSConstants.E_NOTIMPL;

        public int EvaluateAsync(enum_EVALFLAGS flags, IDebugEventCallback2 callback)
        {
            Task Evaluate() =>
                _taskExecutor.SubmitAsync(async () =>
                {
                    EvaluationResult evalResult = await _asyncEvaluator.EvaluateExpressionAsync();
                    _debugEngineHandler.OnEvaluationComplete(
                        Self, evalResult.Result, _program, _thread);
                }, CancellationToken.None, nameof(EvaluateAsync), typeof(DebugAsyncExpression));

            SafeErrorUtil.SafelyLogErrorAndForget(Evaluate,
                                                  "EvaluateAsync: error evaluating " +
                                                  "expression asynchronously");

            return VSConstants.S_OK;
        }

        public int EvaluateSync(enum_EVALFLAGS flags, uint timeout, IDebugEventCallback2 callback,
                                out IDebugProperty2 result)
        {
            EvaluationResult evalResult =
                _taskExecutor.Run(async () => await _asyncEvaluator.EvaluateExpressionAsync());
            result = evalResult.Result;
            return evalResult.Status;
        }

        public int GetEvaluateAsyncOp(uint dwFields, uint dwRadix, uint dwFlags, uint dwTimeout,
                                      IAsyncDebugEvaluateCompletionHandler pCompletionHandler,
                                      out IAsyncDebugEngineOperation ppDebugOperation)
        {
            ppDebugOperation = new AsyncEvaluateExpressionOperation(pCompletionHandler,
                                                                    _asyncEvaluator, _taskExecutor);
            return VSConstants.S_OK;
        }
    }
}