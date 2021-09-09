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
using Google.VisualStudioFake.API.UI;
using Google.VisualStudioFake.Util;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Google.VisualStudioFake.Internal.Jobs
{
    public class LaunchAndAttachFlow
    {
        /// <summary>
        /// This delegate should try bind pending breakpoints and return references to them.
        /// </summary>
        public delegate IEnumerable<IBreakpoint> BindPendingBreakpointsHandler();

        readonly BindPendingBreakpointsHandler _bindPendingBreakpoints;
        readonly Func<IDebugEngine2> _createDebugEngine;
        readonly IDebugEventCallback2 _debugEventCallback;
        readonly IDebugSessionContext _debugSessionContext;
        readonly IProjectAdapter _projectAdapter;
        readonly ITargetAdapter _targetAdapter;
        readonly IJobQueue _jobQueue;
        readonly JoinableTaskContext _taskContext;
        readonly ObserveAndNotifyJob.Factory _observeAndNotifyJobFactory;

        public LaunchAndAttachFlow(BindPendingBreakpointsHandler bindPendingBreakpoints,
                                   Func<IDebugEngine2> createDebugEngine,
                                   IDebugEventCallback2 debugEventCallback,
                                   IDebugSessionContext debugSessionContext,
                                   IProjectAdapter projectAdapter, ITargetAdapter targetAdapter,
                                   IJobQueue jobQueue, JoinableTaskContext taskContext,
                                   ObserveAndNotifyJob.Factory observeAndNotifyJobFactory)
        {
            _bindPendingBreakpoints = bindPendingBreakpoints;
            _createDebugEngine = createDebugEngine;
            _debugEventCallback = debugEventCallback;
            _debugSessionContext = debugSessionContext;
            _projectAdapter = projectAdapter;
            _targetAdapter = targetAdapter;
            _jobQueue = jobQueue;
            _taskContext = taskContext;
            _observeAndNotifyJobFactory = observeAndNotifyJobFactory;
        }

        public void Start() => _jobQueue.Push(new GenericJob(LaunchAndAttach));

        public void StartSuspended() =>
            _jobQueue.Push(new GenericJob(LaunchSuspended));

        public void HandleDebugProgramCreated(DebugEventArgs args)
        {
            if (!(args.Event is IDebugProgramCreateEvent2))
            {
                return;
            }

            HandleDebugProgramCreated(args.Event, args.Program);
        }

        void LaunchSuspended() => Launch(false);

        void LaunchAndAttach() => Launch(true);

        void Launch(bool resume)
        {
            IDebugEngine2 debugEngine = null;
            _taskContext.RunOnMainThread(() => debugEngine = _createDebugEngine());
            _debugSessionContext.DebugEngine = debugEngine;
            _debugSessionContext.ProgramState = ProgramState.NotStarted;

            IDebugEngineLaunch2 debugEngineLauncher = (IDebugEngineLaunch2) debugEngine;

            // TODO: Use the correct DebugLaunchOptions value.
            var launchSettings =
                _targetAdapter.QueryDebugTargets(_projectAdapter.Project, 0).First();

            var port = _targetAdapter.InitializePort(_debugEventCallback, debugEngine);
            HResultChecker.Check(debugEngineLauncher.LaunchSuspended("", // server
                                                                     port,
                                                                     launchSettings.Executable,
                                                                     launchSettings.Arguments,
                                                                     "", // dir,
                                                                     "", // env
                                                                     launchSettings.Options,
                                                                     default(enum_LAUNCH_FLAGS),
                                                                     0, // stdinput,
                                                                     0, // stdoutput,
                                                                     0, // stderr,
                                                                     _debugEventCallback,
                                                                     out IDebugProcess2 process));
            _debugSessionContext.Process = process;
            _debugSessionContext.ProgramState = ProgramState.LaunchSuspended;
            if (resume)
            {
                HResultChecker.Check(debugEngineLauncher.ResumeProcess(process));

                // Note: This normally returns a failure since not all symbols can be loaded, but
                // it is ignored by Visual Studio.
                ((IDebugEngine3)debugEngine).LoadSymbols();
            }
        }

        void HandleDebugProgramCreated(IDebugEvent2 programCreatedEvent, IDebugProgram3 program)
        {
            var breakpoints = _bindPendingBreakpoints();
            Func<bool> predicate = () => breakpoints.All(b => b.Ready);
            _jobQueue.Push(_observeAndNotifyJobFactory.Create(
                               predicate, () => SetProgramAndContinue(programCreatedEvent, program),
                               "Waiting for breakpoints to be bound."));
        }

        void SetProgramAndContinue(IDebugEvent2 programCreatedEvent, IDebugProgram3 program)
        {
            _debugSessionContext.DebugProgram = program;
            _taskContext.RunOnMainThread(
                () => _debugSessionContext.DebugEngine.ContinueFromSynchronousEvent(
                    programCreatedEvent));
            _debugSessionContext.ProgramState = ProgramState.Running;
        }
    }
}