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

﻿using Google.VisualStudioFake.API;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace Google.VisualStudioFake.Internal.Jobs
{
    public class JobOrchestrator : IJobOrchestrator
    {
        public delegate void DebugEventHandler(DebugEventArgs args);

        public event DebugEventHandler DebugEvent;

        readonly IDebugSessionContext _debugSessionContext;
        readonly IJobQueue _queue;
        readonly ProgramStoppedJob.Factory _programStoppedJobFactory;
        readonly ProgramTerminatedJob.Factory _programTerminatedJobFactory;

        public JobOrchestrator(IDebugSessionContext debugSessionContext, IJobQueue queue,
                               ProgramStoppedJob.Factory programStoppedJobFactory,
                               ProgramTerminatedJob.Factory programTerminatedJobFactory)
        {
            _debugSessionContext = debugSessionContext;
            _queue = queue;
            _programStoppedJobFactory = programStoppedJobFactory;
            _programTerminatedJobFactory = programTerminatedJobFactory;
        }

        // Note that this method can be called from a thread other than the test main thread.
        public int HandleCallbackEvent(IDebugEngine2 pEngine, IDebugProcess2 pProcess,
                                       IDebugProgram2 pProgram, IDebugThread2 pThread,
                                       IDebugEvent2 pEvent)
        {
            if (pEvent is IDebugBreakpointEvent2 || pEvent is IDebugBreakEvent2 ||
                pEvent is IDebugStepCompleteEvent2)
            {
                _queue.Push(
                    _programStoppedJobFactory.Create(pEngine, pEvent, _debugSessionContext,
                                                     pThread));
            }
            else if (pEvent is IDebugProgramDestroyEvent2)
            {
                _queue.Push(
                    _programTerminatedJobFactory.Create(pEngine, pEvent, _debugSessionContext));
            }

            var pProgram3 = pProgram as IDebugProgram3;
            if (pProgram3 == null)
            {
                // TODO: Ensure program can be cast to IDebugProgram3 without
                // throwing across the COM/interop boundary.
                throw new NotSupportedException(
                    "'pProgram' must be castable to type " +
                    $"{nameof(IDebugProgram3)} but is of type {pProgram.GetType()}");
            }

            _queue.Push(new GenericJob(() =>
            {
                DebugEventHandler handler = DebugEvent;
                handler?.Invoke(new DebugEventArgs
                {
                    DebugEngine = pEngine,
                    Process = pProcess,
                    Program = pProgram3,
                    Thread = pThread,
                    Event = pEvent
                });
            }, $"{{eventType:\"{pEvent.GetType()}\"}}"));

            return VSConstants.S_OK;
        }
    }
}