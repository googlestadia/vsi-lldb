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
            _debugThreadFactory = new DebugAsyncThread.Factory(_taskExecutor);
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
        public void CanSetNextStatementSameFunction()
        {
            const string frameName = "test";
            const uint threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            mockStackFrame.GetName(out string _).Returns(x =>
            {
                x[0] = frameName;
                return VSConstants.S_OK;
            });
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);
            mockStackFrame.GetThread(out IDebugThread2 _).Returns(x =>
            {
                x[0] = thread;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            var contextInfosDestination = Arg.Any<CONTEXT_INFO[]>();
            mockCodeContext
                .GetInfo(
                    enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS | enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION,
                    contextInfosDestination).Returns(x =>
                {
                    var infos = x[1] as CONTEXT_INFO[];
                    infos[0] = new CONTEXT_INFO
                    {
                        bstrFunction = frameName,
                        bstrAddress = "0xabcd",
                    };
                    return VSConstants.S_OK;
                });
            Assert.AreEqual(VSConstants.S_OK,
                            thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void CanSetNextStatementMatchingPc()
        {
            const ulong address = 0xdeadbeef;
            const string frameName = "test";
            const uint threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            string name;
            mockStackFrame.GetName(out name).Returns(x =>
            {
                x[0] = frameName;
                return VSConstants.S_OK;
            });
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);
            mockStackFrame.GetThread(out IDebugThread2 _).Returns(x =>
            {
                x[0] = thread;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            mockCodeContext.GetInfo(
                enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS | enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION,
                Arg.Do<CONTEXT_INFO[]>(infos =>
                {
                    infos[0].bstrAddress = "0x" + address.ToString("x16");
                    infos[0].bstrFunction = frameName;
                })).Returns(VSConstants.S_OK);
            var mockFrame = Substitute.For<RemoteFrame>();
            mockFrame.GetPC().Returns(address);
            mockThread.GetFrameAtIndex(0).Returns(mockFrame);
            Assert.AreEqual(VSConstants.S_OK,
                            thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void CanSetNextStatementNoThreadOrigin()
        {
            const uint threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);
            mockStackFrame.GetThread(out IDebugThread2 _).Returns(x =>
            {
                x[0] = null;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            Assert.AreEqual(VSConstants.E_FAIL,
                            thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void CanSetNextStatementDifferentThread()
        {
            const uint threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);
            mockStackFrame.GetThread(out IDebugThread2 _).Returns(x =>
            {
                x[0] = Substitute.For<IDebugThread2>();
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            Assert.AreEqual(VSConstants.S_FALSE,
                            thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void CanSetNextStatementFalse()
        {
            const ulong address = 0xdeadbeef;
            const string frameName = "test";
            const ulong anotherAddress = 0xabcd;
            const string anotherName = "test1";
            const uint threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            mockStackFrame.GetName(out string _).Returns(x =>
            {
                x[0] = frameName;
                return VSConstants.S_OK;
            });
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);
            mockStackFrame.GetThread(out IDebugThread2 _).Returns(x =>
            {
                x[0] = thread;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();

            var contextInfoFields = enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS |
                enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION;

            void SetContextInfo(CONTEXT_INFO[] infos)
            {
                infos[0].bstrFunction = anotherName;
                infos[0].bstrAddress = "0x" + anotherAddress.ToString("x16");
                infos[0].dwFields = contextInfoFields;
            }

            mockCodeContext
                .GetInfo(contextInfoFields, Arg.Do((System.Action<CONTEXT_INFO[]>)SetContextInfo))
                .Returns(VSConstants.S_OK);
            ((IDebugMemoryContext2)mockCodeContext)
                .GetInfo(Arg.Any<enum_CONTEXT_INFO_FIELDS>(),
                         Arg.Do((System.Action<CONTEXT_INFO[]>)SetContextInfo))
                .Returns(VSConstants.S_OK);
            var mockFrame = Substitute.For<RemoteFrame>();
            mockFrame.GetPC().Returns(address);
            mockThread.GetFrameAtIndex(0).Returns(mockFrame);
            Assert.AreEqual(VSConstants.S_FALSE,
                            thread.CanSetNextStatement(mockStackFrame, mockCodeContext));
        }

        [Test]
        public void SetNextStatement()
        {
            // We need CanSetNextStatement() to pass in order to execute SetNexStatement().
            const string frameName = "test";
            const ulong address = 0xabcd;
            const uint threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            mockStackFrame.GetName(out string _).Returns(x =>
            {
                x[0] = frameName;
                return VSConstants.S_OK;
            });
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);
            mockStackFrame.GetThread(out IDebugThread2 _).Returns(x =>
            {
                x[0] = thread;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            var contextInfosDestination = Arg.Any<CONTEXT_INFO[]>();
            mockCodeContext
                .GetInfo(
                    enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS | enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION,
                    contextInfosDestination).Returns(x =>
                {
                    var infos = x[1] as CONTEXT_INFO[];
                    infos[0] = new CONTEXT_INFO
                    {
                        bstrFunction = frameName,
                        bstrAddress = "0x" + address.ToString("x16"),
                    };
                    return VSConstants.S_OK;
                });
            var mockDocumentContext = Substitute.For<IDebugDocumentContext2>();
            mockCodeContext.GetDocumentContext(out IDebugDocumentContext2 _).Returns(x =>
            {
                x[0] = mockDocumentContext;
                return VSConstants.S_OK;
            });
            const uint line = 2;
            const string path = "path\\to\\file";
            mockDocumentContext.GetName(enum_GETNAME_TYPE.GN_FILENAME, out string outPath).Returns(
                x =>
                {
                    x[1] = path;
                    return VSConstants.S_OK;
                });
            mockDocumentContext.GetStatementRange(
                Arg.Any<TEXT_POSITION[]>(), Arg.Any<TEXT_POSITION[]>()).Returns(x =>
            {
                var startPosition = x[0] as TEXT_POSITION[];
                startPosition[0].dwLine = line;
                return VSConstants.S_OK;
            });
            var mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(false);
            mockThread.JumpToLine(path, line).Returns(mockError);
            Assert.AreEqual(VSConstants.S_OK,
                            thread.SetNextStatement(mockStackFrame, mockCodeContext));
            mockThread.Received(1).JumpToLine(path, line + 1);
        }

        [Test]
        public void SetNextStatementNoDocumentContext()
        {
            // We need CanSetNextStatement() to pass in order to execute SetNexStatement().
            const string name = "test";
            const ulong address = 0xabcd;
            var threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            mockStackFrame.GetName(out string _).Returns(x =>
            {
                x[0] = name;
                return VSConstants.S_OK;
            });
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);
            mockStackFrame.GetThread(out IDebugThread2 _).Returns(x =>
            {
                x[0] = thread;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();

            void SetContextInfo(CONTEXT_INFO[] infos)
            {
                infos[0].bstrFunction = name;
                infos[0].bstrAddress = "0x" + address.ToString("x16");
                infos[0].dwFields = enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS |
                    enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION;
            }

            mockCodeContext
                .GetInfo(
                    enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS | enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION,
                    Arg.Do((System.Action<CONTEXT_INFO[]>)SetContextInfo))
                .Returns(VSConstants.S_OK);
            ((IDebugMemoryContext2)mockCodeContext)
                .GetInfo(Arg.Any<enum_CONTEXT_INFO_FIELDS>(),
                         Arg.Do((System.Action<CONTEXT_INFO[]>)SetContextInfo))
                .Returns(VSConstants.S_OK);

            mockCodeContext.GetDocumentContext(out IDebugDocumentContext2 _).Returns(x =>
            {
                x[0] = null;
                return VSConstants.S_OK;
            });

            const string dir = "path\\to";
            const string fileName = "file";
            const uint line = 2;
            var mockProcess = Substitute.For<SbProcess>();
            mockThread.GetProcess().Returns(mockProcess);
            var mockTarget = Substitute.For<RemoteTarget>();
            mockProcess.GetTarget().Returns(mockTarget);
            var mockAddress = Substitute.For<SbAddress>();
            var mockLineEntry = new LineEntryInfo();
            mockLineEntry.Directory = dir;
            mockLineEntry.FileName = fileName;
            mockLineEntry.Line = line;
            var mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(false);
            mockThread.JumpToLine(Path.Combine(dir, fileName), line).Returns(mockError);
            mockAddress.GetLineEntry().Returns(mockLineEntry);
            mockTarget.ResolveLoadAddress(address).Returns(mockAddress);
            Assert.AreEqual(VSConstants.S_OK,
                            thread.SetNextStatement(mockStackFrame, mockCodeContext));
            mockThread.Received(1).JumpToLine(Path.Combine(dir, fileName), line);
        }

        [Test]
        public void SetNextStatementCannot()
        {
            const uint threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);
            mockStackFrame.GetThread(out IDebugThread2 _).Returns(x =>
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
            const string name = "test";
            const ulong address = 0xabcd;
            const uint threadId = 1u;
            var mockThread = Substitute.For<RemoteThread>();
            mockThread.GetThreadId().Returns(threadId);
            var mockStackFrame = Substitute.For<IDebugStackFrame2>();
            mockStackFrame.GetName(out string _).Returns(x =>
            {
                x[0] = name;
                return VSConstants.S_OK;
            });
            IDebugThread thread =
                _debugThreadFactory.CreateForTesting(_stackFramesProvider, mockThread);
            mockStackFrame.GetThread(out IDebugThread2 _).Returns(x =>
            {
                x[0] = thread;
                return VSConstants.S_OK;
            });
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();
            var contextInfoFields = enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS |
                enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION;

            void SetContext(CONTEXT_INFO[] infos)
            {
                infos[0].bstrFunction = name;
                infos[0].bstrAddress = "0xabcd";
                infos[0].dwFields = contextInfoFields;
            }

            mockCodeContext
                .GetInfo(Arg.Any<enum_CONTEXT_INFO_FIELDS>(),
                         Arg.Do((System.Action<CONTEXT_INFO[]>)SetContext))
                .Returns(VSConstants.S_OK);
            ((IDebugMemoryContext2)mockCodeContext)
                .GetInfo(Arg.Any<enum_CONTEXT_INFO_FIELDS>(),
                         Arg.Do((System.Action<CONTEXT_INFO[]>)SetContext))
                .Returns(VSConstants.S_OK);

            const string dir = "path\\to";
            const string fileName = "file";
            const uint line = 2;
            var mockProcess = Substitute.For<SbProcess>();
            mockThread.GetProcess().Returns(mockProcess);
            var mockTarget = Substitute.For<RemoteTarget>();
            mockProcess.GetTarget().Returns(mockTarget);
            var mockAddress = Substitute.For<SbAddress>();
            var lineEntry = Substitute.For<LineEntryInfo>();
            lineEntry.Directory = dir;
            lineEntry.FileName = fileName;
            lineEntry.Line = line;
            var mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(true);
            mockError.GetCString().Returns("JumpToLine() failed for some reason.");
            mockThread.JumpToLine(Path.Combine(dir, fileName), line).Returns(mockError);
            mockAddress.GetLineEntry().Returns(lineEntry);
            mockTarget.ResolveLoadAddress(address).Returns(mockAddress);
            Assert.AreEqual(VSConstants.E_FAIL,
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