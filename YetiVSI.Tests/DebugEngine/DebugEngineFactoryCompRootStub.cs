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

using DebuggerGrpcClient;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.VisualStudio.Debugger.Interop;
using TestsCommon.TestSupport;
using YetiCommon;
using YetiCommon.CastleAspects;
using YetiCommon.VSProject;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;
using YetiVSI.Test.TestSupport.DebugEngine.NatvisEngine;
using YetiVSI.Util;
using YetiVSITestsCommon;

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

        IWindowsRegistry _windowsRegistry { get; set; }

        public ServiceManager ServiceManager { get; set; }

        public override ServiceManager CreateServiceManager() => ServiceManager;

        readonly IDebugSessionLauncherFactory _debugSessionLauncherFactory;
        readonly IRemoteDeploy _remoteDeploy;
        readonly IGameLaunchManager _gameLaunchManager;

        ISymbolSettingsProvider _symbolSettingsProvider;

        /// <summary>
        /// Generate light-weight version of <see cref="DebugEngineFactoryCompRoot"/>
        /// with most methods and properties mocked for testing.
        /// </summary>
        /// <param name="factory"><see cref="IDebugSessionLauncherFactory"/> instance to use
        /// instead of created one.
        /// </param>
        /// <param name="remoteDeploy"><see cref="IRemoteDeploy"/> instance to use during the
        /// deployment process.</param>
        /// <param name="gameLaunchManager">
        /// <see cref="IGameLaunchManager"/> instance to use during game launch.
        /// </param>
        public DebugEngineFactoryCompRootStub(IDebugSessionLauncherFactory factory,
                                              IRemoteDeploy remoteDeploy,
                                              IGameLaunchManager gameLaunchManager)
        {
            _debugSessionLauncherFactory = factory;
            _remoteDeploy = remoteDeploy;
            _gameLaunchManager = gameLaunchManager;
        }

        readonly IDialogUtil _dialogUtil = Substitute.For<IDialogUtil>();
        public override IDialogUtil GetDialogUtil() => _dialogUtil;

        public override IDebugEngineFactory CreateDebugEngineFactory()
        {
            ServiceManager serviceManager = CreateServiceManager();
            var joinableTaskContext = GetJoinableTaskContext();
            var vsiService = GetVsiService();
            joinableTaskContext.ThrowIfNotOnMainThread();

            var debugEngineCommands =
                new DebugEngineCommands(joinableTaskContext, null, false);

            var actionRecorder = new ActionRecorder(GetDebugSessionMetrics());
            var backgroundProcessFactory = new BackgroundProcess.Factory();

            var processFactory = new ManagedProcess.Factory();
            var binaryFileUtil = new ElfFileUtil(joinableTaskContext.Factory, processFactory);
            var lldbModuleUtil = new LldbModuleUtil();

            IModuleFileFinder moduleFileFinder = Substitute.For<IModuleFileFinder>();
            var moduleFileLoadRecorderFactory =
                new ModuleFileLoadMetricsRecorder.Factory(lldbModuleUtil, moduleFileFinder);
            var grpcInterceptors = CreateGrpcInterceptors(vsiService.DebuggerOptions);
            var vsOutputWindow =
                serviceManager.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            var callInvokerFactory = new PipeCallInvokerFactory();
            var transportSessionFactory =
                new LldbTransportSession.Factory(new MemoryMappedFileFactory());
            var yetiTransport = new YetiDebugTransport(
                joinableTaskContext, transportSessionFactory, callInvokerFactory,
                new GrpcConnectionFactory(joinableTaskContext.Factory, grpcInterceptors.ToArray()),
                GetTaskExecutor().CancelAsyncOperationIfRequested, processFactory, _dialogUtil,
                vsOutputWindow, vsiService);

            var chromeLauncher = new ChromeLauncher(backgroundProcessFactory);
            var testClientLauncherFactory = new ChromeTestClientLauncher.Factory(
                new ChromeClientLaunchCommandFormatter(GetJsonUtil()), GetSdkConfigFactory(),
                chromeLauncher);

            var exitDialogUtil = new ExitDialogUtil(_dialogUtil, GetDialogExecutionContext());
            var preflightBinaryChecker =
                new PreflightBinaryChecker(GetFileSystem(), binaryFileUtil);

            var paramsFactory = new YetiVSI.DebugEngine.DebugEngine.Params.Factory(GetJsonUtil());

            var cancelableTaskFactory = GetCancelableTaskFactory();
            bool deployLldbServer = true;
            IDebugEngineFactory factory = new YetiVSI.DebugEngine.DebugEngine.Factory(
                joinableTaskContext, serviceManager, GetDebugSessionMetrics(), yetiTransport,
                actionRecorder, null, moduleFileLoadRecorderFactory, moduleFileFinder,
                testClientLauncherFactory, GetNatvis(), GetNatvisDiagnosticLogger(),
                exitDialogUtil, preflightBinaryChecker, _debugSessionLauncherFactory, paramsFactory,
                _remoteDeploy, cancelableTaskFactory, _dialogUtil,
                GetNatvisLoggerOutputWindowListener(), GetSolutionExplorer(), debugEngineCommands,
                GetDebugEventCallbackDecorator(vsiService.DebuggerOptions),
                GetSymbolSettingsProvider(), deployLldbServer, _gameLaunchManager);
            return GetFactoryDecorator().Decorate(factory);
        }

        public override IFileSystem GetFileSystem() => _fileSystem
            ?? (_fileSystem = new MockFileSystem());

        public override IWindowsRegistry GetWindowsRegistry() => _windowsRegistry
            ?? (_windowsRegistry = TestDummyGenerator.Create<IWindowsRegistry>());

        public override YetiVSIService GetVsiService()
        {
            if (_vsiService == null)
            {
                var vsiServiceOptions =
                    new OptionPageGrid
                    {
                        NatvisLoggingLevel = NatvisLoggingLevelFeatureFlag.VERBOSE
                    };

                _vsiService = new YetiVSIService(vsiServiceOptions);
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
            var envDteUtilFactory = new EnvDteUtil.Factory();
            var dte2Util = envDteUtilFactory.Create(taskContext, dte2);
            _solutionExplorer = new SolutionExplorer(taskContext, null, dte2Util);

            return _solutionExplorer;
        }

        CancelableTask.Factory _cancelableTaskFactory;
        public override CancelableTask.Factory GetCancelableTaskFactory()
        {
            if (_cancelableTaskFactory == null)
            {
                _cancelableTaskFactory =
                    FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false);
            }

            return _cancelableTaskFactory;
        }

        public override JoinableTaskContext GetJoinableTaskContext() => _taskContext
            ?? (_taskContext = new JoinableTaskContext());

        public override DialogExecutionContext GetDialogExecutionContext() => null;

        public override NatvisLoggerOutputWindowListener GetNatvisLoggerOutputWindowListener() =>
            null;

        public override IVariableNameTransformer GetVariableNameTransformer() =>
            _variableNameTransformer
            ?? (_variableNameTransformer = new TestNatvisVariableNameTransformer());

        public override ISymbolSettingsProvider GetSymbolSettingsProvider()
        {
            if (_symbolSettingsProvider != null)
            {
                return _symbolSettingsProvider;
            }

            var taskContext = GetJoinableTaskContext();
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
