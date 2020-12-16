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
    public delegate IDebugExpression CreateDebugExpressionDelegate(RemoteFrame frame, string text,
        IDebugEngineHandler debugEngineHandler, IGgpDebugProgram debugProgram,
        IDebugThread2 thread);

    public interface IDebugExpression : IDebugExpression2
    {
    }

    public interface IDebugAsyncExpression : IDebugExpression, IDebugExpression157
    {
    }

    // We need to have a separate factory for different implementations of DebugCommonExpression.
    // In order to castle proxy to work properly, DebugAsyncExpression.Factory must return
    // IDebugAsyncExpression interface, which implements IDebugExpression157 required for
    // asynchronous expressions evaluation. On the other hand, DebugExpression.Factory must return
    // IDebugExpression interface which doesn't include asynchronous feature.
    // If we had a common interface for factories:
    // interface IDebugExpressionFactory {
    //     IDebugExpression Create(...);
    // }
    // then DebugAsyncExpression.Factory would return IDebugExpression, which would make castle
    // proxy erase the information about IDebugExpression157 in runtime.
    public abstract class DebugCommonExpression : SimpleDecoratorSelf<IDebugExpression>,
        IDebugExpression
    {
        readonly IGgpDebugProgram program;
        readonly IDebugThread2 thread;

        readonly IDebugEngineHandler debugEngineHandler;
        readonly IAsyncExpressionEvaluator asyncEvaluator;
        readonly ITaskExecutor taskExecutor;

        protected DebugCommonExpression(IDebugEngineHandler debugEngineHandler,
            IAsyncExpressionEvaluator asyncEvaluator, ITaskExecutor taskExecutor,
            IGgpDebugProgram program, IDebugThread2 thread)
        {
            this.debugEngineHandler = debugEngineHandler;
            this.asyncEvaluator = asyncEvaluator;
            this.taskExecutor = taskExecutor;
            this.program = program;
            this.thread = thread;
        }

        public int Abort()
        {
            return VSConstants.E_NOTIMPL;
        }

        public int EvaluateAsync(enum_EVALFLAGS flags, IDebugEventCallback2 callback)
        {
            Func<Task> evaluationTask = () => taskExecutor.SubmitAsync(async () =>
            {
                EvaluationResult evalResult = await asyncEvaluator.EvaluateExpressionAsync();
                debugEngineHandler.OnEvaluationComplete(Self, evalResult.Result, program, thread);
            }, CancellationToken.None, nameof(EvaluateAsync), typeof(DebugExpression));

            SafeErrorUtil.SafelyLogErrorAndForget(evaluationTask,
                "EvaluateAsync: error evaluating expression asynchronously");

            return VSConstants.S_OK;
        }

        public int EvaluateSync(enum_EVALFLAGS flags, uint timeout, IDebugEventCallback2 callback,
            out IDebugProperty2 result)
        {
            EvaluationResult evalResult = taskExecutor
                .Run(async () => await asyncEvaluator.EvaluateExpressionAsync());
            result = evalResult.Result;
            return evalResult.Status;
        }
    }

    public class DebugExpression : DebugCommonExpression
    {
        public class Factory
        {
            readonly AsyncExpressionEvaluator.Factory asyncEvaluatorFactory;
            readonly ITaskExecutor taskExecutor;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            public Factory(AsyncExpressionEvaluator.Factory asyncEvaluatorFactory,
                ITaskExecutor taskExecutor)
            {
                this.asyncEvaluatorFactory = asyncEvaluatorFactory;
                this.taskExecutor = taskExecutor;
            }

            public virtual IDebugExpression Create(RemoteFrame frame, string text,
                IDebugEngineHandler debugEngineHandler,
                IGgpDebugProgram debugProgram, IDebugThread2 thread)
                => new DebugExpression(debugEngineHandler,
                    asyncEvaluatorFactory.Create(frame, text), taskExecutor,
                    debugProgram, thread);
        }

        private DebugExpression(IDebugEngineHandler debugEngineHandler,
            IAsyncExpressionEvaluator asyncEvaluator, ITaskExecutor taskExecutor,
            IGgpDebugProgram program, IDebugThread2 thread) :
            base(debugEngineHandler, asyncEvaluator, taskExecutor, program, thread)
        {
        }
    }

    // TODO (internal) Merge with DebugExpression class
    public class DebugAsyncExpression : DebugCommonExpression, IDebugAsyncExpression
    {
        public class Factory
        {
            readonly AsyncExpressionEvaluator.Factory asyncEvaluatorFactory;
            readonly ITaskExecutor taskExecutor;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            public Factory(AsyncExpressionEvaluator.Factory asyncEvaluatorFactory,
                ITaskExecutor taskExecutor)
            {
                this.asyncEvaluatorFactory = asyncEvaluatorFactory;
                this.taskExecutor = taskExecutor;
            }

            public virtual IDebugAsyncExpression Create(RemoteFrame frame, string text,
                IDebugEngineHandler debugEngineHandler, IGgpDebugProgram debugProgram,
                IDebugThread2 thread)
            {
                IAsyncExpressionEvaluator evaluator = asyncEvaluatorFactory.Create(frame, text);
                return new DebugAsyncExpression(debugEngineHandler, evaluator, taskExecutor,
                    debugProgram, thread);
            }
        }

        readonly IAsyncExpressionEvaluator asyncEvaluator;
        readonly ITaskExecutor taskExecutor;

        private DebugAsyncExpression(IDebugEngineHandler debugEngineHandler,
            IAsyncExpressionEvaluator asyncEvaluator, ITaskExecutor taskExecutor,
            IGgpDebugProgram program, IDebugThread2 thread) :
            base(debugEngineHandler, asyncEvaluator, taskExecutor, program, thread)
        {
            this.asyncEvaluator = asyncEvaluator;
            this.taskExecutor = taskExecutor;
        }

        public int GetEvaluateAsyncOp(uint dwFields, uint dwRadix, uint dwFlags, uint dwTimeout,
            IAsyncDebugEvaluateCompletionHandler pCompletionHandler,
            out IAsyncDebugEngineOperation ppDebugOperation)
        {
            ppDebugOperation = new AsyncEvaluateExpressionOperation(pCompletionHandler,
                asyncEvaluator, taskExecutor);
            return VSConstants.S_OK;
        }
    }
}