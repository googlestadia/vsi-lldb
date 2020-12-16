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
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.AsyncOperations;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSITestsCommon;

namespace YetiVSI.Test.DebugEngine.AsyncOperations
{
    [TestFixture]
    public class AsyncGetStackFramesOperationTest
    {
        const enum_FRAMEINFO_FLAGS _fieldSpec = enum_FRAMEINFO_FLAGS.FIF_ARGS_ALL;

        AsyncGetStackFramesOperation _getStackFramesOp;
        IDebugThread _debugThread;
        StackFramesProvider _stackFramesProvider;
        ITaskExecutor _taskExecutor;
        IAsyncDebugGetFramesCompletionHandler _completionHandler;

        [SetUp]
        public void SetUp()
        {
            _completionHandler = Substitute.For<IAsyncDebugGetFramesCompletionHandler>();
            _taskExecutor = Substitute.ForPartsOf<FakeTaskExecutor>();
            _debugThread = Substitute.For<IDebugThread>();
            _stackFramesProvider =
                Substitute.For<StackFramesProvider>(null, null, null, null, null);

            _getStackFramesOp = new AsyncGetStackFramesOperation(_debugThread, _stackFramesProvider,
                _fieldSpec, _completionHandler, _taskExecutor);
        }

        [Test]
        public async Task TaskIsSubmittedOnBeginAsync()
        {
            _getStackFramesOp.BeginExecute();
            await _taskExecutor.ReceivedWithAnyArgs(1)
                .SubmitAsync(Arg.Any<Func<Task<IListDebugFrameInfo>>>(),
                             Arg.Any<CancellationToken>(), Arg.Any<string>(), Arg.Any<Type>());
        }

        [Test]
        public void CompletionHandlerIsInvoked()
        {
            _stackFramesProvider.GetAllAsync(_fieldSpec, _debugThread)
                .ReturnsForAnyArgs(Task.FromResult((IList<FRAMEINFO>)new List<FRAMEINFO>()));

            _getStackFramesOp.BeginExecute();

            _completionHandler.Received(1).OnComplete(VSConstants.S_OK,
                Arg.Any<IListDebugFrameInfo>());
        }

        [Test]
        public void CompletionHandlerIsInvokedOnCancel()
        {
            _getStackFramesOp.Cancel();

            _completionHandler
                .Received(1)
                .OnComplete(VSConstants.E_ABORT, null);
        }

        [Test]
        public void CompletionHandlerIsInvokedOnException()
        {
            var e = new TestException();

            _stackFramesProvider.GetAllAsync(_fieldSpec, _debugThread).Throws(e);

            _getStackFramesOp.BeginExecute();

            _completionHandler
                .Received(1)
                .OnComplete(e.HResult, null);
        }

        class TestException : Exception
        {
            const int TestHResult = 0x123;

            public TestException()
            {
                HResult = TestHResult;
            }
        }
    }
}
