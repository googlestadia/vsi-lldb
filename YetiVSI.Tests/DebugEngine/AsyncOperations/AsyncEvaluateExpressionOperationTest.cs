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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using NSubstitute;
using NUnit.Framework;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.AsyncOperations;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.Test.DebugEngine.AsyncOperations
{
    [TestFixture]
    class AsyncEvaluateExpressionOperationTest
    {
        ITaskExecutor _taskExecutor;
        AsyncEvaluateExpressionOperation _evaluateOp;
        IAsyncDebugEvaluateCompletionHandler _completionHandler;
        IAsyncExpressionEvaluator _asyncEvaluator;

        [SetUp]
        public void SetUp()
        {
            _completionHandler = Substitute.For<IAsyncDebugEvaluateCompletionHandler>();
            _asyncEvaluator = Substitute.For<IAsyncExpressionEvaluator>();
            _taskExecutor = Substitute.For<ITaskExecutor>();

            _evaluateOp = new AsyncEvaluateExpressionOperation(_completionHandler,
                _asyncEvaluator, _taskExecutor);
        }

        [Test]
        public async Task TaskIsSubmittedOnBeginAsync()
        {
            _evaluateOp.BeginExecute();
            await _taskExecutor.Received(1)
                .SubmitAsync(Arg.Any<Func<Task<EvaluationResult>>>(), Arg.Any<CancellationToken>(),
                             Arg.Any<string>(), Arg.Any<Type>());
        }

        [Test]
        public void CompletionHandlerIsInvoked()
        {
            var result = Substitute.For<IDebugProperty2>();
            _taskExecutor.SubmitAsync(Arg.Any<Func<Task<EvaluationResult>>>(),
                                      Arg.Any<CancellationToken>(), Arg.Any<string>(),
                                      Arg.Any<Type>())
                .Returns(Task.FromResult(EvaluationResult.FromResult(result)));
            _evaluateOp.BeginExecute();

            _completionHandler
                .Received(1)
                .OnComplete(VSConstants.S_OK, result);
        }

        [Test]
        public void CompletionHandlerIsInvokedOnCancel()
        {
            _evaluateOp.Cancel();

            _completionHandler
                .Received(1)
                .OnComplete(VSConstants.E_ABORT, null);
        }

        [Test]
        public void CompletionHandlerIsInvokedOnException()
        {
            var e = new TestException();

            _taskExecutor
                .SubmitAsync(Arg.Any<Func<Task<EvaluationResult>>>(), Arg.Any<CancellationToken>(),
                             Arg.Any<string>(), Arg.Any<Type>())
                .Returns(Task.FromException<EvaluationResult>(new TestException()));

            _evaluateOp.BeginExecute();

            _completionHandler
                .Received(1)
                .OnComplete(e.HResult, null);
        }

        private class TestException : Exception
        {
            const int TestHResult = 0x123;

            public TestException()
            {
                HResult = TestHResult;
            }
        }
    }
}