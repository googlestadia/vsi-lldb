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
        ITaskExecutor taskExecutor;
        StackFramesProvider _stackFramesProvider;

        [SetUp]
        public void SetUp()
        {
            taskExecutor = Substitute.ForPartsOf<FakeTaskExecutor>();
            _stackFramesProvider =
                Substitute.ForPartsOf<StackFramesProvider>(null, null, null, null, null);
        }

        [Test]
        public void GetName()
        {
            var THREAD_NAME = "thread-name";
            var lldbThread = Substitute.For<RemoteThread>();
            lldbThread.GetName().Returns(THREAD_NAME);
            IDebugThread thread = CreateDebugThread<IDebugThread>(lldbThread);
            string debugThreadName;
            thread.GetName(out debugThreadName);
            Assert.AreEqual(THREAD_NAME, debugThreadName);
        }

        [Test]
        public void CanSetNextStatementSameFunction()
        {
            const string NAME = "test";
            var threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            string name;
            mockStackFrame.GetName(out name).Returns(x =>
            {
                x[0] = NAME;
                return VSConstants.S_OK;
            });
            IDebugThread2 outThread;
            IDebugThread thread = CreateDebugThread<IDebugThread>(mockThread);
            mockStackFrame.GetThread(out outThread).Returns(x =>
            {
                x[0] = thread;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            var contextInfosDestination = Arg.Any<CONTEXT_INFO[]>();
            mockCodeContext.GetInfo(enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS |
                enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION, contextInfosDestination).Returns(x =>
                {
                    var infos = x[1] as CONTEXT_INFO[];
                    infos[0] = new CONTEXT_INFO
                    {
                        bstrFunction = NAME,
                        bstrAddress = "0xabcd",
                    };
                    return VSConstants.S_OK;
                });
            Assert.AreEqual(VSConstants.S_OK, thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void CanSetNextStatementMatchingPc()
        {
            const ulong ADDRESS = 0xdeadbeef;
            const string NAME = "test";
            var threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            string name;
            mockStackFrame.GetName(out name).Returns(x =>
            {
                x[0] = NAME;
                return VSConstants.S_OK;
            });
            IDebugThread2 outThread;
            IDebugThread thread = CreateDebugThread<IDebugThread>(mockThread);
            mockStackFrame.GetThread(out outThread).Returns(x =>
            {
                x[0] = thread;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            mockCodeContext
                .GetInfo(enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS |
                             enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION,
                         Arg.Do<CONTEXT_INFO[]>(infos =>
                         {
                             infos[0].bstrAddress = "0x" + ADDRESS.ToString("x16");
                             infos[0].bstrFunction = NAME;
                         }))
                .Returns(VSConstants.S_OK);
            var mockFrame = Substitute.For<RemoteFrame>();
            mockFrame.GetPC().Returns(ADDRESS);
            mockThread.GetFrameAtIndex(0).Returns(mockFrame);
            Assert.AreEqual(VSConstants.S_OK,
                thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void CanSetNextStatementNoThreadOrigin()
        {
            var threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            IDebugThread2 outThread;
            IDebugThread thread = CreateDebugThread<IDebugThread>(mockThread);
            mockStackFrame.GetThread(out outThread).Returns(x =>
            {
                x[0] = null;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            Assert.AreEqual(VSConstants.E_FAIL, thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void CanSetNextStatementDifferentThread()
        {
            var threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            IDebugThread2 outThread;
            IDebugThread thread = CreateDebugThread<IDebugThread>(mockThread);
            mockStackFrame.GetThread(out outThread).Returns(x =>
            {
                x[0] = Substitute.For<IDebugThread2>();
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            Assert.AreEqual(VSConstants.S_FALSE, thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void CanSetNextStatementFalse()
        {
            const ulong ADDRESS = 0xdeadbeef;
            const string NAME = "test";
            const ulong ANOTHER_ADDRESS = 0xabcd;
            const string ANOTHER_NAME = "test1";
            var threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            string name;
            mockStackFrame.GetName(out name).Returns(x =>
            {
                x[0] = NAME;
                return VSConstants.S_OK;
            });
            IDebugThread2 outThread;
            IDebugThread thread = CreateDebugThread<IDebugThread>(mockThread);
            mockStackFrame.GetThread(out outThread).Returns(x =>
            {
                x[0] = thread;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();

            var contextInfoFields = enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS |
                                    enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION;
            System.Action<CONTEXT_INFO[]> setContextInfo = infos =>
            {
                infos[0].bstrFunction = ANOTHER_NAME;
                infos[0].bstrAddress = "0x" + ANOTHER_ADDRESS.ToString("x16");
                infos[0].dwFields = contextInfoFields;
            };
            mockCodeContext
                .GetInfo(contextInfoFields, Arg.Do(setContextInfo))
                .Returns(VSConstants.S_OK);
            ((IDebugMemoryContext2)mockCodeContext)
                .GetInfo(Arg.Any<enum_CONTEXT_INFO_FIELDS>(), Arg.Do(setContextInfo))
                .Returns(VSConstants.S_OK);
            var mockFrame = Substitute.For<RemoteFrame>();
            mockFrame.GetPC().Returns(ADDRESS);
            mockThread.GetFrameAtIndex(0).Returns(mockFrame);
            Assert.AreEqual(VSConstants.S_FALSE,
                thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void SetNextStatement()
        {
            // We need CanSetNextStatement() to pass in order to execute SetNexStatement().
            const string NAME = "test";
            const ulong ADDRESS = 0xabcd;
            var threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            string name;
            mockStackFrame.GetName(out name).Returns(x =>
            {
                x[0] = NAME;
                return VSConstants.S_OK;
            });
            IDebugThread2 outThread;
            IDebugThread thread = CreateDebugThread<IDebugThread>(mockThread);
            mockStackFrame.GetThread(out outThread).Returns(x =>
            {
                x[0] = thread;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            var contextInfosDestination = Arg.Any<CONTEXT_INFO[]>();
            mockCodeContext.GetInfo(enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS |
                enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION, contextInfosDestination).Returns(x =>
                {
                    var infos = x[1] as CONTEXT_INFO[];
                    infos[0] = new CONTEXT_INFO
                    {
                        bstrFunction = NAME,
                        bstrAddress = "0x" + ADDRESS.ToString("x16"),
                    };
                    return VSConstants.S_OK;
                });
            IDebugDocumentContext2 documentContext;
            var mockDocumentContext = Substitute.For<IDebugDocumentContext2>();
            mockCodeContext.GetDocumentContext(out documentContext).Returns(x =>
            {
                x[0] = mockDocumentContext;
                return VSConstants.S_OK;
            });
            const uint LINE = 2;
            const string PATH = "path\\to\\file";
            string path;
            mockDocumentContext.GetName(enum_GETNAME_TYPE.GN_FILENAME, out path).Returns(x =>
            {
                x[1] = PATH;
                return VSConstants.S_OK;
            });
            mockDocumentContext.GetStatementRange(
                Arg.Any<TEXT_POSITION[]>(), Arg.Any<TEXT_POSITION[]>()).Returns(x =>
                {
                    var startPosition = x[0] as TEXT_POSITION[];
                    startPosition[0].dwLine = LINE;
                    return VSConstants.S_OK;
                });
            var mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(false);
            mockThread.JumpToLine(PATH, LINE).Returns(mockError);
            Assert.AreEqual(VSConstants.S_OK,
                thread.SetNextStatement(mockStackFrame, mockCodeContext));
            mockThread.Received(1).JumpToLine(PATH, LINE + 1);
        }

        [Test]
        public void SetNextStatementNoDocumentContext()
        {
            // We need CanSetNextStatement() to pass in order to execute SetNexStatement().
            const string NAME = "test";
            const ulong ADDRESS = 0xabcd;
            var threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            string name;
            mockStackFrame.GetName(out name).Returns(x =>
            {
                x[0] = NAME;
                return VSConstants.S_OK;
            });
            IDebugThread2 outThread;
            IDebugThread thread = CreateDebugThread<IDebugThread>(mockThread);
            mockStackFrame.GetThread(out outThread).Returns(x =>
            {
                x[0] = thread;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            System.Action<CONTEXT_INFO[]> setContextInfo = infos =>
            {
                infos[0].bstrFunction = NAME;
                infos[0].bstrAddress = "0x" + ADDRESS.ToString("x16");
                infos[0].dwFields = enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS |
                    enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION;
            };
            mockCodeContext
                .GetInfo(enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS |
                    enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION, Arg.Do(setContextInfo))
                .Returns(VSConstants.S_OK);
            ((IDebugMemoryContext2)mockCodeContext)
                .GetInfo(Arg.Any<enum_CONTEXT_INFO_FIELDS>(), Arg.Do(setContextInfo))
                .Returns(VSConstants.S_OK);

            IDebugDocumentContext2 documentContext;
            mockCodeContext.GetDocumentContext(out documentContext).Returns(x =>
            {
                x[0] = null;
                return VSConstants.S_OK;
            });

            const string DIR = "path\\to";
            const string FILE_NAME = "file";
            const uint LINE = 2;
            var mockProcess = Substitute.For<SbProcess>();
            mockThread.GetProcess().Returns(mockProcess);
            var mockTarget = Substitute.For<RemoteTarget>();
            mockProcess.GetTarget().Returns(mockTarget);
            var mockAddress = Substitute.For<SbAddress>();
            var mockLineEntry = new LineEntryInfo();
            mockLineEntry.Directory = DIR;
            mockLineEntry.FileName = FILE_NAME;
            mockLineEntry.Line = LINE;
            var mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(false);
            mockThread.JumpToLine(Path.Combine(DIR, FILE_NAME), LINE).Returns(mockError);
            mockAddress.GetLineEntry().Returns(mockLineEntry);
            mockTarget.ResolveLoadAddress(ADDRESS).Returns(mockAddress);
            Assert.AreEqual(VSConstants.S_OK,
                thread.SetNextStatement(mockStackFrame, mockCodeContext));
            mockThread.Received(1).JumpToLine(Path.Combine(DIR, FILE_NAME), LINE);
        }

        [Test]
        public void SetNextStatementCannot()
        {
            var threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            IDebugThread2 outThread;
            IDebugThread thread = CreateDebugThread<IDebugThread>(mockThread);
            mockStackFrame.GetThread(out outThread).Returns(x =>
            {
                x[0] = Substitute.For<IDebugThread2>();
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            Assert.AreEqual(VSConstants.E_FAIL,
                thread.SetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void SetNextStatementFailToJump()
        {
            // We need CanSetNextStatement() to pass in order to execute SetNexStatement().
            const string NAME = "test";
            const ulong ADDRESS = 0xabcd;
            var threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            string name;
            mockStackFrame.GetName(out name).Returns(x =>
            {
                x[0] = NAME;
                return VSConstants.S_OK;
            });
            IDebugThread2 outThread;
            IDebugThread thread = CreateDebugThread<IDebugThread>(mockThread);
            mockStackFrame.GetThread(out outThread).Returns(x =>
            {
                x[0] = thread;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            var contextInfoFields = enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS |
                enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION;
            System.Action<CONTEXT_INFO[]> setContext = (infos =>
            {
                infos[0].bstrFunction = NAME;
                infos[0].bstrAddress = "0xabcd";
                infos[0].dwFields = contextInfoFields;

            });
            mockCodeContext
                .GetInfo(Arg.Any<enum_CONTEXT_INFO_FIELDS>(), Arg.Do(setContext))
                .Returns(VSConstants.S_OK);
            ((IDebugMemoryContext2)mockCodeContext)
                .GetInfo(Arg.Any<enum_CONTEXT_INFO_FIELDS>(), Arg.Do(setContext))
                .Returns(VSConstants.S_OK);

            const string DIR = "path\\to";
            const string FILE_NAME = "file";
            const uint LINE = 2;
            var mockProcess = Substitute.For<SbProcess>();
            mockThread.GetProcess().Returns(mockProcess);
            var mockTarget = Substitute.For<RemoteTarget>();
            mockProcess.GetTarget().Returns(mockTarget);
            var mockAddress = Substitute.For<SbAddress>();
            var lineEntry = Substitute.For<LineEntryInfo>();
            lineEntry.Directory = DIR;
            lineEntry.FileName = FILE_NAME;
            lineEntry.Line = LINE;
            var mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(true);
            mockError.GetCString().Returns("JumpToLine() failed for some reason.");
            mockThread.JumpToLine(Path.Combine(DIR, FILE_NAME), LINE).Returns(mockError);
            mockAddress.GetLineEntry().Returns(lineEntry);
            mockTarget.ResolveLoadAddress(ADDRESS).Returns(mockAddress);
            Assert.AreEqual(VSConstants.E_FAIL,
                thread.SetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void EnumFrameInfoTest()
        {
            RemoteThread mockThread = Substitute.For<RemoteThread>();
            IDebugThread debugThread = CreateDebugThread<IDebugThread>(mockThread);

            IList<FRAMEINFO> framesList = new List<FRAMEINFO>
            {
                new FRAMEINFO { m_bstrFuncName = "func1" },
                new FRAMEINFO { m_bstrFuncName = "func2" }
            };

            _stackFramesProvider.GetRange(Arg.Any<enum_FRAMEINFO_FLAGS>(), debugThread.Self,
                0, uint.MaxValue).Returns(framesList);

            IEnumDebugFrameInfo2 enumDebugFrameInfo2;
            int result = debugThread.EnumFrameInfo(
                enum_FRAMEINFO_FLAGS.FIF_ARGS, 10, out enumDebugFrameInfo2);

            _stackFramesProvider.ReceivedWithAnyArgs(0)
                .GetRange(Arg.Any<enum_FRAMEINFO_FLAGS>(), Arg.Any<IDebugThread>(),
                Arg.Any<uint>(), Arg.Any<uint>());
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.IsNotNull(enumDebugFrameInfo2);
            uint count;
            Assert.That(enumDebugFrameInfo2.GetCount(out count), Is.EqualTo(VSConstants.S_OK));
            Assert.That(count, Is.EqualTo(2));
            _stackFramesProvider.ReceivedWithAnyArgs(1).GetRange(
                Arg.Any<enum_FRAMEINFO_FLAGS>(), Arg.Any<IDebugThread>(), 0, uint.MaxValue);
        }

        [Test]
        public void GetAllFramesAsyncTest()
        {
            RemoteThread mockThread = Substitute.For<RemoteThread>();
            IDebugThreadAsync debugThread = CreateDebugThread<IDebugThreadAsync>(mockThread);

            IAsyncDebugEngineOperation ppDebugOperation;
            int result = debugThread.GetAllFramesAsync(enum_FRAMEINFO_FLAGS.FIF_ARGS, 0, 10, null,
                out ppDebugOperation);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(ppDebugOperation is AsyncGetStackFramesOperation);
        }

        T CreateDebugThread<T>(RemoteThread thread) where T: IDebugThread
        {
            bool isAsync = typeof(IDebugThreadAsync).IsAssignableFrom(typeof(T));

            T debugThread = isAsync ? (T)new DebugThreadAsync.Factory(new FrameEnumFactory(),
                taskExecutor).CreateForTesting(_stackFramesProvider, thread) :
                (T)new DebugThread.Factory(new FrameEnumFactory(),
                taskExecutor).CreateForTesting(_stackFramesProvider, thread);

            return debugThread;
        }
    }
}