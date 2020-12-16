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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.PlatformUI;
using YetiCommon;
using YetiCommon.SSH;
using YetiCommon.VSProject;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.DebuggerOptions;
using YetiVSI.LoadSymbols;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSI.Util;
using static YetiVSI.DebuggerOptions.DebuggerOptions;
using YetiVSI.DebugEngine.CoreDumps;
using YetiVSI.DebugEngine.Interfaces;

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
            IDebugProgramNode2[] rgpProgramNodes, uint celtPrograms, IDebugEventCallback2 pCallback,
            enum_ATTACH_REASON dwReason);
        public abstract int CanTerminateProcess(IDebugProcess2 pProcess);
        public abstract int CauseBreak();
        public abstract int ContinueFromSynchronousEvent(IDebugEvent2 pEvent);
        public abstract int CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest,
            out IDebugPendingBreakpoint2 ppPendingBP);
        public abstract int DestroyProgram(IDebugProgram2 pProgram);
        public abstract int EnumPrograms(out IEnumDebugPrograms2 ppEnum);
        public abstract int GetEngineId(out Guid pguidEngine);
        public abstract int LaunchSuspended(string pszServer, IDebugPort2 pPort, string pszExe,
            string pszArgs, string pszDir, string bstrEnv, string pszOptions,
            enum_LAUNCH_FLAGS dwLaunchFlags, uint hStdInput, uint hStdOutput, uint hStdError,
            IDebugEventCallback2 pCallback, out IDebugProcess2 ppProcess);
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
            uint Flags);
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
            readonly JoinableTaskContext taskContext;
            readonly ServiceManager serviceManager;
            readonly DebugSessionMetrics debugSessionMetrics;
            readonly YetiDebugTransport yetiTransport;
            readonly ActionRecorder actionRecorder;
            readonly HttpClient symbolServerHttpClient;
            readonly ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory;
            readonly IModuleFileFinder moduleFileFinder;
            readonly YetiCommon.ChromeClientLauncher.Factory chromeClientLauncherFactory;
            readonly NatvisExpander _natvisExpander;
            readonly NatvisDiagnosticLogger natvisLogger;
            readonly ExitDialogUtil exitDialogUtil;
            readonly PreflightBinaryChecker preflightBinaryChecker;
            readonly IDebugSessionLauncherFactory debugSessionLauncherFactory;
            readonly Params.Factory paramsFactory;
            readonly IRemoteDeploy remoteDeploy;
            readonly CancelableTask.Factory cancelableTaskFactory;
            readonly IDialogUtil dialogUtil;
            readonly NatvisLoggerOutputWindowListener natvisLogListener;
            readonly ISolutionExplorer solutionExplorer;
            readonly IDebugEngineCommands debugEngineCommands;
            readonly DebugEventCallbackTransform debugEventCallbackDecorator;
            readonly ISymbolSettingsProvider symbolSettingsProvider;
            readonly bool deployLldbServer;

            public Factory(JoinableTaskContext taskContext, ServiceManager serviceManager,
                           DebugSessionMetrics debugSessionMetrics,
                           YetiDebugTransport yetiTransport, ActionRecorder actionRecorder,
                           HttpClient symbolServerHttpClient,
                           ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory,
                           IModuleFileFinder moduleFileFinder,
                           YetiCommon.ChromeClientLauncher.Factory chromeClientLauncherFactory,
                           NatvisExpander natvisExpander, NatvisDiagnosticLogger natvisLogger,
                           ExitDialogUtil exitDialogUtil,
                           PreflightBinaryChecker preflightBinaryChecker,
                           IDebugSessionLauncherFactory debugSessionLauncherFactory,
                           Params.Factory paramsFactory, IRemoteDeploy remoteDeploy,
                           CancelableTask.Factory cancelableTaskFactory, IDialogUtil dialogUtil,
                           NatvisLoggerOutputWindowListener natvisLogListener,
                           ISolutionExplorer solutionExplorer,
                           IDebugEngineCommands debugEngineCommands,
                           DebugEventCallbackTransform debugEventCallbackDecorator,
                           ISymbolSettingsProvider symbolSettingsProvider, bool deployLldbServer)
            {
                this.taskContext = taskContext;
                this.serviceManager = serviceManager;
                this.debugSessionMetrics = debugSessionMetrics;

                this.yetiTransport = yetiTransport;
                this.actionRecorder = actionRecorder;
                this.symbolServerHttpClient = symbolServerHttpClient;
                this.moduleFileLoadRecorderFactory = moduleFileLoadRecorderFactory;
                this.moduleFileFinder = moduleFileFinder;
                this.chromeClientLauncherFactory = chromeClientLauncherFactory;
                this._natvisExpander = natvisExpander;
                this.natvisLogger = natvisLogger;
                this.exitDialogUtil = exitDialogUtil;
                this.preflightBinaryChecker = preflightBinaryChecker;
                this.debugSessionLauncherFactory = debugSessionLauncherFactory;
                this.paramsFactory = paramsFactory;
                this.remoteDeploy = remoteDeploy;
                this.cancelableTaskFactory = cancelableTaskFactory;
                this.dialogUtil = dialogUtil;
                this.natvisLogListener = natvisLogListener;
                this.solutionExplorer = solutionExplorer;
                this.debugEngineCommands = debugEngineCommands;
                this.debugEventCallbackDecorator = debugEventCallbackDecorator;
                this.symbolSettingsProvider = symbolSettingsProvider;
                this.deployLldbServer = deployLldbServer;
            }

            /// <summary>
            /// Creates a new debug engine.
            /// </summary>
            /// <param name="self">The external identity of the debug engine.</param>
            public virtual IGgpDebugEngine Create(IGgpDebugEngine self)
            {
                taskContext.ThrowIfNotOnMainThread();

                var vsiService =
                    (YetiVSIService)serviceManager.RequireGlobalService(typeof(YetiVSIService));
                var envDTEService =
                    (EnvDTE.DTE)serviceManager.GetGlobalService(typeof(EnvDTE.DTE));
                var extensionOptions = vsiService.Options;
                var debuggerOptions = vsiService.DebuggerOptions;
                var sessionNotifier = serviceManager.GetGlobalService(
                    typeof(SSessionNotifier)) as ISessionNotifier;
                return new DebugEngine(
                    self, Guid.NewGuid(), extensionOptions, debuggerOptions, debugSessionMetrics,
                    taskContext, natvisLogListener, solutionExplorer, cancelableTaskFactory,
                    dialogUtil, yetiTransport, actionRecorder, symbolServerHttpClient,
                    moduleFileLoadRecorderFactory, moduleFileFinder, chromeClientLauncherFactory,
                    _natvisExpander, natvisLogger, exitDialogUtil, preflightBinaryChecker,
                    debugSessionLauncherFactory, paramsFactory, remoteDeploy, debugEngineCommands,
                    debugEventCallbackDecorator, envDTEService?.RegistryRoot, sessionNotifier,
                    symbolSettingsProvider, deployLldbServer);
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
            public class Factory
            {
                private ISerializer serializer;

                public Factory(ISerializer serializer)
                {
                    this.serializer = serializer;
                }

                public Params Deserialize(string json)
                {
                    return serializer.Deserialize<Params>(json);
                }

                public string Serialize(Params parameters)
                {
                    return serializer.Serialize(parameters);
                }

                public Params Create()
                {
                    return new Params();
                }
            }

            public string TargetIp { get; set; }
            public string CoreFilePath { get; set; }
            public bool DeleteCoreFile { get; set; }
            public string DebugSessionId { get; set; }
        }

        readonly TimeSpan DeleteTimeout = TimeSpan.FromSeconds(5);

        readonly JoinableTaskContext taskContext;
        readonly NatvisLoggerOutputWindowListener natvisLogListener;
        readonly DebugSessionMetrics debugSessionMetrics;

        readonly CancelableTask.Factory cancelableTaskFactory;
        readonly ISolutionExplorer solutionExplorer;
        readonly YetiDebugTransport yetiTransport;
        readonly IDialogUtil dialogUtil;
        readonly ActionRecorder actionRecorder;
        readonly IExtensionOptions extensionOptions;
        readonly DebuggerOptions.DebuggerOptions debuggerOptions;
        readonly HttpClient symbolServerHttpClient;
        readonly ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory;
        readonly IModuleFileFinder moduleFileFinder;
        readonly YetiCommon.ChromeClientLauncher.Factory chromeClientLauncherFactory;
        readonly NatvisExpander _natvisExpander;
        readonly NatvisDiagnosticLogger natvisLogger;
        readonly ExitDialogUtil exitDialogUtil;
        readonly PreflightBinaryChecker preflightBinaryChecker;
        readonly IDebugSessionLauncherFactory debugSessionLauncherFactory;
        readonly Params.Factory paramsFactory;
        readonly IRemoteDeploy remoteDeploy;
        readonly IDebugEngineCommands debugEngineCommands;
        readonly DebugEventCallbackTransform debugEventCallbackDecorator;
        readonly ISymbolSettingsProvider symbolSettingsProvider;
        readonly bool deployLldbServer;

        // Keep track of the attach operation, so that it can be aborted by transport errors.
        ICancelableTask attachOperation;

        // Variables set during launch and/or attach.
        string executableFileName;
        string executableFullPath;
        bool rgpEnabled;
        bool renderDocEnabled;
        string workingDirectory;
        SshTarget target;
        string coreFilePath;
        LaunchOption launchOption;
        bool deleteCoreFileAtCleanup;

        // Attached program is set after successfully attaching.
        ILldbAttachedProgram attachedProgram;
        // Timer that is started after successfully attaching.
        ITimer attachedTimer;

        ISessionNotifier sessionNotifier;

        public DebugEngine(IGgpDebugEngine self, Guid id, IExtensionOptions extensionOptions,
                           DebuggerOptions.DebuggerOptions debuggerOptions,
                           DebugSessionMetrics debugSessionMetrics, JoinableTaskContext taskContext,
                           NatvisLoggerOutputWindowListener natvisLogListener,
                           ISolutionExplorer solutionExplorer,
                           CancelableTask.Factory cancelableTaskFactory, IDialogUtil dialogUtil,
                           YetiDebugTransport yetiTransport, ActionRecorder actionRecorder,
                           HttpClient symbolServerHttpClient,
                           ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory,
                           IModuleFileFinder moduleFileFinder,
                           YetiCommon.ChromeClientLauncher.Factory chromeClientLauncherFactory,
                           NatvisExpander natvisExpander, NatvisDiagnosticLogger natvisLogger,
                           ExitDialogUtil exitDialogUtil,
                           PreflightBinaryChecker preflightBinaryChecker,
                           IDebugSessionLauncherFactory debugSessionLauncherFactory,
                           Params.Factory paramsFactory, IRemoteDeploy remoteDeploy,
                           IDebugEngineCommands debugEngineCommands,
                           DebugEventCallbackTransform debugEventCallbackDecorator,
                           string vsRegistryRoot, ISessionNotifier sessionNotifier,
                           ISymbolSettingsProvider symbolSettingsProvider, bool deployLldbServer)
            : base(self)
        {
            taskContext.ThrowIfNotOnMainThread();

            launchOption = LaunchOption.Invalid;

            Id = id;

            this.taskContext = taskContext;
            this.natvisLogListener = natvisLogListener;
            this.debugSessionMetrics = debugSessionMetrics;

            this.extensionOptions = extensionOptions;
            this.debuggerOptions = debuggerOptions;

            this.cancelableTaskFactory = cancelableTaskFactory;
            this.solutionExplorer = solutionExplorer;
            this.dialogUtil = dialogUtil;
            this.yetiTransport = yetiTransport;
            this.actionRecorder = actionRecorder;
            this.symbolServerHttpClient = symbolServerHttpClient;
            this.moduleFileLoadRecorderFactory = moduleFileLoadRecorderFactory;
            this.moduleFileFinder = moduleFileFinder;
            this.chromeClientLauncherFactory = chromeClientLauncherFactory;
            this._natvisExpander = natvisExpander;
            this.natvisLogger = natvisLogger;
            this.exitDialogUtil = exitDialogUtil;
            this.preflightBinaryChecker = preflightBinaryChecker;
            this.debugSessionLauncherFactory = debugSessionLauncherFactory;
            this.paramsFactory = paramsFactory;
            this.remoteDeploy = remoteDeploy;
            this.debugEngineCommands = debugEngineCommands;
            this.debugEventCallbackDecorator = debugEventCallbackDecorator;
            this.sessionNotifier = sessionNotifier;
            this.symbolSettingsProvider = symbolSettingsProvider;
            this.deployLldbServer = deployLldbServer;

            // Register observers on long lived objects last so that they don't hold a reference
            // to this if an exception is thrown during construction.
            this.yetiTransport.OnStop += Abort;
            this.extensionOptions.PropertyChanged += OptionsGrid_PropertyChanged;
            this.debuggerOptions.ValueChanged += OnDebuggerOptionChanged;
            if (this.natvisLogger != null && natvisLogListener != null)
            {
                this.natvisLogger.NatvisLogEvent += natvisLogListener.OnNatvisLogEvent;
            }

            Trace.WriteLine("Debug session started.");
            Trace.WriteLine($"Extension version: {Versions.GetExtensionVersion()}");
            Trace.WriteLine($"SDK version: {Versions.GetSdkVersion()}");
            Trace.WriteLine($"VS version: {Versions.GetVsVersion(vsRegistryRoot)}");
        }

        public override Guid Id { get; }

        public override IDebugEngineCommands DebugEngineCommands
        {
            get
            {
                return debugEngineCommands;
            }
        }

        private void OptionsGrid_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OptionPageGrid.NatvisLoggingLevel))
            {
                if (natvisLogger != null)
                {
                    natvisLogger.SetLogLevel(extensionOptions.NatvisLoggingLevel);
                }
            }
        }

        private void OnDebuggerOptionChanged(object sender, ValueChangedEventArgs args)
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
            uint numPrograms, IDebugEventCallback2 callback, enum_ATTACH_REASON reason)
        {
            taskContext.ThrowIfNotOnMainThread();

            callback = debugEventCallbackDecorator(callback);

            if (numPrograms != 1)
            {
                Trace.WriteLine($"Debug Engine failed to attach. Attempted to attach to " +
                    $"{numPrograms} programs; we only support attaching to one.");
                dialogUtil.ShowError(ErrorStrings.SingleProgramExpected);
                return VSConstants.E_INVALIDARG;
            }

            if (reason == enum_ATTACH_REASON.ATTACH_REASON_AUTO)
            {
                Trace.WriteLine("Debug Engine failed to attach. Auto attach is not supported.");
                dialogUtil.ShowError(ErrorStrings.AutoAttachNotSupported);
                return VSConstants.E_NOTIMPL;
            }

            // save the program ID provided to us
            Guid programId;
            IDebugProcess2 process;
            programs[0].GetProgramId(out programId);
            programs[0].GetProcess(out process);

            if (reason == enum_ATTACH_REASON.ATTACH_REASON_USER)
            {
                IDebugPort2 port;
                process.GetPort(out port);
                var debugPort = port as PortSupplier.DebugPort;
                var gamelet = debugPort?.Gamelet;
                if (gamelet != null && !string.IsNullOrEmpty(gamelet.IpAddr))
                {
                    target = new SshTarget(gamelet);
                }
                else
                {
                    Trace.WriteLine("Unable to find Stadia instance.");
                    dialogUtil.ShowError(ErrorStrings.NoGameletsFound);
                    return VSConstants.E_ABORT;
                }
                debugSessionMetrics.DebugSessionId = debugPort?.DebugSessionId;
            }

            if (string.IsNullOrEmpty(debugSessionMetrics.DebugSessionId))
            {
                debugSessionMetrics.UseNewDebugSessionId();
            }

            Trace.WriteLine("Extension Options:");
            foreach (var option in extensionOptions.Options)
            {
                Trace.WriteLine(string.Format("{0}: {1}", option.Name.ToLower(),
                    option.Value.ToString().ToLower()));
            }
            Trace.WriteLine("Debugger Options:");
            foreach (var option in debuggerOptions)
            {
                Trace.WriteLine(string.Format("{0}: {1}", option.Key.ToString().ToLower(),
                    option.Value.ToString().ToLower()));
            }

            var libPaths = GetLldbSearchPaths();
            uint? attachPid = null;

            if (!string.IsNullOrEmpty(coreFilePath))
            {
                launchOption = LaunchOption.AttachToCore;
            }
            else
            {
                if (reason == enum_ATTACH_REASON.ATTACH_REASON_LAUNCH)
                {
                    launchOption = LaunchOption.LaunchGame;
                }
                else if (reason == enum_ATTACH_REASON.ATTACH_REASON_USER)
                {
                    launchOption = LaunchOption.AttachToGame;
                }
            }

            var lldbDeployAction = actionRecorder.CreateToolAction(ActionType.LldbServerDeploy);
            JoinableTask lldbDeployTask = null;
            if (deployLldbServer && launchOption != LaunchOption.AttachToCore)
            {
                lldbDeployTask = taskContext.Factory.RunAsync(async () =>
                {
                    await TaskScheduler.Default;
                    await remoteDeploy.DeployLldbServerAsync(target, lldbDeployAction);
                });
            }

            try
            {
                var preflightCheckAction = actionRecorder.CreateToolAction(
                    ActionType.DebugPreflightBinaryChecks);
                if (launchOption != LaunchOption.AttachToCore)
                {
                    switch (launchOption)
                    {
                        case LaunchOption.LaunchGame:
                            var remoteTargetPath = Path.Combine(YetiConstants.RemoteDeployPath,
                                executableFileName);
                            cancelableTaskFactory.Create(TaskMessages.CheckingBinaries,
                                async _ => await preflightBinaryChecker.
                                    CheckLocalAndRemoteBinaryOnLaunchAsync(libPaths,
                                    executableFileName, target, remoteTargetPath,
                                    preflightCheckAction))
                                    .RunAndRecord(preflightCheckAction);
                            break;
                        case LaunchOption.AttachToGame:
                            attachPid = GetProcessId(process);
                            if (attachPid.HasValue)
                            {
                                cancelableTaskFactory.Create(TaskMessages.CheckingRemoteBinary,
                                    async _ => await preflightBinaryChecker
                                        .CheckRemoteBinaryOnAttachAsync(attachPid.Value, target,
                                            preflightCheckAction))
                                            .RunAndRecord(preflightCheckAction);
                            }
                            else
                            {
                                Trace.WriteLine("Failed to get target process ID; skipping " +
                                    "remote build id check");
                            }
                            break;
                    }
                }
            }
            catch (PreflightBinaryCheckerException e)
            {
                dialogUtil.ShowWarning(e.Message, e.UserDetails);
            }

            // Attaching the debugger is a synchronous operation that runs on a background thread.
            // The user is given a chance to cancel the operation if it takes too long.
            // Before running the operation, we store a reference to it, so that we can cancel it
            // asynchronously if the YetiTransport fails to start.
            int result = VSConstants.S_OK;
            var exitInfo = ExitInfo.Normal(ExitReason.Unknown);
            var startAction = actionRecorder.CreateToolAction(ActionType.DebugStart);
            var attachTask =
                cancelableTaskFactory.Create(TaskMessages.AttachingToProcess, async task => {
                    if (lldbDeployTask != null)
                    {
                        await lldbDeployAction.RecordAsync(lldbDeployTask.JoinAsync());
                    }

                    // Attempt to start the transport. Pass on to the transport if our attach reason
                    // indicates we need to launch the main debugged process ourselves.
                    yetiTransport.StartPreGame(launchOption, rgpEnabled, renderDocEnabled,
                                        target, out GrpcConnection grpcConnection,
                                        out ITransportSession transportSession);

                    SafeErrorUtil.SafelyLogError(
                        () => RecordParameters(startAction, extensionOptions, debuggerOptions),
                        "Recording debugger parameters");
                    var launcher = debugSessionLauncherFactory.Create(
                        Self, launchOption, coreFilePath, executableFileName, executableFullPath);

                    ILldbAttachedProgram program = await launcher.LaunchAsync(
                        task, process, programId, attachPid, debuggerOptions, libPaths,
                        grpcConnection, transportSession?.GetLocalDebuggerPort() ?? 0,
                        target?.IpAddress, target?.Port ?? 0, callback);

                    // Launch processes that need the game process id.
                    yetiTransport.StartPostGame(launchOption, target, program.RemotePid);

                    return program;
                });
            attachOperation = attachTask;
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
                    attachedProgram = attachTask.Result;
                    attachedProgram.Start(Self);
                    attachedTimer = actionRecorder.CreateStartedTimer();
                    sessionNotifier.NotifySessionLaunched(
                        new SessionLaunchedEventArgs(launchOption, attachedProgram));
                    Trace.WriteLine("LLDB successfully attached");
                }
            }
            catch (AttachException e)
            {
                Trace.WriteLine(e.Message);
                exitInfo = ExitInfo.Error(e);
                result = e.Result;
            }
            catch (TaskAbortedException e) when (e.InnerException != null)
            {
                Trace.WriteLine("Aborting attach because the debug session was aborted: "
                    + e.InnerException.ToString());
                exitInfo = ExitInfo.Error(e.InnerException);
                result = VSConstants.E_ABORT;
            }
            catch (YetiDebugTransportException e)
            {
                Trace.WriteLine($"Failed to start debug transport: {e.ToString()}");
                exitInfo = ExitInfo.Error(e);
                result = VSConstants.E_ABORT;
            }
            catch (DeployException e)
            {
                Trace.WriteLine($"Aborted due to DeployException: {e.ToString()}");
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
                if (attachedProgram == null)
                {
                    Trace.WriteLine("Program create failed. There is no attached program.");
                    return VSConstants.E_FAIL;
                }

                if (launchOption == LaunchOption.AttachToCore)
                {
                    attachedProgram.ContinueInBreakMode();
                }
                else
                {
                    attachedProgram.ContinueFromSuspended();
                }

                // Records the time it took from attaching until resuming the process
                // as well as the number of breakpoints that were placed in that time.
                uint numPending = attachedProgram.GetNumPendingBreakpoints();
                uint numBound = attachedProgram.GetNumBoundBreakpoints();
                var bpData = new VSIBoundBreakpointsData
                {
                    NumPendingBreakpoints = (int) numPending,
                    NumBoundBreakpoints = (int) numBound
                };
                SafeErrorUtil.SafelyLogError(
                    () => actionRecorder.RecordToolAction(
                        ActionType.DebugContinueAfterAttach, attachedTimer,
                        new DeveloperLogEvent {BoundBreakpointsData = bpData}),
                    "Recording attach-to-continue time");
                return VSConstants.S_OK;
            }
            else if (evnt is ProgramDestroyEvent)
            {
                var programDestroyEvent = (ProgramDestroyEvent)evnt;
                EndDebugSession(programDestroyEvent.ExitInfo);
            }
            return VSConstants.S_OK;
        }

        public override int CreatePendingBreakpoint(IDebugBreakpointRequest2 breakpointRequest,
            out IDebugPendingBreakpoint2 pendingBreakpoint)
        {
            taskContext.ThrowIfNotOnMainThread();

            if (attachedProgram == null)
            {
                Trace.WriteLine(
                    "Unable to create pending breakpoint, there is no attached program");
                pendingBreakpoint = null;
                return VSConstants.E_FAIL;
            }

            pendingBreakpoint = attachedProgram.CreatePendingBreakpoint(breakpointRequest);
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
            if (attachedProgram == null)
            {
                Trace.WriteLine("WARNING: SetException was called without a program attached. "
                    + "Exception state will not be set.");
                return VSConstants.E_FAIL;
            }
            attachedProgram.SetExceptions(exceptions);
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
                _natvisExpander.VisualizerScanner.LoadFromRegistry(registryRoot);
            }
            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugEngine3 functions

        public override int SetSymbolPath(string searchPath, string cachePath, uint flags)
        {
            taskContext.ThrowIfNotOnMainThread();

            // We are ignoring input params and ask VS Symbols Settings Store for search and cache
            // paths. Otherwise for whatever reason VS tries to set empty search paths when users
            // explicitly ask to load symbols e.g. via symbols settings page in Run mode (works
            // as expected in Attach / Dump debugging).
            symbolSettingsProvider.GetStorePaths(out string storeSearchPaths,
                                                 out string storeCache);
            moduleFileFinder.SetSearchPaths(
                SymbolUtils.GetCombinedLookupPaths(storeSearchPaths, storeCache));
            return VSConstants.S_OK;
        }

        public override int LoadSymbols()
        {
            taskContext.ThrowIfNotOnMainThread();

            if (attachedProgram == null)
            {
                Trace.WriteLine("Load symbols failed. There is no attached program.");
                return VSConstants.E_FAIL;
            }

            // Due to performance implications we do not use symbols servers paths by default,
            // but require to turn on "Enable symbol server support" option in Stadia settings.
            if (!symbolSettingsProvider.IsSymbolServerEnabled)
            {
                return VSConstants.S_OK;
            }

            var action = actionRecorder.CreateToolAction(ActionType.DebugEngineLoadSymbols);

            int result = VSConstants.S_OK;
            var inclusionSettings = symbolSettingsProvider.GetInclusionSettings();
            var loadSymbolsTask = cancelableTaskFactory.Create("Loading symbols...", task => {
                result = attachedProgram.LoadModuleFiles(
                    inclusionSettings, task, moduleFileLoadRecorderFactory.Create(action));
            });
            return loadSymbolsTask.RunAndRecord(action) ? result : VSConstants.E_ABORT;
        }

        public override int SetJustMyCodeState(int fUpdate, uint dwModules,
            JMC_CODE_SPEC[] rgJMCSpec)
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

        // Launches a process in a suspended state to begin debugging.
        // For our remote debugging, we must launch what is necessary and
        // set up the connection.
        public override int LaunchSuspended(string server, IDebugPort2 port,
            string executableFullPath, string args, string dir, string env, string options,
            enum_LAUNCH_FLAGS launchFlags, uint stdInput, uint stdOutput, uint stdError,
            IDebugEventCallback2 callback, out IDebugProcess2 process)
        {
            if (attachedProgram != null)
            {
                Trace.WriteLine("Unable to launch while another program is attached");
                process = null;
                return VSConstants.E_FAIL;
            }
            callback = debugEventCallbackDecorator(callback);

            this.executableFullPath = executableFullPath;
            executableFileName = Path.GetFileName(executableFullPath);
            YetiCommon.ChromeClientLauncher gameLauncher;
            if (string.IsNullOrEmpty(args))
            {
                gameLauncher = null;
            }
            else
            {
                try
                {
                    gameLauncher = chromeClientLauncherFactory.Create(args);
                }
                catch (SerializationException e)
                {
                    Trace.WriteLine(string.Format("Failed to parse launch arguments: {0}", e));
                    process = null;
                    return VSConstants.E_FAIL;
                }
            }

            workingDirectory = dir;
            if (!string.IsNullOrEmpty(options))
            {
                var parameters = paramsFactory.Deserialize(options);
                coreFilePath = parameters.CoreFilePath;
                debugSessionMetrics.DebugSessionId = parameters.DebugSessionId;
                deleteCoreFileAtCleanup = parameters.DeleteCoreFile;
                if (parameters.TargetIp != null)
                {
                    target = new SshTarget(parameters.TargetIp);
                }
            }
            else
            {
                // This case handles launch of dump file from double-click or from
                // File -> Open menu in Visual Studio.
                coreFilePath = executableFullPath;
                debugSessionMetrics.UseNewDebugSessionId();
                deleteCoreFileAtCleanup = false;
            }

            // Only start Chrome Client in the launch case, and not when attaching to a core.
            if (string.IsNullOrEmpty(coreFilePath))
            {
                if (gameLauncher == null)
                {
                    Trace.WriteLine($"Chrome Client parameters have not been supplied");
                    process = null;
                    return VSConstants.E_FAIL;
                }
                rgpEnabled = gameLauncher.LaunchParams.Rgp;
                renderDocEnabled = gameLauncher.LaunchParams.RenderDoc;

                var urlBuildStatus = gameLauncher.BuildLaunchUrl(out string launchUrl);
                // TODO: Currently the status severity can not be higher than Warning.
                // Once the Game Launch Api is used, the Error severity
                // case should be implemented. http://(internal)
                if (urlBuildStatus.IsWarningLevel)
                {
                    MessageDialog.Show(ErrorStrings.Warning, urlBuildStatus.WarningMessage,
                                       MessageDialogCommandSet.Ok);
                }
                else if (!urlBuildStatus.IsOk)
                {
                    throw new NotImplementedException();
                }

                // Start Chrome Client. We are starting it as earlier as possible, so we can
                // initialize the debugger and start the game in parallel.
                gameLauncher.StartChrome(launchUrl, workingDirectory);
            }

            AD_PROCESS_ID pid = new AD_PROCESS_ID();
            pid.ProcessIdType = (int)enum_AD_PROCESS_ID.AD_PROCESS_ID_GUID;
            pid.guidProcessId = Guid.NewGuid();
            if (port.GetProcess(pid, out process) != 0)
            {
                Trace.WriteLine("Launch failed. Could not get a process from the port supplier");
                return VSConstants.E_FAIL;
            }
            return VSConstants.S_OK;
        }

        // Meant to resume the process launched by LaunchSuspended.
        // Actual resumption of the process may only be possible after events have completed,
        // and so would occur in ContinueFromSynchronousEvent or elsewhere.
        // Creates and provides the SDM with a program node; the SDM then calls Attach,
        // sending the events marking the debug engine's initialization.
        public override int ResumeProcess(IDebugProcess2 process)
        {
            taskContext.ThrowIfNotOnMainThread();

            IDebugPort2 port;
            if (process.GetPort(out port) != 0)
            {
                Trace.WriteLine("Resume failed. Could not get the port supplier from the process");
                return VSConstants.E_FAIL;
            }

            IDebugPortNotify2 portNotify;
            IDebugDefaultPort2 defaultPort = (IDebugDefaultPort2)port;
            if (defaultPort == null)
            {
                Trace.WriteLine("Resume failed. Could not get the default port supplier");
                return VSConstants.E_FAIL;
            }
            if (defaultPort.GetPortNotify(out portNotify) != 0)
            {
                Trace.WriteLine("Resume failed. Could not get the port notifier");
                return VSConstants.E_FAIL;
            }

            // The SDM will call Attach. Attach sends EngineCreate and ProgramCreate events.
            // When Program Create event is finished, the process is actually resumed.
            if (portNotify.AddProgramNode(new DebugProgramNode(taskContext, process)) !=
                VSConstants.S_OK)
            {
                Trace.WriteLine("Resume failed. Could not add a program node");
                return VSConstants.E_FAIL;
            }

            // If the attach returned an error, ensure we also return an error here or else the SDM
            // is left in a bad state.
            if (attachedProgram == null)
            {
                return VSConstants.E_ABORT;
            }

            return VSConstants.S_OK;
        }

        // Used to terminate the process launched by LaunchSuspended.
        // Other classes' Terminate calls may occur beforehand.
        public override int TerminateProcess(IDebugProcess2 process)
        {
            if (launchOption != LaunchOption.AttachToCore)
            {
                return VSConstants.S_OK;
            }

            attachedProgram?.Abort(ExitInfo.Normal(ExitReason.DebuggerTerminated));
            return VSConstants.S_OK;
        }

        #endregion

        private uint? GetProcessId(IDebugProcess2 process)
        {
            taskContext.ThrowIfNotOnMainThread();

            AD_PROCESS_ID[] pid = new AD_PROCESS_ID[1];
            if (process.GetPhysicalProcessId(pid) != VSConstants.S_OK ||
                pid[0].ProcessIdType != (uint)enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM)
            {
                return null;
            }
            return pid[0].dwProcessId;
        }

        private void StopTransportAndCleanup(ExitInfo exitInfo)
        {
            exitInfo.IfError(exitDialogUtil.ShowExitDialog);
            yetiTransport.Stop(exitInfo.ExitReason);
            if (symbolServerHttpClient != null)
            {
                symbolServerHttpClient.Dispose();
            }
            if (launchOption == LaunchOption.AttachToCore && File.Exists(coreFilePath) &&
                deleteCoreFileAtCleanup)
            {
                // There's a race between yetiTransport.Stop() and File.Delete() so we try a few
                // times before giving up.
                var timer = new Stopwatch();
                timer.Start();
                while (File.Exists(coreFilePath))
                {
                    if (timer.Elapsed > DeleteTimeout)
                    {
                        Trace.WriteLine($"Warning: Unable to delete {coreFilePath}");
                        break;
                    }
                    try
                    {
                        File.Delete(coreFilePath);
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        Trace.WriteLine($"Warning: Deletion failed with exception: {exception}");
                        Trace.WriteLine($"Retrying in {DeleteTimeout.TotalSeconds} seconds.");
                    }
                    Thread.Sleep((int)DeleteTimeout.TotalMilliseconds);
                }
            }
        }

        private void Abort(Exception e)
        {
            attachOperation?.Abort(e);
            // This method can be called on a thread that isn't the main thread which means that
            // technically the attachedProgram can be null when we enter this method. The use
            // of the null conditional operator here is thread-safe so if the attached program
            // becomes null at any point we are covered ((internal)).
            attachedProgram?.Abort(ExitInfo.Error(e));
        }

        /// <summary>
        /// Destroy the program and cleanup all resources.
        /// </summary>
        private void EndDebugSession(ExitInfo exitInfo)
        {
            RaiseSessionEnding(new EventArgs());

            if (attachedProgram == null)
            {
                return;
            }

            attachedProgram.Stop();
            sessionNotifier.NotifySessionStopped(new SessionStoppedEventArgs(attachedProgram));
            StopTransportAndCleanup(exitInfo);
            RecordDebugEnd(exitInfo);
            attachedProgram = null;


            if (extensionOptions != null)
            {
                extensionOptions.PropertyChanged -= OptionsGrid_PropertyChanged;
            }
            if (debuggerOptions != null)
            {
                debuggerOptions.ValueChanged -= OnDebuggerOptionChanged;
            }

            if (natvisLogger != null && natvisLogListener != null)
            {
                natvisLogger.NatvisLogEvent -= natvisLogListener.OnNatvisLogEvent;
            }
            RaiseSessionEnded(new EventArgs());
        }

        /// <summary>
        /// Record that the debug session ended, including the reason and any errors.
        /// </summary>
        /// <remarks>Only called if debug session has started successfully.</remarks>
        /// <param name="exitInfo">details about why the session ended</param>
        private void RecordDebugEnd(ExitInfo exitInfo)
        {
            exitInfo.HandleResult(
                onNormal: reason => actionRecorder.RecordSuccess(ActionType.DebugEnd,
                    CreateDebugSessionEndData(MapExitReason(reason))),
                onError: ex => actionRecorder.RecordFailure(ActionType.DebugEnd, ex,
                    CreateDebugSessionEndData(
                        DebugSessionEndData.Types.EndReason.DebuggerError)));
        }

        static DeveloperLogEvent CreateDebugSessionEndData(
            DebugSessionEndData.Types.EndReason reason)
        {
            return new DeveloperLogEvent
            {
                DebugSessionEndData = new DebugSessionEndData {EndReason = reason}
            };
        }

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
            public string UserDetails { get { return null; } }

            public int Result { get; private set; }

            public AttachException(int result, string message) : base(message)
            {
                Result = result;
            }

            public AttachException(int result, string message, Exception inner)
                : base(message, inner)
            {
                Result = result;
            }
        }

        // Get the list of search paths that should be passed to LLDB.
        private HashSet<string> GetLldbSearchPaths()
        {
            var libPaths = new HashSet<string>(SDKUtil.GetLibraryPaths());

            if (!string.IsNullOrEmpty(coreFilePath))
            {
                libPaths.Add(Path.GetDirectoryName(coreFilePath));
            }

            // Add search paths for all open projects.
            foreach (var project in solutionExplorer.EnumerateProjects())
            {
                // TODO: Perform proper null checks in VCProjectAdapter instead of
                // catching exceptions at the call site.
                string outputDirectory;
                try
                {
                    outputDirectory = project.OutputDirectory;
                }
                catch (Exception ex) when (
                    ex is Microsoft.VisualStudio.ProjectSystem.ProjectException
                    || ex is NullReferenceException
                    || ex is COMException)
                {
                    Trace.WriteLine("WARNING: Unable to get project output directory." +
                        $"{Environment.NewLine}{ex.ToString()}");
                    outputDirectory = "";
                }
                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    libPaths.Add(outputDirectory);
                }

                string targetDirectory;
                try
                {
                    targetDirectory = project.TargetDirectory;
                }
                catch (Exception ex) when (
                    ex is Microsoft.VisualStudio.ProjectSystem.ProjectException
                    || ex is NullReferenceException
                    || ex is COMException)
                {
                    Trace.WriteLine("WARNING: Unable to get project target directory." +
                        $"{Environment.NewLine}{ex.ToString()}");
                    targetDirectory = "";
                }
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    libPaths.Add(targetDirectory);
                }
            }
            foreach (var path in libPaths)
            {
                Trace.WriteLine("Adding LLDB search path: " + path);
            }
            return libPaths;
        }

        private void RecordParameters(IAction action, IExtensionOptions extensionOptions,
            DebuggerOptions.DebuggerOptions debuggerOptions)
        {
            var debugParams = new VSIDebugParameters();
            debugParams.ExtensionOptions.AddRange(BuildExtensionOptions(extensionOptions));
            debugParams.ExperimentalOptions.AddRange(BuildExperimentalOptions(debuggerOptions));

            // Update action with all the parameters.
            action.UpdateEvent(new DeveloperLogEvent {DebugParameters = debugParams});
        }

        IEnumerable<VSIDebugParameters.Types.EnumOption> BuildExtensionOptions(
            IExtensionOptions extensionOptions)
            => extensionOptions.Options
                // Note that EnumOption can store only enums or bools.
                // To store other types of options, add different option types to the log protos.
                .Where(o => o.Type.IsEnum || o.Type.Equals(typeof(bool)))
                .Select(option =>
                    new VSIDebugParameters.Types.EnumOption
                    {
                        Name = option.Name,
                        Value = Convert.ToUInt32(option.Value),
                        IsDefaultValue = option.IsDefaultValue
                    });

        IEnumerable<VSIDebugParameters.Types.EnumOption> BuildExperimentalOptions(
            DebuggerOptions.DebuggerOptions debuggerOptions)
            => debuggerOptions
                // Note: all these options are enums.
                .Select(option =>
                    new VSIDebugParameters.Types.EnumOption
                    {
                        Name = option.Key.ToString(),
                        Value = Convert.ToUInt32(option.Value),
                        IsDefaultValue = option.Value.Equals(Defaults[option.Key])
                    });
    }
}
