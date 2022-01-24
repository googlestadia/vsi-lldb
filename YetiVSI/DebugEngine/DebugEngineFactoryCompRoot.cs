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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Runtime.Serialization;
using Castle.DynamicProxy;
using DebuggerGrpcClient;
using EnvDTE;
using EnvDTE80;
using GgpGrpc.Cloud;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using SymbolStores;
using YetiCommon;
using YetiCommon.CastleAspects;
using YetiCommon.Cloud;
using YetiCommon.ExceptionRecorder;
using YetiCommon.Logging;
using YetiCommon.MethodRecorder;
using YetiCommon.PerformanceTracing;
using YetiVSI.DebugEngine.CastleAspects;
using YetiVSI.DebugEngine.CoreDumps;
using YetiVSI.DebugEngine.DiagnosticTools;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.DebuggerOptions;
using YetiVSI.GameLaunch;
using YetiVSI.LLDBShell;
using YetiVSI.Metrics;
using YetiVSI.ProjectSystem.Abstractions;
using YetiVSI.Shared.Metrics;
using YetiVSI.Util;
using static YetiVSI.DebugEngine.DebugEngine;

namespace YetiVSI.DebugEngine
{
    public class DebugEngineFactoryCompRoot
    {
        // Interval in which batched events are sent.
        const int _metricsEventsBatchIntervalMs = 3000;

        YetiVSIService _vsiService;

        ISymbolSettingsProvider _symbolSettingsProvider;

        ITimeSource _timeSource;

        IFileSystem _fileSystem;

        IWindowsRegistry _windowsRegistry;

        NatvisLoader _natvisLoader;

        NatvisExpander _natvisExpander;

        NatvisDiagnosticLogger _natvisLogger;
        NatvisVisualizerScanner _natvisVisualizerScanner;
        NatvisExpressionEvaluator _natvisExpressionEvaluator;

        INatvisFileSource _natvisFileSource;

        TaskExecutor _taskExecutor;

        JoinableTaskContext _joinableTaskContext;

        DebugSessionMetrics _debugSessionMetrics;

        DebugEventRecorder _debugEventRecorder;

        ExpressionEvaluationRecorder _expressionEvaluationRecorder;

        IExceptionRecorder _exceptionRecorder;

        IDecorator _factoryDecorator;

        IVariableInformationFactory _variableInformationFactory;

        VarInfoBuilder _varInfoBuilder;

        LLDBVariableInformationFactory _lldbVarInfoFactory;

        IVariableNameTransformer _variableNameTransformer;

        CancelableTask.Factory _cancelableTaskFactory;

        ISolutionExplorer _solutionExplorer;

        IDialogUtil _dialogUtil;

        NatvisLoggerOutputWindowListener _natvisLogListener;

        ChromeTracingLogger _chromeTracingLogger;

        NLog.ILogger _callSequenceLogger;

        JsonUtil _jsonUtil;

        SdkConfig.Factory _sdkConfigFactory;

        CloudRunner _cloudRunner;

        IChromeLauncher _chromeLauncher;

        public class DebugEngineCommands : IDebugEngineCommands
        {
            readonly JoinableTaskContext _taskContext;
            readonly NatvisExpander _natvisExpander;
            readonly bool _allowNatvisReload;

            public DebugEngineCommands(JoinableTaskContext taskContext,
                                       NatvisExpander natvisExpander, bool allowNatvisReload)
            {
                _taskContext = taskContext;
                _natvisExpander = natvisExpander;
                _allowNatvisReload = allowNatvisReload;
            }

#region IDebugEngineCommands

            public void LogNatvisStats(TextWriter writer, int verbosityLevel)
            {
                if (_natvisExpander == null)
                {
                    Trace.WriteLine("Unable to log Natvis stats.  No Natvis engine exists.");
                    return;
                }

                _natvisExpander.VisualizerScanner.LogStats(writer, verbosityLevel);
            }

            public bool ReloadNatvis(TextWriter writer, out string resultDescription)
            {
                _taskContext.ThrowIfNotOnMainThread();

                if (_natvisExpander == null || !_allowNatvisReload)
                {
                    // User facing string (shown in the Watch window).
                    resultDescription =
                        _natvisExpander == null
                        ? "No Natvis engine exists." : "Natvis is disabled.";

                    return false;
                }

                _natvisExpander.VisualizerScanner.Reload(writer);
                // User facing string (shown in the Watch window).
                resultDescription = "Natvis update successful.";
                return true;
            }

#endregion
        }

        /// <summary>
        /// Creates a new debug engine factory.
        /// </summary>
        /// <param name="self">The external identity of the debug engine.</param>
        public virtual IDebugEngineFactory CreateDebugEngineFactory()
        {
            ServiceManager serviceManager = CreateServiceManager();

            GetJoinableTaskContext().ThrowIfNotOnMainThread();

            var vsExpressionCreator = new VsExpressionCreator();

            var debugEngineCommands = new DebugEngineCommands(
                GetJoinableTaskContext(), GetNatvis(),
                GetVsiService().Options.LLDBVisualizerSupport == LLDBVisualizerSupport.ENABLED);

            var actionRecorder = new ActionRecorder(GetDebugSessionMetrics());
            var backgroundProcessFactory = new BackgroundProcess.Factory();

            var processFactory = new ManagedProcess.Factory();
            var binaryFileUtil = new ElfFileUtil(processFactory);

            var symbolServerRequestHandler = new WebRequestHandler(){
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                UseDefaultCredentials = true,
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore),
            };
            var symbolServerHttpClient = new HttpClient(symbolServerRequestHandler);
            var symbolPathParser = new SymbolPathParser(
                GetFileSystem(), binaryFileUtil, symbolServerHttpClient,
                new CrashReportClient(GetCloudRunner()),
                SDKUtil.GetDefaultSymbolCachePath(), SDKUtil.GetDefaultSymbolStorePath(),
                YetiConstants.SymbolServerExcludeList);
            IModuleFileFinder moduleFileFinder = new ModuleFileFinder(symbolPathParser);
            IModuleFileLoaderFactory moduleFileLoaderFactory = new ModuleFileLoader.Factory();

            var moduleFileLoadRecorderFactory =
                new ModuleFileLoadMetricsRecorder.Factory(moduleFileFinder);

            var symbolLoaderFactory =
                new SymbolLoader.Factory(binaryFileUtil, moduleFileFinder);
            var binaryLoaderFactory = new BinaryLoader.Factory(moduleFileFinder);

            var cancelableTaskFactory = GetCancelableTaskFactory();

            var exceptionManagerFactory =
                new LldbExceptionManager.Factory(LinuxSignals.GetDefaultSignalsMap());
            DebugModule.Factory debugModuleFactory =
                GetFactoryDecorator().Decorate(new DebugModule.Factory(
                    cancelableTaskFactory, actionRecorder, moduleFileLoadRecorderFactory,
                    GetSymbolSettingsProvider()));
            var debugMemoryContextFactory =
                GetFactoryDecorator().Decorate(new DebugMemoryContext.Factory());

            var debugDocumentContextFactory =
                GetFactoryDecorator().Decorate(new DebugDocumentContext.Factory());

            var debugCodeContextFactory = GetFactoryDecorator().Decorate(
                new DebugCodeContext.Factory(debugMemoryContextFactory));

            var varInfoEnumFactory =
                GetFactoryDecorator().Decorate<IVariableInformationEnumFactory>(
                    new VariableInformationEnum.Factory(GetTaskExecutor()));

            var concreteChildrenProviderFactory = new ChildrenProvider.Factory();
            var childrenProviderFactory = GetFactoryDecorator().Decorate<IChildrenProviderFactory>(
                concreteChildrenProviderFactory);

            var debugPropertyFactory = GetFactoryDecorator()
                .Decorate(new DebugAsyncProperty.Factory(varInfoEnumFactory,
                                                         childrenProviderFactory,
                                                         debugCodeContextFactory,
                                                         vsExpressionCreator, GetTaskExecutor()));

            concreteChildrenProviderFactory.Initialize(debugPropertyFactory);

            var asyncEvaluatorFactory = GetFactoryDecorator().Decorate(
                new AsyncExpressionEvaluator.Factory(debugPropertyFactory, GetVarInfoBuilder(),
                                                     vsExpressionCreator,
                                                     new ErrorDebugProperty.Factory(),
                                                     debugEngineCommands, GetVsiService().Options,
                                                     GetExpressionEvaluationRecorder(),
                                                     GetTimeSource()));

            var debugAsyncExpressionFactory = GetFactoryDecorator().Decorate(
                new DebugAsyncExpression.Factory(asyncEvaluatorFactory, GetTaskExecutor()));

            var registerSetsBuilderFactory = GetFactoryDecorator().Decorate(
                new RegisterSetsBuilder.Factory(GetVariableInformationFactory()));

            var debugStackFrameFactory = GetFactoryDecorator().Decorate(
                new DebugAsyncStackFrame.Factory(debugDocumentContextFactory,
                                                 childrenProviderFactory, debugCodeContextFactory,
                                                 debugAsyncExpressionFactory,
                                                 GetVariableInformationFactory(),
                                                 varInfoEnumFactory, registerSetsBuilderFactory,
                                                 GetTaskExecutor()));

            var frameEnumFactory = GetFactoryDecorator().Decorate(new FrameEnumFactory());
            var debugThreadAsyncFactory = GetFactoryDecorator()
                .Decorate(new DebugAsyncThread.Factory(_taskExecutor, frameEnumFactory));
            var gameletFactory = GetGameletClientFactory();
            var launchParamsConverter =
                new LaunchGameParamsConverter(new QueryParametersParser());
            var gameLaunchManager = new GameLaunchBeHelper(gameletFactory.Create(GetCloudRunner()),
                                                          launchParamsConverter);
            var debugProgramFactory =
                GetFactoryDecorator().Decorate<IDebugProgramFactory>(new DebugProgram.Factory(
                    GetJoinableTaskContext(),
                    GetFactoryDecorator().Decorate(new DebugDisassemblyStream.Factory(
                        debugCodeContextFactory, debugDocumentContextFactory)),
                    debugDocumentContextFactory, debugCodeContextFactory,
                    GetFactoryDecorator().Decorate(new ThreadEnumFactory()),
                    GetFactoryDecorator().Decorate(new ModuleEnumFactory()),
                    GetFactoryDecorator().Decorate(new CodeContextEnumFactory())));
            var breakpointErrorEnumFactory =
                GetFactoryDecorator().Decorate(new BreakpointErrorEnumFactory());
            var boundBreakpointEnumFactory =
                GetFactoryDecorator().Decorate(new BoundBreakpointEnumFactory());
            var breakpointManagerFactory = new LldbBreakpointManager.Factory(
                GetJoinableTaskContext(),
                GetFactoryDecorator().Decorate(new DebugPendingBreakpoint.Factory(
                    GetJoinableTaskContext(),
                    GetFactoryDecorator().Decorate(new DebugBoundBreakpoint.Factory(
                        debugDocumentContextFactory, debugCodeContextFactory,
                        GetFactoryDecorator().Decorate(new DebugBreakpointResolution.Factory()))),
                    breakpointErrorEnumFactory, boundBreakpointEnumFactory)),
                GetFactoryDecorator().Decorate(new DebugWatchpoint.Factory(
                    GetJoinableTaskContext(),
                    GetFactoryDecorator().Decorate(new DebugWatchpointResolution.Factory()),
                    breakpointErrorEnumFactory, boundBreakpointEnumFactory)));
            var eventManagerFactory =
                new LldbEventManager.Factory(boundBreakpointEnumFactory, GetJoinableTaskContext());
            var lldbDebuggerFactory = new GrpcDebuggerFactory();
            var lldbListenerFactory = new GrpcListenerFactory();
            var lldbPlatformShellCommandFactory = new GrpcPlatformShellCommandFactory();
            var lldbPlatformFactory = new GrpcPlatformFactory();
            var lldbPlatformConnectOptionsFactory = new GrpcPlatformConnectOptionsFactory();
            var grpcInterceptors = CreateGrpcInterceptors(GetVsiService().DebuggerOptions);
            var vsOutputWindow =
                serviceManager.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            var callInvokerFactory = new PipeCallInvokerFactory();
            var yetiTransport = new YetiDebugTransport(
                GetJoinableTaskContext(), callInvokerFactory,
                new GrpcConnectionFactory(GetJoinableTaskContext().Factory,
                                          grpcInterceptors.ToArray()),
                GetTaskExecutor().CancelAsyncOperationIfRequested, processFactory, GetDialogUtil(),
                vsOutputWindow, GetVsiService());

            var testClientLauncherFactory = new ChromeClientsLauncher.Factory(
                new ChromeClientLaunchCommandFormatter(),
                GetSdkConfigFactory(), GetChromeLauncher(backgroundProcessFactory));

            var exitDialogUtil = new ExitDialogUtil(GetDialogUtil(), GetDialogExecutionContext());
            var preflightBinaryChecker =
                new PreflightBinaryChecker(GetFileSystem(), binaryFileUtil);
            var lldbShell = serviceManager.GetGlobalService(typeof(SLLDBShell)) as ILLDBShell;
            var attachedProgramFactory =
                GetFactoryDecorator().Decorate<ILldbAttachedProgramFactory>(
                    new LldbAttachedProgram.Factory(
                        GetJoinableTaskContext(),
                        GetFactoryDecorator().Decorate<IDebugEngineHandlerFactory>(
                            new DebugEngineHandler.Factory(GetJoinableTaskContext())),
                        GetTaskExecutor(), eventManagerFactory, debugProgramFactory,
                        debugModuleFactory, debugThreadAsyncFactory,
                        debugStackFrameFactory, lldbShell, breakpointManagerFactory,
                        symbolLoaderFactory, binaryLoaderFactory, moduleFileLoaderFactory));
            bool fastExpressionEvaluation = GetVsiService().Options.FastExpressionEvaluation ==
                                            FastExpressionEvaluation.ENABLED;
            var dumpModulesProvider = new DumpModulesProvider(GetFileSystem());
            var moduleSearchLogHolder = new ModuleSearchLogHolder();
            var coreAttachWarningDialog = new CoreAttachWarningDialogUtil(
                GetJoinableTaskContext(),
                GetDialogUtil());
            var debugSessionLauncherFactory = new DebugSessionLauncher.Factory(
                GetJoinableTaskContext(), lldbListenerFactory,
                lldbPlatformConnectOptionsFactory, lldbPlatformShellCommandFactory,
                attachedProgramFactory, actionRecorder, moduleFileLoadRecorderFactory,
                exceptionManagerFactory, moduleFileFinder, dumpModulesProvider,
                moduleSearchLogHolder, GetSymbolSettingsProvider(), coreAttachWarningDialog);
            var vsiLaunchFactory = new VsiGameLaunchFactory(gameletFactory.Create(GetCloudRunner()),
                                                            GetCancelableTaskFactory(),
                                                            gameLaunchManager, actionRecorder,
                                                            GetDialogUtil());
            var gameLauncher = new GameLauncher(gameletFactory.Create(GetCloudRunner()),
                                                GetVsiService(), launchParamsConverter,
                                                GetCancelableTaskFactory(), actionRecorder,
                                                GetDialogUtil(), vsiLaunchFactory);

            var remoteCommand = new RemoteCommand(processFactory);
            var remoteFile = new RemoteFile(processFactory);
            var remoteDeploy = new RemoteDeploy(remoteCommand, remoteFile, processFactory,
                                                GetFileSystem());
            bool deployLldbServer = IsInternalEngine();

            StadiaLldbDebugger.Factory stadiaLldbDebuggerFactory = new StadiaLldbDebugger.Factory(
                lldbDebuggerFactory, lldbPlatformFactory, GetFileSystem(), actionRecorder,
                fastExpressionEvaluation);

            IDebugEngineFactory factory = new DebugEngine.Factory(
                GetJoinableTaskContext(), serviceManager, GetDebugSessionMetrics(),
                stadiaLldbDebuggerFactory, yetiTransport, actionRecorder, symbolServerHttpClient,
                moduleFileLoadRecorderFactory, moduleFileFinder, testClientLauncherFactory,
                GetNatvis(), GetNatvisDiagnosticLogger(), exitDialogUtil, preflightBinaryChecker,
                debugSessionLauncherFactory, remoteDeploy, cancelableTaskFactory,
                GetDialogUtil(), _vsiService,
                GetNatvisLoggerOutputWindowListener(), GetSolutionExplorer(),
                debugEngineCommands,
                GetDebugEventCallbackDecorator(GetVsiService().DebuggerOptions),
                GetSymbolSettingsProvider(), deployLldbServer, gameLauncher,
                GetDebugEventRecorder(), GetExpressionEvaluationRecorder());
            return GetFactoryDecorator().Decorate(factory);
        }

        public virtual bool IsInternalEngine()
        {
#if INTERNAL_BUILD
            return true;
#else
            return false;
#endif
        }

        public virtual ServiceManager CreateServiceManager()
        {
            return new ServiceManager();
        }

        public virtual ITimeSource GetTimeSource()
        {
            if (_timeSource == null)
            {
                _timeSource = new StopwatchTimeSource();
            }

            return _timeSource;
        }

        public virtual YetiVSIService GetVsiService()
        {
            if (_vsiService == null)
            {
                _vsiService = (YetiVSIService) CreateServiceManager().RequireGlobalService(
                    typeof(YetiVSIService));
            }

            return _vsiService;
        }

        public virtual ISymbolSettingsProvider GetSymbolSettingsProvider()
        {
            GetJoinableTaskContext().ThrowIfNotOnMainThread();

            if (_symbolSettingsProvider == null)
            {
                var symbolManager = CreateServiceManager().GetGlobalService(
                    typeof(SVsShellDebugger)) as IVsDebuggerSymbolSettingsManager120A;

                var symbolServerEnabled =
                    GetVsiService().Options.SymbolServerSupport == SymbolServerSupport.ENABLED;

                var debuggerService =
                    (IVsDebugger2) new ServiceManager().GetGlobalService(typeof(SVsShellDebugger));

                _symbolSettingsProvider = new SymbolSettingsProvider(
                    symbolManager, debuggerService, symbolServerEnabled, GetJoinableTaskContext());
            }

            return _symbolSettingsProvider;
        }

        public virtual IFileSystem GetFileSystem()
        {
            if (_fileSystem == null)
            {
                _fileSystem = new FileSystem();
            }

            return _fileSystem;
        }

        public virtual IChromeLauncher GetChromeLauncher(BackgroundProcess.Factory factory)
        {
            if (_chromeLauncher == null)
            {
                _chromeLauncher = new ChromeLauncher(factory);
            }

            return _chromeLauncher;
        }

        public virtual IWindowsRegistry GetWindowsRegistry()
        {
            if (_windowsRegistry == null)
            {
                _windowsRegistry = new WindowsRegistry();
            }

            return _windowsRegistry;
        }

        public virtual NatvisExpressionEvaluator GetNatvisExpressionEvaluator()
        {
            if (_natvisExpressionEvaluator == null)
            {
                _natvisExpressionEvaluator = new NatvisExpressionEvaluator(
                    GetNatvisDiagnosticLogger(), new VsExpressionCreator(), GetVsiService().Options,
                    GetExpressionEvaluationRecorder(), GetTimeSource());
            }

            return _natvisExpressionEvaluator;
        }

        public virtual NatvisDiagnosticLogger GetNatvisDiagnosticLogger()
        {
            if (_natvisLogger == null)
            {
                _natvisLogger = new NatvisDiagnosticLogger(
                    YetiLog.GetLogger(""), GetVsiService().Options.NatvisLoggingLevel);
            }

            return _natvisLogger;
        }

        public NatvisVisualizerScanner GetNatvisVisualizerScanner()
        {
            if (_natvisVisualizerScanner == null)
            {
                _natvisVisualizerScanner = new NatvisVisualizerScanner(
                    GetNatvisDiagnosticLogger(), GetNatvisLoader(), GetJoinableTaskContext());
            }

            return _natvisVisualizerScanner;
        }

        public bool GetCustomListsEnabled() =>
            GetVsiService().DebuggerOptions[DebuggerOption.NATVIS_EXPERIMENTAL] ==
            DebuggerOptionState.ENABLED;

        public virtual ITaskExecutor GetTaskExecutor()
        {
            if (_taskExecutor == null)
            {
                _taskExecutor = new TaskExecutor(GetJoinableTaskContext().Factory);

                if (GetVsiService().DebuggerOptions[DebuggerOption.DEBUGGER_TRACING] ==
                    DebuggerOptionState.ENABLED)
                {
                    var taskExecutorTracingHelper =
                        new TaskExecutorTracingHelper(GetChromeTracingLogger(), GetTimeSource());

                    _taskExecutor.OnAsyncTaskStarted +=
                        taskExecutorTracingHelper.OnAsyncTaskStarted;
                    _taskExecutor.OnAsyncTaskEnded += taskExecutorTracingHelper.OnAsyncTaskEnded;
                }
            }

            return _taskExecutor;
        }

        public virtual IVariableNameTransformer GetVariableNameTransformer()
        {
            if (_variableNameTransformer == null)
            {
                _variableNameTransformer = new NatvisVariableNameTransformer();
            }

            return _variableNameTransformer;
        }

        public virtual JoinableTaskContext GetJoinableTaskContext()
        {
            if (_joinableTaskContext == null)
            {
                _joinableTaskContext = CreateServiceManager().GetJoinableTaskContext();
            }

            return _joinableTaskContext;
        }

        public DebugSessionMetrics GetDebugSessionMetrics()
        {
            if (_debugSessionMetrics == null)
            {
                _debugSessionMetrics = new DebugSessionMetrics(
                    CreateServiceManager().GetGlobalService(typeof(SMetrics)) as IMetrics);
            }

            return _debugSessionMetrics;
        }

        public DebugEventRecorder GetDebugEventRecorder()
        {
            if (_debugEventRecorder == null)
            {
                var schedulerFactory = new EventScheduler.Factory();
                var debugEventAggregator =
                    new BatchEventAggregator<DebugEventBatch, DebugEventBatchParams,
                        DebugEventBatchSummary>(_metricsEventsBatchIntervalMs, schedulerFactory,
                                                GetExceptionRecorder());
                _debugEventRecorder =
                    new DebugEventRecorder(debugEventAggregator, GetDebugSessionMetrics());
            }

            return _debugEventRecorder;
        }

        public ExpressionEvaluationRecorder GetExpressionEvaluationRecorder()
        {
            if (_expressionEvaluationRecorder == null)
            {
                var schedulerFactory = new EventScheduler.Factory();

                var expressionEvaluationEventAggregator =
                    new BatchEventAggregator<ExpressionEvaluationBatch,
                        ExpressionEvaluationBatchParams, ExpressionEvaluationBatchSummary>(
                        _metricsEventsBatchIntervalMs, schedulerFactory, GetExceptionRecorder());

                _expressionEvaluationRecorder =
                    new ExpressionEvaluationRecorder(expressionEvaluationEventAggregator,
                                                     GetDebugSessionMetrics());
            }

            return _expressionEvaluationRecorder;
        }

        public virtual IExceptionRecorder GetExceptionRecorder()
        {
            if (_exceptionRecorder == null)
            {
                _exceptionRecorder = new ExceptionRecorder(GetDebugSessionMetrics());
            }

            return _exceptionRecorder;
        }

        /// <summary>
        /// Gets decorator used for Debug object factories and other factories for types
        /// that we want to wrap with aspects provided by CreateApiAspects().
        /// </summary>
        public IDecorator GetFactoryDecorator()
        {
            if (_factoryDecorator == null)
            {
                var decoratorUtil =
                    new DecoratorUtil(new ProxyGenerationOptions(new DebugEngineProxyHook()));
                _factoryDecorator = decoratorUtil.CreateFactoryDecorator(
                    new ProxyGenerator(), CreateApiAspects().ToArray());
            }

            return _factoryDecorator;
        }

        public virtual IVariableInformationFactory GetVariableInformationFactory()
        {
            if (_variableInformationFactory == null)
            {
                if (GetVsiService().Options.LLDBVisualizerSupport != LLDBVisualizerSupport.DISABLED)
                {
                    var expandVariableFactory =
                        new ExpandVariableInformation.Factory(GetTaskExecutor());

                    if (GetVsiService().Options.LLDBVisualizerSupport ==
                        LLDBVisualizerSupport.ENABLED)
                    {
                        _variableInformationFactory = new NatvisVariableInformationFactory(
                            GetNatvis(), GetLldbVariableInformationFactory(),
                            expandVariableFactory);
                    }
                    else if (GetVsiService().Options.LLDBVisualizerSupport ==
                             LLDBVisualizerSupport.BUILT_IN_ONLY)
                    {
                        _variableInformationFactory =
                            new CustomVisualizerVariableInformationFactory(
                                GetNatvis(), GetLldbVariableInformationFactory(),
                                expandVariableFactory);
                    }
                }
            }

            return _variableInformationFactory;
        }

        public virtual VarInfoBuilder GetVarInfoBuilder()
        {
            if (_varInfoBuilder == null)
            {
                _varInfoBuilder = new VarInfoBuilder(GetVariableInformationFactory());
            }

            return _varInfoBuilder;
        }

        public virtual LLDBVariableInformationFactory GetLldbVariableInformationFactory()
        {
            if (_lldbVarInfoFactory == null)
            {
                _lldbVarInfoFactory =
                    new LLDBVariableInformationFactory(new RemoteValueChildAdapter.Factory());

                _lldbVarInfoFactory.SetVarInfoBuilder(GetVarInfoBuilder());
            }

            return _lldbVarInfoFactory;
        }

        public virtual ISolutionExplorer GetSolutionExplorer()
        {
            if (_solutionExplorer == null)
            {
                JoinableTaskContext taskContext = GetJoinableTaskContext();
                var vcProjectInfoFactory = new VsProjectInfo.Factory();
                var dte2 = Package.GetGlobalService(typeof(DTE)) as DTE2;
                var envDteUtil = new EnvDteUtil(taskContext, dte2);
                _solutionExplorer =
                    new SolutionExplorer(taskContext, vcProjectInfoFactory, envDteUtil);
            }

            return _solutionExplorer;
        }

        public virtual CancelableTask.Factory GetCancelableTaskFactory()
        {
            if (_cancelableTaskFactory == null)
            {
                _cancelableTaskFactory = new CancelableTask.Factory(GetJoinableTaskContext(),
                                                                    new ProgressDialog.Factory());
            }

            return _cancelableTaskFactory;
        }

        public virtual IDialogUtil GetDialogUtil()
        {
            if (_dialogUtil == null)
            {
                _dialogUtil = new DialogUtil();
            }

            return _dialogUtil;
        }

        public virtual DialogExecutionContext GetDialogExecutionContext()
        {
            return System.Windows.Application.Current.Dispatcher.Invoke;
        }

        public virtual List<IInterceptor> CreateApiAspects()
        {
            DebuggerOptions.DebuggerOptions debuggerOptions = GetVsiService().DebuggerOptions;
            DebugSessionMetrics debugSessionMetrics = GetDebugSessionMetrics();

            // Creates aspects for tracing, exception handling and metrics.
            // Additionally, it creates gRPC interceptors for use in the DebugTransport.
            var apiAspects = new List<IInterceptor>();

            // It is important that this aspect be added as the first aspect so that it is the
            // outer most decorator.
            if (debuggerOptions[DebuggerOption.EXCEPTION_LOGGING] == DebuggerOptionState.ENABLED)
            {
                apiAspects.Add(new LogExceptionAspect());
            }

            if (debuggerOptions[DebuggerOption.EXCEPTION_METRICS] == DebuggerOptionState.ENABLED)
            {
                apiAspects.Add(new ExceptionRecorderAspect(GetExceptionRecorder()));
            }

            if (debuggerOptions[DebuggerOption.DEBUGGER_TRACING] == DebuggerOptionState.ENABLED)
            {
                SetupDebuggerTracing(apiAspects);
            }

            if (debuggerOptions[DebuggerOption.CALL_SEQUENCE_LOGGING] ==
                DebuggerOptionState.ENABLED)
            {
                SetupCallSequenceLogging(apiAspects);
            }

            if (debuggerOptions[DebuggerOption.PARAMETER_LOGGING] == DebuggerOptionState.ENABLED)
            {
                apiAspects.Add(new InvocationLoggerAspect(new InvocationLogUtil()));
            }

            if (debuggerOptions[DebuggerOption.STEP_TIME_METRICS] == DebuggerOptionState.ENABLED)
            {
                SetupStepTimeMetrics(apiAspects);
            }

            return apiAspects;
        }

        /// <summary>
        /// Sets up aspects and interceptors to enable debugger call tracing.
        /// </summary>
        public virtual void SetupDebuggerTracing(List<IInterceptor> apiAspects)
        {
            var traceLogger = GetChromeTracingLogger();
            var timeSource = GetTimeSource();
            var tracingAspect = new TracingAspect(traceLogger, timeSource);
            apiAspects.Add(tracingAspect);
        }

        /// <summary>
        /// Returns a transformation callback that might decorate the IDebugEventCallback2 instance
        /// provided by Visual Studio.
        /// </summary>
        /// <param name="debuggerOptions"></param>
        /// <returns></returns>
        public virtual DebugEventCallbackTransform GetDebugEventCallbackDecorator(
            DebuggerOptions.DebuggerOptions debuggerOptions)
        {
            if (debuggerOptions[DebuggerOption.CALL_SEQUENCE_LOGGING] ==
                DebuggerOptionState.ENABLED)
            {
                return callback => new CallSequenceLoggedDebugEventCallback(
                           GetJoinableTaskContext(), callback, GetCallSequenceLogger());
            }

            return callback => callback;
        }

        /// <summary>
        /// Sets up aspects and interceptors to enable call sequence logging.
        /// </summary>
        public virtual void SetupCallSequenceLogging(List<IInterceptor> apiAspects)
        {
            var traceLogger = GetCallSequenceLogger();
            var objectIdGenerator = new ObjectIDGenerator();
            var tracingAspect = new CallSequenceLoggingAspect(objectIdGenerator, traceLogger);
            apiAspects.Add(tracingAspect);
        }

        public virtual List<Grpc.Core.Interceptors.Interceptor> CreateGrpcInterceptors(
            DebuggerOptions.DebuggerOptions debuggerOptions)
        {
            var grpcInterceptors = new List<Grpc.Core.Interceptors.Interceptor>();
            if (debuggerOptions[DebuggerOption.DEBUGGER_TRACING] == DebuggerOptionState.ENABLED)
            {
                var traceLogger = GetChromeTracingLogger();
                var timeSource = GetTimeSource();
                grpcInterceptors.Add(new TracingGrpcInterceptor(traceLogger, timeSource));
            }

            return grpcInterceptors;
        }

        public virtual ChromeTracingLogger GetChromeTracingLogger()
        {
            if (_chromeTracingLogger == null)
            {
                int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                var logger = YetiLog.GetTraceLogger(YetiLog.ToLogFileDateTime(DateTime.Now));
                _chromeTracingLogger = new ChromeTracingLogger(
                    processId, logger, GetTaskExecutor().IsInsideAsyncContext);
            }

            return _chromeTracingLogger;
        }

        public virtual NLog.ILogger GetCallSequenceLogger()
        {
            if (_callSequenceLogger == null)
            {
                _callSequenceLogger = YetiLog.GetCallSequenceLogger();
            }

            return _callSequenceLogger;
        }

        public virtual void SetupStepTimeMetrics(List<IInterceptor> apiAspects)
        {
            var timeSource = GetTimeSource();
            var metricsCollectionAspect =
                new MetricsCollectionAspect(GetDebugEventRecorder(), timeSource);
            apiAspects.Add(metricsCollectionAspect);
        }

        public virtual IGameletClientFactory GetGameletClientFactory() =>
            new GameletClient.Factory();

        public virtual NatvisLoggerOutputWindowListener GetNatvisLoggerOutputWindowListener()
        {
            JoinableTaskContext taskContext = GetJoinableTaskContext();
            taskContext.ThrowIfNotOnMainThread();

            if (_natvisLogListener == null)
            {
                _natvisLogListener = NatvisLoggerOutputWindowListener.Create(
                    taskContext, CreateServiceManager().GetGlobalService(typeof(SVsOutputWindow))
                                     as IVsOutputWindow);
            }

            return _natvisLogListener;
        }

        public virtual NatvisLoader GetNatvisLoader()
        {
            if (_natvisLoader == null)
            {
                _natvisLoader = new NatvisLoader(
                    GetJoinableTaskContext(), GetTaskExecutor(), GetNatvisDiagnosticLogger(),
                    GetSolutionNatvisFileSource(),
                    new NatvisValidator.Factory(GetFileSystem(), GetNatvisDiagnosticLogger()),
                    GetWindowsRegistry(), GetFileSystem(), GetExceptionRecorder());
            }

            return _natvisLoader;
        }

        public virtual NatvisExpander GetNatvis()
        {
            if (_natvisExpander == null)
            {
                var natvisSizeParser = new NatvisSizeParser(GetNatvisDiagnosticLogger(),
                                                            GetNatvisExpressionEvaluator());

                var natvisStringFormatter = new NatvisStringFormatter(
                    GetNatvisExpressionEvaluator(), GetNatvisDiagnosticLogger(),
                    GetNatvisVisualizerScanner(), GetTaskExecutor());

                var natvisCollectionFactory = CreateNatvisCollectionFactory(
                    GetNatvisExpressionEvaluator(), natvisSizeParser, natvisStringFormatter);

                var natvisSmartPointerFactory = new SmartPointerEntity.Factory(
                    GetNatvisDiagnosticLogger(), GetNatvisExpressionEvaluator());

                _natvisExpander =
                    new NatvisExpander(natvisCollectionFactory, natvisSmartPointerFactory,
                                       natvisStringFormatter, GetNatvisVisualizerScanner());
            }

            return _natvisExpander;
        }

        public NatvisCollectionEntity.Factory CreateNatvisCollectionFactory(
            NatvisExpressionEvaluator evaluator, NatvisSizeParser sizeParser,
            NatvisStringFormatter stringFormatter)
        {
            var itemFactory = new ItemEntity.Factory(GetNatvisDiagnosticLogger(), evaluator);
            var indexListItemsFactory = new IndexListItemsEntity.Factory(
                GetNatvisDiagnosticLogger(), evaluator, sizeParser);
            var arrayItemsFactory =
                new ArrayItemsEntity.Factory(GetNatvisDiagnosticLogger(), evaluator, sizeParser);
            var syntheticItemFactory = new SyntheticItemEntity.Factory(GetNatvisDiagnosticLogger(),
                                                                       evaluator, stringFormatter);
            var expandedItemFactory =
                new ExpandedItemEntity.Factory(GetNatvisDiagnosticLogger(), evaluator);
            var linkedListItemsFactory = new LinkedListItemsEntity.Factory(
                GetNatvisDiagnosticLogger(), evaluator, sizeParser);
            var treeItemsFactory =
                new TreeItemsEntity.Factory(GetNatvisDiagnosticLogger(), evaluator, sizeParser);

            var customListItemsFactory = new CustomListItemsEntity.Factory(
                GetNatvisDiagnosticLogger(), evaluator, GetVariableNameTransformer(), sizeParser);

            return new NatvisCollectionEntity.Factory(
                itemFactory, syntheticItemFactory, expandedItemFactory, indexListItemsFactory,
                arrayItemsFactory, linkedListItemsFactory, treeItemsFactory, customListItemsFactory,
                GetNatvisDiagnosticLogger(), GetCustomListsEnabled);
        }

        public virtual INatvisFileSource GetSolutionNatvisFileSource()
        {
            if (_natvisFileSource == null)
            {
                _natvisFileSource = new HostNatvisProject(GetJoinableTaskContext());
            }

            return _natvisFileSource;
        }

        public virtual JsonUtil GetJsonUtil()
        {
            if (_jsonUtil == null)
            {
                _jsonUtil = new JsonUtil(GetFileSystem());
            }
            return _jsonUtil;
        }

        public virtual SdkConfig.Factory GetSdkConfigFactory()
        {
            if (_sdkConfigFactory == null)
            {
                _sdkConfigFactory = new SdkConfig.Factory(GetJsonUtil());
            }
            return _sdkConfigFactory;
        }

        public virtual ICloudRunner GetCloudRunner()
        {
            if (_cloudRunner == null)
            {
                _cloudRunner = new CloudRunner(
                    GetSdkConfigFactory(),
                    new CredentialManager(new CredentialConfig.Factory(GetJsonUtil()),
                                          new VsiAccountOptionLoader(GetVsiService().Options)),
                    new CloudConnection(GetFileSystem(), new CloudConnection.ChannelFactory()),
                    new GgpSDKUtil());
            }

            return _cloudRunner;
        }
    }
}
