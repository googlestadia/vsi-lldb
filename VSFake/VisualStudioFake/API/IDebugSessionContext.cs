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

﻿using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace Google.VisualStudioFake.API
{
    /// <summary>
    /// Defines the possible states for a program being debugged.
    /// </summary>
    public enum ProgramState
    {
        NotStarted,
        LaunchSuspended,
        AtBreak,
        Running,
        Terminated,
    }

    public interface IDebugSessionContext
    {
        ProgramState ProgramState { get; set; }

        IDebugEngine2 DebugEngine { get; set; }
        IDebugProgram3 DebugProgram { get; set; }
        IDebugThread2 SelectedThread { get; set; }
        IDebugStackFrame2 SelectedStackFrame { get; set; }
        IDebugProcess2 Process { get; set; }

        /// <summary>
        /// If true, integers are displayed as hexadecimals by default instead of decimals.
        /// </summary>
        bool HexadecimalDisplay { get; set; }

        /// <summary>
        /// Notifies listeners that the selected thread has changed.
        /// </summary>
        event Action SelectedThreadChanged;

        /// <summary>
        /// Notifies listeners that the selected stack frame has changed.
        /// </summary>
        event Action SelectedStackFrameChanged;

        /// <summary>
        /// Notifies listeners that the HexadecimalDisplay flag has changed.
        /// </summary>
        event Action HexadecimalDisplayChanged;
    }
}
