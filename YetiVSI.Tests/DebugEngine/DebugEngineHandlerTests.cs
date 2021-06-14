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

﻿using DebuggerApi;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using TestsCommon.TestSupport;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Exit;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugEngineHandlerTests
    {
        readonly Guid programId = new Guid("3aac3ded-21b6-43bd-b4b6-77a77d9634fc");
        readonly string programName = "My Program";
        readonly string threadName = "My Thread";
        readonly uint threadId = uint.MaxValue;

        LogSpy logSpy;
        IGgpDebugProgram program;
        IDebugEngine2 debugEngine;
        IDebugEventCallback2 callback;
        IDebugThread thread;

        DebugEngineHandler handler;

        [SetUp]
        public void SetUp()
        {
            var taskContext = new JoinableTaskContext();

            logSpy = new LogSpy();
            logSpy.Attach();

            program = Substitute.For<IGgpDebugProgram>();
            debugEngine = Substitute.For<IDebugEngine2>();
            callback = Substitute.For<IDebugEventCallback2>();

            handler = new DebugEngineHandler(taskContext, debugEngine, callback);

            var idArg = Arg.Any<Guid>();
            program.GetProgramId(out idArg)
                .Returns(x =>
                {
                    x[0] = programId;
                    return VSConstants.S_OK;
                });

            var nameArg = Arg.Any<string>();
            program.GetName(out nameArg)
                .Returns(x =>
                {
                    x[0] = programName;
                    return VSConstants.S_OK;
                });

            thread = Substitute.For<IDebugThread>();

            var threadNameArg = Arg.Any<string>();
            thread.GetName(out threadNameArg)
                .Returns(x =>
                {
                    x[0] = threadName;
                    return VSConstants.S_OK;
                });

            var threadIdArg = Arg.Any<uint>();
            thread.GetThreadId(out threadIdArg)
                .Returns(x =>
                {
                    x[0] = threadId;
                    return VSConstants.S_OK;
                });
        }

        [TearDown]
        public void TearDown()
        {
            logSpy.Detach();
        }

        [Test]
        public void SendEventDebugThreadSendsToEventCallback()
        {
            var evnt = new TestEvent(5);

            handler.SendEvent(evnt, program, thread);

            var id = Arg.Any<Guid>();
            callback.Received(1).Event(debugEngine, null, program, thread, evnt, ref id, 5);

            var output = logSpy.GetOutput();
            Assert.That(output, Does.Contain(programName));
            Assert.That(output, Does.Contain(programId.ToString()));
            Assert.That(output, Does.Contain(threadName));
            Assert.That(output, Does.Contain(threadId.ToString()));
        }

        [Test]
        public void SendEventDebugThreadWhenNoProgramName()
        {
            var nameArg = Arg.Any<string>();
            program.GetName(out nameArg)
                .Returns(x =>
                {
                    x[0] = null;
                    return VSConstants.S_FALSE;
                });

            var evnt = new TestEvent(5);

            handler.SendEvent(evnt, program, thread);

            var id = Arg.Any<Guid>();
            callback.Received(1).Event(debugEngine, null, program, thread, evnt, ref id, 5);

            var output = logSpy.GetOutput();
            Assert.That(output, Does.Not.Contain(programName));
            Assert.That(output, Does.Contain(program.ToString()));
            Assert.That(output, Does.Contain(programId.ToString()));
            Assert.That(output, Does.Contain(threadName));
            Assert.That(output, Does.Contain(threadId.ToString()));
        }

        [Test]
        public void SendEventDebugThreadWhenNoProgramId()
        {
            var idArg = Arg.Any<Guid>();
            program.GetProgramId(out idArg)
                .Returns(x =>
                {
                    x[0] = Guid.NewGuid();
                    return VSConstants.S_FALSE;
                });

            var evnt = new TestEvent(5);
            handler.SendEvent(evnt, program, thread);

            var id = Arg.Any<Guid>();
            callback.Received(1).Event(debugEngine, null, program, thread, evnt, ref id, 5);

            var output = logSpy.GetOutput();
            Assert.That(output, Does.Contain(programName));
            Assert.That(output, Does.Not.Contain(programId.ToString()));
            Assert.That(output, Does.Contain(threadName));
            Assert.That(output, Does.Contain(threadId.ToString()));
        }

        [Test]
        public void SendEventDebugThreadWithNullThread()
        {
            var evnt = new TestEvent(5);
            handler.SendEvent(evnt, program, (IDebugThread2)null);

            var id = Arg.Any<Guid>();
            callback.Received(1).Event(debugEngine, null, program, null, evnt, ref id, 5);

            var output = logSpy.GetOutput();
            Assert.That(output, Does.Contain(programName));
            Assert.That(output, Does.Contain(programId.ToString()));
            Assert.That(output, Does.Contain($"thread [null]"));
        }

        [Test]
        public void SendEventDebugThreadWithNoThreadId()
        {
            var idArg = Arg.Any<uint>();
            thread.GetThreadId(out idArg)
                .Returns(x =>
                {
                    x[0] = threadId;
                    return VSConstants.S_FALSE;
                });

            var evnt = new TestEvent(5);
            handler.SendEvent(evnt, program, thread);

            var id = Arg.Any<Guid>();
            callback.Received(1).Event(debugEngine, null, program, thread, evnt, ref id, 5);

            var output = logSpy.GetOutput();
            Assert.That(output, Does.Contain(programName));
            Assert.That(output, Does.Contain(programId.ToString()));
            Assert.That(output, Does.Contain(threadName));
            Assert.That(output, Does.Not.Contain(threadId.ToString()));
        }

        [Test]
        public void SendEventRemoteThreadSendsWithDebugThreadFromProgram()
        {
            var remoteThread = Substitute.For<RemoteThread>();
            program.GetDebugThread(remoteThread).Returns(thread);

            var evnt = new TestEvent(5);
            handler.SendEvent(evnt, program, remoteThread);

            var id = Arg.Any<Guid>();
            callback.Received(1).Event(debugEngine, null, program, thread, evnt, ref id, 5);

            var output = logSpy.GetOutput();
            Assert.That(output, Does.Contain(programName));
            Assert.That(output, Does.Contain(programId.ToString()));
            Assert.That(output, Does.Contain(threadName));
            Assert.That(output, Does.Contain(threadId.ToString()));
        }
    }

    [TestFixture]
    class DebugEngineHandlerExtensionsTests
    {
        IDebugEngineHandler debugEngineHandler;
        IGgpDebugProgram program;

        [SetUp]
        public void SetUp()
        {
            debugEngineHandler = Substitute.For<IDebugEngineHandler>();
            program = Substitute.For<IGgpDebugProgram>();
        }

        [Test]
        public void SendEventSendsWithNullDebugThread()
        {
            var evnt = new TestEvent(5);
            debugEngineHandler.SendEvent(evnt, program);
            debugEngineHandler.Received(1).SendEvent(evnt, program, (IDebugThread2)null);
        }

        [TestCase(ExitReason.DebuggerTerminated)]
        [TestCase(ExitReason.ProcessExited)]
        [TestCase(ExitReason.DebuggerDetached)]
        [TestCase(ExitReason.ProcessDetached)]
        public void AbortSendsProgramDestroyEvent(ExitReason exitReason)
        {
            ProgramDestroyEvent destroyEvent = null;
            debugEngineHandler.SendEvent(
                Arg.Do<DebugEvent>(e => destroyEvent = e as ProgramDestroyEvent),
                Arg.Any<IGgpDebugProgram>(), Arg.Any<IDebugThread2>())
                .Returns(VSConstants.S_OK);

            debugEngineHandler.Abort(program, ExitInfo.Normal(exitReason));
            debugEngineHandler.Received(1).SendEvent(
                Arg.Is<DebugEvent>(e => e is ProgramDestroyEvent), program, (IDebugThread2)null);

            destroyEvent.ExitInfo.HandleResult(
                r => Assert.That(r, Is.EqualTo(exitReason)),
                ex => Assert.Fail("Unexpected error: " + ex.ToString()));
        }

        [Test]
        public void OnBreakPointErrorSendsBreakpointErrorEvent()
        {
            var breakpointError =
                new DebugBreakpointError(null, enum_BP_ERROR_TYPE.BPET_SEV_GENERAL, "oops");

            Predicate<DebugEvent> isBreakpointErrorEvent = e =>
            {
                var be = e as BreakpointErrorEvent;
                if (be == null)
                {
                    return false;
                }
                IDebugErrorBreakpoint2 error;
                be.GetErrorBreakpoint(out error);
                return error == breakpointError;
            };

            debugEngineHandler.OnBreakpointError(breakpointError, program);
            debugEngineHandler.Received(1).SendEvent(
                Arg.Is<DebugEvent>(e => isBreakpointErrorEvent(e)), program, (IDebugThread2)null);
        }

        [Test]
        public void OnBreakpointBoundSendsBreakpointBoundEvent()
        {
            var breakpoint = Substitute.For<IPendingBreakpoint>();
            var boundLocations = Substitute.For<IEnumerable<IDebugBoundBreakpoint2>>();
            debugEngineHandler.OnBreakpointBound(breakpoint, boundLocations, program);
            debugEngineHandler.Received(1).SendEvent(
                Arg.Is<DebugEvent>(e => IsBreakpointBoundEvent(breakpoint, e)), program,
                (IDebugThread2)null);
        }

        [Test]
        public void OnWatchpointBoundSendsWatchpointBoundEvent()
        {
            var watchpoint = Substitute.For<IWatchpoint>();
            debugEngineHandler.OnWatchpointBound(watchpoint, program);
            debugEngineHandler.Received(1).SendEvent(
                Arg.Is<DebugEvent>(e => IsBreakpointBoundEvent(watchpoint, e)), program,
                (IDebugThread2)null);
        }

        bool IsBreakpointBoundEvent(IDebugPendingBreakpoint2 expected, DebugEvent actual)
        {
            var be = actual as BreakpointBoundEvent;
            if (be == null)
            {
                return false;
            }

            IDebugPendingBreakpoint2 pendingBreakpoint;
            be.GetPendingBreakpoint(out pendingBreakpoint);
            return pendingBreakpoint == expected;
        }
    }

    internal class TestEvent : DebugEvent
    {
        public TestEvent(uint attributes) : base(attributes, Guid.NewGuid())
        {
        }
    }
}
