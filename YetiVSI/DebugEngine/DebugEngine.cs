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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.DebugEngine.CoreDumps;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.DebuggerOptions;
using YetiVSI.GameLaunch;
using YetiVSI.LoadSymbols;
using YetiVSI.Metrics;
using YetiVSI.ProjectSystem.Abstractions;
using YetiVSI.Shared.Metrics;
using YetiVSI.Util;
using static YetiVSI.DebuggerOptions.DebuggerOptions;

namespace YetiVSI.DebugEngine
{
    public interface IDebugEngineFactory
    {
        IGgpDebugEngine Create(IGgpDebugEngine self);
    }

    /// <summary>
    /// Workaround to enable Castle interception of SetEngineGuid().
    /// </summary>
    /// <remarks>
    /// IDebugEngine3.SetEngineGuid() would raise a AccessViolationException if decorated
    /// with a Castle interceptor when running in Visual Studio 2015.  See (internal) for more
    /// info.
    /// </remarks>
    public abstract class GgpDebugEngine : IGgpDebugEngine
    {
        // The identity of 'this' to the outside world.  |self| should be used instead of 'this'
        // when the reference will be exposed externally.
        //
        // The IDecoratorSelf<T> pattern isn't used because Visual Studio uses a constructor as the
        // entry point for constructing a debug engine and it's misleading to have Self behave
        // subtly different in this case, i.e. The Self set during aspect decoration is incorrect.
        protected IGgpDebugEngine Self { get; private set; }

        // Construct a new GgpDebugEngine using the given IGgpDebugEngine as the external identity
        // of this object. Pass in self=null to use this object as its own external identity.
        // See 'Self' property for details.
        protected GgpDebugEngine(IGgpDebugEngine self)
        {
            Self = self ?? this;
        }

        public int SetEngineGuid(ref Guid guidEngine)
        {
            // Converts |guidEngine| to a non ref type arg so that Castle DynamicProxy doesn't fail
            // to assign values back to it.  See (internal) for more info.
            return Self.SetEngineGuidImpl(guidEngine);
        }

        public abstract IDebugEngineCommands DebugEngineCommands { get; }
        public abstract Guid Id { get; }

        public event EventHandler SessionEnded;
        public event EventHandler SessionEnding;

        protected void RaiseSessionEnded(EventArgs e)
        {
            SessionEnded?.Invoke(this, e);
        }

        protected void RaiseSessionEnding(EventArgs e)
        {
            SessionEnding?.Invoke(this, e);
        }

        public abstract int Attach(IDebugProgram2[] rgpPrograms,
                                   IDebugProgramNode2[] rgpProgramNodes, uint celtPrograms,
                                   IDebugEventCallback2 pCallback, enum_ATTACH_REASON dwReason);

        public abstract int CanTerminateProcess(IDebugProcess2 pProcess);
        public abstract int CauseBreak();
        public abstract int ContinueFromSynchronousEvent(IDebugEvent2 pEvent);

        public abstract int CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest,
                                                    out IDebugPendingBreakpoint2 ppPendingBP);

        public abstract int DestroyProgram(IDebugProgram2 pProgram);
        public abstract int EnumPrograms(out IEnumDebugPrograms2 ppEnum);
        public abstract int GetEngineId(out Guid pguidEngine);

        public abstract int LaunchSuspended(string pszServer, IDebugPort2 pPort, string pszExe,
                                            string pszArgs, string pszDir, string bstrEnv,
                                            string pszOptions, enum_LAUNCH_FLAGS dwLaunchFlags,
                                            uint hStdInput, uint hStdOutput, uint hStdError,
                                            IDebugEventCallback2 pCallback,
                                            out IDebugProcess2 ppProcess);

        public abstract int LoadSymbols();
        public abstract int RemoveAllSetExceptions(ref Guid guidType);
        public abstract int RemoveSetException(EXCEPTION_INFO[] pException);
        public abstract int ResumeProcess(IDebugProcess2 pProcess);
        public abstract int SetAllExceptions(enum_EXCEPTION_STATE dwState);
        public abstract int SetEngineGuidImpl(Guid guidEngine);
        public abstract int SetException(EXCEPTION_INFO[] pException);

        public abstract int SetJustMyCodeState(int fUpdate, uint dwModules,
                                               JMC_CODE_SPEC[] rgJMCSpec);

        public abstract int SetLocale(ushort wLangID);
        public abstract int SetMetric(string pszMetric, object varValue);
        public abstract int SetRegistryRoot(string pszRegistryRoot);

        public abstract int SetSymbolPath(string szSymbolSearchPath, string szSymbolCachePath,
                                          uint flags);

        public abstract int TerminateProcess(IDebugProcess2 pProcess);
    }

    public class DebugEngine : GgpDebugEngine
    {
        // Transformation callback that enables clients to decorate the IDebugEventCallback2
        // instance provided by Visual Studio.
        public delegate IDebugEventCallback2 DebugEventCallbackTransform(
            IDebugEventCallback2 callback);

        public class Factory : IDebugEngineFactory
        {
            readonly JoinableTaskContext _taskContext;
            readonly ServiceManager _serviceManager;
            readonly DebugSessionMetrics _debugSessionMetrics;
            readonly IStadiaLldbDebuggerFactory _stadiaLldbDebuggerFactory;
            readonly YetiDebugTransport _yetiTransport;
            readonly ActionRecorder _actionRecorder;
            readonly HttpClient _symbolServerHttpClient;
            readonly ModuleFileLoadMetricsRecorder.Factory _moduleFileLoadRecorderFactory;
            readonly IModuleFileFinder _moduleFileFinder;
            readonly ChromeClientsLauncher.Factory _testClientLauncherFactory;
            readonly NatvisExpander _natvisExpander;
            readonly NatvisDiagnosticLogger _natvisLogger;
            readonly ExitDialogUtil _exitDialogUtil;
            readonly PreflightBinaryChecker _preflightBinaryChecker;
            readonly IDebugSessionLauncherFactory _debugSessionLauncherFactory;
            readonly IRemoteDeploy _remoteDeploy;
            readonly CancelableTask.Factory _cancelableTaskFactory;
            readonly IDialogUtil _dialogUtil;
            readonly IYetiVSIService _vsiService;
            readonly NatvisLoggerOutputWindowListener _natvisLogListener;
            readonly ISolutionExplorer _solutionExplorer;
            readonly IDebugEngineCommands _debugEngineCommands;
            readonly DebugEventCallbackTransform _debugEventCallbackDecorator;
            readonly ISymbolSettingsProvider _symbolSettingsProvider;
            readonly bool _deployLldbServer;
            readonly IGameLauncher _gameLauncher;
            readonly DebugEventRecorder _debugEventRecorder;
            readonly ExpressionEvaluationRecorder _expressionEvaluationRecorder;

            public Factory(JoinableTaskContext taskContext, ServiceManager serviceManager,
                           DebugSessionMetrics debugSessionMetrics,
                           IStadiaLldbDebuggerFactory stadiaLldbDebuggerFactory,
                           YetiDebugTransport yetiTransport, ActionRecorder actionRecorder,
                           HttpClient symbolServerHttpClient,
                           ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory,
                           IModuleFileFinder moduleFileFinder,
                           ChromeClientsLauncher.Factory testClientLauncherFactory,
                           NatvisExpander natvisExpander, NatvisDiagnosticLogger natvisLogger,
                           ExitDialogUtil exitDialogUtil,
                           PreflightBinaryChecker preflightBinaryChecker,
                           IDebugSessionLauncherFactory debugSessionLauncherFactory,
                           IRemoteDeploy remoteDeploy, CancelableTask.Factory cancelableTaskFactory,
                           IDialogUtil dialogUtil, IYetiVSIService vsiService,
                           NatvisLoggerOutputWindowListener natvisLogListener,
                           ISolutionExplorer solutionExplorer,
                           IDebugEngineCommands debugEngineCommands,
                           DebugEventCallbackTransform debugEventCallbackDecorator,
                           ISymbolSettingsProvider symbolSettingsProvider, bool deployLldbServer,
                           IGameLauncher gameLauncher, DebugEventRecorder debugEventRecorder,
                           ExpressionEvaluationRecorder expressionEvaluationRecorder)
            {
                _taskContext = taskContext;
                _serviceManager = serviceManager;
                _debugSessionMetrics = debugSessionMetrics;

                _stadiaLldbDebuggerFactory = stadiaLldbDebuggerFactory;
                _yetiTransport = yetiTransport;
                _actionRecorder = actionRecorder;
                _symbolServerHttpClient = symbolServerHttpClient;
                _moduleFileLoadRecorderFactory = moduleFileLoadRecorderFactory;
                _moduleFileFinder = moduleFileFinder;
                _testClientLauncherFactory = testClientLauncherFactory;
                _natvisExpander = natvisExpander;
                _natvisLogger = natvisLogger;
                _exitDialogUtil = exitDialogUtil;
                _preflightBinaryChecker = preflightBinaryChecker;
                _debugSessionLauncherFactory = debugSessionLauncherFactory;
                _remoteDeploy = remoteDeploy;
                _cancelableTaskFactory = cancelableTaskFactory;
                _dialogUtil = dialogUtil;
                _vsiService = vsiService;
                _natvisLogListener = natvisLogListener;
                _solutionExplorer = solutionExplorer;
                _debugEngineCommands = debugEngineCommands;
                _debugEventCallbackDecorator = debugEventCallbackDecorator;
                _symbolSettingsProvider = symbolSettingsProvider;
                _deployLldbServer = deployLldbServer;
                _gameLauncher = gameLauncher;
                _debugEventRecorder = debugEventRecorder;
                _expressionEvaluationRecorder = expressionEvaluationRecorder;
            }

            /// <summary>
            /// Creates a new debug engine.
            /// </summary>
            /// <param name="self">The external identity of the debug engine.</param>
            public virtual IGgpDebugEngine Create(IGgpDebugEngine self)
            {
                _taskContext.ThrowIfNotOnMainThread();

                var vsiService =
                    (YetiVSIService) _serviceManager.RequireGlobalService(typeof(YetiVSIService));
                var extensionOptions = vsiService.Options;
                var debuggerOptions = vsiService.DebuggerOptions;
                var sessionNotifier =
                    _serviceManager.GetGlobalService(typeof(SSessionNotifier)) as ISessionNotifier;

                return new DebugEngine(self, Guid.NewGuid(), extensionOptions, debuggerOptions,
                                       _debugSessionMetrics, _taskContext, _natvisLogListener,
                                       _solutionExplorer, _cancelableTaskFactory,
                                       _stadiaLldbDebuggerFactory, _dialogUtil, _vsiService,
                                       _yetiTransport, _actionRecorder, _symbolServerHttpClient,
                                       _moduleFileLoadRecorderFactory, _moduleFileFinder,
                                       _testClientLauncherFactory, _natvisExpander, _natvisLogger,
                                       _exitDialogUtil, _preflightBinaryChecker,
                                       _debugSessionLauncherFactory, _remoteDeploy,
                                       _debugEngineCommands, _debugEventCallbackDecorator,
                                       sessionNotifier, _symbolSettingsProvider, _deployLldbServer,
                                       _gameLauncher, _debugEventRecorder,
                                       _expressionEvaluationRecorder);
            }
        }

        // Enum to specify the type of launch we are doing.
        public enum LaunchOption
        {
            Invalid,

            // Attach to a running process on the gamelet.
            AttachToGame,

            // Launch the game on the gamelet before attaching to it.
            LaunchGame,

            // Attach to a local core file.
            AttachToCore,
        }

        public class Params
        {
            public string TargetIp { get; set; }
            public string CoreFilePath { get; set; }
            public bool DeleteCoreFile { get; set; }
            public string DebugSessionId { get; set; }
        }

        readonly TimeSpan _deleteTimeout = TimeSpan.FromSeconds(5);

        readonly JoinableTaskContext _taskContext;
        readonly NatvisLoggerOutputWindowListener _natvisLogListener;
        readonly DebugSessionMetrics _debugSessionMetrics;

        readonly CancelableTask.Factory _cancelableTaskFactory;
        readonly IStadiaLldbDebuggerFactory _stadiaLldbDebuggerFactory;
        readonly ISolutionExplorer _solutionExplorer;
        readonly YetiDebugTransport _yetiTransport;
        readonly IDialogUtil _dialogUtil;
        readonly IYetiVSIService _vsiService;
        readonly ActionRecorder _actionRecorder;
        readonly IExtensionOptions _extensionOptions;
        readonly DebuggerOptions.DebuggerOptions _debuggerOptions;
        readonly HttpClient _symbolServerHttpClient;
        readonly ModuleFileLoadMetricsRecorder.Factory _moduleFileLoadRecorderFactory;
        readonly IModuleFileFinder _moduleFileFinder;
        readonly ChromeClientsLauncher.Factory _testClientLauncherFactory;
        readonly NatvisExpander _natvisExpander;
        readonly NatvisDiagnosticLogger _natvisLogger;
        readonly ExitDialogUtil _exitDialogUtil;
        readonly PreflightBinaryChecker _preflightBinaryChecker;
        readonly IDebugSessionLauncherFactory _debugSessionLauncherFactory;
        readonly IRemoteDeploy _remoteDeploy;
        readonly IDebugEngineCommands _debugEngineCommands;
        readonly DebugEventCallbackTransform _debugEventCallbackDecorator;
        readonly ISymbolSettingsProvider _symbolSettingsProvider;
        readonly bool _deployLldbServer;
        readonly IGameLauncher _gameLauncher;
        readonly DebugEventRecorder _debugEventRecorder;
        readonly ExpressionEvaluationRecorder _expressionEvaluationRecorder;
        readonly ISessionNotifier _sessionNotifier;

        // Keep track of the attach operation, so that it can be aborted by transport errors.
        ICancelableTask _attachOperation;

        // Variables set during launch and/or attach.
        string _executableFileName;
        string _executableFullPath;
        bool _rgpEnabled;
        bool _diveEnabled;
        bool _renderDocEnabled;
        string _workingDirectory;
        SshTarget _target;
        string _coreFilePath;
        LaunchOption _launchOption;
        LaunchParams _launchParams;
        bool _deleteCoreFileAtCleanup;
        IVsiGameLaunch _vsiGameLaunch;

        // Attached program is set after successfully attaching.
        ILldbAttachedProgram _attachedProgram;

        // Timer that is started after successfully attaching.
        ITimer _attachedTimer;

        JoinableTask<StadiaLldbDebugger> _lldbDebuggerCreator;
        HashSet<string> _libPaths;
        YetiDebugTransport.GrpcSession _grpcSession;

        public DebugEngine(IGgpDebugEngine self, Guid id, IExtensionOptions extensionOptions,
                           DebuggerOptions.DebuggerOptions debuggerOptions,
                           DebugSessionMetrics debugSessionMetrics, JoinableTaskContext taskContext,
                           NatvisLoggerOutputWindowListener natvisLogListener,
                           ISolutionExplorer solutionExplorer,
                           CancelableTask.Factory cancelableTaskFactory,
                           IStadiaLldbDebuggerFactory stadiaLldbDebuggerFactory,
                           IDialogUtil dialogUtil, IYetiVSIService vsiService,
                           YetiDebugTransport yetiTransport, ActionRecorder actionRecorder,
                           HttpClient symbolServerHttpClient,
                           ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory,
                           IModuleFileFinder moduleFileFinder,
                           ChromeClientsLauncher.Factory testClientLauncherFactory,
                           NatvisExpander natvisExpander, NatvisDiagnosticLogger natvisLogger,
                           ExitDialogUtil exitDialogUtil,
                           PreflightBinaryChecker preflightBinaryChecker,
                           IDebugSessionLauncherFactory debugSessionLauncherFactory,
                           IRemoteDeploy remoteDeploy, IDebugEngineCommands debugEngineCommands,
                           DebugEventCallbackTransform debugEventCallbackDecorator,
                           ISessionNotifier sessionNotifier,
                           ISymbolSettingsProvider symbolSettingsProvider, bool deployLldbServer,
                           IGameLauncher gameLauncher, DebugEventRecorder debugEventRecorder,
                           ExpressionEvaluationRecorder expressionEvaluationRecorder) : base(self)
        {
            taskContext.ThrowIfNotOnMainThread();

            _launchOption = LaunchOption.Invalid;

            Id = id;

            _taskContext = taskContext;
            _natvisLogListener = natvisLogListener;
            _debugSessionMetrics = debugSessionMetrics;

            _extensionOptions = extensionOptions;
            _debuggerOptions = debuggerOptions;

            _cancelableTaskFactory = cancelableTaskFactory;
            _stadiaLldbDebuggerFactory = stadiaLldbDebuggerFactory;
            _solutionExplorer = solutionExplorer;
            _dialogUtil = dialogUtil;
            _vsiService = vsiService;
            _yetiTransport = yetiTransport;
            _actionRecorder = actionRecorder;
            _symbolServerHttpClient = symbolServerHttpClient;
            _moduleFileLoadRecorderFactory = moduleFileLoadRecorderFactory;
            _moduleFileFinder = moduleFileFinder;
            _testClientLauncherFactory = testClientLauncherFactory;
            _natvisExpander = natvisExpander;
            _natvisLogger = natvisLogger;
            _exitDialogUtil = exitDialogUtil;
            _preflightBinaryChecker = preflightBinaryChecker;
            _debugSessionLauncherFactory = debugSessionLauncherFactory;
            _remoteDeploy = remoteDeploy;
            _debugEngineCommands = debugEngineCommands;
            _debugEventCallbackDecorator = debugEventCallbackDecorator;
            _sessionNotifier = sessionNotifier;
            _symbolSettingsProvider = symbolSettingsProvider;
            _deployLldbServer = deployLldbServer;
            _gameLauncher = gameLauncher;
            _debugEventRecorder = debugEventRecorder;
            _expressionEvaluationRecorder = expressionEvaluationRecorder;

            // Register observers on long lived objects last so that they don't hold a reference
            // to this if an exception is thrown during construction.
            _yetiTransport.OnStop += Abort;
            _extensionOptions.PropertyChanged += OptionsGrid_PropertyChanged;
            _debuggerOptions.ValueChanged += OnDebuggerOptionChanged;
            if (_natvisLogger != null && natvisLogListener != null)
            {
                _natvisLogger.NatvisLogEvent += natvisLogListener.OnNatvisLogEvent;
            }

            Trace.WriteLine("Debug session started.");
            Trace.WriteLine($"Extension version: {Versions.GetExtensionVersion()}");
            Trace.WriteLine($"SDK version: {Versions.GetSdkVersion()}");
            Trace.WriteLine($"VS version: {VsVersion.GetVisualStudioVersion()}");
        }

        public override Guid Id { get; }

        public override IDebugEngineCommands DebugEngineCommands => _debugEngineCommands;

        void OptionsGrid_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OptionPageGrid.NatvisLoggingLevel))
            {
                _natvisLogger?.SetLogLevel(_extensionOptions.NatvisLoggingLevel);
            }
        }

        void OnDebuggerOptionChanged(object sender, ValueChangedEventArgs args)
        {
            Trace.WriteLine($"DebuggerOptionChanged: DebuggerOption[{args.Option}]={args.State}");

            if (args.Option == DebuggerOption.NATVIS_EXPERIMENTAL)
            {
                // TODO: Enable/disable natvis experimental features. Current use of
                // the experimental flag is CustomListItems which is polling for updates. We should
                // examine whether or not polling will work in all cases and if so, remove this
                // code.
            }
        }

        #region IDebugEngine2 functions

        // Attaches the debugger to program(s). Sends events indicating the debugger is starting.
        // Called automatically during IDebugPortNotify2.AddProgramNode,
        // which we call during ResumeProcess.
        public override int Attach(IDebugProgram2[] programs, IDebugProgramNode2[] programNodes,
                                   uint numPrograms, IDebugEventCallback2 callback,
                                   enum_ATTACH_REASON reason)
        {
            _taskContext.ThrowIfNotOnMainThread();

            callback = _debugEventCallbackDecorator(callback);

            if (numPrograms != 1)
            {
                Trace.WriteLine($"Debug Engine failed to attach. Attempted to attach to " +
                                $"{numPrograms} programs; we only support attaching to one.");
                _dialogUtil.ShowError(ErrorStrings.SingleProgramExpected);
                return VSConstants.E_INVALIDARG;
            }

            if (reason == enum_ATTACH_REASON.ATTACH_REASON_AUTO)
            {
                Trace.WriteLine("Debug Engine failed to attach. Auto attach is not supported.");
                _dialogUtil.ShowError(ErrorStrings.AutoAttachNotSupported);
                return VSConstants.E_NOTIMPL;
            }

            // save the program ID provided to us
            programs[0].GetProgramId(out Guid programId);
            programs[0].GetProcess(out IDebugProcess2 process);

            if (reason == enum_ATTACH_REASON.ATTACH_REASON_USER)
            {
                process.GetPort(out IDebugPort2 port);
                var debugPort = port as PortSupplier.DebugPort;
                var gamelet = debugPort?.Gamelet;
                if (gamelet != null && !string.IsNullOrEmpty(gamelet.IpAddr))
                {
                    _target = new SshTarget(gamelet);
                }
                else
                {
                    Trace.WriteLine("Unable to find Stadia instance.");
                    _dialogUtil.ShowError(ErrorStrings.NoGameletsFound);
                    return VSConstants.E_ABORT;
                }

                _debugSessionMetrics.DebugSessionId = debugPort?.DebugSessionId;
            }

            if (string.IsNullOrEmpty(_debugSessionMetrics.DebugSessionId))
            {
                _debugSessionMetrics.UseNewDebugSessionId();
            }

            Trace.WriteLine("Extension Options:");
            foreach (var option in _extensionOptions.Options)
            {
                Trace.WriteLine($"{option.Name.ToLower()}: {option.Value.ToString().ToLower()}");
            }

            Trace.WriteLine("Debugger Options:");
            foreach (var option in _debuggerOptions)
            {
                Trace.WriteLine(
                    $"{option.Key.ToString().ToLower()}: {option.Value.ToString().ToLower()}");
            }

            uint? attachPid = null;

            if (!string.IsNullOrEmpty(_coreFilePath))
            {
                _launchOption = LaunchOption.AttachToCore;
            }
            else if (reason == enum_ATTACH_REASON.ATTACH_REASON_LAUNCH)
            {
                _launchOption = LaunchOption.LaunchGame;
            }
            else if (reason == enum_ATTACH_REASON.ATTACH_REASON_USER)
            {
                _launchOption = LaunchOption.AttachToGame;
            }

            // If the debugger launches or attaches to core dump this initialization is called in
            // LaunchSuspended method.
            if (_launchOption == LaunchOption.AttachToGame)
            {
                if (!LaunchLldbDebuggerInBackground())
                {
                    Trace.WriteLine("Aborting attach because the user canceled it.");
                    return VSConstants.E_ABORT;
                }
            }

            var lldbDeployAction = _actionRecorder.CreateToolAction(ActionType.LldbServerDeploy);
            JoinableTask lldbDeployTask = null;
            if (_deployLldbServer && _launchOption != LaunchOption.AttachToCore)
            {
                lldbDeployTask = _taskContext.Factory.RunAsync(async () =>
                {
                    await TaskScheduler.Default;
                    await _remoteDeploy.DeployLldbServerAsync(_target, lldbDeployAction);
                });
            }

            try
            {
                var preflightCheckAction = _actionRecorder.CreateToolAction(
                    ActionType.DebugPreflightBinaryChecks);

                if (_launchOption == LaunchOption.LaunchGame)
                {
                    string cmd =
                        _launchParams?.Cmd?.Split(' ').First(s => !string.IsNullOrEmpty(s)) ??
                        _executableFileName;

                    // Note that Path.Combine works for both relative and full paths.
                    // It returns cmd if cmd starts with '/' or '\'.
                    var remoteTargetPath = Path.Combine(YetiConstants.RemoteGamePath, cmd);

                    // This field should be initialized in LaunchLldbDebuggerInBackground
                    if (_libPaths == null)
                    {
                        throw new ArgumentNullException(nameof(_libPaths));
                    }

                    _cancelableTaskFactory.Create(TaskMessages.CheckingBinaries,
                                                  async _ =>
                                                      await _preflightBinaryChecker
                                                          .CheckLocalAndRemoteBinaryOnLaunchAsync(
                                                              _libPaths, _executableFileName,
                                                              _target, remoteTargetPath,
                                                              preflightCheckAction))
                        .RunAndRecord(preflightCheckAction);
                }
                else if (_launchOption == LaunchOption.AttachToGame)
                {
                    attachPid = GetProcessId(process);
                    if (attachPid.HasValue)
                    {
                        _cancelableTaskFactory.Create(TaskMessages.CheckingRemoteBinary,
                                                      async _ =>
                                                          await _preflightBinaryChecker
                                                              .CheckRemoteBinaryOnAttachAsync(
                                                                  attachPid.Value, _target,
                                                                  preflightCheckAction))
                            .RunAndRecord(preflightCheckAction);
                    }
                    else
                    {
                        Trace.WriteLine(
                            "Failed to get target process ID; skipping remote build id check");
                    }
                }
            }
            catch (PreflightBinaryCheckerException e)
            {
                _dialogUtil.ShowWarning(e.Message, e);
            }

            // Attaching the debugger is a synchronous operation that runs on a background thread.
            // The user is given a chance to cancel the operation if it takes too long.
            // Before running the operation, we store a reference to it, so that we can cancel it
            // asynchronously if the YetiTransport fails to start.
            int result = VSConstants.S_OK;
            var exitInfo = ExitInfo.Normal(ExitReason.Unknown);
            var glData = new GameLaunchData
            {
                LaunchId = _vsiGameLaunch?.LaunchId
            };

            var startAction = _actionRecorder.CreateToolAction(ActionType.DebugStart);
            startAction.UpdateEvent(new DeveloperLogEvent { GameLaunchData = glData });

            async Task<ILldbAttachedProgram> AttachAsync(ICancelable task)
            {
                if (lldbDeployTask != null)
                {
                    using (new TestBenchmark("WaitForLLDBDeploy", TestBenchmarkScope.Recorder))
                    {
                        await lldbDeployAction.RecordAsync(lldbDeployTask.JoinAsync());
                    }
                }

                // This field should be initialized in LaunchLldbDebuggerInBackground
                if (_grpcSession == null)
                {
                    throw new ArgumentNullException(nameof(_grpcSession));
                }

                // Attempt to start the transport. Pass on to the transport if our attach reason
                // indicates we need to launch the main debugged process ourselves.
                _yetiTransport.StartPreGame(_launchOption, _rgpEnabled, _diveEnabled,
                                            _renderDocEnabled, _target, _grpcSession);

                SafeErrorUtil.SafelyLogError(
                    () => RecordParameters(startAction, _extensionOptions, _debuggerOptions),
                    "Recording debugger parameters");

                ILldbAttachedProgram program;

                // This field should be initialized in LaunchLldbDebuggerInBackground
                if (_lldbDebuggerCreator == null)
                {
                    throw new ArgumentNullException(nameof(_lldbDebuggerCreator));
                }

                StadiaLldbDebugger lldbDebugger = null;
                using (new TestBenchmark("WaitForLldbDebugger", TestBenchmarkScope.Recorder))
                {
                    lldbDebugger = await _lldbDebuggerCreator;
                }

                var launcher = _debugSessionLauncherFactory.Create(
                    Self, _coreFilePath, _executableFileName, _vsiGameLaunch);
                program = await launcher.LaunchAsync(task, process, programId, attachPid,
                                                     _grpcSession.GrpcConnection,
                                                     _grpcSession.GetLocalDebuggerPort(),
                                                     _target?.IpAddress, _target?.Port ?? 0,
                                                     _launchOption, callback, lldbDebugger);

                // Launch processes that need the game process id.
                _yetiTransport.StartPostGame(_launchOption, _target, program.RemotePid);

                return program;
            }

            var attachTask = _cancelableTaskFactory.Create(TaskMessages.AttachingToProcess,
                                                           AttachAsync);
            _attachOperation = attachTask;
            try
            {
                if (!attachTask.RunAndRecord(startAction))
                {
                    Trace.WriteLine("Aborting attach because the user canceled it.");
                    exitInfo = ExitInfo.Normal(ExitReason.AttachCanceled);
                    result = VSConstants.E_ABORT;
                }
                else
                {
                    _attachedProgram = attachTask.Result;
                    _attachedProgram.Start(Self);
                    _attachedTimer = _actionRecorder.CreateStartedTimer();
                    _sessionNotifier.NotifySessionLaunched(
                        new SessionLaunchedEventArgs(_launchOption, _attachedProgram));
                    Trace.WriteLine("LLDB successfully attached.");
                }
            }
            catch (AttachException e)
            {
                Trace.WriteLine($"Attach failed: {e.Demystify()}");
                exitInfo = ExitInfo.Error(e);
                result = e.Result;
            }
            catch (TaskAbortedException e) when (e.InnerException != null)
            {
                Trace.WriteLine($"Aborting attach because the debug session was aborted: " +
                                $"{e.InnerException.Demystify()}");
                exitInfo = ExitInfo.Error(e.InnerException);
                result = VSConstants.E_ABORT;
            }
            catch (YetiDebugTransportException e)
            {
                Trace.WriteLine($"Failed to start debug transport: {e.Demystify()}");
                exitInfo = ExitInfo.Error(e);
                result = VSConstants.E_ABORT;
            }
            catch (DeployException e)
            {
                Trace.WriteLine($"Aborted due to DeployException: {e.Demystify()}");
                exitInfo = ExitInfo.Error(e);
                result = VSConstants.E_ABORT;
            }
            catch (CoreAttachStoppedException)
            {
                Trace.WriteLine($"Aborting attach because the user stopped it.");
                exitInfo = ExitInfo.Normal(ExitReason.AttachCanceled);
                result = VSConstants.E_ABORT;
            }

            if (result != VSConstants.S_OK)
            {
                StopTransportAndCleanup(exitInfo);
                return result;
            }

            return VSConstants.S_OK;
        }

        public override int CauseBreak()
        {
            return VSConstants.E_NOTIMPL;
        }

        public override int ContinueFromSynchronousEvent(IDebugEvent2 evnt)
        {
            if (evnt is ProgramCreateEvent)
            {
                if (_attachedProgram == null)
                {
                    Trace.WriteLine("Program create failed. There is no attached program.");
                    return VSConstants.E_FAIL;
                }

                if (_launchOption == LaunchOption.AttachToCore)
                {
                    _attachedProgram.ContinueInBreakMode();
                }
                else
                {
                    _attachedProgram.ContinueFromSuspended();
                }

                // Records the time it took from attaching until resuming the process
                // as well as the number of breakpoints that were placed in that time.
                uint numPending = _attachedProgram.GetNumPendingBreakpoints();
                uint numBound = _attachedProgram.GetNumBoundBreakpoints();
                var bpData = new VSIBoundBreakpointsData
                {
                    NumPendingBreakpoints = (int) numPending,
                    NumBoundBreakpoints = (int) numBound
                };
                SafeErrorUtil.SafelyLogError(
                    () => _actionRecorder.RecordToolAction(ActionType.DebugContinueAfterAttach,
                                                           _attachedTimer,
                                                           new DeveloperLogEvent
                                                               { BoundBreakpointsData = bpData }),
                    "Recording attach-to-continue time");
                return VSConstants.S_OK;
            }
            else if (evnt is ProgramDestroyEvent)
            {
                var programDestroyEvent = (ProgramDestroyEvent) evnt;
                EndDebugSession(programDestroyEvent.ExitInfo);
            }

            return VSConstants.S_OK;
        }

        public override int CreatePendingBreakpoint(IDebugBreakpointRequest2 breakpointRequest,
                                                    out IDebugPendingBreakpoint2 pendingBreakpoint)
        {
            _taskContext.ThrowIfNotOnMainThread();

            if (_attachedProgram == null)
            {
                Trace.WriteLine(
                    "Unable to create pending breakpoint, there is no attached program");
                pendingBreakpoint = null;
                return VSConstants.E_FAIL;
            }

            pendingBreakpoint = _attachedProgram.CreatePendingBreakpoint(breakpointRequest);
            return VSConstants.S_OK;
        }

        public override int DestroyProgram(IDebugProgram2 program)
        {
            return VSConstants.E_NOTIMPL;
        }

        public override int EnumPrograms(out IEnumDebugPrograms2 programsEnum)
        {
            programsEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        public override int GetEngineId(out Guid engineGuid)
        {
            engineGuid = YetiConstants.DebugEngineGuid;
            return VSConstants.S_OK;
        }

        public override int RemoveAllSetExceptions(ref Guid guidType)
        {
            return VSConstants.E_NOTIMPL;
        }

        public override int RemoveSetException(EXCEPTION_INFO[] exception)
        {
            return VSConstants.E_NOTIMPL;
        }

        public override int SetException(EXCEPTION_INFO[] exceptions)
        {
            if (_attachedProgram == null)
            {
                Trace.WriteLine("WARNING: SetException was called without a program attached. " +
                                "Exception state will not be set.");
                return VSConstants.E_FAIL;
            }

            _attachedProgram.SetExceptions(exceptions);
            return VSConstants.S_OK;
        }

        public override int SetLocale(ushort languageId)
        {
            return VSConstants.S_OK;
        }

        public override int SetMetric(string metric, object value)
        {
            return VSConstants.E_NOTIMPL;
        }

        public override int SetRegistryRoot(string registryRoot)
        {
            if (_natvisExpander != null)
            {
                _natvisExpander.VisualizerScanner.SetRegistryRoot(registryRoot);
            }

            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugEngine3 functions

        public override int SetSymbolPath(string searchPath, string cachePath, uint flags)
        {
            _taskContext.ThrowIfNotOnMainThread();

            // We are ignoring input params and ask VS Symbols Settings Store for search and cache
            // paths. Otherwise for whatever reason VS tries to set empty search paths when users
            // explicitly ask to load symbols e.g. via symbols settings page in Run mode (works
            // as expected in Attach / Dump debugging).
            _symbolSettingsProvider.GetStorePaths(out string storeSearchPaths,
                                                  out string storeCache);
            _moduleFileFinder.SetSearchPaths(
                SymbolUtils.GetCombinedLookupPaths(storeSearchPaths, storeCache));
            return VSConstants.S_OK;
        }

        public override int LoadSymbols()
        {
            _taskContext.ThrowIfNotOnMainThread();

            if (_attachedProgram == null)
            {
                Trace.WriteLine("Load symbols failed. There is no attached program.");
                return VSConstants.E_FAIL;
            }

            var action = _actionRecorder.CreateToolAction(ActionType.DebugEngineLoadSymbols);

            var inclusionSettings = _symbolSettingsProvider.GetInclusionSettings();
            var isSymbolServerEnabled = _symbolSettingsProvider.IsSymbolServerEnabled;
            var isStadiaSymbolsServerUsed = _moduleFileFinder.IsStadiaSymbolsServerUsed;

            var loadSymbolsTask = _cancelableTaskFactory.Create(
                "Loading symbols...",
                task =>
                {
                    return _attachedProgram.LoadModuleFilesAsync(
                        inclusionSettings, isSymbolServerEnabled, isStadiaSymbolsServerUsed, task,
                        _moduleFileLoadRecorderFactory.Create(action));
                });

            var completed = loadSymbolsTask.RunAndRecord(action);
            if (!completed)
            {
                // The operation was cancelled by the user, the task doesn't have a Result.
                return VSConstants.E_ABORT;
            }

            if (loadSymbolsTask.Result.SuggestToEnableSymbolStore &&
                _vsiService.Options.ShowSuggestionToEnableSymbolsServer != ShowOption.NeverShow &&
                !isStadiaSymbolsServerUsed)
            {
                bool showAgain = _dialogUtil.ShowOkNoMoreWithDocumentationDisplayWarning(
                    ErrorStrings.SymbolStoreEnableSuggestion,
                    ErrorStrings.SymbolStoreEnableDocumentationLink,
                    ErrorStrings.SymbolStoreEnableDocumentationText, new[]
                    {
                        "Tools", "Options", "Stadia SDK", OptionPageGrid.LldbDebugger,
                        OptionPageGrid.SuggestToEnableSymbolStore
                    });

                if (!showAgain)
                {
                    _vsiService.Options.HideSuggestionToEnableSymbolsServer();
                }
            }

            return loadSymbolsTask.Result.ResultCode;
        }

        public override int SetJustMyCodeState(int fUpdate, uint dwModules,
                                               JMC_CODE_SPEC[] rgJmcSpec)
        {
            return VSConstants.E_NOTIMPL;
        }

        public override int SetEngineGuidImpl(Guid guidEngine)
        {
            Debug.Assert(guidEngine == YetiConstants.DebugEngineGuid);
            return VSConstants.S_OK;
        }

        public override int SetAllExceptions(enum_EXCEPTION_STATE dwState)
        {
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IDebugEngineLaunch2 functions

        public override int CanTerminateProcess(IDebugProcess2 process)
        {
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Launches a process in a suspended state to begin debugging.
        /// For our remote debugging, we must launch what is necessary and
        /// set up the connection.
        /// </summary>
        public override int LaunchSuspended(string server, IDebugPort2 port,
                                            string executableFullPath, string args, string dir,
                                            string env, string options,
                                            enum_LAUNCH_FLAGS launchFlags, uint stdInput,
                                            uint stdOutput, uint stdError,
                                            IDebugEventCallback2 callback,
                                            out IDebugProcess2 process)
        {
            process = null;
            if (_attachedProgram != null)
            {
                Trace.WriteLine("Unable to launch while another program is attached");
                return VSConstants.E_FAIL;
            }

            callback = _debugEventCallbackDecorator(callback);

            _executableFullPath = executableFullPath;
            _executableFileName = Path.GetFileName(executableFullPath);

            _workingDirectory = dir;
            if (!string.IsNullOrEmpty(options))
            {
                var parameters = JsonConvert.DeserializeObject<Params>(options);
                _coreFilePath = parameters.CoreFilePath;
                _debugSessionMetrics.DebugSessionId = parameters.DebugSessionId;
                _deleteCoreFileAtCleanup = parameters.DeleteCoreFile;
                if (parameters.TargetIp != null)
                {
                    _target = new SshTarget(parameters.TargetIp);
                }
            }
            else
            {
                // This case handles launch of dump file from double-click or from
                // File -> Open menu in Visual Studio.
                _coreFilePath = executableFullPath;
                _debugSessionMetrics.UseNewDebugSessionId();
                _deleteCoreFileAtCleanup = false;
            }

            if (!LaunchLldbDebuggerInBackground(_coreFilePath))
            {
                Trace.WriteLine("Aborting launch because the user canceled it.");
                return VSConstants.E_FAIL;
            }

            ChromeClientsLauncher chromeLauncher;
            if (string.IsNullOrEmpty(args))
            {
                chromeLauncher = null;
            }
            else
            {
                try
                {
                    chromeLauncher = _testClientLauncherFactory.Create(args);
                }
                catch (SerializationException e)
                {
                    Trace.WriteLine($"Failed to parse launch arguments: {e.Demystify()}");
                    return VSConstants.E_FAIL;
                }
            }

            // Only start Chrome Client in the launch case, and not when attaching to a core.
            if (string.IsNullOrEmpty(_coreFilePath))
            {
                if (chromeLauncher == null)
                {
                    Trace.WriteLine("Chrome Client parameters have not been supplied");
                    return VSConstants.E_FAIL;
                }

                _rgpEnabled = chromeLauncher.LaunchParams.Rgp;
                _diveEnabled = chromeLauncher.LaunchParams.Dive;
                _renderDocEnabled = chromeLauncher.LaunchParams.RenderDoc;

                LaunchGame(chromeLauncher);
                if (_vsiGameLaunch == null)
                {
                    return VSConstants.E_FAIL;
                }
            }

            var pid = new AD_PROCESS_ID
            {
                ProcessIdType = (int) enum_AD_PROCESS_ID.AD_PROCESS_ID_GUID,
                guidProcessId = Guid.NewGuid()
            };
            if (port.GetProcess(pid, out process) != 0)
            {
                Trace.WriteLine("Launch failed. Could not get a process from the port supplier");
                return VSConstants.E_FAIL;
            }

            return VSConstants.S_OK;
        }

        HashSet<string> GetLLDBSearchPaths(string coreFilePath)
        {
            var libPaths = new HashSet<string>(SDKUtil.GetLibraryPaths());

            if (!string.IsNullOrEmpty(coreFilePath))
            {
                libPaths.Add(Path.GetDirectoryName(coreFilePath));
            }

            // Add search paths for all open projects.
            foreach (ISolutionExplorerProject project in _solutionExplorer.EnumerateProjects())
            {
                string outputDirectory = project.OutputDirectory;
                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    libPaths.Add(outputDirectory);
                }

                string targetDirectory = project.TargetDirectory;
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    libPaths.Add(targetDirectory);
                }
            }

            foreach (string path in libPaths)
            {
                Trace.WriteLine($"Adding LLDB search path: {path}");
            }

            return libPaths;
        }

        bool LaunchLldbDebuggerInBackground(string coreFilePath = "")
        {
            _libPaths = GetLLDBSearchPaths(coreFilePath);
            bool isCoreDumpAttach = !string.IsNullOrEmpty(coreFilePath);

            // This should take less then 100ms.
            // However if GRPC server will be blocked during start this will give partners
            // way to cancel it.
            ICancelableTask<YetiDebugTransport.GrpcSession> startGrpcTask =
                _cancelableTaskFactory.Create(TaskMessages.LaunchingGrpcServer,
                                              _ => _yetiTransport.StartGrpcServer());
            if (!startGrpcTask.Run())
            {
                return false;
            }

            _grpcSession = startGrpcTask.Result;
            _lldbDebuggerCreator = _taskContext.Factory.RunAsync(async () =>
            {
                return await Task.Run(() =>
                {
                    return
                        _stadiaLldbDebuggerFactory
                            .Create(
                                _grpcSession
                                    .GrpcConnection,
                                _debuggerOptions,
                                _libPaths,
                                _executableFullPath,
                                isCoreDumpAttach);
                });
            });

            return true;
        }

        // _vsiGameLaunch will only be non-null when VS manages the launch. This only
        // applies to the flow with the Launch API and it must be "Debug" flow
        // (not Attach to Process / Attach to Stadia Crash Dump).
        void LaunchGame(IChromeClientsLauncher chromeClientsLauncher)
        {
            _launchParams = chromeClientsLauncher.LaunchParams;
            _vsiGameLaunch = _gameLauncher.CreateLaunch(chromeClientsLauncher.LaunchParams);
            if (_vsiGameLaunch != null)
            {
                chromeClientsLauncher.MaybeLaunchChrome(_vsiGameLaunch.LaunchName,
                                                        _vsiGameLaunch.LaunchId, _workingDirectory);
            }
        }

        /// <summary>
        /// Meant to resume the process launched by LaunchSuspended.
        /// Actual resumption of the process may only be possible after events have completed,
        /// and so would occur in ContinueFromSynchronousEvent or elsewhere.
        /// Creates and provides the SDM with a program node; the SDM then calls Attach,
        /// sending the events marking the debug engine's initialization.
        /// </summary>
        public override int ResumeProcess(IDebugProcess2 process)
        {
            _taskContext.ThrowIfNotOnMainThread();

            if (_vsiGameLaunch != null)
            {
                bool status;
                using (new TestBenchmark("WaitUntilGameLaunched", TestBenchmarkScope.Recorder))
                {
                    status = _vsiGameLaunch.WaitUntilGameLaunched();
                }

                if (!status)
                {
                    Trace.WriteLine("Resume failed due to failed launch.");
                    return VSConstants.E_ABORT;
                }
            }

            if (process.GetPort(out IDebugPort2 port) != 0)
            {
                Trace.WriteLine("Resume failed. Could not get the port supplier from the process");
                return VSConstants.E_FAIL;
            }

            IDebugDefaultPort2 defaultPort = (IDebugDefaultPort2) port;
            if (defaultPort == null)
            {
                Trace.WriteLine("Resume failed. Could not get the default port supplier");
                return VSConstants.E_FAIL;
            }

            if (defaultPort.GetPortNotify(out IDebugPortNotify2 portNotify) != 0)
            {
                Trace.WriteLine("Resume failed. Could not get the port notifier");
                return VSConstants.E_FAIL;
            }

            // The SDM will call Attach. Attach sends EngineCreate and ProgramCreate events.
            // When Program Create event is finished, the process is actually resumed.
            using (new TestBenchmark("AddProgramNode", TestBenchmarkScope.Recorder))
            {
                if (portNotify.AddProgramNode(new DebugProgramNode(_taskContext, process)) !=
                    VSConstants.S_OK)
                {
                    Trace.WriteLine("Resume failed. Could not add a program node");
                    return VSConstants.E_FAIL;
                }
            }

            // If the attach returned an error, ensure we also return an error here or else the SDM
            // is left in a bad state.
            if (_attachedProgram == null)
            {
                return VSConstants.E_ABORT;
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Used to terminate the process launched by LaunchSuspended.
        /// Other classes' Terminate calls may occur beforehand.
        /// </summary>
        public override int TerminateProcess(IDebugProcess2 process)
        {
            if (_launchOption != LaunchOption.AttachToCore)
            {
                return VSConstants.S_OK;
            }

            _attachedProgram?.Abort(ExitInfo.Normal(ExitReason.DebuggerTerminated));
            return VSConstants.S_OK;
        }

        #endregion

        uint? GetProcessId(IDebugProcess2 process)
        {
            _taskContext.ThrowIfNotOnMainThread();

            var pid = new AD_PROCESS_ID[1];
            if (process.GetPhysicalProcessId(pid) != VSConstants.S_OK || pid[0].ProcessIdType !=
                (uint) enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM)
            {
                return null;
            }

            return pid[0].dwProcessId;
        }

        void StopTransportAndCleanup(ExitInfo exitInfo)
        {
            exitInfo.IfError(_exitDialogUtil.ShowExitDialog);
            _yetiTransport.Stop(exitInfo.ExitReason);
            _symbolServerHttpClient?.Dispose();
            if (_launchOption == LaunchOption.AttachToCore && File.Exists(_coreFilePath) &&
                _deleteCoreFileAtCleanup)
            {
                // There's a race between yetiTransport.Stop() and File.Delete() so we try a few
                // times before giving up.
                var timer = new Stopwatch();
                timer.Start();
                while (File.Exists(_coreFilePath))
                {
                    if (timer.Elapsed > _deleteTimeout)
                    {
                        Trace.WriteLine($"Warning: Unable to delete {_coreFilePath}");
                        break;
                    }

                    try
                    {
                        File.Delete(_coreFilePath);
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        Trace.WriteLine($"Warning: Deletion failed with exception: {e.Message}");
                        Trace.WriteLine($"Retrying in {_deleteTimeout.TotalSeconds} seconds.");
                    }

                    Thread.Sleep((int) _deleteTimeout.TotalMilliseconds);
                }
            }
        }

        void Abort(Exception e)
        {
            _attachOperation?.Abort(e);
            // This method can be called on a thread that isn't the main thread which means that
            // technically the attachedProgram can be null when we enter this method. The use
            // of the null conditional operator here is thread-safe so if the attached program
            // becomes null at any point we are covered ((internal)).
            _attachedProgram?.Abort(ExitInfo.Error(e));
        }

        /// <summary>
        /// Destroy the program and cleanup all resources.
        /// </summary>
        void EndDebugSession(ExitInfo exitInfo)
        {
            RaiseSessionEnding(new EventArgs());

            if (_attachedProgram == null)
            {
                return;
            }

            // The flush of the metrics that are batched at this point doesn't guarantee that all
            // the events are sent. Some end of session debug events could be ignored ((internal)).
            _debugEventRecorder.Flush();
            _expressionEvaluationRecorder.Flush();

            _attachedProgram.Stop();
            _sessionNotifier.NotifySessionStopped(new SessionStoppedEventArgs(_attachedProgram));
            StopTransportAndCleanup(exitInfo);
            RecordDebugEnd(exitInfo);
            _attachedProgram = null;


            if (_extensionOptions != null)
            {
                _extensionOptions.PropertyChanged -= OptionsGrid_PropertyChanged;
            }

            if (_debuggerOptions != null)
            {
                _debuggerOptions.ValueChanged -= OnDebuggerOptionChanged;
            }

            if (_natvisLogger != null && _natvisLogListener != null)
            {
                _natvisLogger.NatvisLogEvent -= _natvisLogListener.OnNatvisLogEvent;
            }

            RaiseSessionEnded(new EventArgs());
        }

        /// <summary>
        /// Record that the debug session ended, including the reason and any errors.
        /// </summary>
        /// <remarks>Only called if debug session has started successfully.</remarks>
        /// <param name="exitInfo">details about why the session ended</param>
        void RecordDebugEnd(ExitInfo exitInfo)
        {
            exitInfo.HandleResult(onNormal: reason =>
                                  {
                                      DebugSessionEndData.Types.EndReason exitReason =
                                          MapExitReason(reason);
                                      DeveloperLogEvent endData =
                                          CreateDebugSessionEndData(exitReason);
                                      _actionRecorder.RecordSuccess(ActionType.DebugEnd, endData);

                                      if (_vsiGameLaunch != null)
                                      {
                                          SafeErrorUtil.SafelyLogErrorAndForget(
                                              _vsiGameLaunch.WaitForGameLaunchEndedAndRecordAsync,
                                              "Failed to retrieve game launch end status.");
                                      }
                                  },
                                  onError: ex => _actionRecorder.RecordFailure(
                                      ActionType.DebugEnd, ex,
                                      CreateDebugSessionEndData(
                                          DebugSessionEndData.Types.EndReason.DebuggerError)));
        }

        static DeveloperLogEvent CreateDebugSessionEndData(
            DebugSessionEndData.Types.EndReason reason) =>
            new DeveloperLogEvent
            {
                DebugSessionEndData = new DebugSessionEndData { EndReason = reason }
            };

        static DebugSessionEndData.Types.EndReason MapExitReason(ExitReason reason)
        {
            switch (reason)
            {
                case ExitReason.DebuggerTerminated:
                    return DebugSessionEndData.Types.EndReason.DebuggerStop;
                case ExitReason.ProcessExited:
                    return DebugSessionEndData.Types.EndReason.ExecutableExit;
                case ExitReason.DebuggerDetached:
                    return DebugSessionEndData.Types.EndReason.DebuggerDetached;
                case ExitReason.ProcessDetached:
                    return DebugSessionEndData.Types.EndReason.ProcessDetached;
                case ExitReason.AttachCanceled:
                // We are not interested in the attach workflow.
                case ExitReason.Unknown:
                default:
                    return DebugSessionEndData.Types.EndReason.UnknownEndReason;
            }
        }

        public class AttachException : Exception, IUserVisibleError
        {
            public int Result { get; }

            public AttachException(int result, string message) : base(message)
            {
                Result = result;
            }

            public AttachException(int result, string message, Exception inner) : base(
                message, inner)
            {
                Result = result;
            }
        }

        public class GameLaunchAttachException : AttachException, IGameLaunchFailError
        {
            public GameLaunchAttachException(int result, string message) : base(result, message)
            {
            }
        }

        void RecordParameters(IAction action, IExtensionOptions extensionOptions,
                              DebuggerOptions.DebuggerOptions debuggerOptions)
        {
            var debugParams = new VSIDebugParameters();
            debugParams.ExtensionOptions.AddRange(BuildExtensionOptions(extensionOptions));
            debugParams.ExperimentalOptions.AddRange(BuildExperimentalOptions(debuggerOptions));

            // Update action with all the parameters.
            action.UpdateEvent(new DeveloperLogEvent { DebugParameters = debugParams });
        }

        IEnumerable<VSIDebugParameters.Types.EnumOption> BuildExtensionOptions(
            IExtensionOptions extensionOptions) => extensionOptions.Options
            // Note that EnumOption can store only enums or bools.
            // To store other types of options, add different option types to the log protos.
            .Where(o => o.Type.IsEnum || o.Type.Equals(typeof(bool))).Select(
                option => new VSIDebugParameters.Types.EnumOption
                {
                    Name = option.Name,
                    Value = Convert.ToUInt32(option.Value),
                    IsDefaultValue = option.IsDefaultValue
                });

        IEnumerable<VSIDebugParameters.Types.EnumOption> BuildExperimentalOptions(
            DebuggerOptions.DebuggerOptions debuggerOptions) => debuggerOptions
            // Note: all these options are enums.
            .Select(option => new VSIDebugParameters.Types.EnumOption
            {
                Name = option.Key.ToString(),
                Value = Convert.ToUInt32(option.Value),
                IsDefaultValue = option.Value.Equals(Defaults[option.Key])
            });
    }
}