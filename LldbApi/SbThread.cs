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

namespace LldbApi
{
    // Enumeration of reasons for a thread to be stopped.
    public enum StopReason
    {
        INVALID = 0,
        NONE,
        TRACE,
        BREAKPOINT,
        WATCHPOINT,
        SIGNAL,
        EXCEPTION,
        EXEC,
        PLAN_COMPLETE,
        EXITING,
        INSTRUMENTATION,
        PROCESSOR_TRACE,
        FORK,
        VFORK,
        VFORK_DONE,
    };

    // Interface mirrors the SBThread API as closely as possible.
    public interface SbThread
    {
        // Returns the process this thread belongs to.
        SbProcess GetProcess();

        // Returns the thread name.
        string GetName();

        // Returns the thread ID.
        ulong GetThreadId();

        // Returns detailed information about the thread.
        string GetStatus();

        // Steps into the next statement.
        void StepInto();

        // Steps over the next statement.
        void StepOver();

        // Steps out of the current stack frame.
        void StepOut();

        // Steps to the next instruction.
        void StepInstruction(bool stepOver);

        // Get the number of stack frames.
        uint GetNumFrames();

        // Get a specific stack frame at the specified index.
        SbFrame GetFrameAtIndex(uint index);

        // Returns the stop reason of the thread.
        StopReason GetStopReason();

        /// <summary>
        /// Gets information associated with a stop reason.
        /// Breakpoint stop reasons will have data that consists of pairs of
        /// breakpoint IDs followed by the breakpoint location IDs (they always come
        /// in pairs). I.e. GetStopReasonDataAtIndex(2*n) is a breakpoint ID while
        /// GetStopReasonDataAtIndex(2*n + 1) is the corresponding breakpoint location
        /// ID.
        ///
        /// Stop Reason              Count Data Type
        /// ======================== ===== =========================================
        /// eStopReasonNone          0
        /// eStopReasonTrace         0
        /// eStopReasonBreakpoint    N     duple: {breakpoint id, location id}
        /// eStopReasonWatchpoint    1     watchpoint id
        /// eStopReasonSignal        1     unix signal number
        /// eStopReasonException     N     exception data
        /// eStopReasonExec          0
        /// eStopReasonPlanComplete  0
        /// eStopReasonFork          1     pid of the child process
        /// eStopReasonVFork         1     pid of the child process
        /// eStopReasonVForkDone     0
        ///--------------------------------------------------------------------------
        /// </summary>
        ulong GetStopReasonDataAtIndex(uint index);

        // Gets the number of words associated with the stop reason.
        uint GetStopReasonDataCount();
    }
}
