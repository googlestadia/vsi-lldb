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
using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace Google.VisualStudioFake.Internal
{
    /// <summary>
    /// Models for DebugSession
    /// </summary>
    public class DebugSessionContext : IDebugSessionContext
    {
        public ProgramState ProgramState { get; set; } = ProgramState.NotStarted;

        public IDebugEngine2 DebugEngine { get; set; }
        public IDebugProgram3 DebugProgram { get; set; }
        public IDebugThread2 SelectedThread { get; set; }
        public IDebugProcess2 Process { get; set; }

        public IDebugStackFrame2 SelectedStackFrame
        {
            get => _selectedStackFrame;
            set
            {
                if (_selectedStackFrame == value)
                {
                    return;
                }

                _selectedStackFrame = value;
                SelectedStackFrameChanged?.Invoke();
            }
        }

        IDebugStackFrame2 _selectedStackFrame;
        public event Action SelectedStackFrameChanged;

        public bool HexadecimalDisplay
        {
            get => _hexadecimalDisplay;
            set
            {
                if (_hexadecimalDisplay == value)
                {
                    return;
                }

                _hexadecimalDisplay = value;
                HexadecimalDisplayChanged?.Invoke();
            }
        }

        bool _hexadecimalDisplay;
        public event Action HexadecimalDisplayChanged;
    }
}