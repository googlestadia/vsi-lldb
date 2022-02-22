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
using NUnit.Framework;
using NSubstitute;
using YetiVSI.DebugEngine;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System.IO;
using DebuggerCommonApi;
using YetiVSITestsCommon;
using YetiVSI.DebugEngine.AsyncOperations;
using System.Collections.Generic;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugThreadTests
    {
        ITaskExecutor _taskExecutor;
        StackFramesProvider _stackFramesProvider;
        IDebugThreadFactory _debugThreadFactory;

        [SetUp]
        public void SetUp()
        {
            _taskExecutor = Substitute.ForPartsOf<FakeTaskExecutor>();
            _stackFramesProvider =
                Substitute.ForPartsOf<StackFramesProvider>(null, null, null, null, null);
            _debugThreadFactory = new DebugAsyncThread.Factory(_taskExecutor,
                                                               new FrameEnumFactory());
        }

        [Test]
        public void ConstructorDoesNotFetchName()
        {
            var lldbThread = Substitute.For<RemoteThread>();
            _debugThreadFactory.CreateForTesting(_stackFramesProvider, lldbThread);

            lldbThread.Received(1).GetThreadId();
            lldbThread.DidNotReceive().GetName();
        }

        [Test]
        public void GetNameNotCached()
        {
            var lldbThread = Substitute.For<RemoteThread>();
            IDebugThread debugThreadImpl =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, lldbThread);
            Assert.Multiple(() =>
            {
                foreach (string expectedName in new[] { "thread-name-1",
                                                        "thread-name-2",
                                                        "thread-name-3" })
                {
                    lldbThread.GetName().Returns(expectedName);
                    debugThreadImpl.GetName(out string debugThreadName);
                    Assert.AreEqual(expectedName, debugThreadName);
                }
            });
        }

        [Test]
        public void CanSetNextStatementDifferentThread()
        {
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(1u);
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);

            var mockStackFrame = Substitute.For<IDebugStackFrame>();
            mockStackFrame.Thread.GetThreadId().Returns(2u);

            var mockCodeContext = Substitute.For<IGgpDebugCodeContext>();
            Assert.AreEqual(VSConstants.E_FAIL,
                            thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void CanSetNextStatementMatchingPC()
        {
            const ulong threadId = 42;

            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);

            var mockStackFrame = Substitute.For<IDebugStackFrame>();
            mockStackFrame.Frame.GetPC().Returns(0xdeadbeef);
            mockStackFrame.Thread.Returns(mockThread);

            var mockCodeContext = Substitute.For<IGgpDebugCodeContext>();
            mockCodeContext.Address.Returns(0xdeadbeef);

            Assert.AreEqual(VSConstants.S_OK,
                            thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void CanSetNextStatementSameFunction()
        {
            const ulong threadId = 42;
            const string frameName = "test()";

            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);

            var mockStackFrame = Substitute.For<IDebugStackFrame>();
            mockStackFrame.Thread.Returns(mockThread);
            mockStackFrame.GetNameWithSignature(out string _).Returns(x =>
            {
                x[0] = frameName;
                return VSConstants.S_OK;
            });

            var mockCodeContext = Substitute.For<IGgpDebugCodeContext>();
            mockCodeContext.Address.Returns(0xabcdUL);
            mockCodeContext.FunctionName.Returns(frameName);

            Assert.AreEqual(VSConstants.S_OK,
                            thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void CanSetNextStatementDifferentFunction()
        {
            const ulong threadId = 42;

            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);

            var mockStackFrame = Substitute.For<IDebugStackFrame>();
            mockStackFrame.Thread.Returns(mockThread);
            mockStackFrame.Frame.GetPC().Returns(0xdeadbeef);
            mockStackFrame.GetNameWithSignature(out string _).Returns(x =>
            {
                x[0] = "test()";
                return VSConstants.S_OK;
            });

            var mockCodeContext = Substitute.For<IGgpDebugCodeContext>();
            mockCodeContext.FunctionName.Returns("test2()");
            mockCodeContext.Address.Returns(0xdeadc0de);

            var mockFrame = Substitute.For<RemoteFrame>();
            Assert.AreEqual(VSConstants.S_FALSE,
                            thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void SetNextStatement()
        {
            const ulong address = 0xabcd;

            var mockThread = Substitute.For<RemoteThread>();
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);

            var mockStackFrame = Substitute.For<IDebugStackFrame>();
            mockStackFrame.Frame.SetPC(address).Returns(true);

            var mockCodeContext = Substitute.For<IGgpDebugCodeContext>();
            mockCodeContext.Address.Returns(address);
            mockCodeContext.FunctionName.Returns("test()");

            Assert.AreEqual(VSConstants.S_OK,
                            thread.SetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void EnumFrameInfoTest()
        {
            var mockThread = Substitute.For<RemoteThread>();
            IDebugThread debugThread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);

            IList<FRAMEINFO> framesList = new List<FRAMEINFO>
            {
                new FRAMEINFO { m_bstrFuncName = "func1" },
                new FRAMEINFO { m_bstrFuncName = "func2" }
            };

            _stackFramesProvider.GetRange(Arg.Any<enum_FRAMEINFO_FLAGS>(), debugThread.Self,
                                          0, uint.MaxValue).Returns(framesList);

            int result = debugThread.EnumFrameInfo(enum_FRAMEINFO_FLAGS.FIF_ARGS, 10,
                                                   out IEnumDebugFrameInfo2 enumDebugFrameInfo2);

            _stackFramesProvider.ReceivedWithAnyArgs(0).GetRange(
                Arg.Any<enum_FRAMEINFO_FLAGS>(), Arg.Any<IDebugThread>(), Arg.Any<uint>(),
                Arg.Any<uint>());
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.IsNotNull(enumDebugFrameInfo2);
            Assert.That(enumDebugFrameInfo2.GetCount(out uint count), Is.EqualTo(VSConstants.S_OK));
            Assert.That(count, Is.EqualTo(2));
            _stackFramesProvider.ReceivedWithAnyArgs(1).GetRange(
                Arg.Any<enum_FRAMEINFO_FLAGS>(), Arg.Any<IDebugThread>(), 0, uint.MaxValue);
        }

        [Test]
        public void GetAllFramesAsyncTest()
        {
            var mockThread = Substitute.For<RemoteThread>();
            IDebugThread debugThread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);

            int result = debugThread.GetAllFramesAsync(enum_FRAMEINFO_FLAGS.FIF_ARGS, 0, 10, null,
                                                       out IAsyncDebugEngineOperation
                                                           ppDebugOperation);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(ppDebugOperation is AsyncGetStackFramesOperation);
        }
    }
}