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
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;

namespace Google.VisualStudioFake.Internal.Jobs
{
    public class ProgramTerminatedJob : SynchronousJob
    {
        public new class Factory : SynchronousJob.Factory
        {
            public Factory(JoinableTaskContext taskContext)
                : base(taskContext) { }

            public ProgramTerminatedJob Create(IDebugEngine2 debugEngine, IDebugEvent2 evnt,
                IDebugSessionContext debugSessionContext) =>
                new ProgramTerminatedJob(taskContext, debugEngine, evnt, debugSessionContext);
        }

        readonly IDebugSessionContext _debugSessionContext;

        public ProgramTerminatedJob(JoinableTaskContext taskContext, IDebugEngine2 debugEngine,
            IDebugEvent2 evnt, IDebugSessionContext debugSessionContext)
            : base(taskContext, debugEngine, evnt)
        {
            _debugSessionContext = debugSessionContext;
        }

        protected override void RunJobTasks()
        {
            _debugSessionContext.ProgramState = ProgramState.Terminated;
            _debugSessionContext.DebugProgram = null;
        }
    }
}
