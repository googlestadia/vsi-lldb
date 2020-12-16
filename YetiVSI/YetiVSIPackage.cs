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

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
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
using YetiVSI.Util;
using static YetiVSI.DebuggerOptions.DebuggerOptions;

namespace YetiVSI
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules",
                     "SA1650:ElementDocumentationMustBeSpelledCorrectly",
                     Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideOptionPage(typeof(OptionPageGrid), "Stadia SDK", "General", 0, 0, true)]
    [ProvideService(typeof(YetiVSIService))]
    [ProvideService(typeof(SLLDBShell))]
    [ProvideService(typeof(SMetrics))]
    [ProvideService(typeof(SDebugEngineManager))]
    [ProvideService(typeof(SSessionNotifier))]
    [ProvideStadiaExceptions()]

    // This is boilerplate code needed to add a new entry to the Tools menu.
    // TODO: Migrate to AsyncPackage
    public sealed class YetiVSIPackage : Package
    {
        public const string PackageGuidString = "5fc8481d-4b1a-4cdc-b123-fd6d32fc4096";

        JoinableTaskContext _taskContext;

        public YetiVSIPackage()
        {
            ServiceCreatorCallback callback = CreateService;
            (this as IServiceContainer).AddService(typeof(YetiVSIService), callback, true);
            (this as IServiceContainer).AddService(typeof(SLLDBShell), callback, true);
            (this as IServiceContainer).AddService(typeof(SMetrics), callback, true);
            (this as IServiceContainer).AddService(typeof(SDebugEngineManager), callback, true);
            (this as IServiceContainer).AddService(typeof(SSessionNotifier), callback, true);
        }

        #region Test Helpers

        // Test helper to make Initialize() accessible from tests.
        // Clients should make sure to call Uninitialize() during tear down.
        public void InitializeForTest(JoinableTaskContext taskContext)
        {
            taskContext.ThrowIfNotOnMainThread();

            Initialize(taskContext);
        }

        // Test helper to make Initialize() accessible from tests.
        public void UninitializeForTest()
        {
            YetiLog.Uninitialize();
        }

        #endregion

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the
        /// package is sited, so this is the place where you can put all the
        /// initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            var serviceManager = new ServiceManager();
            var taskContext = serviceManager.GetJoinableTaskContext();
            taskContext.ThrowIfNotOnMainThread();

            Initialize(taskContext);
        }

        #endregion

        void Initialize(JoinableTaskContext taskContext)
        {
            _taskContext = taskContext;
            taskContext.ThrowIfNotOnMainThread();

            base.Initialize();
            YetiLog.Initialize("YetiVSI", DateTime.Now);

            CoreAttachCommand.Register(this);
            LLDBShellCommandTarget.Register(taskContext, this);
            DebuggerOptionsCommand.Register(taskContext, this);
        }

        object CreateService(IServiceContainer container, Type serviceType)
        {
            _taskContext.ThrowIfNotOnMainThread();

            if (typeof(YetiVSIService) == serviceType)
            {
                var options = GetDialogPage(typeof(OptionPageGrid)) as OptionPageGrid;
                var yeti = new YetiVSIService(options);
                yeti.DebuggerOptions.ValueChanged += OnDebuggerOptionChanged;
                return yeti;
            }

            if (typeof(SLLDBShell) == serviceType)
            {
                var writer = new CommandWindowWriter(_taskContext,
                                                     (IVsCommandWindow) GetService(
                                                         typeof(SVsCommandWindow)));
                return new LLDBShell.LLDBShell(_taskContext, writer);
            }

            if (typeof(SMetrics) == serviceType)
            {
                return new MetricsService(_taskContext,
                                          Versions.Populate(
                                              (GetService(typeof(EnvDTE.DTE)) as EnvDTE._DTE)
                                              ?.RegistryRoot));
            }

            if (typeof(SDebugEngineManager) == serviceType)
            {
                return new DebugEngineManager();
            }

            if (typeof(SSessionNotifier) == serviceType)
            {
                ISessionNotifier sessionNotifier = new SessionNotifierService();
                var vsiService = (YetiVSIService) GetGlobalService(typeof(YetiVSIService));
                var exceptionRecorder =
                    new ExceptionRecorder((IMetrics) GetService(typeof(SMetrics)));
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

            return null;
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