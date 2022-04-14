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
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI.DebugEngine
{
    public class LldbEventManager : IEventManager
    {
        public class Factory
        {
            readonly BoundBreakpointEnumFactory _boundBreakpointEnumFactory;
            readonly JoinableTaskContext _taskContext;

            public Factory(BoundBreakpointEnumFactory boundBreakpointEnumFactory,
                           JoinableTaskContext taskContext)
            {
                _boundBreakpointEnumFactory = boundBreakpointEnumFactory;
                _taskContext = taskContext;
            }

            public virtual IEventManager Create(IDebugEngineHandler debugEngineHandler,
                                                IBreakpointManager breakpointManager,
                                                IGgpDebugProgram program, SbProcess process,
                                                LldbListenerSubscriber listenerSubscriber)
            {
                return new LldbEventManager(debugEngineHandler, breakpointManager,
                                            _boundBreakpointEnumFactory, program, process,
                                            listenerSubscriber, _taskContext);
            }
        }

        readonly IDebugEngineHandler _debugEngineHandler;
        readonly IBreakpointManager _lldbBreakpointManager;
        readonly IGgpDebugProgram _program;
        readonly BoundBreakpointEnumFactory _boundBreakpointEnumFactory;
        readonly SbProcess _lldbProcess;
        readonly LldbListenerSubscriber _lldbListenerSubscriber;
        readonly JoinableTaskContext _taskContext;

        public bool IsRunning => _lldbListenerSubscriber.IsRunning;
        bool _subscribedToEvents = false;
        /// <summary>
        /// Controls whether or not we break on exec calls. Initially set to false, but after the
        /// first exec event is set back to true. This is because our launcher process (/bin/sh)
        /// execs out to the real game binary, and it is always the first exec.
        /// </summary>
        bool _shouldBreakOnExec = false;

        LldbEventManager(IDebugEngineHandler debugEngineHandler,
                         IBreakpointManager breakpointManager,
                         BoundBreakpointEnumFactory boundBreakpointEnumFactory,
                         IGgpDebugProgram program, SbProcess process,
                         LldbListenerSubscriber listenerSubscriber, JoinableTaskContext taskContext)
        {
            _debugEngineHandler = debugEngineHandler;
            _lldbBreakpointManager = breakpointManager;
            _boundBreakpointEnumFactory = boundBreakpointEnumFactory;
            _program = program;
            _lldbProcess = process;
            _lldbListenerSubscriber = listenerSubscriber;
            _taskContext = taskContext;
        }

        void LldbListenerOnExceptionOccured(object sender, ExceptionOccuredEventArgs e)
        {
            Trace.WriteLine($"Exception in listener: {e.Exception.Demystify()}");
            _debugEngineHandler.Abort(_program, ExitInfo.Error(e.Exception));
        }

        void LldbListenerOnStateChanged(object sender, StateChangedEventArgs e)
        {
            OnStateChangedEvent(e.Event);
        }

        /// <summary>
        /// Spin off a thread that will constantly get new events from the SbListener.
        /// </summary>
        public void StartListener()
        {
            SubscribeToChanges();
            _lldbListenerSubscriber.Start();
        }

        /// <summary>
        /// Subscribe to state changes and exceptions raised by SBListener instance.
        /// </summary>
        public void SubscribeToChanges()
        {
            if (!_subscribedToEvents)
            {
                _lldbListenerSubscriber.StateChanged += LldbListenerOnStateChanged;
                _lldbListenerSubscriber.ExceptionOccured += LldbListenerOnExceptionOccured;
                _subscribedToEvents = true;
            }
        }

        /// <summary>
        /// Stop the listenerSubscriber.
        /// </summary>
        public void StopListener()
        {
            _lldbListenerSubscriber.Stop();
            UnsubscribeFromChanges();
        }

        /// <summary>
        /// Unsubscribe from state changes and exceptions raised by SBListener instance.
        /// </summary>
        public void UnsubscribeFromChanges()
        {
            if (_subscribedToEvents)
            {
                _lldbListenerSubscriber.StateChanged -= LldbListenerOnStateChanged;
                _lldbListenerSubscriber.ExceptionOccured -= LldbListenerOnExceptionOccured;
                _subscribedToEvents = false;
            }
        }

        // Called when we receive a state changed LLDB event.
        void OnStateChangedEvent(SbEvent sbEvent)
        {
            if (sbEvent == null)
            {
                return;
            }

            var type = sbEvent.GetStateType();
            Debug.WriteLine("Received LLDB event: " + Enum.GetName(type.GetType(), type));
            switch (type)
            {
                case StateType.STOPPED:
                    if (sbEvent.GetProcessRestarted())
                    {
                        break;
                    }

                    var currentThread = _lldbProcess.GetSelectedThread();
                    var currentStopReason = StopReason.INVALID;
                    if (currentThread != null)
                    {
                        currentStopReason = currentThread.GetStopReason();
                    }

                    // When stopping pick the most relevant thread based on the stop reason.
                    if (currentThread == null || currentStopReason == StopReason.INVALID ||
                        currentStopReason == StopReason.NONE)
                    {
                        int numThreads = _lldbProcess.GetNumThreads();
                        RemoteThread planThread = null;
                        RemoteThread otherThread = null;
                        for (int i = 0; i < numThreads; ++i)
                        {
                            RemoteThread thread = _lldbProcess.GetThreadAtIndex(i);
                            switch (thread.GetStopReason())
                            {
                                case StopReason.INVALID:
                                // fall-through
                                case StopReason.NONE:
                                    break;
                                case StopReason.SIGNAL:
                                    if (otherThread == null)
                                    {
                                        var signalNumber = thread.GetStopReasonDataAtIndex(0);
                                        var unixSignals = _lldbProcess.GetUnixSignals();
                                        if (unixSignals != null &&
                                            unixSignals.GetShouldStop((int)signalNumber))
                                        {
                                            otherThread = thread;
                                        }
                                    }
                                    break;
                                case StopReason.TRACE:
                                // fall-through
                                case StopReason.BREAKPOINT:
                                // fall-through
                                case StopReason.WATCHPOINT:
                                // fall-through
                                case StopReason.EXCEPTION:
                                // fall-through
                                case StopReason.EXEC:
                                // fall-through
                                case StopReason.EXITING:
                                // fall-through
                                case StopReason.INSTRUMENTATION:
                                    if (otherThread == null)
                                    {
                                        otherThread = thread;
                                    }
                                    break;
                                case StopReason.PLAN_COMPLETE:
                                    if (planThread == null)
                                    {
                                        planThread = thread;
                                    }
                                    break;
                            }
                        }
                        if (planThread != null)
                        {
                            currentThread = planThread;
                        }
                        else if (otherThread != null)
                        {
                            currentThread = otherThread;
                        }
                        else if (currentThread == null)
                        {
                            currentThread = _lldbProcess.GetThreadAtIndex(0);
                        }
                        if (currentThread == null)
                        {
                            Trace.WriteLine("Error: Cannot handle event. No thread found.");
                            return;
                        }
                        _lldbProcess.SetSelectedThreadById(currentThread.GetThreadId());
                        currentStopReason = currentThread.GetStopReason();
                    }

                    // Log specific information about the stop event.
                    string message = "Received stop event.  Reason: " + currentStopReason;

                    var stopReasonDataCount = currentThread.GetStopReasonDataCount();
                    if (stopReasonDataCount > 0)
                    {
                        message += " Data:";
                        for (uint i = 0; i < stopReasonDataCount; i++)
                        {
                            message += " " + currentThread.GetStopReasonDataAtIndex(i);
                        }
                    }
                    Trace.WriteLine(message);

                    _taskContext.Factory.Run(async () => {
                        // We run the event resolution on the main thread to make sure
                        // we do not race with concurrent modifications in the breakpoint
                        // manager class (we could just have hit breakpoint that is being
                        // added by the main thread!).
                        await _taskContext.Factory.SwitchToMainThreadAsync();
                        IGgpDebugEvent eventToSend = null;
                        switch (currentStopReason)
                        {
                        case StopReason.BREAKPOINT:
                            eventToSend = HandleBreakpointStop(currentThread);
                            break;
                        case StopReason.WATCHPOINT:
                            eventToSend = HandleWatchpointStop(currentThread);
                            break;
                        case StopReason.SIGNAL:
                            eventToSend = HandleSignalStop(currentThread);
                            break;
                        case StopReason.PLAN_COMPLETE:
                            eventToSend = new StepCompleteEvent();
                            break;
                        case StopReason.EXEC:
                            if (_shouldBreakOnExec)
                            {
                                // Breaking here forces the VS to break on the exec event.
                                break;
                            }
                            _shouldBreakOnExec = true;
                            // Skip over this stop. See the discussion in the comment on the
                            // _shouldBreakOnExec instance variable.
                            this._lldbProcess.Continue();
                            return;
                        default:
                            break;
                        }
                        if (eventToSend == null)
                        {
                            eventToSend = new BreakEvent();
                        }
                        _debugEngineHandler.SendEvent(eventToSend, _program, currentThread);
                    });
                    break;
                case StateType.EXITED:
                    {
                        // There are two ways to exit a debug session without an error:
                        //   - We call program.Terminate, which causes LLDB to send this event.
                        //   - Program exits by itself, resulting in this event.
                        // We distinguish these events by checking if we called Terminate.
                        ExitReason exitReason = _program.TerminationRequested
                                                    ? ExitReason.DebuggerTerminated
                                                    : ExitReason.ProcessExited;
                        _debugEngineHandler.Abort(_program, ExitInfo.Normal(exitReason));
                    }
                    break;
                case StateType.DETACHED:
                    {
                        // Normally the only way to detach the process is by program.Detach.
                        // However, this check was retained to mirror the EXITED case and to
                        // record unexpected instances of detaching through some other path.
                        ExitReason exitReason = _program.DetachRequested
                                                    ? ExitReason.DebuggerDetached
                                                    : ExitReason.ProcessDetached;
                        _debugEngineHandler.Abort(_program, ExitInfo.Normal(exitReason));
                    }
                    break;
            }
        }

        /// <summary>
        /// Handle a breakpoint stop event.
        /// </summary>
        IGgpDebugEvent HandleBreakpointStop(RemoteThread thread)
        {
            uint stopReasonDataCount = thread.GetStopReasonDataCount();
            List<IDebugBoundBreakpoint2> boundBreakpoints = new List<IDebugBoundBreakpoint2>();
            for (uint i = 0; i < stopReasonDataCount; i += 2)
            {
                int pendingId = (int)thread.GetStopReasonDataAtIndex(i);
                int boundId = (int)thread.GetStopReasonDataAtIndex(i + 1);
                if (!_lldbBreakpointManager.GetPendingBreakpointById(
                        pendingId, out IPendingBreakpoint pendingBreakpoint))
                {
                    Trace.WriteLine($"Warning: Missing pending breakpoint with ID {pendingId}");
                    continue;
                }

                if (!pendingBreakpoint.GetBoundBreakpointById(boundId,
                                                              out IBoundBreakpoint boundBreakpoint))
                {
                    Trace.WriteLine(
                        $"Warning: Missing bound breakpoint with ID {pendingId}.{boundId}");
                    continue;
                }
                boundBreakpoint.OnHit();
                boundBreakpoints.Add(boundBreakpoint);
            }
            if (boundBreakpoints.Count <= 0)
            {
                return null;
            }
            return new BreakpointEvent(
                _boundBreakpointEnumFactory.Create(boundBreakpoints.ToArray()));
        }

        /// <summary>
        /// Handle a watchpoint stop event.
        /// </summary>
        IGgpDebugEvent HandleWatchpointStop(RemoteThread thread)
        {
            int id = (int)thread.GetStopReasonDataAtIndex(0);
            if (!_lldbBreakpointManager.GetWatchpointById(id, out IWatchpoint watchpoint))
            {
                return null;
            }
            IDebugBoundBreakpoint2[] boundBreakpoints = { watchpoint };
            return new BreakpointEvent(_boundBreakpointEnumFactory.Create(boundBreakpoints));
        }

        /// <summary>
        /// Handle a signal stop event.
        /// </summary>
        IGgpDebugEvent HandleSignalStop(RemoteThread thread)
        {
            var signalNumber = thread.GetStopReasonDataAtIndex(0);
            var signalTuple = SignalMap.Map[signalNumber];
            var name = signalTuple.Item1;
            var description = signalTuple.Item2;

            // We get a SIGSTOP when the user clicks on the pause execution button.
            if (name == "SIGSTOP")
            {
                return null;
            }
            return new ExceptionEvent(name, (uint)signalNumber, AD7Constants.VsExceptionStopState,
                                      description);
        }
    }
}
