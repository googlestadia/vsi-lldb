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

using Google.VisualStudioFake.API;
using Google.VisualStudioFake.Internal.UI;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.Linq;

namespace Google.VisualStudioFake.Internal.Jobs
{
    /// <summary>
    /// Sets the program state to AtBreak and updates the session context with the selected thread
    /// and stack frame.
    /// </summary>
    public class ProgramStoppedJob : SynchronousJob
    {
        public new class Factory : SynchronousJob.Factory
        {
            readonly IFiredBreakpointSetter _firedBreakpointSetter;
            readonly IJobQueue _jobQueue;

            public Factory(JoinableTaskContext taskContext,
                           IFiredBreakpointSetter firedBreakpointSetter, IJobQueue jobQueue) : base(
                taskContext)
            {
                _firedBreakpointSetter = firedBreakpointSetter;
                _jobQueue = jobQueue;
            }

            public ProgramStoppedJob Create(IDebugEngine2 debugEngine, IDebugEvent2 evnt,
                                            IDebugSessionContext debugSessionContext,
                                            IDebugThread2 thread)
            {
                return new ProgramStoppedJob(taskContext, debugEngine, evnt, debugSessionContext,
                                             thread, _firedBreakpointSetter, _jobQueue);
            }
        }

        readonly IDebugSessionContext _debugSessionContext;
        readonly IDebugThread2 _thread;
        readonly IFiredBreakpointSetter _firedBreakpointSetter;
        readonly IJobQueue _jobQueue;

        public ProgramStoppedJob(JoinableTaskContext taskContext, IDebugEngine2 debugEngine,
                                 IDebugEvent2 evnt, IDebugSessionContext debugSessionContext,
                                 IDebugThread2 thread, IFiredBreakpointSetter firedBreakpointSetter,
                                 IJobQueue jobQueue) : base(taskContext, debugEngine, evnt)
        {
            _debugSessionContext = debugSessionContext;
            _thread = thread;
            _firedBreakpointSetter = firedBreakpointSetter;
            _jobQueue = jobQueue;
        }

        protected override void RunJobTasks()
        {
            _debugSessionContext.SelectedThread = _thread;
            _firedBreakpointSetter.Set(GetBoundBreakpointsFired());
            // Queue a sub-job since setting SelectedThread will
            // queue a job to update the selected frame.
            _jobQueue.Push(new GenericJob(
                               () => _debugSessionContext.ProgramState = ProgramState.AtBreak,
                               "Set program state AtBreak"));
        }

        IEnumerable<IDebugBoundBreakpoint2> GetBoundBreakpointsFired()
        {
            var breakpointEvent = evnt as IDebugBreakpointEvent2;
            if (breakpointEvent == null)
            {
                return Enumerable.Empty<IDebugBoundBreakpoint2>();
            }

            IEnumDebugBoundBreakpoints2 boundBreakpointsEnum;
            HResultChecker.Check(breakpointEvent.EnumBreakpoints(out boundBreakpointsEnum));
            HResultChecker.Check(boundBreakpointsEnum.Reset());
            uint count;
            HResultChecker.Check(boundBreakpointsEnum.GetCount(out count));
            var boundBreakpoints = new IDebugBoundBreakpoint2[count];
            uint actual = 0;
            HResultChecker.Check(boundBreakpointsEnum.Next(count, boundBreakpoints, ref actual));
            if (actual != count)
            {
                throw new VSFakeException("Could not fetch all bound breakpoints. " +
                                          $"Expected: {count}, got: {actual}");
            }

            return boundBreakpoints;
        }
    }
}