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
using System.Linq;
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
using YetiVSITestsCommon;

namespace YetiVSI.Test.DebugEngine.AsyncOperations
{
    [TestFixture]
    class AsyncGetPropertiesOperationTest
    {
        ITaskExecutor _taskExecutor;
        AsyncGetPropertiesOperation _getPropertiesOp;
        IAsyncDebugGetPropertiesCompletionHandler _completionHandler;
        IChildrenProvider _childrenProvider;

        const int _fromIndex = 0;
        const int _count = 2;


        [SetUp]
        public void SetUp()
        {
            _completionHandler = Substitute.For<IAsyncDebugGetPropertiesCompletionHandler>();
            _childrenProvider = Substitute.For<IChildrenProvider>();
            _taskExecutor = Substitute.ForPartsOf<FakeTaskExecutor>();
            _getPropertiesOp = new AsyncGetPropertiesOperation(_taskExecutor, _completionHandler,
                                                               _childrenProvider, _fromIndex,
                                                               _count);
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
            var propertyInfos = new DEBUG_PROPERTY_INFO[_count];
            propertyInfos[0] = new DEBUG_PROPERTY_INFO();
            propertyInfos[1] = new DEBUG_PROPERTY_INFO();

            _childrenProvider.GetChildrenCountAsync().Returns(_count);

            _childrenProvider
                .GetChildrenAsync(0, _count, FillOutProperties(propertyInfos))
                .Returns(_count);

            _getPropertiesOp.BeginExecute();

            _completionHandler.Received(1)
                .OnComplete(VSConstants.S_OK, _count, MatchProperties(propertyInfos));
        }

        [Test]
        public void StatusIsOkWhenMoreChildrenRequestedThanChildrenProviderHas()
        {
            const int requestedCount = 42;
            const int realCount = 1;
            _getPropertiesOp = new AsyncGetPropertiesOperation(_taskExecutor, _completionHandler,
                                                               _childrenProvider, _fromIndex,
                                                               requestedCount);

            var propertyInfos = new DEBUG_PROPERTY_INFO[realCount];
            propertyInfos[0] = new DEBUG_PROPERTY_INFO();

            _childrenProvider.GetChildrenCountAsync().Returns(realCount);

            _childrenProvider
                .GetChildrenAsync(0, requestedCount, FillOutProperties(propertyInfos))
                .Returns(realCount);

            _getPropertiesOp.BeginExecute();

            _completionHandler.Received(1)
                .OnComplete(VSConstants.S_OK, realCount, MatchProperties(propertyInfos));
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

            _childrenProvider.GetChildrenAsync(0, _count, Arg.Any<DEBUG_PROPERTY_INFO[]>())
                .Throws(e);

            _getPropertiesOp.BeginExecute();

            _completionHandler.Received(1).OnComplete(e.HResult, 0, null);
        }

        DEBUG_PROPERTY_INFO[] FillOutProperties(DEBUG_PROPERTY_INFO[] propertyInfos)
        {
            return Arg.Do<DEBUG_PROPERTY_INFO[]>(outPropertyInfo =>
            {
                for (int n = 0; n < propertyInfos.Length; ++n)
                {
                    outPropertyInfo[n] = propertyInfos[n];
                }
            });
        }

        IListDebugPropertyInfo MatchProperties(DEBUG_PROPERTY_INFO[] expectedProperties)
        {
            int count = expectedProperties.Length;
            Func<IListDebugPropertyInfo, bool> correctPropertyPredicate = properties =>
                properties.Count == count &&
                Enumerable.Range(0, count).All(i => expectedProperties[i].Equals(properties[i]));

            return Arg.Is<IListDebugPropertyInfo>(actualProperties =>
                                                      correctPropertyPredicate(actualProperties));
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