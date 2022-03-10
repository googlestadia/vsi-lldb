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

using System.Collections.Generic;
using DebuggerGrpcClient;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Grpc.Core.Interceptors;
using Microsoft.VisualStudio.Debugger.Interop;
using TestsCommon.TestSupport;
using YetiCommon;
using YetiCommon.CastleAspects;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;
using YetiVSI.Test.TestSupport.DebugEngine.NatvisEngine;
using YetiVSI.Util;
using YetiVSITestsCommon;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiVSI.Test.DebugEngine
{
    // TODO: review what else might be mocked and clean code after the tests are ready
    /// <summary>
    /// <see cref="DebugEngineFactoryCompRoot"/>'s version with all methods, used
    /// in DebugEngineTests, substituted with mocks.
    /// </summary>
    public class DebugEngineFactoryCompRootStub : DebugEngineFactoryCompRoot
    {
        JoinableTaskContext _taskContext;

        IVariableNameTransformer _variableNameTransformer;

        IFileSystem _fileSystem;

        YetiVSIService _vsiService;

        IWindowsRegistry _windowsRegistry;

        public ServiceManager ServiceManager { get; set; }

        public override ServiceManager CreateServiceManager() => ServiceManager;

        readonly IStadiaLldbDebuggerFactory _stadiaLldbDebuggerFactory;
        readonly IDebugSessionLauncherFactory _debugSessionLauncherFactory;
        readonly IRemoteDeploy _remoteDeploy;
        readonly IGameLauncher _gameLauncher;

        ISymbolSettingsProvider _symbolSettingsProvider;

        /// <summary>
        /// Generate light-weight version of <see cref="DebugEngineFactoryCompRoot"/>
        /// with most methods and properties mocked for testing.
        /// </summary>
        /// <param name="stadiaLldbDebuggerFactory"><see cref="IStadiaLldbDebuggerFactory"/>
        /// instance.</param>
        /// <param name="factory"><see cref="IDebugSessionLauncherFactory"/> instance to use
        /// instead of created one.
        /// </param>
        /// <param name="remoteDeploy"><see cref="IRemoteDeploy"/> instance to use during the
        /// deployment process.</param>
        /// <param name="gameLauncher">Game launcher.</param>
        public DebugEngineFactoryCompRootStub(IStadiaLldbDebuggerFactory stadiaLldbDebuggerFactory,
                                              IDebugSessionLauncherFactory factory,
                                              IRemoteDeploy remoteDeploy,
                                              IGameLauncher gameLauncher)
        {
            _stadiaLldbDebuggerFactory = stadiaLldbDebuggerFactory;
            _debugSessionLauncherFactory = factory;
            _remoteDeploy = remoteDeploy;
            _gameLauncher = gameLauncher;
        }

        readonly IDialogUtil _dialogUtil = Substitute.For<IDialogUtil>();
        public override IDialogUtil GetDialogUtil() => _dialogUtil;

        public override IDebugEngineFactory CreateDebugEngineFactory()
        {
            ServiceManager serviceManager = CreateServiceManager();
            JoinableTaskContext joinableTaskContext = GetJoinableTaskContext();
            YetiVSIService vsiService = GetVsiService();
            joinableTaskContext.ThrowIfNotOnMainThread();

            var debugEngineCommands = new DebugEngineCommands(joinableTaskContext, null, false);

            var actionRecorder = new ActionRecorder(GetDebugSessionMetrics());
            var backgroundProcessFactory = new BackgroundProcess.Factory();

            var processFactory = new ManagedProcess.Factory();
            var moduleFileFinder = Substitute.For<IModuleFileFinder>();
            var moduleFileLoadRecorderFactory =
                new ModuleFileLoadMetricsRecorder.Factory(moduleFileFinder);
            List<Interceptor> grpcInterceptors = CreateGrpcInterceptors(vsiService.DebuggerOptions);
            var vsOutputWindow =
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
                serviceManager.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
            var callInvokerFactory = new PipeCallInvokerFactory();
            var yetiTransport = new YetiDebugTransport(
                joinableTaskContext, callInvokerFactory,
                new GrpcConnectionFactory(joinableTaskContext.Factory, grpcInterceptors.ToArray()),
                GetTaskExecutor().CancelAsyncOperationIfRequested, processFactory, _dialogUtil,
                vsOutputWindow, vsiService);

            var chromeLauncher = new ChromeLauncher(backgroundProcessFactory);
            var testClientLauncherFactory = new ChromeClientsLauncher.Factory(
                new ChromeClientLaunchCommandFormatter(), GetSdkConfigFactory(), chromeLauncher);

            var exitDialogUtil = new ExitDialogUtil(_dialogUtil, GetDialogExecutionContext());
            var preflightBinaryChecker =
                new PreflightBinaryChecker(GetFileSystem(), new ModuleParser());

            var cancelableTaskFactory = GetCancelableTaskFactory();
            bool deployLldbServer = true;
            IDebugEngineFactory factory = new YetiVSI.DebugEngine.DebugEngine.Factory(
                joinableTaskContext, serviceManager, GetDebugSessionMetrics(),
                _stadiaLldbDebuggerFactory, yetiTransport, actionRecorder, null,
                moduleFileLoadRecorderFactory, moduleFileFinder, testClientLauncherFactory,
                GetNatvis(), GetNatvisDiagnosticLogger(), exitDialogUtil, preflightBinaryChecker,
                _debugSessionLauncherFactory, _remoteDeploy, cancelableTaskFactory, _dialogUtil,
                _vsiService, GetNatvisLoggerOutputWindowListener(), GetSolutionExplorer(),
                debugEngineCommands, GetDebugEventCallbackDecorator(vsiService.DebuggerOptions),
                GetSymbolSettingsProvider(), deployLldbServer, _gameLauncher,
                GetDebugEventRecorder(), GetExpressionEvaluationRecorder(),
                GetProfilerSshTunnelManager(processFactory));
            return GetFactoryDecorator().Decorate(factory);
        }

        public override IFileSystem GetFileSystem() =>
            _fileSystem ?? (_fileSystem = new MockFileSystem());

        public override IWindowsRegistry GetWindowsRegistry() => _windowsRegistry ??
            (_windowsRegistry = TestDummyGenerator.Create<IWindowsRegistry>());

        public override YetiVSIService GetVsiService()
        {
            if (_vsiService == null)
            {
                var serviceOptions = OptionPageGrid.CreateForTesting();
                serviceOptions.NatvisLoggingLevel = NatvisLoggingLevelFeatureFlag.VERBOSE;

                _vsiService = new YetiVSIService(serviceOptions);
            }

            return _vsiService;
        }

        ISolutionExplorer _solutionExplorer;

        public override ISolutionExplorer GetSolutionExplorer()
        {
            if (_solutionExplorer != null)
            {
                return _solutionExplorer;
            }

            var taskContext = GetJoinableTaskContext();
            var dte2 = Substitute.For<DTE2>();
            var dte2Util = new EnvDteUtil(taskContext, dte2);
            _solutionExplorer = new SolutionExplorer(taskContext, null, dte2Util);

            return _solutionExplorer;
        }

        CancelableTask.Factory _cancelableTaskFactory;

        public override CancelableTask.Factory GetCancelableTaskFactory()
        {
            if (_cancelableTaskFactory == null)
            {
                _cancelableTaskFactory =
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
                    FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false);
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
            }

            return _cancelableTaskFactory;
        }

        public override JoinableTaskContext GetJoinableTaskContext() => _taskContext
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            ?? (_taskContext = new JoinableTaskContext());
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

        public override DialogExecutionContext GetDialogExecutionContext() => null;

        public override NatvisLoggerOutputWindowListener GetNatvisLoggerOutputWindowListener() =>
            null;

        public override IVariableNameTransformer GetVariableNameTransformer() =>
            _variableNameTransformer ??
            (_variableNameTransformer = new TestNatvisVariableNameTransformer());

        public override ISymbolSettingsProvider GetSymbolSettingsProvider()
        {
            if (_symbolSettingsProvider != null)
            {
                return _symbolSettingsProvider;
            }

            JoinableTaskContext taskContext = GetJoinableTaskContext();
            var symbolSettingsManager = Substitute.For<IVsDebuggerSymbolSettingsManager120A>();
            var debuggerService = Substitute.For<IVsDebugger2>();
            bool symbolServerEnabled = GetVsiService().Options.SymbolServerSupport ==
                SymbolServerSupport.ENABLED;
            _symbolSettingsProvider = new SymbolSettingsProvider(
                symbolSettingsManager, debuggerService, symbolServerEnabled, taskContext);
            return _symbolSettingsProvider;
        }
    }
}