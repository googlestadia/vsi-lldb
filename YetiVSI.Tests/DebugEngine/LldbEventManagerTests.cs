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
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSITestsCommon;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    [Timeout(3000)]
    class LldbEventManagerStartTests
    {
        SbListener _listener;
        LldbListenerSubscriber _listenerSubscriber;
        IEventManager _eventManager;

        [SetUp]
        public void SetUp()
        {
            _listener = Substitute.For<SbListener>();
            _listenerSubscriber = Substitute.For<LldbListenerSubscriber>(_listener);
            _eventManager = new LldbEventManager.Factory(new BoundBreakpointEnumFactory(), null)
                                .Create(null, null, null, null, _listenerSubscriber);
        }

        [Test]
        public void StartListenerToCompletion()
        {
            var events = new CountdownEvent(5);
            _listener.When(i => i.WaitForEvent(Arg.Any<uint>(), out SbEvent _)).Do(_ => {
                Assert.IsTrue(_eventManager.IsRunning);
                events.Signal();
                if (events.IsSet)
                {
                    _eventManager.StopListener();
                    Assert.IsFalse(_eventManager.IsRunning);
                }
            });

            Assert.IsFalse(_eventManager.IsRunning);
            _eventManager.StartListener();
            events.Wait();
        }
    }

    [TestFixture]
    [Timeout(3000)]
    class LldbEventManagerTests
    {
        readonly List<ulong> _breakpointStopData = new List<ulong> { 1, 2, 1, 3, 2, 1 };
        readonly List<ulong> _watchpointStopData = new List<ulong> { 1 };
        IEventManager _eventManager;

        SbListener _mockSbListener;
        LldbListenerSubscriber _mockListenerSubscriber;
        SbProcess _mockSbProcess;
        SbEvent _mockSbEvent;
        RemoteThread _mockRemoteThread;
        IBreakpointManager _mockBreakpointManager;
        IDebugEngineHandler _mockDebugEngineHandler;
        IPendingBreakpoint _mockPendingBreakpoint1;
        IPendingBreakpoint _mockPendingBreakpoint2;
        IBoundBreakpoint _mockBoundBreakpoint1;
        IBoundBreakpoint _mockBoundBreakpoint2;
        IBoundBreakpoint _mockBoundBreakpoint3;
        IWatchpoint _mockWatchpoint;
        IGgpDebugProgram _mockProgram;

        FakeMainThreadContext _threadContext;

        [SetUp]
        public void SetUp()
        {
            _mockSbListener = Substitute.For<SbListener>();
            _mockSbProcess = Substitute.For<SbProcess>();
            _mockSbEvent = Substitute.For<SbEvent>();
            _mockRemoteThread = Substitute.For<RemoteThread>();
            _mockBreakpointManager = Substitute.For<IBreakpointManager>();
            _mockDebugEngineHandler = Substitute.For<IDebugEngineHandler>();

            _mockPendingBreakpoint1 = Substitute.For<IPendingBreakpoint>();
            _mockPendingBreakpoint2 = Substitute.For<IPendingBreakpoint>();
            _mockBoundBreakpoint1 = Substitute.For<IBoundBreakpoint>();
            _mockBoundBreakpoint2 = Substitute.For<IBoundBreakpoint>();
            _mockBoundBreakpoint3 = Substitute.For<IBoundBreakpoint>();
            _mockWatchpoint = Substitute.For<IWatchpoint>();
            _mockProgram = Substitute.For<IGgpDebugProgram>();

            MockEvent(EventType.STATE_CHANGED, StateType.STOPPED, false);
            MockListener(_mockSbEvent, true);
            MockThread(_mockRemoteThread, StopReason.BREAKPOINT, _breakpointStopData);
            MockProcess(new List<RemoteThread> { _mockRemoteThread });
            MockBreakpointManager();

            _mockListenerSubscriber = Substitute.For<LldbListenerSubscriber>(_mockSbListener);

            _threadContext = new FakeMainThreadContext();

            _eventManager =
                new LldbEventManager
                    .Factory(new BoundBreakpointEnumFactory(), _threadContext.JoinableTaskContext)
                    .Create(_mockDebugEngineHandler, _mockBreakpointManager, _mockProgram,
                            _mockSbProcess, _mockListenerSubscriber);

            var lldbEventManager = _eventManager as LldbEventManager;
            lldbEventManager?.SubscribeToChanges();
        }

        [TearDown]
        public void TearDown()
        {
            var lldbEventManager = _eventManager as LldbEventManager;
            lldbEventManager?.UnsubscribeFromChanges();
            _threadContext.Dispose();
        }

        [Test]
        public void HandleEventNoEvent()
        {
            _mockSbEvent = null;

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.DidNotReceive().SendEvent(
                Arg.Any<IGgpDebugEvent>(), _mockProgram, Arg.Any<RemoteThread>());
            _mockDebugEngineHandler.DidNotReceive().SendEvent(
                Arg.Any<IGgpDebugEvent>(), _mockProgram, Arg.Any<IDebugThread2>());
        }

        [Test]
        public void HandleEventTypeNotStateChanged()
        {
            MockEvent(EventType.INTERRUPT, StateType.INVALID, false);

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.DidNotReceive().SendEvent(
                Arg.Any<IGgpDebugEvent>(), _mockProgram, Arg.Any<RemoteThread>());
            _mockDebugEngineHandler.DidNotReceive().SendEvent(
                Arg.Any<IGgpDebugEvent>(), _mockProgram, Arg.Any<IDebugThread2>());
        }

        [Test]
        public void HandleEventNullThread()
        {
            MockProcess(null);

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.DidNotReceive().SendEvent(
                Arg.Any<IGgpDebugEvent>(), _mockProgram, Arg.Any<RemoteThread>());
            _mockDebugEngineHandler.DidNotReceive().SendEvent(
                Arg.Any<IGgpDebugEvent>(), _mockProgram, Arg.Any<IDebugThread2>());
        }

        [Test]
        public void HandleEventStopped()
        {
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, _mockRemoteThread);
        }

        [Test]
        public void HandleEventInvalidWorkerThread()
        {
            const ulong mainThreadId = 1;
            _mockRemoteThread.GetThreadId().Returns(mainThreadId);
            var mockWorkerThread = Substitute.For<RemoteThread>();
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            MockThread(mockWorkerThread, StopReason.INVALID, new List<ulong>());
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, _mockRemoteThread);
            _mockSbProcess.Received(1).SetSelectedThreadById(mainThreadId);
        }

        [Test]
        public void HandleEventFallbackToMainThread()
        {
            const ulong mainThreadId = 1;
            _mockRemoteThread.GetThreadId().Returns(mainThreadId);
            var mockWorkerThread = Substitute.For<RemoteThread>();
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            MockThread(mockWorkerThread, StopReason.NONE, new List<ulong>());
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });
            _mockSbProcess.GetSelectedThread().Returns((RemoteThread)null);

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, _mockRemoteThread);
            _mockSbProcess.Received(1).SetSelectedThreadById(mainThreadId);
        }

        [Test]
        public void HandleEventBreakpointWorkerThread()
        {
            const ulong mockWorkerThreadId = 2;
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            var mockWorkerThread = Substitute.For<RemoteThread>();
            mockWorkerThread.GetThreadId().Returns(mockWorkerThreadId);
            MockThread(mockWorkerThread, StopReason.BREAKPOINT, new List<ulong> { 1u, 2u });
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });
            // We want to inspect the BreakpointEvent further, so capture it when OnSendEvent is
            // called.
            BreakpointEvent resultEvent = null;
            _mockDebugEngineHandler.SendEvent(
                Arg.Do<BreakpointEvent>(x => resultEvent = x),
                _mockProgram, mockWorkerThread);

            RaiseSingleStateChanged();

            _mockSbProcess.Received(1).SetSelectedThreadById(mockWorkerThreadId);
            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakpointEvent>(), _mockProgram, mockWorkerThread);
            AssertBreakpointEvent(resultEvent,
                                  new List<IDebugBoundBreakpoint2> { _mockBoundBreakpoint2 });
        }

        [Test]
        public void HandleEventNoneWorkerThread()
        {
            const ulong mainThreadId = 1;
            _mockRemoteThread.GetThreadId().Returns(mainThreadId);
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            var mockWorkerThread = Substitute.For<RemoteThread>();
            mockWorkerThread.GetStopReason().Returns(StopReason.NONE);
            mockWorkerThread.GetStopReasonDataCount().Returns(0u);
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, _mockRemoteThread);
            _mockSbProcess.Received(1).SetSelectedThreadById(mainThreadId);
        }

        [Test]
        public void HandleEventTraceWorkerThread()
        {
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            const ulong mockWorkerThreadId = 2;
            var mockWorkerThread = Substitute.For<RemoteThread>();
            mockWorkerThread.GetThreadId().Returns(mockWorkerThreadId);
            MockThread(mockWorkerThread, StopReason.TRACE, new List<ulong>());
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, mockWorkerThread);
            _mockSbProcess.Received(1).SetSelectedThreadById(mockWorkerThreadId);
        }

        [Test]
        public void HandleEventSignalWorkerThread()
        {
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            const ulong mockWorkerThreadId = 2;
            const int signalNumber = 1;
            var mockWorkerThread = Substitute.For<RemoteThread>();
            mockWorkerThread.GetThreadId().Returns(mockWorkerThreadId);
            MockThread(mockWorkerThread, StopReason.SIGNAL, new List<ulong> { signalNumber });
            var mockUnixSignals = Substitute.For<SbUnixSignals>();
            mockUnixSignals.GetShouldStop(signalNumber).Returns(true);
            _mockSbProcess.GetUnixSignals().Returns(mockUnixSignals);
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });
            ExceptionEvent resultEvent = null;
            _mockDebugEngineHandler.SendEvent(
                Arg.Do<ExceptionEvent>(x => resultEvent = x),
                _mockProgram, mockWorkerThread);

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<ExceptionEvent>(), _mockProgram, mockWorkerThread);
            _mockSbProcess.Received(1).SetSelectedThreadById(mockWorkerThreadId);
            AssertExceptionEvent(resultEvent, signalNumber);
        }

        [Test]
        public void HandleEventWatchpointWorkerThread()
        {
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            var mockWorkerThread = Substitute.For<RemoteThread>();
            const ulong mockWorkerThreadId = 2;
            mockWorkerThread.GetThreadId().Returns(mockWorkerThreadId);
            MockThread(mockWorkerThread, StopReason.WATCHPOINT, _watchpointStopData);
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });
            MockBreakpointManagerForWatchpoint();
            BreakpointEvent resultEvent = null;
            _mockDebugEngineHandler.SendEvent(
                Arg.Do<BreakpointEvent>(x => resultEvent = x),
                _mockProgram, mockWorkerThread);

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakpointEvent>(), _mockProgram, mockWorkerThread);
            AssertBreakpointEvent(resultEvent,
                                  new List<IDebugBoundBreakpoint2> { _mockWatchpoint });
            _mockSbProcess.Received(1).SetSelectedThreadById(mockWorkerThreadId);
        }

        [Test]
        public void HandleEventExceptionWorkerThread()
        {
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            var mockWorkerThread = Substitute.For<RemoteThread>();
            const ulong mockWorkerThreadId = 2;
            mockWorkerThread.GetThreadId().Returns(mockWorkerThreadId);
            MockThread(mockWorkerThread, StopReason.EXCEPTION, new List<ulong>());
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, mockWorkerThread);
            _mockSbProcess.Received(1).SetSelectedThreadById(mockWorkerThreadId);
        }

        [Test]
        public void HandleEventExecWorkerThread()
        {
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            var mockWorkerThread = Substitute.For<RemoteThread>();
            const ulong mockWorkerThreadId = 2;
            mockWorkerThread.GetThreadId().Returns(mockWorkerThreadId);
            MockThread(mockWorkerThread, StopReason.EXEC, new List<ulong>());
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, mockWorkerThread);
            _mockSbProcess.Received(1).SetSelectedThreadById(mockWorkerThreadId);
        }

        [Test]
        public void HandleEventPlanCompleteWorkerThread()
        {
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            var mockWorkerThread = Substitute.For<RemoteThread>();
            const ulong mockWorkerThreadId = 2;
            mockWorkerThread.GetThreadId().Returns(mockWorkerThreadId);
            MockThread(mockWorkerThread, StopReason.PLAN_COMPLETE, new List<ulong>());
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<StepCompleteEvent>(), _mockProgram,
                mockWorkerThread);
            _mockSbProcess.Received(1).SetSelectedThreadById(mockWorkerThreadId);
        }

        [Test]
        public void HandleEventSignalNotStopWorkerThread()
        {
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            const ulong mockWorkerThreadId = 2;
            const int signalNumber = 1;
            var mockWorkerThread = Substitute.For<RemoteThread>();
            mockWorkerThread.GetThreadId().Returns(mockWorkerThreadId);
            MockThread(mockWorkerThread, StopReason.SIGNAL, new List<ulong> { signalNumber });
            var mockUnixSignals = Substitute.For<SbUnixSignals>();
            mockUnixSignals.GetShouldStop(signalNumber).Returns(false);
            _mockSbProcess.GetUnixSignals().Returns(mockUnixSignals);
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, _mockRemoteThread);
            _mockSbProcess.DidNotReceive().SetSelectedThreadById(mockWorkerThreadId);
        }

        [Test]
        public void HandleEventExitingWorkerThread()
        {
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            var mockWorkerThread = Substitute.For<RemoteThread>();
            const ulong mockWorkerThreadId = 2;
            mockWorkerThread.GetThreadId().Returns(mockWorkerThreadId);
            MockThread(mockWorkerThread, StopReason.EXITING, new List<ulong>());
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, mockWorkerThread);
            _mockSbProcess.Received(1).SetSelectedThreadById(mockWorkerThreadId);
        }

        [Test]
        public void HandleEventInstrumentationWorkerThread()
        {
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            var mockWorkerThread = Substitute.For<RemoteThread>();
            const ulong mockWorkerThreadId = 2;
            mockWorkerThread.GetThreadId().Returns(mockWorkerThreadId);
            MockThread(mockWorkerThread, StopReason.INSTRUMENTATION, new List<ulong>());
            MockProcess(new List<RemoteThread> { _mockRemoteThread, mockWorkerThread });

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, mockWorkerThread);
            _mockSbProcess.Received(1).SetSelectedThreadById(mockWorkerThreadId);
        }

        [Test]
        public void HandleEventPrioritizePlanFinishes()
        {
            MockThread(_mockRemoteThread, StopReason.NONE, new List<ulong>());
            const ulong planThreadId = 2;
            var mockPlanThread = Substitute.For<RemoteThread>();
            mockPlanThread.GetThreadId().Returns(planThreadId);
            MockThread(mockPlanThread, StopReason.PLAN_COMPLETE, new List<ulong>());
            var mockOtherThread = Substitute.For<RemoteThread>();
            MockThread(mockOtherThread, StopReason.TRACE, new List<ulong>());
            MockProcess(
                new List<RemoteThread> { _mockRemoteThread, mockPlanThread, mockOtherThread });

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<StepCompleteEvent>(), _mockProgram, mockPlanThread);
            _mockSbProcess.Received(1).SetSelectedThreadById(planThreadId);
        }

        [TestCase(true, ExitReason.DebuggerDetached)]
        [TestCase(false, ExitReason.ProcessDetached)]
        public void HandleEventDetached(bool detachLocally, ExitReason exitReason)
        {
            ProgramDestroyEvent capturedEvent = null;
            _mockDebugEngineHandler
                .SendEvent(Arg.Do<ProgramDestroyEvent>(x => capturedEvent = x),
                           Arg.Any<IGgpDebugProgram>())
                .Returns(0);

            _mockProgram.DetachRequested.Returns(detachLocally);
            MockEvent(EventType.STATE_CHANGED, StateType.DETACHED, false);

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<ProgramDestroyEvent>(), _mockProgram);

            capturedEvent.ExitInfo.HandleResult(
                er => Assert.That(er, Is.EqualTo(exitReason)),
                ex => Assert.Fail("Unexpected error in exit info: " + ex));
        }

        [TestCase(true, ExitReason.DebuggerTerminated)]
        [TestCase(false, ExitReason.ProcessExited)]
        public void HandleEventExited(bool terminateLocally, ExitReason exitReason)
        {
            ProgramDestroyEvent capturedEvent = null;
            _mockDebugEngineHandler
                .SendEvent(Arg.Do<ProgramDestroyEvent>(x => capturedEvent = x),
                           Arg.Any<IGgpDebugProgram>())
                .Returns(0);

            _mockProgram.TerminationRequested.Returns(terminateLocally);
            MockEvent(EventType.STATE_CHANGED, StateType.EXITED, false);

            RaiseSingleStateChanged();
            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<ProgramDestroyEvent>(), _mockProgram);

            capturedEvent.ExitInfo.HandleResult(
                er => Assert.That(er, Is.EqualTo(exitReason)),
                ex => Assert.Fail("Unexpected error in exit info: " + ex));
        }

        [Test]
        public void HandleEventBreakpointNoStopReasonData()
        {
            MockThread(_mockRemoteThread, StopReason.BREAKPOINT, new List<ulong>());

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, _mockRemoteThread);
        }

        [Test]
        public void HandleEventBreakpoint()
        {
            // We want to inspect the BreakpointEvent further, so capture it when OnSendEvent is
            // called.
            BreakpointEvent resultEvent = null;
            _mockDebugEngineHandler.SendEvent(
                Arg.Do<BreakpointEvent>(x => resultEvent = x),
                _mockProgram, _mockRemoteThread);

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakpointEvent>(), _mockProgram, _mockRemoteThread);
            AssertBreakpointEvent(resultEvent, new List<IDebugBoundBreakpoint2> {
                _mockBoundBreakpoint2, _mockBoundBreakpoint3, _mockBoundBreakpoint1
            });
        }

        [Test]
        public void HandleEventInvalidPendingBreakpoint()
        {
            // Return null and false for one of the pending breakpoints.
            _mockBreakpointManager
                .GetPendingBreakpointById(Arg.Any<int>(), out IPendingBreakpoint _)
                .ReturnsForAnyArgs(x => {
                    int id = (int)x[0];
                    switch (id)
                    {
                    case 1:
                        x[1] = _mockPendingBreakpoint1;
                        return true;
                    default:
                        x[1] = null;
                        return false;
                    }
                });

            // We want to inspect the BreakpointEvent further, so capture it when OnSendEvent is
            // called.
            BreakpointEvent resultEvent = null;
            _mockDebugEngineHandler.SendEvent(
                Arg.Do<BreakpointEvent>(x => resultEvent = x),
                _mockProgram, _mockRemoteThread);

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakpointEvent>(), _mockProgram, _mockRemoteThread);
            AssertBreakpointEvent(
                resultEvent,
                new List<IDebugBoundBreakpoint2> { _mockBoundBreakpoint2, _mockBoundBreakpoint3 });
        }

        [Test]
        public void HandleEventInvalidBoundBreakpoint()
        {
            // Return null and false for all bound breakpoints on the pending breakpoint.
            _mockPendingBreakpoint1.GetBoundBreakpointById(Arg.Any<int>(), out IBoundBreakpoint _)
                .ReturnsForAnyArgs(x => {
                    x[1] = null;
                    return false;
                });

            // We want to inspect the BreakpointEvent further, so capture it when OnSendEvent is
            // called.
            BreakpointEvent resultEvent = null;
            _mockDebugEngineHandler.SendEvent(
                Arg.Do<BreakpointEvent>(x => resultEvent = x),
                _mockProgram, _mockRemoteThread);

            _mockDebugEngineHandler.SendEvent(Arg.Any<BreakpointEvent>(),
                                              _mockProgram, _mockRemoteThread);

            RaiseSingleStateChanged();

            AssertBreakpointEvent(resultEvent,
                                  new List<IDebugBoundBreakpoint2> { _mockBoundBreakpoint1 });
        }

        [Test]
        public void HandleEventNoValidBreakpoint()
        {

            // Return null and false for all pending breakpoints.
            _mockBreakpointManager
                .GetPendingBreakpointById(Arg.Any<int>(), out IPendingBreakpoint _)
                .ReturnsForAnyArgs(x => {
                    x[1] = null;
                    return false;
                });

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, _mockRemoteThread);
        }

        [Test]
        public void HandleEventWatchpoint()
        {
            MockThread(_mockRemoteThread, StopReason.WATCHPOINT, _watchpointStopData);
            MockProcess(new List<RemoteThread> { _mockRemoteThread });
            MockBreakpointManagerForWatchpoint();

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakpointEvent>(), _mockProgram, _mockRemoteThread);
        }

        [Test]
        public void HandleEventWatchpointNoWatchpoint()
        {
            MockThread(_mockRemoteThread, StopReason.WATCHPOINT, _watchpointStopData);
            _mockBreakpointManager.GetWatchpointById(Arg.Any<int>(), out IWatchpoint _)
                .ReturnsForAnyArgs(x => {
                    x[1] = null;
                    return false;
                });

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, _mockRemoteThread);
        }

        [Test]
        public void HandleSignalSigstop()
        {
            const ulong sigstop = 19;
            var stopData = new List<ulong> { sigstop };
            MockThread(_mockRemoteThread, StopReason.SIGNAL, stopData);

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<BreakEvent>(), _mockProgram, _mockRemoteThread);
        }

        [Test]
        public void HandleSignalOther()
        {
            const ulong sigabrt = 6;
            var stopData = new List<ulong> { sigabrt };
            MockThread(_mockRemoteThread, StopReason.SIGNAL, stopData);
            MockProcess(new List<RemoteThread> { _mockRemoteThread });
            ExceptionEvent exceptionEvent = null;
            _mockDebugEngineHandler.SendEvent(
                Arg.Do<ExceptionEvent>(x => exceptionEvent = x),
                _mockProgram, _mockRemoteThread);

            RaiseSingleStateChanged();

            _mockDebugEngineHandler.Received(1).SendEvent(
                Arg.Any<ExceptionEvent>(), _mockProgram, _mockRemoteThread);
            AssertExceptionEvent(exceptionEvent, (uint)sigabrt);
        }

        [Test]
        public void StartListenerAborted()
        {
            _mockListenerSubscriber.ExceptionOccured +=
                Raise.EventWith(new ExceptionOccuredEventArgs(new TestException()));

            _mockDebugEngineHandler.Received(1).SendEvent(Arg.Any<ProgramDestroyEvent>(),
                                                          _mockProgram);
        }

        class TestException : Exception
        {
        }

        void MockEvent(EventType eventType, StateType stateType, bool processResumed)
        {
            _mockSbEvent.GetEventType().Returns(eventType);
            _mockSbEvent.GetStateType().Returns(stateType);
            _mockSbEvent.GetProcessRestarted().Returns(processResumed);
        }

        void MockListener(SbEvent sbEvent, bool waitForEventResult)
        {
            _mockSbListener.WaitForEvent(Arg.Any<uint>(), out SbEvent _).Returns(x => {
                x[1] = sbEvent;
                return waitForEventResult;
            });
        }

        void MockThread(RemoteThread thread, StopReason stopReason, List<ulong> stopData)
        {
            thread.GetStopReason().Returns(stopReason);
            thread.GetStopReasonDataCount().Returns((uint)stopData.Count);
            thread.GetStopReasonDataAtIndex(Arg.Any<uint>())
                .Returns(i => stopData[Convert.ToInt32(i[0])]);
        }

        void RaiseSingleStateChanged()
        {
            _mockListenerSubscriber.StateChanged +=
                Raise.EventWith(new StateChangedEventArgs(_mockSbEvent));
        }

        void MockProcess(List<RemoteThread> remoteThreads)
        {
            if (remoteThreads != null && remoteThreads.Count > 0)
            {
                _mockSbProcess.GetNumThreads().Returns(remoteThreads.Count);
                _mockSbProcess.GetSelectedThread().Returns(remoteThreads[0]);
                _mockSbProcess.GetThreadAtIndex(Arg.Any<int>())
                    .Returns(x => remoteThreads[(int)x[0]]);
            }
            else
            {
                _mockSbProcess.GetNumThreads().Returns(0);
                _mockSbProcess.GetSelectedThread().Returns((RemoteThread)null);
                _mockSbProcess.GetThreadAtIndex(Arg.Any<int>()).Returns((RemoteThread)null);
            }
        }

        void MockBreakpointManagerForWatchpoint()
        {
            _mockBreakpointManager.GetWatchpointById(1, out IWatchpoint _).Returns(x => {
                x[1] = _mockWatchpoint;
                return true;
            });
        }

        void MockBreakpointManager()
        {
            // NSubstitute will try to match the value of the out parameter if you just use
            // 'Returns'.  Since we have no way of resetting the value of pendingBreakpoint between
            // calls in the same test (since this happens in real code), we have to return for all
            // args, and do the argument matching ourselves.
            _mockBreakpointManager.GetPendingBreakpointById(Arg.Any<int>(), out var _)
                .ReturnsForAnyArgs(x => {
                    int id = (int)x[0];
                    switch (id)
                    {
                    case 1:
                        x[1] = _mockPendingBreakpoint1;
                        return true;
                    case 2:
                        x[1] = _mockPendingBreakpoint2;
                        return true;
                    default:
                        x[1] = null;
                        return false;
                    }
                });

            _mockPendingBreakpoint1.GetBoundBreakpointById(Arg.Any<int>(), out var _)
                .ReturnsForAnyArgs(x => {
                    int id = (int)x[0];
                    switch (id)
                    {
                    case 2:
                        x[1] = _mockBoundBreakpoint2;
                        return true;
                    case 3:
                        x[1] = _mockBoundBreakpoint3;
                        return true;
                    default:
                        x[1] = null;
                        return false;
                    }
                });
            _mockPendingBreakpoint2.GetBoundBreakpointById(Arg.Any<int>(), out var _)
                .ReturnsForAnyArgs(x => {
                    int id = (int)x[0];
                    switch (id)
                    {
                    case 1:
                        x[1] = _mockBoundBreakpoint1;
                        return true;
                    default:
                        x[1] = null;
                        return false;
                    }
                });
        }

        void AssertBreakpointEvent(BreakpointEvent breakpointEvent,
                                   List<IDebugBoundBreakpoint2> expectedBoundBreakpoints)
        {
            int numberBreakpoints = expectedBoundBreakpoints.Count;
            Assert.AreNotEqual(null, breakpointEvent);
            breakpointEvent.EnumBreakpoints(out IEnumDebugBoundBreakpoints2 enumBoundBreakpoints);
            enumBoundBreakpoints.GetCount(out uint count);
            Assert.AreEqual(numberBreakpoints, count);
            var boundBreakpoints = new IDebugBoundBreakpoint2[numberBreakpoints];
            uint numberReturned = 0;
            enumBoundBreakpoints.Next((uint)numberBreakpoints, boundBreakpoints,
                                      ref numberReturned);
            Assert.AreEqual(numberBreakpoints, numberReturned);
            var difference = expectedBoundBreakpoints.Except(boundBreakpoints).ToList();
            Assert.IsEmpty(difference);
        }

        void AssertExceptionEvent(ExceptionEvent e, uint signalNumber)
        {
            EXCEPTION_INFO[] infos = new EXCEPTION_INFO[1];
            e.GetException(infos);
            (string name, string description) = SignalMap.Map[signalNumber];
            Assert.AreEqual(signalNumber, infos[0].dwCode);
            Assert.AreEqual(name, infos[0].bstrExceptionName);
            e.GetExceptionDescription(out string exceptionDescription);
            Assert.AreEqual(name + ": " + description, exceptionDescription);
        }
    }
}
