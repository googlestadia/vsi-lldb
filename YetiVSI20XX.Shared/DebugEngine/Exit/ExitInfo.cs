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

﻿using System;

namespace YetiVSI.DebugEngine.Exit
{
    /// <summary>
    /// Represents the reason for a normal program exit.
    /// </summary>
    public enum ExitReason
    {
        /// <summary>
        /// Default value to use in placeholder ExitInfo.Normal values.
        /// </summary>
        Unknown,

        /// <summary>
        /// Process being debugged has exited by itself.
        /// </summary>
        ProcessExited,

        /// <summary>
        /// DebugProgram.Terminate() was used to exit the process.
        /// </summary>
        DebuggerTerminated,

        /// <summary>
        /// Process has detached by itself - perhaps by direct command to LLDB.
        /// </summary>
        ProcessDetached,

        /// <summary>
        /// DebugProgram.Detach() was used to detach the process.
        /// </summary>
        DebuggerDetached,

        /// <summary>
        /// Attaching to the process was canceled by the user.
        /// </summary>
        AttachCanceled
    }

    /// <summary>
    /// ExitInfo captures relevant information when the debugger exits.
    /// </summary>
    public class ExitInfo
    {
        public ExitReason ExitReason { get; }

        public Exception ExitException { get; }

        ExitInfo(ExitReason exitReason)
        {
            ExitReason = exitReason;
        }

        ExitInfo(Exception exitException)
        {
            ExitException = exitException;
        }

        /// <summary>
        /// Calls |action| with the exception that caused an exit if there is an exception.
        /// </summary>
        public void IfError(Action<Exception> action)
        {
            if (ExitException != null)
            {
                action(ExitException);
            }
        }

        /// <summary>
        /// Calls |onError| with the exception that caused an exit if there is an exception.
        /// Otherwise calls |onNormal| with the exit reason.
        /// </summary>
        public void HandleResult(Action<ExitReason> onNormal, Action<Exception> onError)
        {
            if (ExitException != null)
            {
                onError(ExitException);
            }
            else
            {
                onNormal(ExitReason);
            }
        }

        /// <summary>
        /// Create an exit info instance representing the "normal" exit case. This is the case when
        /// the debugger exits without error.
        /// </summary>
        public static ExitInfo Normal(ExitReason er) => new ExitInfo(er);

        /// <summary>
        /// Create an exit info instance representing exiting in an error case.
        /// </summary>
        public static ExitInfo Error(Exception ex) => new ExitInfo(ex);
    }
}
