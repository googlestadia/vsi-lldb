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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.Logging;
using YetiVSI.Attributes;
using YetiVSI.CoreAttach;
using YetiVSI.DebugEngine;
using YetiVSI.DebuggerOptions;
using YetiVSI.LLDBShell;
using YetiVSI.LoadSymbols;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using static YetiVSI.DebuggerOptions.DebuggerOptions;
using Task = System.Threading.Tasks.Task;

namespace YetiVSI
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.71.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(_packageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules",
                     "SA1650:ElementDocumentationMustBeSpelledCorrectly",
                     Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
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
    // TODO: Migrate to AsyncPackage
    public sealed class YetiVSIPackage : AsyncPackage
    {
        const string _packageGuidString = "5fc8481d-4b1a-4cdc-b123-fd6d32fc4096";
        JoinableTaskContext _taskContext;

        /// <summary>
        /// Initialization of the package; this method is called right after the
        /// package is sited, so this is the place where you can put all the
        /// initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override async Task InitializeAsync(
           CancellationToken cancellationToken,
           IProgress<ServiceProgressData> progress)
        {
            var serviceManager = new ServiceManager();
            _taskContext = serviceManager.GetJoinableTaskContext();
            AddServices();

            await InitializeAsync(_taskContext);
        }

        async Task InitializeAsync(JoinableTaskContext taskContext)
        {
            YetiLog.Initialize("YetiVSI", DateTime.Now);

            await taskContext.Factory.SwitchToMainThreadAsync();

            CoreAttachCommand.Register(this);
            LLDBShellCommandTarget.Register(taskContext, this);
            DebuggerOptionsCommand.Register(taskContext, this);
        }

        void AddServices()
        {
            AddService(typeof(YetiVSIService), CreateYetiVsiServiceAsync, true);
            AddService(typeof(SLLDBShell), CreateSLLDBShellAsync, true);
            AddService(typeof(SMetrics), CreateSMetricsAsync, true);
            AddService(typeof(SDebugEngineManager), CreateSDebugEngineManagerAsync, true);
            AddService(typeof(SSessionNotifier), CreateSSessionNotifierAsync, true);
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
            await _taskContext.Factory.SwitchToMainThreadAsync();
            var vsCommandWindow = (IVsCommandWindow)
                await GetServiceAsync(typeof(SVsCommandWindow));
            var commandWindowWriter = new CommandWindowWriter(_taskContext, vsCommandWindow);
            return new LLDBShell.LLDBShell(_taskContext, commandWindowWriter);
        }

        async Task<object> CreateSMetricsAsync(IAsyncServiceContainer container,
                                               CancellationToken cancellationToken,
                                               Type serviceType)
        {
            await _taskContext.Factory.SwitchToMainThreadAsync();
            var dte = await GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE._DTE;
            return new MetricsService(_taskContext,
                                      Versions.Populate(dte?.RegistryRoot));
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
            ISessionNotifier sessionNotifier = new SessionNotifierService();
            var vsiService = (YetiVSIService)GetGlobalService(typeof(YetiVSIService));
            await _taskContext.Factory.SwitchToMainThreadAsync();
            var metricsService = (IMetrics)await GetServiceAsync(typeof(SMetrics));
            var exceptionRecorder = new ExceptionRecorder(metricsService);
            var loadSymbolsCommand = new LoadSymbolsCommand(
                _taskContext, this, exceptionRecorder, vsiService);
            sessionNotifier.SessionLaunched += loadSymbolsCommand.OnSessionLaunched;
            sessionNotifier.SessionStopped += loadSymbolsCommand.OnSessionStopped;

            var noSourceWindowHider = new NoSourceWindowHider(_taskContext, this,
                                                              exceptionRecorder, vsiService);
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