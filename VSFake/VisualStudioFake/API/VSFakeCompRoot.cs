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

using Castle.DynamicProxy;
using Google.VisualStudioFake.API.UI;
using Google.VisualStudioFake.Internal;
using Google.VisualStudioFake.Internal.ExecutionSyncPoint;
using Google.VisualStudioFake.Internal.Interop;
using Google.VisualStudioFake.Internal.Jobs;
using Google.VisualStudioFake.Internal.UI;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using YetiCommon.CastleAspects;

namespace Google.VisualStudioFake.API
{
    /// <summary>
    /// Creates a VSFake object.
    /// </summary>
    /// <remarks>
    /// Based on design found at (internal).
    /// </remarks>
    public class VSFakeCompRoot
    {
        /// <summary>
        /// Defines how a VSFake is configured.
        /// </summary>
        public class Config
        {
            public VSFakeTimeoutSource Timeouts { get; set; } = new VSFakeTimeoutSource();
            public string SamplesRoot { get; set; }
        }

        // Batch sizes Visual Studio uses in VariableInformationEnum.Next for expanding variables.
        const int _watchWindowExpandBatchSize = 10;

        readonly Config config;
        readonly IDebugQueryTargetFactory debugQueryTargetFactory;
        readonly JoinableTaskContext taskContext;
        readonly NLog.ILogger logger;

        JobQueue jobQueue;
        IProjectAdapter projectAdapter;
        ITargetAdapter targetAdapter;
        ISessionDebugManager sessionDebugManager;
        IDebugSession debugSession;
        Decorator apiDecorator;
        IDebugSessionContext debugSessionContext;
        BreakpointView breakpointViewInternal;
        IBreakpointView breakpointView;
        JobOrchestrator jobOrchestrator;
        LaunchAndAttachFlow launchAndAttachFlow;
        SyncPointInterceptor syncPointInterceptor;

        Func<IDebugEngine2> createDebugEngine;

        public VSFakeCompRoot(Config config, IDebugQueryTargetFactory debugQueryTargetFactory,
                              JoinableTaskContext taskContext, NLog.ILogger logger)
        {
            if (config.SamplesRoot == null)
            {
                throw new ArgumentNullException(nameof(config.SamplesRoot));
            }

            this.config = config;
            this.debugQueryTargetFactory = debugQueryTargetFactory;
            this.taskContext = taskContext;
            this.logger = logger;
        }

        public IVSFake Create(Func<IDebugEngine2> createDebugEngine)
        {
            this.createDebugEngine = createDebugEngine;

            var solutionExplorer = new SolutionExplorerFake();
            GetProjectAdapter().ProjectLoaded += solutionExplorer.HandleProjectLoaded;

            GetJobOrchestrator().DebugEvent += breakpointViewInternal.HandleBindResultEvent;

            return GetAPIDecorator().Decorate<IVSFake>(
                new VSFake(GetTargetAdapter(), GetProjectAdapter(), GetSessionDebugManager(),
                           solutionExplorer, GetDebugSession(), config.Timeouts));
        }

        public virtual IJobQueue GetJobQueue()
        {
            if (jobQueue == null)
            {
                jobQueue = new JobQueue();
            }

            return jobQueue;
        }

        public virtual ISessionDebugManager GetSessionDebugManager()
        {
            if (sessionDebugManager == null)
            {
                var jobExecutor = new JobExecutor();
                sessionDebugManager = new SessionDebugManager(
                    jobExecutor, GetJobQueue(), GetLaunchAndAttachFlow(), GetDebugSession());

                GetSyncPointInterceptor().SetSessionDebugManager(sessionDebugManager);

                // Decorate after SetSessionDebugManager since the decorated sessionDebugManager
                // calls the sync point interceptor, which calls sessionDebugManager, creating an
                // infinite loop.
                sessionDebugManager = GetAPIDecorator().Decorate(sessionDebugManager);
            }

            return sessionDebugManager;
        }

        public virtual IDebugSession GetDebugSession()
        {
            if (debugSession == null)
            {
                var variableExpanderFactory =
                    new VariableExpander.Factory(GetDebugSessionContext(),
                                                 _watchWindowExpandBatchSize);

                var variableEntryFactory = new VariableEntry.Factory(GetJobQueue(),
                                                                     GetDebugSessionContext(),
                                                                     variableExpanderFactory);

                variableExpanderFactory.SetVariableEntryFactory(variableEntryFactory);

                var threadsWindow = new ThreadsWindow(GetDebugSessionContext(), GetJobQueue());

                var callStackWindow = new CallStackWindow(GetDebugSessionContext(), GetJobQueue());

                var watchWindow = new SyncWatchWindow(GetDebugSessionContext(),
                                                      variableEntryFactory);

                var controlFlowView = GetAPIDecorator().Decorate<IControlFlowView>(
                    new ControlFlowView(GetDebugSessionContext()));

                debugSession = new DebugSession(GetDebugSessionContext(), GetBreakpointView(),
                                                controlFlowView, threadsWindow, callStackWindow,
                                                watchWindow);
            }

            return debugSession;
        }

        public virtual IProjectAdapter GetProjectAdapter()
        {
            if (projectAdapter == null)
            {
                projectAdapter = new ProjectAdapter(logger, config.SamplesRoot);
            }

            return projectAdapter;
        }

        public virtual ITargetAdapter GetTargetAdapter()
        {
            if (targetAdapter == null)
            {
                var debugQueryTarget = debugQueryTargetFactory.Create();
                var debugProgramFactory = new DebugProgram.Factory();
                var defaultPortFactory = new DefaultPort.Factory();
                var processFactory = new Process.Factory();
                var portNotifyFactory = new DebugPortNotify.Factory(
                    debugProgramFactory, defaultPortFactory, processFactory);
                targetAdapter = new TargetAdapter(debugQueryTarget, portNotifyFactory,
                                                  defaultPortFactory, processFactory);
            }

            return targetAdapter;
        }

        public virtual Decorator GetAPIDecorator()
        {
            if (apiDecorator == null)
            {
                apiDecorator = new Decorator(new ProxyGenerator(), GetSyncPointInterceptor());
            }

            return apiDecorator;
        }

        public virtual IDebugSessionContext GetDebugSessionContext()
        {
            if (debugSessionContext == null)
            {
                debugSessionContext = new DebugSessionContext();
            }

            return debugSessionContext;
        }

        public virtual IBreakpointView GetBreakpointView()
        {
            if (breakpointView == null)
            {
                breakpointView = GetAPIDecorator()
                    .Decorate<IBreakpointView>(GetBreakpointViewInternal());
            }

            return breakpointView;
        }

        public virtual JobOrchestrator GetJobOrchestrator()
        {
            if (jobOrchestrator == null)
            {
                jobOrchestrator = new JobOrchestrator(GetDebugSessionContext(), GetJobQueue(),
                                                      new ProgramStoppedJob.Factory(
                                                          taskContext, GetBreakpointViewInternal(),
                                                          GetJobQueue()),
                                                      new ProgramTerminatedJob.Factory(
                                                          taskContext));
            }

            return jobOrchestrator;
        }

        LaunchAndAttachFlow GetLaunchAndAttachFlow()
        {
            if (createDebugEngine == null)
            {
                throw new InvalidOperationException($"{createDebugEngine} has not been set.");
            }

            if (launchAndAttachFlow == null)
            {
                var callback = new EventCallbackFake(GetJobOrchestrator());
                launchAndAttachFlow = new LaunchAndAttachFlow(
                    GetBreakpointViewInternal().BindPendingBreakpoints, createDebugEngine, callback,
                    GetDebugSessionContext(), GetProjectAdapter(), GetTargetAdapter(),
                    GetJobQueue(), taskContext, new ObserveAndNotifyJob.Factory(GetJobQueue()));
                GetJobOrchestrator().DebugEvent += launchAndAttachFlow.HandleDebugProgramCreated;
            }

            return launchAndAttachFlow;
        }

        SyncPointInterceptor GetSyncPointInterceptor()
        {
            if (syncPointInterceptor == null)
            {
                syncPointInterceptor = new SyncPointInterceptor(config.Timeouts);
            }

            return syncPointInterceptor;
        }

        BreakpointView GetBreakpointViewInternal()
        {
            if (breakpointViewInternal == null)
            {
                breakpointViewInternal = new BreakpointView(GetDebugSessionContext(), GetJobQueue(),
                                                            taskContext);
            }

            return breakpointViewInternal;
        }
    }
}