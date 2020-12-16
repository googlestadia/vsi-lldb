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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.AsyncOperations;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.DebugEngine.Variables;
using YetiVSITestsCommon;

namespace YetiVSI.Test.DebugEngine.AsyncOperations
{
    [TestFixture]
    class AsyncGetRootPropertiesOperationTest
    {
        AsyncGetRootPropertiesOperation _getPropertiesOp;
        FrameVariablesProvider _frameVariablesProvider;
        ITaskExecutor _taskExecutor;
        IAsyncDebugGetPropertiesCompletionHandler _completionHandler;
        IChildrenProviderFactory _childrenProviderFactory;
        IChildrenProvider _childrenProvider;

        [SetUp]
        public void SetUp()
        {
            _completionHandler = Substitute.For<IAsyncDebugGetPropertiesCompletionHandler>();
            _childrenProvider = Substitute.For<IChildrenProvider>();
            _childrenProviderFactory = Substitute.For<IChildrenProviderFactory>();

            _childrenProviderFactory.Create(Arg.Any<IChildAdapter>(),
                                            Arg.Any<enum_DEBUGPROP_INFO_FLAGS>(), Arg.Any<uint>())
                .Returns(_childrenProvider);

            _taskExecutor = Substitute.ForPartsOf<FakeTaskExecutor>();
            _frameVariablesProvider = Substitute.For<FrameVariablesProvider>(null, null, null);

            _getPropertiesOp = new AsyncGetRootPropertiesOperation(_frameVariablesProvider,
                                                                   _taskExecutor,
                                                                   _completionHandler,
                                                                   _childrenProviderFactory,
                                                                   enum_DEBUGPROP_INFO_FLAGS
                                                                       .DEBUGPROP_INFO_FULLNAME, 10,
                                                                   new Guid(
                                                                       "12345678-1234-1234-1234-123456789123"));

            var varibleStub = Substitute.For<IVariableInformation>();
            _frameVariablesProvider.Get(Arg.Any<Guid>())
                .Returns(new List<IVariableInformation>
                {
                    varibleStub
                });
        }

        [Test]
        public async Task TaskIsSubmittedOnBeginAsync()
        {
            _getPropertiesOp.BeginExecute();
            await _taskExecutor.ReceivedWithAnyArgs(1)
                .SubmitAsync(Arg.Any<Func<Task<IListDebugPropertyInfo>>>(),
                             Arg.Any<CancellationToken>(), Arg.Any<string>(), Arg.Any<Type>());
        }

        [Test]
        public void CompletionHandlerIsInvoked()
        {
            _childrenProvider
                .GetChildrenAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DEBUG_PROPERTY_INFO[]>())
                .Returns(args => Task.FromResult((int) args[1]));

            _getPropertiesOp.BeginExecute();

            _completionHandler.Received(1)
                .OnComplete(VSConstants.S_OK, 1, Arg.Any<IListDebugPropertyInfo>());
        }

        [Test]
        public void CompletionHandlerIsInvokedOnError()
        {
            _frameVariablesProvider.Get(Arg.Any<Guid>())
                .Returns((ICollection<IVariableInformation>) null);

            _getPropertiesOp.BeginExecute();

            _completionHandler.Received(1)
                .OnComplete(VSConstants.E_FAIL, 0, null);
        }

        [Test]
        public void CompletionHandlerIsInvokedOnCancel()
        {
            _getPropertiesOp.Cancel();

            _completionHandler.Received(1).OnComplete(VSConstants.E_ABORT, 0, null);
        }

        [Test]
        public void CompletionHandlerIsInvokedOnException()
        {
            var e = new TestException();

            _childrenProvider.GetChildrenAsync(0, 1, Arg.Any<DEBUG_PROPERTY_INFO[]>()).Throws(e);

            _getPropertiesOp.BeginExecute();

            _completionHandler.Received(1).OnComplete(e.HResult, 0, null);
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