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
using DebuggerGrpcClient;
using GgpGrpc.Cloud;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using SymbolStores;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using YetiCommon;
using YetiCommon.CastleAspects;
using YetiCommon.ExceptionRecorder;
using YetiCommon.Logging;
using YetiCommon.MethodRecorder;
using YetiCommon.PerformanceTracing;
using YetiVSI.DebugEngine.CastleAspects;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.DebuggerOptions;
using YetiVSI.LLDBShell;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSI.Util;
using static YetiVSI.DebugEngine.DebugEngine;
using System.IO;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using EnvDTE80;
using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiCommon.VSProject;
using YetiVSI.DebugEngine.DiagnosticTools;
using YetiVSI.DebugEngine.Interfaces;
using YetiCommon.Cloud;
using YetiVSI.DebugEngine.CoreDumps;
using GameLauncher = YetiVSI.GameLaunch.GameLauncher;

namespace YetiVSI.DebugEngine
{
    public class DebugEngineFactoryCompRoot
    {
        // Minimum interval without debug events we should wait before creating
        // a new debug event batch.
        const int _minimumDebugEventBatchSeparationInMillis = 1000;

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

        IDispatcher _dispatcher;

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

            if (GetVsiService().Options.LLDBVisualizerSupport == LLDBVisualizerSupport.ENABLED)
            {
                GetNatvis().VisualizerScanner.LoadProjectFiles();
            }

            var debugEngineCommands = new DebugEngineCommands(
                GetJoinableTaskContext(), GetNatvis(),
                GetVsiService().Options.LLDBVisualizerSupport == LLDBVisualizerSupport.ENABLED);

            var actionRecorder = new ActionRecorder(GetDebugSessionMetrics());
            var backgroundProcessFactory = new BackgroundProcess.Factory();

            var processFactory = new ManagedProcess.Factory();
            var binaryFileUtil = new ElfFileUtil(GetJoinableTaskContext().Factory, processFactory);
            var lldbModuleUtil = new LldbModuleUtil();

            var symbolServerRequestHandler = new WebRequestHandler(){
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                UseDefaultCredentials = true,
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore),
            };
            var symbolServerHttpClient = new HttpClient(symbolServerRequestHandler);
            var fileReferenceFactory = new FileReference.Factory(GetFileSystem());
            var httpFileReferenceFactory = new HttpFileReference.Factory(
                GetJoinableTaskContext().Factory, GetFileSystem(), symbolServerHttpClient);
            var symbolPathParser = new SymbolPathParser(
                GetFileSystem(),
                new StructuredSymbolStore.Factory(GetFileSystem(), fileReferenceFactory),
                new FlatSymbolStore.Factory(GetFileSystem(), binaryFileUtil, fileReferenceFactory),
                new SymbolStoreSequence.Factory(binaryFileUtil), new SymbolServer.Factory(),
                new HttpSymbolStore.Factory(GetJoinableTaskContext().Factory,
                                            symbolServerHttpClient, httpFileReferenceFactory),
                new StadiaSymbolStore.Factory(GetJoinableTaskContext().Factory,
                                              symbolServerHttpClient, httpFileReferenceFactory,
                                              GetCloudRunner(),
                                              new CrashReportClient(GetCloudRunner())),
                SDKUtil.GetDefaultSymbolCachePath(), SDKUtil.GetDefaultSymbolStorePath(),
                YetiConstants.SymbolServerExcludeList);
            IModuleFileFinder moduleFileFinder = new ModuleFileFinder(symbolPathParser);
            IModuleFileLoaderFactory moduleFileLoaderFactory = new ModuleFileLoader.Factory();

            var moduleFileLoadRecorderFactory =
                new ModuleFileLoadMetricsRecorder.Factory(lldbModuleUtil, moduleFileFinder);

            var symbolLoaderFactory =
                new SymbolLoader.Factory(lldbModuleUtil, binaryFileUtil, moduleFileFinder);
            var binaryLoaderFactory = new BinaryLoader.Factory(lldbModuleUtil, moduleFileFinder);

            var cancelableTaskFactory = GetCancelableTaskFactory();

            var exceptionManagerFactory =
                new LldbExceptionManager.Factory(LinuxSignals.GetDefaultSignalsMap());
            DebugModule.Factory debugModuleFactory =
                GetFactoryDecorator().Decorate(new DebugModule.Factory(
                    cancelableTaskFactory, actionRecorder, moduleFileLoadRecorderFactory,
                    lldbModuleUtil, GetSymbolSettingsProvider()));
            var debugModuleCacheFactory = new DebugModuleCache.Factory(GetDispatcher());
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

            // TODO: Add test for this: (internal)
            bool asyncInterfacesEnabled =
                GetVsiService().Options.AsyncInterfaces == AsyncInterfaces.ENABLED;

            var debugPropertyFactory = GetFactoryDecorator().Decorate(new DebugProperty.Factory(
                varInfoEnumFactory, childrenProviderFactory, debugCodeContextFactory,
                vsExpressionCreator, GetTaskExecutor()));
            var debugPropertyAsyncFactory =
                GetFactoryDecorator().Decorate(new DebugAsyncProperty.Factory(
                    varInfoEnumFactory, childrenProviderFactory, debugCodeContextFactory,
                    vsExpressionCreator, GetTaskExecutor()));

            var createDebugPropertyDelegate =
                asyncInterfacesEnabled
                ?(CreateDebugPropertyDelegate)
                    debugPropertyAsyncFactory.Create : debugPropertyFactory.Create;

            concreteChildrenProviderFactory.Initialize(createDebugPropertyDelegate);

            var asyncEvaluatorFactory =
                GetFactoryDecorator().Decorate(new AsyncExpressionEvaluator.Factory(
                    createDebugPropertyDelegate, GetVarInfoBuilder(), vsExpressionCreator,
                    new ErrorDebugProperty.Factory(), debugEngineCommands,
                    GetVsiService().Options));

            var debugExpressionFactory = GetFactoryDecorator().Decorate(
                new DebugExpression.Factory(asyncEvaluatorFactory, GetTaskExecutor()));
            var debugAsyncExpressionFactory = GetFactoryDecorator().Decorate(
                new DebugAsyncExpression.Factory(asyncEvaluatorFactory, GetTaskExecutor()));
            var debugExpressionCreator =
                asyncInterfacesEnabled
                ?(CreateDebugExpressionDelegate)
                    debugAsyncExpressionFactory.Create : debugExpressionFactory.Create;
            var registerSetsBuilderFactory = GetFactoryDecorator().Decorate(
                new RegisterSetsBuilder.Factory(GetVariableInformationFactory()));
            var debugStackFrameFactory = GetFactoryDecorator().Decorate(new DebugStackFrame.Factory(
                debugDocumentContextFactory, childrenProviderFactory, debugCodeContextFactory,
                debugExpressionCreator, GetVariableInformationFactory(), varInfoEnumFactory,
                registerSetsBuilderFactory, GetTaskExecutor()));
            var debugStackFrameFactoryAsync =
                GetFactoryDecorator().Decorate(new DebugStackFrameAsync.Factory(
                    debugDocumentContextFactory, childrenProviderFactory, debugCodeContextFactory,
                    debugExpressionCreator, GetVariableInformationFactory(), varInfoEnumFactory,
                    registerSetsBuilderFactory, GetTaskExecutor()));
            var debugStackFrameCreator =
                asyncInterfacesEnabled ?(CreateDebugStackFrameDelegate)
                    debugStackFrameFactoryAsync.Create : debugStackFrameFactory.Create;
            var debugThreadFactory = GetFactoryDecorator().Decorate(new DebugThread.Factory(
                GetFactoryDecorator().Decorate(new FrameEnumFactory()), _taskExecutor));
            var debugThreadAsyncFactory =
                GetFactoryDecorator().Decorate(new DebugThreadAsync.Factory(
                    GetFactoryDecorator().Decorate(new FrameEnumFactory()), _taskExecutor));
            var debugThreadCreator = asyncInterfacesEnabled ?(CreateDebugThreadDelegate)
                                         debugThreadAsyncFactory.Create : debugThreadFactory.Create;
            var gameletFactory = new GameletClient.Factory();
            IGameletClient gameletClient = gameletFactory.Create(GetCloudRunner());
            var gameLauncher = new GameLauncher(gameletClient, GetSdkConfigFactory(),
                                                cancelableTaskFactory, GetVsiService());
            var debugProgramFactory =
                GetFactoryDecorator().Decorate<IDebugProgramFactory>(new DebugProgram.Factory(
                    GetJoinableTaskContext(),
                    GetFactoryDecorator().Decorate(new DebugDisassemblyStream.Factory(
                        debugCodeContextFactory, debugDocumentContextFactory)),
                    debugDocumentContextFactory, debugCodeContextFactory,
                    GetFactoryDecorator().Decorate(new ThreadEnumFactory()),
                    GetFactoryDecorator().Decorate(new ModuleEnumFactory()),
                    GetFactoryDecorator().Decorate(new CodeContextEnumFactory()), gameLauncher));
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
            var transportSessionFactory =
                new LldbTransportSession.Factory(new MemoryMappedFileFactory());
            var yetiTransport = new YetiDebugTransport(
                GetJoinableTaskContext(), transportSessionFactory, callInvokerFactory,
                new GrpcConnectionFactory(GetJoinableTaskContext().Factory,
                                          grpcInterceptors.ToArray()),
                GetTaskExecutor().CancelAsyncOperationIfRequested, processFactory, GetDialogUtil(),
                vsOutputWindow, GetVsiService());

            var chromeClientLauncherFactory = new YetiCommon.ChromeClientLauncher.Factory(
                backgroundProcessFactory, new ChromeClientLaunchCommandFormatter(GetJsonUtil()),
                GetSdkConfigFactory());

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
                        debugModuleCacheFactory, debugModuleFactory, debugThreadCreator,
                        debugStackFrameCreator, lldbShell, breakpointManagerFactory,
                        symbolLoaderFactory, binaryLoaderFactory, moduleFileLoaderFactory));
            bool fastExpressionEvaluation = GetVsiService().Options.FastExpressionEvaluation ==
                                            FastExpressionEvaluation.ENABLED;
            var dumpModulesProvider = new DumpModulesProvider(GetFileSystem());
            var moduleSearchLogHolder = new ModuleSearchLogHolder();
            var coreAttachWarningDialog = new CoreAttachWarningDialogUtil(
                GetJoinableTaskContext(),
                GetDialogUtil());
            var debugSessionLauncherFactory = new DebugSessionLauncher.Factory(
                GetJoinableTaskContext(), lldbDebuggerFactory, lldbListenerFactory,
                lldbPlatformFactory, lldbPlatformConnectOptionsFactory,
                lldbPlatformShellCommandFactory, attachedProgramFactory, actionRecorder,
                moduleFileLoadRecorderFactory, exceptionManagerFactory, GetFileSystem(),
                fastExpressionEvaluation, moduleFileFinder, dumpModulesProvider,
                moduleSearchLogHolder, GetSymbolSettingsProvider(), coreAttachWarningDialog);
            var paramsFactory = new Params.Factory(GetJsonUtil());

            var remoteCommand = new RemoteCommand(processFactory);
            var socketSender = new LocalSocketSender();
            var remoteFile = new RemoteFile(processFactory, transportSessionFactory, socketSender,
                                            GetFileSystem());
            var remoteDeploy = new RemoteDeploy(remoteCommand, remoteFile, processFactory,
                                                GetFileSystem(), binaryFileUtil);
            bool deployLldbServer = IsInternalEngine();
            IDebugEngineFactory factory = new DebugEngine.Factory(
                GetJoinableTaskContext(), serviceManager, GetDebugSessionMetrics(), yetiTransport,
                actionRecorder, symbolServerHttpClient, moduleFileLoadRecorderFactory,
                moduleFileFinder, chromeClientLauncherFactory, GetNatvis(),
                GetNatvisDiagnosticLogger(), exitDialogUtil, preflightBinaryChecker,
                debugSessionLauncherFactory, paramsFactory, remoteDeploy, cancelableTaskFactory,
                GetDialogUtil(), GetNatvisLoggerOutputWindowListener(), GetSolutionExplorer(),
                debugEngineCommands,
                GetDebugEventCallbackDecorator(GetVsiService().DebuggerOptions),
                GetSymbolSettingsProvider(), deployLldbServer, gameLauncher);
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
                    GetNatvisDiagnosticLogger(), new VsExpressionCreator(),
                    GetVsiService().Options);
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

        public virtual IDispatcher GetDispatcher()
        {
            if (_dispatcher == null)
            {
                _dispatcher = new MainThreadDispatcher();
            }

            return _dispatcher;
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
                var vcProjectAdapterFactory = new VcProjectAdapter.Factory();
                var dte2 = Package.GetGlobalService(typeof(DTE)) as DTE2;
                var envDteUtilFactory = new EnvDteUtil.Factory();
                var envDteUtil = envDteUtilFactory.Create(taskContext, dte2);
                _solutionExplorer =
                    new SolutionExplorer(taskContext, vcProjectAdapterFactory, envDteUtil);
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
                apiAspects.Add(
                    new ExceptionRecorderAspect(new ExceptionRecorder(debugSessionMetrics)));
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
                SetupStepTimeMetrics(apiAspects, debugSessionMetrics);
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

        public virtual void SetupStepTimeMetrics(List<IInterceptor> apiAspects, IMetrics metrics)
        {
            var debugEventBatchFactory = new DebugEventBatch.Factory();
            var schedulerFactory = new EventScheduler.Factory();
            var timerFactory = new Timer.Factory();

            var timer = timerFactory.Create();
            var debugEventAggregator = new DebugEventAggregator(
                debugEventBatchFactory, _minimumDebugEventBatchSeparationInMillis, schedulerFactory,
                timer);
            var debugEventRecorder = new DebugEventRecorder(debugEventAggregator, metrics);

            var timeSource = GetTimeSource();
            var metricsCollectionAspect =
                new MetricsCollectionAspect(debugEventRecorder, timeSource);
            apiAspects.Add(metricsCollectionAspect);
        }

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
                    GetWindowsRegistry(), GetFileSystem());
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
                GetNatvisDiagnosticLogger(), evaluator, GetVariableNameTransformer());

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
