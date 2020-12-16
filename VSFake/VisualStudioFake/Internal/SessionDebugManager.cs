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
using Google.VisualStudioFake.Internal.Jobs;
using System;
using System.Threading;

namespace Google.VisualStudioFake.Internal
{
    public class SessionDebugManager : ISessionDebugManager
    {
        public class ManualExecutionScope : ISDMExecutionScope
        {
            readonly SessionDebugManager _sessionDebugManager;
            readonly SDMExecutionMode _previousExecutionMode;

            public ManualExecutionScope(SessionDebugManager sessionDebugManager)
            {
                _sessionDebugManager = sessionDebugManager;

                _previousExecutionMode = sessionDebugManager.ExecutionMode;
                _sessionDebugManager.ExecutionMode = SDMExecutionMode.MANUAL;
            }

            public void Dispose()
            {
                _sessionDebugManager.ExecutionMode = _previousExecutionMode;
            }
        }

        readonly JobExecutor _jobExecutor;
        readonly IJobQueue _jobQueue;
        readonly LaunchAndAttachFlow _launchAndAttachFlow;

        public SessionDebugManager(JobExecutor jobExecutor, IJobQueue jobQueue,
                                   LaunchAndAttachFlow launchAndAttachFlow,
                                   IDebugSession debugSession)
        {
            _jobExecutor = jobExecutor;
            _jobQueue = jobQueue;
            _launchAndAttachFlow = launchAndAttachFlow;
            Session = debugSession;
        }

        public IDebugSession Session { get; }

        public SDMExecutionMode ExecutionMode { get; private set; }

        public ISDMExecutionScope StartManualMode()
        {
            return new ManualExecutionScope(this);
        }

        public void RunUntilIdle(TimeSpan timeout)
        {
            RunUntil(() => _jobQueue.Empty, timeout);
        }

        public void RunUntilBreak(TimeSpan timeout)
        {
            RunUntil(() => ProgramState == ProgramState.AtBreak, timeout);
        }

        public void RunUntil(API.ExecutionSyncPoint syncPoint, TimeSpan timeout)
        {
            RunUntil(GetPredicate(syncPoint), timeout);
        }

        public void RunUntil(Func<bool> predicate, TimeSpan timeout)
        {
            var cancellationTokenSource = new CancellationTokenSource(timeout);
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (predicate())
                {
                    break;
                }

                // TODO: Don't busy wait.
                IJob job;
                if (_jobQueue.Pop(out job))
                {
                    _jobExecutor.Execute(job);
                }
            }

            if (cancellationTokenSource.Token.IsCancellationRequested)
            {
                throw CreateTimeoutException(timeout);
            }
        }

        public void LaunchAndAttach()
        {
            if (ProgramState != ProgramState.NotStarted)
            {
                throw new InvalidOperationException(
                    $"Program has already started. (Current state: {ProgramState})");
            }

            _launchAndAttachFlow.Start();
        }

        TimeoutException CreateTimeoutException(TimeSpan timeout)
        {
            return new TimeoutException(
                $"Timeout exceeded while running jobs. ({timeout.TotalSeconds} s)");
        }

        /// <summary>
        /// Builds a predicate for an ExecutionSyncPoint that can be used with RunUntil(Func<bool>).
        /// </summary>
        Func<bool> GetPredicate(API.ExecutionSyncPoint syncPoint)
        {
            var context = Session.Context;
            return () =>
            {
                return
                    (syncPoint.HasFlag(API.ExecutionSyncPoint.ENGINE_CREATED) &&
                        context.DebugEngine != null) ||
                    (syncPoint.HasFlag(API.ExecutionSyncPoint.PROGRAM_SELECTED) &&
                        context.DebugProgram != null) ||
                    (syncPoint.HasFlag(API.ExecutionSyncPoint.THREAD_SELECTED) &&
                        context.SelectedThread != null) ||
                    (syncPoint.HasFlag(API.ExecutionSyncPoint.FRAME_SELECTED) &&
                        context.SelectedStackFrame != null) ||
                    (syncPoint.HasFlag(API.ExecutionSyncPoint.BREAK) &&
                        context.ProgramState == ProgramState.AtBreak) ||
                    (syncPoint.HasFlag(API.ExecutionSyncPoint.IDLE) && _jobQueue.Empty) ||
                    (syncPoint.HasFlag(API.ExecutionSyncPoint.PROGRAM_RUNNING) &&
                        context.ProgramState == ProgramState.Running) ||
                    (syncPoint.HasFlag(API.ExecutionSyncPoint.PROGRAM_TERMINATED) &&
                        context.ProgramState == ProgramState.Terminated);
            };
        }

        ProgramState ProgramState => Session.Context.ProgramState;
    }
}