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
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using DebuggerCommonApi;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Exit;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugProgramTests
    {
        const uint LINE = 0;
        const string DIRECTORY = "path\\to";
        const string FILE_NAME = "file";
        const ulong ADDRESS = 0xdeadbeef;
        const string NAME = "test";
        IGgpDebugProgram program;
        RemoteThread remoteThread;
        IDebugThread mockThread;
        IDebugProcess2 mockProcess;
        SbProcess mockSbProcess;
        RemoteTarget mockRemoteTarget;
        DebugDocumentContext.Factory mockDocumentContextFactory;
        DebugCodeContext.Factory mockCodeContextFactory;
        ThreadEnumFactory threadEnumFactory;
        ModuleEnumFactory moduleEnumFactory;
        CodeContextEnumFactory codeContextEnumFactory;
        IDebugModuleCache mockDebugModuleCache;
        IDebugEngineHandler mockDebugEngineHandler;
        DebugProgram.ThreadCreator mockDebugThreadCreator;
        DebugDisassemblyStream.Factory mockDebugDisassemblyStreamFactory;

        [SetUp]
        public void SetUp()
        {
            var taskContext = new JoinableTaskContext();
            remoteThread = Substitute.For<RemoteThread>();
            mockThread = Substitute.For<IDebugThread>();
            mockThread.GetRemoteThread().Returns(remoteThread);
            mockDebugEngineHandler = Substitute.For<IDebugEngineHandler>();
            mockDebugThreadCreator = Substitute.For<DebugProgram.ThreadCreator>();
            mockDebugDisassemblyStreamFactory = Substitute.For<DebugDisassemblyStream.Factory>();
            mockProcess = Substitute.For<IDebugProcess2>();
            var guid = new Guid();
            mockSbProcess = Substitute.For<SbProcess>();
            mockRemoteTarget = Substitute.For<RemoteTarget>();
            mockDocumentContextFactory = Substitute.For<DebugDocumentContext.Factory>();
            mockCodeContextFactory = Substitute.For<DebugCodeContext.Factory>();
            threadEnumFactory = new ThreadEnumFactory();
            moduleEnumFactory = new ModuleEnumFactory();
            codeContextEnumFactory = new CodeContextEnumFactory();

            mockDebugModuleCache = Substitute.For<IDebugModuleCache>();
            program = new DebugProgram.Factory(taskContext, mockDebugDisassemblyStreamFactory,
                    mockDocumentContextFactory, mockCodeContextFactory, threadEnumFactory,
                    moduleEnumFactory, codeContextEnumFactory)
                .Create(mockDebugEngineHandler, mockDebugThreadCreator, mockProcess, guid,
                    mockSbProcess, mockRemoteTarget, mockDebugModuleCache, false);
        }

        [Test]
        public void StepInto()
        {
            Assert.AreEqual(VSConstants.S_OK,
                program.Step(mockThread, enum_STEPKIND.STEP_INTO, enum_STEPUNIT.STEP_STATEMENT));
            remoteThread.Received(1).StepInto();
        }

        [Test]
        public void StepOver()
        {
            Assert.AreEqual(VSConstants.S_OK,
                program.Step(mockThread, enum_STEPKIND.STEP_OVER, enum_STEPUNIT.STEP_STATEMENT));
            remoteThread.Received(1).StepOver();
        }

        [Test]
        public void StepOut()
        {
            Assert.AreEqual(VSConstants.S_OK,
                program.Step(mockThread, enum_STEPKIND.STEP_OUT, enum_STEPUNIT.STEP_STATEMENT));
            remoteThread.Received(1).StepOut();
        }

        [Test]
        public void StepInstructionInto()
        {
            Assert.AreEqual(VSConstants.S_OK,
                program.Step(mockThread, enum_STEPKIND.STEP_INTO, enum_STEPUNIT.STEP_INSTRUCTION));
            remoteThread.Received(1).StepInstruction(false);
        }

        [Test]
        public void StepInstructionOver()
        {
            Assert.AreEqual(VSConstants.S_OK,
                program.Step(mockThread, enum_STEPKIND.STEP_OVER, enum_STEPUNIT.STEP_INSTRUCTION));
            remoteThread.Received(1).StepInstruction(true);
        }

        [Test]
        public void StepInstructionOut()
        {
            Assert.AreEqual(VSConstants.S_OK,
                program.Step(mockThread, enum_STEPKIND.STEP_OUT, enum_STEPUNIT.STEP_INSTRUCTION));
            remoteThread.Received(1).StepOut();
        }

        [Test]
        public void EnumThreadsNoRemoteThreads()
        {
            mockSbProcess.GetNumThreads().Returns(0);
            IEnumDebugThreads2 enumThreads;
            Assert.AreEqual(VSConstants.S_OK, program.EnumThreads(out enumThreads));
            AssertEnumThreads(new List<IDebugThread>(), enumThreads);
        }

        [Test]
        public void EnumThreadsRemoteThreadsFail()
        {
            mockSbProcess.GetNumThreads().Returns(1);
            mockSbProcess.GetThreadAtIndex(0).Returns((RemoteThread)null);
            IEnumDebugThreads2 enumThreads;
            Assert.AreEqual(VSConstants.S_OK, program.EnumThreads(out enumThreads));
            AssertEnumThreads(new List<IDebugThread>(), enumThreads);
        }

        [Test]
        public void EnumThreadsNoRemoteThreadsAfterValidThreads()
        {
            uint threadId;
            var mockRemoteThread = Substitute.For<RemoteThread>();
            var mockThread = Substitute.For<IDebugThread>();
            var mockThreads = new List<IDebugThread> { mockThread };
            mockRemoteThread.GetThreadId().Returns(1UL);
            mockThread.GetThreadId(out threadId).Returns(x =>
            {
                x[0] = 1U;
                return VSConstants.S_OK;
            });
            mockSbProcess.GetNumThreads().Returns(1);
            mockSbProcess.GetThreadAtIndex(0).Returns(mockRemoteThread);
            mockDebugThreadCreator(mockRemoteThread, program).Returns(mockThread);

            // Enum threads once to fill the cache.
            IEnumDebugThreads2 enumThreads;
            Assert.AreEqual(VSConstants.S_OK, program.EnumThreads(out enumThreads));
            AssertEnumThreads(mockThreads, enumThreads);
            mockDebugEngineHandler.Received(1).SendEvent(Arg.Any<ThreadCreateEvent>(),
                program, mockThread);

            // Enum threads again, and this time it fails. In this case we should return the list
            // of cached threads.
            mockSbProcess.GetNumThreads().Returns(0);
            Assert.AreEqual(VSConstants.S_OK, program.EnumThreads(out enumThreads));
            AssertEnumThreads(mockThreads, enumThreads);
        }

        [Test]
        public void EnumThreadsRemoveExited()
        {
            uint threadId;
            var mockRemoteThread1 = Substitute.For<RemoteThread>();
            var mockRemoteThread2 = Substitute.For<RemoteThread>();
            var mockThread1 = Substitute.For<IDebugThread>();
            var mockThread2 = Substitute.For<IDebugThread>();
            var mockThreads = new List<IDebugThread> { mockThread1, mockThread2 };
            mockRemoteThread1.GetThreadId().Returns(1UL);
            mockRemoteThread2.GetThreadId().Returns(2UL);
            mockThread1.GetThreadId(out threadId).Returns(x =>
            {
                x[0] = 1U;
                return VSConstants.S_OK;
            });
            mockThread2.GetThreadId(out threadId).Returns(x =>
            {
                x[0] = 2U;
                return VSConstants.S_OK;
            });
            mockSbProcess.GetNumThreads().Returns(2);
            mockSbProcess.GetThreadAtIndex(0).Returns(mockRemoteThread1);
            mockSbProcess.GetThreadAtIndex(1).Returns(mockRemoteThread2);
            mockDebugThreadCreator(mockRemoteThread1, program).Returns(mockThread1);
            mockDebugThreadCreator(mockRemoteThread2, program).Returns(mockThread2);

            // Enum threads once to fill the cache.
            IEnumDebugThreads2 enumThreads;
            Assert.AreEqual(VSConstants.S_OK, program.EnumThreads(out enumThreads));
            AssertEnumThreads(mockThreads, enumThreads);
            mockDebugEngineHandler.Received(1).SendEvent(Arg.Any<ThreadCreateEvent>(),
                program, mockThread1);
            mockDebugEngineHandler.Received(1).SendEvent(Arg.Any<ThreadCreateEvent>(),
                program, mockThread2);

            // Enum threads again, but this time with fewer threads.  The missing thread should be
            // removed.
            mockSbProcess.GetNumThreads().Returns(1);
            var mockUpdatedThreads = new List<IDebugThread> { mockThread1 };
            Assert.AreEqual(VSConstants.S_OK, program.EnumThreads(out enumThreads));
            AssertEnumThreads(mockUpdatedThreads, enumThreads);
            mockDebugEngineHandler.Received(1).SendEvent(Arg.Any<ThreadDestroyEvent>(),
                program, mockThread2);
        }

        [Test]
        public void GetMemoryBytes()
        {
            IDebugMemoryBytes2 memoryBytes;
            Assert.AreEqual(VSConstants.S_OK, program.GetMemoryBytes(out memoryBytes));
            Assert.AreEqual((IDebugMemoryBytes2)program, memoryBytes);
        }

        [Test]
        public void ReadAt()
        {
            const uint COUNT_TO_READ = 4;
            const ulong READ_ADDRESS = 0xdeadbeef;
            const string ADDRESS_STR = "0xdeadbeef";
            byte[] expectedMemory = { 1, 2, 3, 4 };
            SbError error;
            var mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(false);
            var mockMemoryContext = Substitute.For<IDebugMemoryContext2>();
            mockMemoryContext.GetInfo(
                enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, Arg.Any<CONTEXT_INFO[]>())
                .Returns(x =>
                {
                    var contextInfos = x[1] as CONTEXT_INFO[];
                    contextInfos[0].dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS;
                    contextInfos[0].bstrAddress = ADDRESS_STR;
                    return VSConstants.S_OK;
                });
            byte[] byteArray = new byte[4];
            uint countRead;
            uint countUnreadable = 0;
            var anyByteArray = Arg.Any<byte[]>();
            mockSbProcess.ReadMemory(
                READ_ADDRESS, anyByteArray, COUNT_TO_READ, out error).Returns(
                x =>
                {
                    var destination = x[1] as byte[];
                    destination[0] = expectedMemory[0];
                    destination[1] = expectedMemory[1];
                    destination[2] = expectedMemory[2];
                    destination[3] = expectedMemory[3];
                    x[3] = mockError;
                    return 4u;
                });
            Assert.AreEqual(VSConstants.S_OK, program.ReadAt(mockMemoryContext, COUNT_TO_READ,
                byteArray, out countRead, ref countUnreadable));
            Assert.AreEqual(expectedMemory, byteArray);
        }

        [Test]
        public void ReadAtInvalidAddress()
        {
            const uint COUNT_TO_READ = 4;
            const string INVALID_ADDRESS = "ZZZ";
            var mockError = Substitute.For<SbError>();
            var mockMemoryContext = Substitute.For<IDebugMemoryContext2>();
            mockMemoryContext.GetInfo(
                enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, Arg.Any<CONTEXT_INFO[]>())
                .Returns(x =>
                {
                    var contextInfos = x[1] as CONTEXT_INFO[];
                    contextInfos[0].dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS;
                    contextInfos[0].bstrAddress = INVALID_ADDRESS;
                    return VSConstants.S_OK;
                });
            byte[] byteArray = new byte[4];
            uint countRead;
            uint countUnreadable = 0;
            var anyByteArray = Arg.Any<byte[]>();
            Assert.AreEqual(VSConstants.E_FAIL, program.ReadAt(mockMemoryContext, COUNT_TO_READ,
                byteArray, out countRead, ref countUnreadable));
        }

        [Test]
        public void ReadAt_ReadMemoryFails()
        {
            const uint COUNT_TO_READ = 4;
            const ulong READ_ADDRESS = 0xdeadbeef;
            const string ADDRESS_STR = "0xdeadbeef";
            SbError error;
            var mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(true);
            var mockMemoryContext = Substitute.For<IDebugMemoryContext2>();
            mockMemoryContext.GetInfo(
                enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, Arg.Any<CONTEXT_INFO[]>())
                .Returns(x =>
                {
                    var contextInfos = x[1] as CONTEXT_INFO[];
                    contextInfos[0].dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS;
                    contextInfos[0].bstrAddress = ADDRESS_STR;
                    return VSConstants.S_OK;
                });
            byte[] byteArray = new byte[4];
            uint countRead;
            uint countUnreadable = 0;
            var anyByteArray = Arg.Any<byte[]>();
            mockSbProcess.ReadMemory(READ_ADDRESS, anyByteArray, COUNT_TO_READ, out error)
                .Returns(x => {
                    x[3] = mockError;
                    return 123u;
                });
            // TODO This is not the best approach - even if Lldb's ReadMemory fails, we
            // should return S_OK and set countUnreadable appropriately (and set countRead == 0).
            Assert.AreEqual(VSConstants.E_FAIL,
                            program.ReadAt(mockMemoryContext, COUNT_TO_READ, byteArray,
                                           out countRead, ref countUnreadable));
        }

        [Test]
        public void ReadAt_ReadMemoryReturnsSuzccessButZeroSize()
        {
            const uint COUNT_TO_READ = 4;
            const ulong READ_ADDRESS = 0xdeadbeef;
            const string ADDRESS_STR = "0xdeadbeef";
            SbError error;
            var mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(false);
            var mockMemoryContext = Substitute.For<IDebugMemoryContext2>();
            mockMemoryContext
                .GetInfo(enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, Arg.Any<CONTEXT_INFO[]>())
                .Returns(x => {
                    var contextInfos = x[1] as CONTEXT_INFO[];
                    contextInfos[0].dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS;
                    contextInfos[0].bstrAddress = ADDRESS_STR;
                    return VSConstants.S_OK;
                });
            byte[] byteArray = new byte[4];
            uint countRead;
            uint countUnreadable = 0;
            var anyByteArray = Arg.Any<byte[]>();
            mockSbProcess.ReadMemory(
                READ_ADDRESS, anyByteArray, COUNT_TO_READ, out error).Returns(
                x =>
                {
                    x[3] = mockError;
                    return 0u;
                });
            // TODO This is not the best approach - even if Lldb's ReadMemory fails, we
            // should return S_OK and set countUnreadable appropriately (and set countRead == 0).
            Assert.AreEqual(VSConstants.E_FAIL, program.ReadAt(mockMemoryContext, COUNT_TO_READ,
                byteArray, out countRead, ref countUnreadable));
        }

        [Test]
        public void WriteAt()
        {
            const ulong ADDRESS = 0xdeadbeef;
            const string ADDRESS_STR = "0xdeadbeef";
            const uint SIZE = 4;
            var mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(false);
            var mockMemoryContext = Substitute.For<IDebugMemoryContext2>();
            mockMemoryContext.GetInfo(
                enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, Arg.Any<CONTEXT_INFO[]>())
                .Returns(x =>
                {
                    var contextInfos = x[1] as CONTEXT_INFO[];
                    contextInfos[0].dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS;
                    contextInfos[0].bstrAddress = ADDRESS_STR;
                    return VSConstants.S_OK;
                });
            byte[] buffer = { 1, 2, 3, 4 };
            SbError error;
            mockSbProcess.WriteMemory(
                ADDRESS, buffer, SIZE, out error).Returns(
                x =>
                {
                    x[3] = mockError;
                    return SIZE;
                });
            Assert.AreEqual(VSConstants.S_OK, program.WriteAt(mockMemoryContext, SIZE, buffer));
        }

        [Test]
        public void WriteAtInvalidAddress()
        {
            const uint SIZE = 4;
            const string INVALID_ADDRESS_STRING = "zzz";
            byte[] buffer = { 1, 2, 3, 4 };
            var mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(true);
            var mockMemoryContext = Substitute.For<IDebugMemoryContext2>();
            mockMemoryContext.GetInfo(
                enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, Arg.Any<CONTEXT_INFO[]>())
                .Returns(x =>
                {
                    var contextInfos = x[1] as CONTEXT_INFO[];
                    contextInfos[0].dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS;
                    contextInfos[0].bstrAddress = INVALID_ADDRESS_STRING;
                    return VSConstants.S_OK;
                });
            Assert.AreEqual(VSConstants.E_FAIL, program.WriteAt(mockMemoryContext, SIZE, buffer));
        }

        [Test]
        public void WriteAtFail()
        {
            const ulong ADDRESS = 0xdeadbeef;
            const string ADDRESS_STR = "0xdeadbeef";
            const uint SIZE = 4;
            var mockError = Substitute.For<SbError>();
            mockError.Fail().Returns(true);
            var mockMemoryContext = Substitute.For<IDebugMemoryContext2>();
            mockMemoryContext.GetInfo(
                enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, Arg.Any<CONTEXT_INFO[]>())
                .Returns(x =>
                {
                    var contextInfos = x[1] as CONTEXT_INFO[];
                    contextInfos[0].dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS;
                    contextInfos[0].bstrAddress = ADDRESS_STR;
                    return VSConstants.S_OK;
                });
            byte[] buffer = { 1, 2, 3, 4 };
            SbError error;
            mockSbProcess.WriteMemory(
                ADDRESS, buffer, SIZE, out error).Returns(
                x =>
                {
                    x[3] = mockError;
                    return 0u;
                });
            Assert.AreEqual(VSConstants.E_FAIL, program.WriteAt(mockMemoryContext, SIZE, buffer));
        }

        [Test]
        public void GetDisassemblyStream()
        {
            var mockCodeContext = Substitute.For<IDebugCodeContext2>();

            var mockDisassemblyStream = Substitute.For<IDebugDisassemblyStream2>();
            mockDebugDisassemblyStreamFactory
                .Create(enum_DISASSEMBLY_STREAM_SCOPE.DSS_ALL, mockCodeContext, mockRemoteTarget)
                .Returns(mockDisassemblyStream);

            IDebugDisassemblyStream2 disassemblyStream;
            Assert.AreEqual(VSConstants.S_OK,
                program.GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE.DSS_ALL,
                    mockCodeContext, out disassemblyStream));
            Assert.AreEqual(mockDisassemblyStream, disassemblyStream);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        public void EnumCodeContexts(int numLocations)
        {
            var mockDocumentPosition = Substitute.For<IDebugDocumentPosition2>();
            mockDocumentPosition.GetRange(Arg.Any<TEXT_POSITION[]>(), null).Returns(x =>
            {
                var startPositions = x[0] as TEXT_POSITION[];
                startPositions[0].dwLine = LINE;
                return VSConstants.S_OK;
            });

            mockDocumentPosition.GetFileName(out string _).Returns(x =>
            {
                x[0] = Path.Combine(DIRECTORY, FILE_NAME);
                return VSConstants.S_OK;
            });
            var mockBreakpoint = Substitute.For<RemoteBreakpoint>();
            var mockCodeContexts = new IDebugCodeContext2[numLocations];
            for (uint i = 0; i < numLocations; ++i)
            {
                var mockAddress = Substitute.For<SbAddress>();
                mockAddress.GetLoadAddress(mockRemoteTarget).Returns(ADDRESS + i);
                var mockFunction = Substitute.For<SbFunction>();
                mockFunction.GetName().Returns(NAME + i);
                mockAddress.GetFunction().Returns(mockFunction);
                var lineEntry = new LineEntryInfo();
                mockAddress.GetLineEntry().Returns(lineEntry);
                var mockDocumentContext = Substitute.For<IDebugDocumentContext2>();
                mockDocumentContextFactory.Create(lineEntry).Returns(mockDocumentContext);
                var mockCodeContext = Substitute.For<IDebugCodeContext2>();
                mockCodeContextFactory
                    .Create(ADDRESS + i, NAME + i, mockDocumentContext, Guid.Empty)
                    .Returns(mockCodeContext);
                var mockBreakpointLocation = Substitute.For<SbBreakpointLocation>();
                mockBreakpointLocation.GetAddress().Returns(mockAddress);
                mockBreakpoint.GetLocationAtIndex(i).Returns(mockBreakpointLocation);
                mockCodeContexts[i] = mockCodeContext;
            }
            mockBreakpoint.GetNumLocations().Returns((uint)numLocations);
            mockRemoteTarget.BreakpointCreateByLocation(Path.Combine(DIRECTORY, FILE_NAME),
                LINE + 1).Returns(mockBreakpoint);
            int result = program.EnumCodeContexts(mockDocumentPosition,
                                                  out IEnumDebugCodeContexts2 enumCodeContexts);
            Assert.AreEqual(VSConstants.S_OK, result);
            enumCodeContexts.GetCount(out uint count);
            Assert.AreEqual(numLocations, count);
            IDebugCodeContext2[] codeContexts = new IDebugCodeContext2[count];
            uint actual = 0;
            enumCodeContexts.Next(count, codeContexts, ref actual);
            Assert.AreEqual(count, actual);
            Assert.AreEqual(mockCodeContexts, codeContexts);
        }

        [Test]
        public void EnumCodeContextsNoRange()
        {
            var mockDocumentPosition = Substitute.For<IDebugDocumentPosition2>();
            mockDocumentPosition.GetRange(Arg.Any<TEXT_POSITION[]>(), null).Returns(
                VSConstants.E_FAIL);
            IEnumDebugCodeContexts2 contextsEnum;
            Assert.AreEqual(VSConstants.E_FAIL, program.EnumCodeContexts(mockDocumentPosition,
                out contextsEnum));
        }

        [Test]
        public void EnumCodeContextsCreateBreakpointFailed()
        {
            var mockDocumentPosition = Substitute.For<IDebugDocumentPosition2>();
            mockDocumentPosition.GetRange(Arg.Any<TEXT_POSITION[]>(), null).Returns(x =>
            {
                var startPositions = x[0] as TEXT_POSITION[];
                startPositions[0].dwLine = LINE;
                return VSConstants.S_OK;
            });
            string fileName;
            mockDocumentPosition.GetFileName(out fileName).Returns(x =>
            {
                x[0] = Path.Combine(DIRECTORY, FILE_NAME);
                return VSConstants.S_OK;
            });
            mockRemoteTarget.BreakpointCreateByLocation(Path.Combine(DIRECTORY, FILE_NAME),
                LINE + 1).Returns((RemoteBreakpoint)null);

            IEnumDebugCodeContexts2 contextsEnum;
            Assert.AreEqual(VSConstants.E_FAIL, program.EnumCodeContexts(mockDocumentPosition,
                out contextsEnum));
            Assert.AreEqual(null, contextsEnum);
        }

        [Test]
        public void EnumCodeContextsNullAddress()
        {
            var mockDocumentPosition = Substitute.For<IDebugDocumentPosition2>();
            mockDocumentPosition.GetRange(Arg.Any<TEXT_POSITION[]>(), null).Returns(x =>
            {
                var startPositions = x[0] as TEXT_POSITION[];
                startPositions[0].dwLine = LINE;
                return VSConstants.S_OK;
            });
            string fileName;
            mockDocumentPosition.GetFileName(out fileName).Returns(x =>
            {
                x[0] = Path.Combine(DIRECTORY, FILE_NAME);
                return VSConstants.S_OK;
            });
            var mockBreakpoint = Substitute.For<RemoteBreakpoint>();
            mockBreakpoint.GetNumLocations().Returns((uint)1);
            var mockBreakpointLocation = Substitute.For<SbBreakpointLocation>();
            mockBreakpointLocation.GetAddress().Returns((SbAddress)null);
            mockBreakpoint.GetLocationAtIndex(0).Returns(mockBreakpointLocation);
            mockRemoteTarget.BreakpointCreateByLocation(Path.Combine(DIRECTORY, FILE_NAME),
                LINE + 1).Returns(mockBreakpoint);

            IEnumDebugCodeContexts2 contextsEnum;
            Assert.AreEqual(VSConstants.S_OK, program.EnumCodeContexts(mockDocumentPosition,
                out contextsEnum));
            uint count;
            contextsEnum.GetCount(out count);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void DetachRequested()
        {
            // Testing default value of Detached property.
            Assert.IsFalse(program.DetachRequested);
        }

        [Test]
        public void DetachSuccess()
        {
            DebugEvent capturedEvent = null;
            mockDebugEngineHandler
                .SendEvent(Arg.Do<DebugEvent>(x => capturedEvent = x), Arg.Any<IGgpDebugProgram>())
                .Returns(0);
            mockSbProcess.Detach().Returns(true);

            Assert.AreEqual(VSConstants.S_OK, program.Detach());

            Assert.IsTrue(program.DetachRequested);
            ((ProgramDestroyEvent)capturedEvent).ExitInfo.HandleResult(
                er => Assert.That(er, Is.EqualTo(ExitReason.DebuggerDetached)),
                ex => Assert.Fail("Unexpected error in exit info: " + ex.ToString()));
        }

        [Test]
        public void DetachFailed()
        {
            mockSbProcess.Kill().Returns(false);

            Assert.AreEqual(VSConstants.E_FAIL, program.Detach());

            Assert.IsFalse(program.DetachRequested);
            mockDebugEngineHandler.DidNotReceive().SendEvent(Arg.Any<DebugEvent>(),
                Arg.Any<IGgpDebugProgram>());
        }

        [Test]
        public void TerminationRequested()
        {
            // Testing default value of Terminated property.
            Assert.IsFalse(program.TerminationRequested);
        }

        [Test]
        public void TerminateSuccess()
        {
            DebugEvent capturedEvent = null;
            mockDebugEngineHandler
                .SendEvent(Arg.Do<DebugEvent>(x => capturedEvent = x), Arg.Any<IGgpDebugProgram>())
                .Returns(0);
            mockSbProcess.Kill().Returns(true);

            Assert.AreEqual(VSConstants.S_OK, program.Terminate());

            Assert.IsTrue(program.TerminationRequested);
            ((ProgramDestroyEvent)capturedEvent).ExitInfo.HandleResult(
                er => Assert.That(er, Is.EqualTo(ExitReason.DebuggerTerminated)),
                ex => Assert.Fail("Unexpected error in exit info: " + ex.ToString()));
        }

        [Test]
        public void TerminateFailed()
        {
            DebugEvent capturedEvent = null;
            mockDebugEngineHandler
                .SendEvent(Arg.Do<DebugEvent>(x => capturedEvent = x), Arg.Any<IGgpDebugProgram>())
                .Returns(0);
            mockSbProcess.Kill().Returns(false);

            Assert.AreEqual(VSConstants.E_FAIL, program.Terminate());

            Assert.IsTrue(program.TerminationRequested);
            ((ProgramDestroyEvent)capturedEvent).ExitInfo.HandleResult(
                er => Assert.Fail("Unexpected reason in exit info: " + er.ToString()),
                ex => Assert.That(ex, Is.TypeOf<DebugProgram.TerminateProcessException>()));

        }

        void AssertEnumThreads(List<IDebugThread> mockThreads, IEnumDebugThreads2 enumThreads)
        {
            var numberThreads = mockThreads.Count;
            Assert.NotNull(enumThreads);
            uint fetched = 0;
            IDebugThread2[] debugthreads = new IDebugThread2[numberThreads];
            enumThreads.Next((uint)numberThreads, debugthreads, ref fetched);
            Assert.AreEqual(numberThreads, fetched);
            for(int i = 0; i < numberThreads; i++)
            {
                Assert.AreEqual(mockThreads[i], debugthreads[i]);
            }
        }
    }
}
