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

            public Factory(JoinableTaskContext taskContext,
                IFiredBreakpointSetter firedBreakpointSetter)
                : base(taskContext)
            {
                _firedBreakpointSetter = firedBreakpointSetter;
            }

            public ProgramStoppedJob Create(IDebugEngine2 debugEngine, IDebugEvent2 evnt,
                IDebugSessionContext debugSessionContext, IDebugThread2 thread)
            {
                return new ProgramStoppedJob(taskContext, debugEngine, evnt, debugSessionContext,
                    thread, _firedBreakpointSetter);
            }
        }

        readonly IDebugSessionContext _debugSessionContext;
        readonly IDebugThread2 _thread;
        readonly IFiredBreakpointSetter _firedBreakpointSetter;

        public ProgramStoppedJob(JoinableTaskContext taskContext, IDebugEngine2 debugEngine,
            IDebugEvent2 evnt, IDebugSessionContext debugSessionContext, IDebugThread2 thread,
            IFiredBreakpointSetter firedBreakpointSetter)
            : base(taskContext, debugEngine, evnt)
        {
            _debugSessionContext = debugSessionContext;
            _thread = thread;
            _firedBreakpointSetter = firedBreakpointSetter;
        }

        protected override void RunJobTasks()
        {
            _debugSessionContext.SelectedThread = _thread;
            _debugSessionContext.SelectedStackFrame = GetFrames().FirstOrDefault();
            _firedBreakpointSetter.Set(GetBoundBreakpointsFired());
            _debugSessionContext.ProgramState = ProgramState.AtBreak;
        }

        IEnumerable<IDebugStackFrame2> GetFrames()
        {
            IEnumDebugFrameInfo2 enumFrames;
            var result = _thread.EnumFrameInfo(enum_FRAMEINFO_FLAGS.FIF_FRAME, 0, out enumFrames);
            HResultChecker.Check(result);

            uint count;
            result = enumFrames.GetCount(out count);
            HResultChecker.Check(result);

            var frames = new FRAMEINFO[count];
            uint numFetched = 0;
            result = enumFrames.Next(count, frames, ref numFetched);
            HResultChecker.Check(result);
            return frames.Select(f => f.m_pFrame);
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
