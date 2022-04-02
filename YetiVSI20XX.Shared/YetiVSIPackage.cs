// Copyright 2021 Google LLC
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

using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Metrics.Shared;
using YetiCommon;
using YetiCommon.Logging;
using YetiVSI.Attributes;
using YetiVSI.CoreAttach;
using YetiVSI.DebugEngine;
using YetiVSI.DebuggerOptions;
using YetiVSI.LLDBShell;
using YetiVSI.LoadSymbols;
using Metrics;
using YetiVSI.Profiling;
using static YetiVSI.DebuggerOptions.DebuggerOptions;
using Task = System.Threading.Tasks.Task;
using YetiVSI.Metrics;

namespace YetiVSI
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.78.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid("5fc8481d-4b1a-4cdc-b123-fd6d32fc4096")]
    [ProvideOptionPage(typeof(OptionPageGrid), "Stadia SDK", "General", 0, 0, true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideProfile(typeof(OptionPageGrid), "Stadia SDK", "General", 0, 110, true,
                    DescriptionResourceID = 113)]
    [ProvideService(typeof(YetiVSIService))]
    [ProvideService(typeof(SLLDBShell))]
    [ProvideService(typeof(SMetrics))]
    [ProvideService(typeof(SDebugEngineManager))]
    [ProvideService(typeof(SSessionNotifier))]
    [ProvideStadiaExceptions()]

    // This is boilerplate code needed to add a new entry to the Tools menu.
    public sealed class YetiVSIPackage : AsyncPackage
    {
        DTEEvents _dteEvents;
        VsiMetricsService _metricsService;

        /// <summary>
        /// Initialization of the package; this method is called right after the
        /// package is sited, so this is the place where you can put all the
        /// initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override async Task InitializeAsync(CancellationToken cancellationToken,
                                                      IProgress<ServiceProgressData> progress)
        {
            // First register the services this package provides.
            AddService(typeof(YetiVSIService), CreateYetiVsiServiceAsync, true);
            AddService(typeof(SLLDBShell), CreateSLLDBShellAsync, true);
            AddService(typeof(SMetrics), CreateSMetricsAsync, true);
            AddService(typeof(SDebugEngineManager), CreateSDebugEngineManagerAsync, true);
            AddService(typeof(SSessionNotifier), CreateSSessionNotifierAsync, true);

            // MetricsService was just registered above, so it should be present.
            _metricsService = (VsiMetricsService)await GetServiceAsync(typeof(SMetrics));
            Assumes.Present(_metricsService);
            _metricsService.RecordEvent(DeveloperEventType.Types.Type.VsiInitialized,
                                        new DeveloperLogEvent());

            YetiLog.Initialize("YetiVSI", DateTime.Now);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte2 = (DTE2)await GetServiceAsync(typeof(DTE));
            Assumes.Present(dte2);
            _dteEvents = dte2.Events.DTEEvents;
            // Microsoft documentation states that `OnBeginShutdown` is for internal use only.
            // However there are multiple opensource projects which use this api.
            // The other closest thing we could use is `QueryClose` or `CanClose` methods
            // from AsyncPackage. But those methods are executed each time visual studio
            // attempts to close, which can happen multiple times during visual studio session.
            _dteEvents.OnBeginShutdown += () =>
            {
                _metricsService.RecordEvent(
                    DeveloperEventType.Types.Type.VsiShutdown, new DeveloperLogEvent());
            };

            var dialogUtil = new DialogUtil();
            CoreAttachCommand.Register(this);
            LaunchWithProfilerCommand.Register(this, ProfilerType.Orbit,
                                               "Orbit CPU Profiler", dialogUtil);
            LaunchWithProfilerCommand.Register(this, ProfilerType.Dive,
                                               "Dive GPU Profiler", dialogUtil);
            LLDBShellCommandTarget.Register(ThreadHelper.JoinableTaskContext, this);
            DebuggerOptionsCommand.Register(ThreadHelper.JoinableTaskContext, this);

            await ReportBug.InitializeAsync(this);
        }

        Task<object> CreateYetiVsiServiceAsync(IAsyncServiceContainer container,
                                               CancellationToken cancellationToken,
                                               Type serviceType)
        {
            var options = GetDialogPage(typeof(OptionPageGrid)) as OptionPageGrid;
            var yeti = new YetiVSIService(options);
            yeti.DebuggerOptions.ValueChanged += OnDebuggerOptionChanged;
            return Task.FromResult<object>(yeti);
        }

        async Task<object> CreateSLLDBShellAsync(IAsyncServiceContainer container,
                                                 CancellationToken cancellationToken,
                                                 Type serviceType)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var vsCommandWindow =
                (IVsCommandWindow)await GetServiceAsync(typeof(SVsCommandWindow));
            var commandWindowWriter = new CommandWindowWriter(
                ThreadHelper.JoinableTaskContext, vsCommandWindow);
            return new LLDBShell.LLDBShell(
                ThreadHelper.JoinableTaskContext, commandWindowWriter);
        }

        async Task<object> CreateSMetricsAsync(IAsyncServiceContainer container,
                                               CancellationToken cancellationToken,
                                               Type serviceType)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            string vsVersion = await VsVersion.GetVisualStudioVersionAsync(this);
            return new VsiMetricsService(
                ThreadHelper.JoinableTaskContext, Versions.Populate(vsVersion));
        }

        Task<object> CreateSDebugEngineManagerAsync(IAsyncServiceContainer container,
                                                    CancellationToken cancellationToken,
                                                    Type serviceType)
        {
            var debugEngineManager = new DebugEngineManager();
            return Task.FromResult<object>(debugEngineManager);
        }

        async Task<object> CreateSSessionNotifierAsync(IAsyncServiceContainer container,
                                                       CancellationToken cancellationToken,
                                                       Type serviceType)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var vsiService = (YetiVSIService)await GetServiceAsync(typeof(YetiVSIService));
            var metricsService = (IVsiMetrics)await GetServiceAsync(typeof(SMetrics));
            var exceptionRecorder = new ExceptionRecorder(metricsService);
            var loadSymbolsCommand = new LoadSymbolsCommand(
                ThreadHelper.JoinableTaskContext, this, exceptionRecorder, vsiService);

            ISessionNotifier sessionNotifier = new SessionNotifierService();
            sessionNotifier.SessionLaunched += loadSymbolsCommand.OnSessionLaunched;
            sessionNotifier.SessionStopped += loadSymbolsCommand.OnSessionStopped;

            var noSourceWindowHider = new NoSourceWindowHider(
                ThreadHelper.JoinableTaskContext, this, exceptionRecorder, vsiService);
            sessionNotifier.SessionLaunched += noSourceWindowHider.OnSessionLaunched;
            sessionNotifier.SessionStopped += noSourceWindowHider.OnSessionStopped;

            return sessionNotifier;
        }

        void OnDebuggerOptionChanged(object sender, ValueChangedEventArgs args)
        {
            if (args.Option == DebuggerOption.GRPC_LOGGING)
            {
                YetiLog.ToggleGrpcLogging(args.State == DebuggerOptionState.ENABLED);
            }
        }
    }
} // namespace YetiVSI