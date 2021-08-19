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
using Google.VisualStudioFake.API.UI;
using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace Google.VisualStudioFake.Internal.UI
{
    public class ControlFlowView : IControlFlowView
    {
        IDebugSessionContext _debugSessionContext;

        public ControlFlowView(IDebugSessionContext debugSessionContext)
        {
            _debugSessionContext = debugSessionContext;
        }

        public void Continue()
        {
            EnsureProgramIsSelected();
            EnsureThreadIsSelected();
            var curThread = _debugSessionContext.SelectedThread;
            SetRunningState();
            // TODO: Figure out if we need to unwrap interop values passed back
            // to the debug engine.
            HResultChecker.Check(_debugSessionContext.DebugProgram.ExecuteOnThread(curThread));
        }

        public void Pause()
        {
            EnsureProgramIsSelected();
            HResultChecker.Check(_debugSessionContext.DebugProgram.CauseBreak());
        }

        public void StepInto()
        {
            Step(enum_STEPKIND.STEP_INTO, enum_STEPUNIT.STEP_STATEMENT);
        }

        public void StepOver()
        {
            Step(enum_STEPKIND.STEP_OVER, enum_STEPUNIT.STEP_STATEMENT);
        }

        public void StepOut()
        {
            Step(enum_STEPKIND.STEP_OUT, enum_STEPUNIT.STEP_STATEMENT);
        }

        public void Stop()
        {
            EnsureProgramIsSelected();
            HResultChecker.Check(_debugSessionContext.DebugProgram.Terminate());
        }

        void Step(enum_STEPKIND stepKind, enum_STEPUNIT stepUnit)
        {
            EnsureProgramIsSelected();
            EnsureThreadIsSelected();
            var curThread = _debugSessionContext.SelectedThread;
            SetRunningState();
            // TODO: Figure out if we need to unwrap interop values passed back
            // to the debug engine.
            HResultChecker.Check(
                _debugSessionContext.DebugProgram.Step(curThread, stepKind, stepUnit));
        }

        void EnsureProgramIsSelected()
        {
            if (_debugSessionContext.DebugProgram == null)
            {
                throw new InvalidOperationException("There is no program selected; " +
                    $"{nameof(_debugSessionContext.DebugProgram)} is null. " +
                    $"Program state = {_debugSessionContext.ProgramState}.");
            }
        }

        void EnsureThreadIsSelected()
        {
            if (_debugSessionContext.SelectedThread == null)
            {
                throw new InvalidOperationException("There is no thread selected; " +
                    $"{nameof(_debugSessionContext.SelectedThread)} is null. " +
                    $"Program state = {_debugSessionContext.ProgramState}.");
            }
        }

        void SetRunningState()
        {
            // We don't set DebugProgram to null because it is needed by ::Pause().
            _debugSessionContext.SelectedThread = null;
            _debugSessionContext.SelectedStackFrame = null;
            _debugSessionContext.ProgramState = ProgramState.Running;
        }
    }
}
