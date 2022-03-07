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

using DebuggerCommonApi;
using DebuggerGrpcClient.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DebuggerApi
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
    };

    /// <summary>
    /// Interface based off of the SBThread API.
    /// </summary>
    public interface RemoteThread
    {
        /// <summary>
        /// Returns the process this thread belongs to.
        /// </summary>
        SbProcess GetProcess();

        /// <summary>
        /// Returns the thread name.
        /// </summary>
        string GetName();

        /// <summary>
        /// Returns the thread ID.
        /// </summary>
        ulong GetThreadId();

        /// <summary>
        /// Returns detailed information about the thread.
        /// </summary>
        string GetStatus();

        /// <summary>
        /// Steps into the next statement.
        /// </summary>
        void StepInto();

        /// <summary>
        /// Steps over the next statement.
        /// </summary>
        void StepOver();

        /// <summary>
        /// Steps out of the current stack frame.
        /// </summary>
        void StepOut();

        /// <summary>
        /// Steps to the next instruction.
        /// </summary>
        void StepInstruction(bool stepOver);

        /// <summary>
        /// Get the number of stack frames.
        /// </summary>
        uint GetNumFrames();

        /// <summary>
        /// Get a specific stack frame at the specified index.
        /// </summary>
        RemoteFrame GetFrameAtIndex(uint index);

        /// <summary>
        /// Returns the stop reason of the thread.
        /// </summary>
        StopReason GetStopReason();

        //--------------------------------------------------------------------------
        // Gets information associated with a stop reason.
        //
        // Breakpoint stop reasons will have data that consists of pairs of
        // breakpoint IDs followed by the breakpoint location IDs (they always come
        // in pairs). I.e. GetStopReasonDataAtIndex(2*n) is a breakpoint ID while
        // GetStopReasonDataAtIndex(2*n + 1) is the corresponding breakpoint location
        // ID.
        //
        // Stop Reason Count Data Type
        // ======================== ===== =========================================
        // eStopReasonNone 0
        // eStopReasonTrace 0
        // eStopReasonBreakpoint N duple: {breakpoint id, location id}
        // eStopReasonWatchpoint 1 watchpoint id
        // eStopReasonSignal 1 unix signal number
        // eStopReasonException N exception data
        // eStopReasonExec 0
        // eStopReasonPlanComplete 0
        //--------------------------------------------------------------------------
        ulong GetStopReasonDataAtIndex(uint index);

        /// <summary>
        /// Gets the number of words associated with the stop reason.
        /// </summary>
        uint GetStopReasonDataCount();

        /// <summary>
        /// Retrieves requested information synchronously about the frames of this thread.
        /// </summary>
        List<FrameInfoPair> GetFramesWithInfo(
            FrameInfoFlags fields, uint startIndex, uint maxCount);

        /// <summary>
        /// Retrieves requested information asynchronously about the frames of this thread.
        /// </summary>
        Task<List<FrameInfoPair>> GetFramesWithInfoAsync(
            FrameInfoFlags fields, uint startIndex, uint maxCount);
    }
}
