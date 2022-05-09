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

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DebuggerApi;
using DebuggerGrpcClient;
using ELFSharp.ELF;
using GgpGrpc.Models;
using Metrics.Shared;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using YetiCommon;
using YetiVSI.DebugEngine.CoreDumps;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;
using static YetiVSI.DebugEngine.DebugEngine;

namespace YetiVSI.DebugEngine
{

    public interface IDebugSessionLauncherFactory
    {
        IDebugSessionLauncher Create(IDebugEngine3 debugEngine, string coreFilePath,
                                     string executableFileName, IVsiGameLaunch gameLaunch);
    }


    public interface IDebugSessionLauncher
    {
        Task<ILldbAttachedProgram> LaunchAsync(ICancelable task, IDebugProcess2 process,
                                               Guid programId, uint? attachPid,
                                               GrpcConnection grpcConnection, int localDebuggerPort,
                                               string targetIpAddress, int targetPort,
                                               LaunchOption launchOption,
                                               IDebugEventCallback2 callback,
                                               StadiaLldbDebugger stadiaDebugger);
    }



    /// <summary>
    /// The debug session launcher establishes a debug session by attaching to either a process
    /// running on a remote machine or to a core file. This code handles 3 use cases:
    /// 1) Attaching to a process that we just launched running on a gamelet.
    /// 2) Attaching to a process that is already running on a gamelet
    ///      (hand selected using a modal dialog).
    /// 3) Attaching to a core file
    /// In the future, these use cases should be supported in a more straightforward way with
    /// several different classes ((internal))
    /// </summary>
    public class DebugSessionLauncher : IDebugSessionLauncher
    {
        public class Factory : IDebugSessionLauncherFactory
        {
            readonly JoinableTaskContext _taskContext;
            readonly ActionRecorder _actionRecorder;
            readonly ModuleFileLoadMetricsRecorder.Factory _moduleFileLoadRecorderFactory;
            readonly ILldbAttachedProgramFactory _attachedProgramFactory;
            readonly GrpcListenerFactory _lldbListenerFactory;
            readonly GrpcPlatformConnectOptionsFactory _lldbPlatformConnectOptionsFactory;
            readonly GrpcPlatformShellCommandFactory _lldbPlatformShellCommandFactory;
            readonly LldbExceptionManager.Factory _exceptionManagerFactory;
            readonly CoreAttachWarningDialogUtil _warningDialog;
            readonly NatvisVisualizerScanner _natvisVisualizerScanner;

            readonly IModuleFileFinder _moduleFileFinder;
            readonly IDumpModulesProvider _dumpModulesProvider;
            readonly IModuleSearchLogHolder _moduleSearchLogHolder;
            readonly ISymbolSettingsProvider _symbolSettingsProvider;

            public Factory(JoinableTaskContext taskContext, GrpcListenerFactory lldbListenerFactory,
                           GrpcPlatformConnectOptionsFactory lldbPlatformConnectOptionsFactory,
                           GrpcPlatformShellCommandFactory lldbPlatformShellCommandFactory,
                           ILldbAttachedProgramFactory attachedProgramFactory,
                           ActionRecorder actionRecorder,
                           ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory,
                           LldbExceptionManager.Factory exceptionManagerFactory,
                           IModuleFileFinder moduleFileFinder,
                           IDumpModulesProvider dumpModulesProvider,
                           IModuleSearchLogHolder moduleSearchLogHolder,
                           ISymbolSettingsProvider symbolSettingsProvider,
                           CoreAttachWarningDialogUtil warningDialog,
                           NatvisVisualizerScanner natvisVisualizerScanner)
            {
                _taskContext = taskContext;
                _lldbListenerFactory = lldbListenerFactory;
                _lldbPlatformConnectOptionsFactory = lldbPlatformConnectOptionsFactory;
                _lldbPlatformShellCommandFactory = lldbPlatformShellCommandFactory;
                _attachedProgramFactory = attachedProgramFactory;
                _actionRecorder = actionRecorder;
                _moduleFileLoadRecorderFactory = moduleFileLoadRecorderFactory;
                _exceptionManagerFactory = exceptionManagerFactory;
                _moduleFileFinder = moduleFileFinder;
                _dumpModulesProvider = dumpModulesProvider;
                _moduleSearchLogHolder = moduleSearchLogHolder;
                _symbolSettingsProvider = symbolSettingsProvider;
                _warningDialog = warningDialog;
                _natvisVisualizerScanner = natvisVisualizerScanner;
            }

            public IDebugSessionLauncher Create(IDebugEngine3 debugEngine, string coreFilePath,
                                                string executableFileName,
                                                IVsiGameLaunch gameLaunch) =>
                new DebugSessionLauncher(_taskContext, _lldbListenerFactory,
                                         _lldbPlatformConnectOptionsFactory,
                                         _lldbPlatformShellCommandFactory, _attachedProgramFactory,
                                         debugEngine, _actionRecorder,
                                         _moduleFileLoadRecorderFactory, coreFilePath,
                                         executableFileName, _exceptionManagerFactory,
                                         _moduleFileFinder, _dumpModulesProvider,
                                         _moduleSearchLogHolder, _symbolSettingsProvider,
                                         _warningDialog, gameLaunch, _natvisVisualizerScanner);
        }

        const string _lldbConnectUrl = "connect://localhost";
        readonly static Regex _clangVersionRegex = new Regex("clang version ([0-9]+)");

        readonly TimeSpan _launchTimeout = TimeSpan.FromSeconds(60);
        readonly TimeSpan _launchRetryDelay = TimeSpan.FromMilliseconds(500);

        readonly JoinableTaskContext _taskContext;
        readonly GrpcListenerFactory _lldbListenerFactory;
        readonly GrpcPlatformConnectOptionsFactory _lldbPlatformConnectOptionsFactory;
        readonly GrpcPlatformShellCommandFactory _lldbPlatformShellCommandFactory;
        readonly ILldbAttachedProgramFactory _attachedProgramFactory;
        readonly IDebugEngine3 _debugEngine;
        readonly LldbExceptionManager.Factory _exceptionManagerFactory;

        readonly ActionRecorder _actionRecorder;
        readonly ModuleFileLoadMetricsRecorder.Factory _moduleFileLoadRecorderFactory;

        readonly string _coreFilePath;
        readonly string _executableFileName;

        readonly IModuleFileFinder _moduleFileFinder;
        readonly IDumpModulesProvider _dumpModulesProvider;
        readonly IModuleSearchLogHolder _moduleSearchLogHolder;
        readonly ISymbolSettingsProvider _symbolSettingsProvider;
        readonly CoreAttachWarningDialogUtil _warningDialog;
        readonly IVsiGameLaunch _gameLaunch;
        readonly NatvisVisualizerScanner _natvisVisualizerScanner;

        public DebugSessionLauncher(
            JoinableTaskContext taskContext, GrpcListenerFactory lldbListenerFactory,
            GrpcPlatformConnectOptionsFactory lldbPlatformConnectOptionsFactory,
            GrpcPlatformShellCommandFactory lldbPlatformShellCommandFactory,
            ILldbAttachedProgramFactory attachedProgramFactory, IDebugEngine3 debugEngine,
            ActionRecorder actionRecorder,
            ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory,
            string coreFilePath, string executableFileName,
            LldbExceptionManager.Factory exceptionManagerFactory,
            IModuleFileFinder moduleFileFinder, IDumpModulesProvider dumpModulesProvider,
            IModuleSearchLogHolder moduleSearchLogHolder,
            ISymbolSettingsProvider symbolSettingsProvider,
            CoreAttachWarningDialogUtil warningDialog, IVsiGameLaunch gameLaunch,
            NatvisVisualizerScanner natvisVisualizerScanner)
        {
            _taskContext = taskContext;
            _lldbListenerFactory = lldbListenerFactory;
            _lldbPlatformConnectOptionsFactory = lldbPlatformConnectOptionsFactory;
            _lldbPlatformShellCommandFactory = lldbPlatformShellCommandFactory;
            _attachedProgramFactory = attachedProgramFactory;
            _debugEngine = debugEngine;
            _exceptionManagerFactory = exceptionManagerFactory;
            _actionRecorder = actionRecorder;
            _moduleFileLoadRecorderFactory = moduleFileLoadRecorderFactory;
            _coreFilePath = coreFilePath;
            _executableFileName = executableFileName;
            _moduleFileFinder = moduleFileFinder;
            _dumpModulesProvider = dumpModulesProvider;
            _moduleSearchLogHolder = moduleSearchLogHolder;
            _symbolSettingsProvider = symbolSettingsProvider;
            _warningDialog = warningDialog;
            _gameLaunch = gameLaunch;
            _natvisVisualizerScanner = natvisVisualizerScanner;
        }

        public async Task<ILldbAttachedProgram> LaunchAsync(
            ICancelable task, IDebugProcess2 process, Guid programId, uint? attachPid,
            GrpcConnection grpcConnection, int localDebuggerPort, string targetIpAddress,
            int targetPort, LaunchOption launchOption, IDebugEventCallback2 callback,
            StadiaLldbDebugger stadiaDebugger)
        {
            Stopwatch launchTimer = Stopwatch.StartNew();
            SbPlatform lldbPlatform = stadiaDebugger.CreatePlatform(grpcConnection);
            if (lldbPlatform == null)
            {
                throw new AttachException(VSConstants.E_FAIL,
                                          ErrorStrings.FailedToCreateLldbPlatform);
            }

            if (launchOption == LaunchOption.Invalid)
            {
                throw new AttachException(VSConstants.E_ABORT, ErrorStrings.InvalidLaunchOption(
                                                                   launchOption.ToString()));
            }

            task.Progress.Report(TaskMessages.DebuggerAttaching);
            if (launchOption == LaunchOption.AttachToGame ||
                launchOption == LaunchOption.LaunchGame)
            {
                ConnectToRemoteProcess(task, localDebuggerPort, targetIpAddress, targetPort,
                                       stadiaDebugger, launchTimer, lldbPlatform);
            }

            task.ThrowIfCancellationRequested();
            stadiaDebugger.Debugger.SetSelectedPlatform(lldbPlatform);

            var lldbListener = CreateListener(grpcConnection);
            // This is required to catch breakpoint change events.
            stadiaDebugger.Target.AddListener(lldbListener, TargetEventType.BREAKPOINT_CHANGED |
                                                                TargetEventType.MODULES_LOADED |
                                                                TargetEventType.MODULES_UNLOADED);
            var listenerSubscriber = new LldbListenerSubscriber(lldbListener);
            LldbFileUpdateListener fileUpdateListener = new LldbFileUpdateListener(
                listenerSubscriber, task);
            listenerSubscriber.Start();
            fileUpdateListener.Subscribe();

            var shouldStopListening = true;
            try
            {
                // Get process ID.
                uint processId = 0;
                switch (launchOption)
                {
                    case LaunchOption.AttachToCore:
                        // No process to attach to, just attach to core and early out.
                        return await AttachToCoreAsync(process, programId, callback, stadiaDebugger,
                                                       listenerSubscriber);
                    case LaunchOption.AttachToGame:
                        processId = GetProcessIdFromAttachPid(attachPid);
                        break;
                    case LaunchOption.LaunchGame:
                        processId = GetProcessIdFromGamelet(task, launchTimer, lldbPlatform,
                                                            processId);
                        break;
                }

                SbProcess debuggerProcess = AttachToProcess(stadiaDebugger, lldbPlatform,
                                                            lldbListener, processId);
                await _taskContext.Factory.SwitchToMainThreadAsync();
                ILldbAttachedProgram attachedProgram = _attachedProgramFactory.Create(
                    process, programId, _debugEngine, callback, stadiaDebugger.Debugger,
                    stadiaDebugger.Target, listenerSubscriber, debuggerProcess,
                    stadiaDebugger.Debugger.GetCommandInterpreter(), false,
                    _exceptionManagerFactory.Create(debuggerProcess), _moduleSearchLogHolder,
                    processId);

                // At this point the executable should always be present locally (i.e. it was found
                // in the search path, or downloaded by LLDB into the module cache) and accessible
                // via SbTarget object.
                // Add compiler specific visualizers and load the Natvis.
                ApplyCompilerSpecificNatvis(stadiaDebugger.Target);
                _natvisVisualizerScanner.SetTarget(stadiaDebugger.Target);
                _natvisVisualizerScanner.Reload();

                shouldStopListening = false;
                return attachedProgram;
            }
            finally
            {
                fileUpdateListener.Unsubscribe();
                if (shouldStopListening)
                {
                    listenerSubscriber.Stop();
                }
            }
        }

        SbListener CreateListener(GrpcConnection grpcConnection)
        {
            var lldbListener = _lldbListenerFactory.Create(grpcConnection, "LLDBWorker Listener");
            if (lldbListener == null)
            {
                throw new AttachException(VSConstants.E_ABORT,
                                          ErrorStrings.FailedToCreateDebugListener);
            }

            return lldbListener;
        }

        SbProcess AttachToProcess(StadiaLldbDebugger stadiaDebugger, SbPlatform lldbPlatform,
                                  SbListener listener, uint processId)
        {
            Trace.WriteLine($"Attaching to pid {processId}");
            IAction debugAttachAction = _actionRecorder.CreateToolAction(ActionType.DebugAttach);
            SbProcess debuggerProcess = null;
            debugAttachAction.Record(() =>
            {
                var moduleFileLoadRecorder =
                    _moduleFileLoadRecorderFactory.Create(debugAttachAction);
                moduleFileLoadRecorder.RecordBeforeLoad(Array.Empty<SbModule>());

                using (new TestBenchmark("AttachToProcessWithID", TestBenchmarkScope.Recorder))
                {
                    debuggerProcess = stadiaDebugger.Target.AttachToProcessWithID(
                        listener, processId, out SbError lldbError);

                    if (lldbError.Fail())
                    {
                        throw new AttachException(
                            VSConstants.E_ABORT,
                            GetLldbAttachErrorDetails(lldbError, lldbPlatform, processId));
                    }
                }

                RecordModules(stadiaDebugger.Target, moduleFileLoadRecorder);
            });
            return debuggerProcess;
        }

        uint GetProcessIdFromGamelet(ICancelable task, Stopwatch launchTimer,
                                     SbPlatform lldbPlatform, uint processId)
        {
            IAction debugWaitAction =
                _actionRecorder.CreateToolAction(ActionType.DebugWaitProcess);

            try
            {
                // Since we have no way of knowing when the remote process actually
                // starts, try a few times to get the pid.
                debugWaitAction.Record(
                    () => RetryWithTimeout(task, () => TryGetRemoteProcessId(lldbPlatform,
                                                                             ref processId,
                                                                             debugWaitAction),
                                           _launchRetryDelay, _launchTimeout, launchTimer));
            }
            catch (TimeoutException e)
            {
                throw new AttachException(VSConstants.E_ABORT,
                                            ErrorStrings.FailedToRetrieveProcessId, e);
            }

            return processId;
        }

        bool TryGetRemoteProcessId(SbPlatform lldbPlatform, ref uint processId,
                                   IAction debugWaitAction)
        {
            if (GetRemoteProcessId(_executableFileName, lldbPlatform, out processId))
            {
                return true;
            }

            VerifyGameIsReady(debugWaitAction);
            return false;
        }

        static uint GetProcessIdFromAttachPid(uint? attachPid)
        {
            if (!attachPid.HasValue)
            {
                throw new AttachException(VSConstants.E_ABORT,
                                          ErrorStrings.FailedToRetrieveProcessId);
            }

            return attachPid.Value;
        }

        async Task<ILldbAttachedProgram> AttachToCoreAsync(
            IDebugProcess2 process, Guid programId, IDebugEventCallback2 callback,
            StadiaLldbDebugger stadiaDebugger, LldbListenerSubscriber listenerSubscriber)
        {
            var loadCoreAction = _actionRecorder.CreateToolAction(ActionType.DebugLoadCore);
            SbProcess lldbDebuggerProcess = null;
            loadCoreAction.Record(() =>
                lldbDebuggerProcess = LoadCore(stadiaDebugger.Target, loadCoreAction));

            await _taskContext.Factory.SwitchToMainThreadAsync();

            // Load custom visualizers after successful attach.
            _natvisVisualizerScanner.Reload();

            return _attachedProgramFactory.Create(
                process, programId, _debugEngine, callback, stadiaDebugger.Debugger,
                stadiaDebugger.Target, listenerSubscriber, lldbDebuggerProcess,
                stadiaDebugger.Debugger.GetCommandInterpreter(), true,
                null, _moduleSearchLogHolder, remotePid: 0);
        }

        void ConnectToRemoteProcess(ICancelable task, int localDebuggerPort, string targetIpAddress,
                                    int targetPort, StadiaLldbDebugger stadiaDebugger,
                                    Stopwatch launchTimer, SbPlatform lldbPlatform)
        {
            task.ThrowIfCancellationRequested();
            Trace.WriteLine("Attempting to connect debugger");
            task.Progress.Report(TaskMessages.DebuggerConnecting);
            SbPlatformConnectOptions lldbConnectOptions = CreatePlatformConnectionOptions(
                localDebuggerPort, targetIpAddress, targetPort, stadiaDebugger);

            IAction debuggerWaitAction =
                _actionRecorder.CreateToolAction(ActionType.DebugWaitDebugger);

            try
            {
                debuggerWaitAction.Record(() => RetryWithTimeout(
                                             task, () => TryConnectRemote(lldbPlatform,
                                                                          lldbConnectOptions,
                                                                          debuggerWaitAction),
                                             _launchRetryDelay, _launchTimeout, launchTimer));
            }
            catch (TimeoutException e)
            {
                throw new AttachException(
                    VSConstants.E_ABORT,
                    ErrorStrings.FailedToConnectDebugger(lldbConnectOptions.GetUrl()), e);
            }
            Trace.WriteLine("LLDB successfully connected");
        }

        bool TryConnectRemote(SbPlatform lldbPlatform, SbPlatformConnectOptions lldbConnectOptions,
                              IAction debugerWaitAction)
        {
            if (lldbPlatform.ConnectRemote(lldbConnectOptions).Success())
            {
                return true;
            }

            VerifyGameIsReady(debugerWaitAction);
            return false;
        }

        SbPlatformConnectOptions CreatePlatformConnectionOptions(
            int localDebuggerPort,string targetIpAddress, int targetPort,
            StadiaLldbDebugger stadiaDebugger)
        {
            string connectRemoteUrl = $"{_lldbConnectUrl}:{localDebuggerPort}";
            string connectRemoteArgument =
                CreateConnectRemoteArgument(connectRemoteUrl, targetIpAddress, targetPort,
                                            stadiaDebugger.IsStadiaPlatformAvailable());

            SbPlatformConnectOptions lldbConnectOptions =
                _lldbPlatformConnectOptionsFactory.Create(connectRemoteArgument);
            return lldbConnectOptions;
        }

        // TODO: remove once the backend bug is fixed.
        /// <summary>
        /// Verifies that the current launch is in RunningGame state, otherwise aborts the attach
        /// process by throwing AttachException.
        /// </summary>
        void VerifyGameIsReady(IAction action)
        {
            // Either new launch api is disabled, or we didn't run the game and only have process
            // id to attach to. We don't match the game launch to the remote process id.
            if (_gameLaunch != null)
            {
                GgpGrpc.Models.GameLaunch state =
                    _taskContext.Factory.Run(async () =>
                                                 await _gameLaunch.GetLaunchStateAsync(action));
                if (state.GameLaunchState != GameLaunchState.RunningGame)
                {
                    var devEvent = new DeveloperLogEvent
                    {
                        GameLaunchData = new GameLaunchData
                        {
                            LaunchId = _gameLaunch.LaunchId
                        }
                    };
                    string error = ErrorStrings.GameNotRunningDuringAttach;
                    if (state.GameLaunchEnded != null)
                    {
                        devEvent.GameLaunchData.EndReason = (int)state.GameLaunchEnded.EndReason;
                        error = LaunchUtils.GetEndReason(state.GameLaunchEnded, state.GameletName);
                    }

                    action.UpdateEvent(devEvent);

                    throw new GameLaunchAttachException(VSConstants.E_FAIL, error);
                }
            }
        }

        string RunShellCommand(string command, SbPlatform platform)
        {
            SbPlatformShellCommand shellCommand = _lldbPlatformShellCommandFactory.Create(command);
            SbError error = platform.Run(shellCommand);
            if (error.Fail())
            {
                return null;
            }

            return shellCommand.GetOutput();
        }

        /// <summary>
        /// Get a more detailed error message if attaching to the remote process failed.
        ///
        /// At the moment, we handle specially only the case when another tracer is attached.
        /// In that case, we detect and show which process is the tracer.
        /// </summary>
        /// <param name="lldbError">Error object from Lldb attach.</param>
        /// <param name="platform">Lldb platform, used to run shell commands.</param>
        /// <param name="processId">Process Id of the debuggee.</param>
        /// <returns>Returns the error string.</returns>
        string GetLldbAttachErrorDetails(SbError lldbError, SbPlatform platform, uint processId)
        {
            string lldbMessageWhenAlreadyTraced = "Operation not permitted";

            // Compute the fallback error message.
            string errorString = ErrorStrings.FailedToAttachToProcess(lldbError.GetCString());

            // If the error does not need special handling, just return the default message.
            if (platform == null || lldbError.GetCString() != lldbMessageWhenAlreadyTraced)
            {
                return errorString;
            }

            // Let us detect if there is a debugger already attached and provide a better error
            // message there.
            string output = RunShellCommand($"cat /proc/{processId}/status", platform);
            if (string.IsNullOrEmpty(output))
            {
                return errorString;
            }

            Regex tracerRE = new Regex("[\\r\\n]TracerPid:\\W*([0-9]+)[\\r\\n]");
            Regex parentRE = new Regex("[\\r\\n]PPid:\\W*([0-9]+)[\\r\\n]");
            Regex firstLineRE = new Regex("^([^\\r\\n]*)([\\r\\n]|$)");

            // Find the line with tracer pid in the proc-status file.
            Match tracerMatch = tracerRE.Match(output);
            Match parentMatch = parentRE.Match(output);
            if (!tracerMatch.Success || !parentMatch.Success)
            {
                return errorString;
            }
            string parentPid = parentMatch.Groups[1].Value;
            string tracerPid = tracerMatch.Groups[1].Value;

            // If there was no tracer, just show the default message.
            if (tracerPid == "0")
            {
                return errorString;
            }

            // If the tracer is the parent process, then the debuggee is tracing itself.
            if (tracerPid == parentPid)
            {
                return ErrorStrings.FailedToAttachToProcessSelfTrace;
            }

            // Try to find the tracer in the list of processes and report it in the error message.
            string commOutput = RunShellCommand($"cat /proc/{tracerPid}/comm", platform);
            if (string.IsNullOrEmpty(output))
            {
                return errorString;
            }

            // Get the first line as the process name.
            Match commMatch = firstLineRE.Match(commOutput);
            string tracerName = commMatch.Success ? commMatch.Groups[1].Value : "<unkown>";

            return ErrorStrings.FailedToAttachToProcessOtherTracer(tracerName, tracerPid);
        }

        /// <summary>
        /// If PlatformStadia is available we pass the gamelet's ip and port for an scp
        /// connection in pre-generated scp command (it includes the full path to scp executable
        /// and all other flags).
        /// </summary>
        /// <param name="debuggerUrl">Url for ConnectRemote.</param>
        /// <param name="targetIpAddress">Gamelet ip address.</param>
        /// <param name="targetPort">Gamelet port./</param>
        /// <returns>ConnectRemote url enriched with a
        /// configuration for scp.exe if applicable.</returns>
        string CreateConnectRemoteArgument(string debuggerUrl, string targetIpAddress,
                                           int targetPort, bool stadiaPlatformAvailable)
        {
            if (!stadiaPlatformAvailable)
            {
                return debuggerUrl;
            }

            string executable = Path.Combine(SDKUtil.GetSshPath(), YetiConstants.ScpWinExecutable);
            string sshKeyFilePath = SDKUtil.GetSshKeyFilePath();
            string sshKnownHostFilePath = SDKUtil.GetSshKnownHostsFilePath();

            return
                $"{debuggerUrl};\"{executable}\" -v -T -i \"{sshKeyFilePath}\" -F NUL -P " +
                $"{targetPort} -oStrictHostKeyChecking=yes " +
                $"-oUserKnownHostsFile=\"{sshKnownHostFilePath}\" cloudcast@{targetIpAddress}:";
        }

        SbProcess LoadCore(RemoteTarget lldbTarget, IAction loadCoreAction)
        {
            var moduleFileLoadRecorder = _moduleFileLoadRecorderFactory.Create(loadCoreAction);
            moduleFileLoadRecorder.RecordBeforeLoad(Array.Empty<SbModule>());

            bool isFullDump = _coreFilePath.EndsWith(".core");
            if (isFullDump)
            {
                string combinedPath = _taskContext.Factory.Run(async () =>
                {
                    await _taskContext.Factory.SwitchToMainThreadAsync();
                    _symbolSettingsProvider.GetStorePaths(out string paths, out string cache);
                    return SymbolUtils.GetCombinedLookupPaths(paths, cache);
                });

                _moduleFileFinder.SetSearchPaths(combinedPath);

                TextWriter searchLog = new StringWriter();
                DumpReadResult dump = _dumpModulesProvider.GetModules(_coreFilePath);

                DumpModule[] executableModules =
                    dump.Modules.Where(module => module.IsExecutable).ToArray();
                DumpModule[] nonExecutableModules =
                    dump.Modules.Where(module => !module.IsExecutable).ToArray();

                // We should not pre-load non executable modules if there wasn't any successfully
                // loaded executable modules. Otherwise lldb will not upgrade image list during the
                // attachment to the core dump and will try to resolve the executable on the
                // developer machine. See (internal)
                if (executableModules.Any() &&
                    executableModules.All(module =>
                                              TryPreloadModule(lldbTarget, searchLog, module)))
                {
                    foreach (DumpModule module in nonExecutableModules)
                    {
                        TryPreloadModule(lldbTarget, searchLog, module);
                    }
                }

                if (dump.Warning == DumpReadWarning.None && !executableModules.Any())
                {
                    dump.Warning = DumpReadWarning.ExecutableBuildIdMissing;
                }
                if (dump.Warning != DumpReadWarning.None)
                {
                    var shouldAttach = _taskContext.Factory.Run(
                        async () => await _warningDialog.ShouldAttachToIncosistentCoreFileAsync(
                            dump.Warning));
                    if (!shouldAttach)
                    {
                        throw new CoreAttachStoppedException();
                    }
                }
            }

            SbProcess lldbDebuggerProcess = lldbTarget.LoadCore(_coreFilePath);
            if (lldbDebuggerProcess == null)
            {
                throw new AttachException(VSConstants.E_ABORT,
                                          ErrorStrings.FailedToLoadCore(_coreFilePath));
            }

            RecordModules(lldbTarget, moduleFileLoadRecorder);
            return lldbDebuggerProcess;
        }

        bool TryPreloadModule(RemoteTarget lldbTarget, TextWriter searchLog, DumpModule module)
        {
            string moduleOriginPath = Path.GetDirectoryName(module.Path);
            string moduleName = Path.GetFileName(module.Path);
            string modulePath = _taskContext.Factory.Run(() =>
            {
                return _moduleFileFinder.FindFileAsync(moduleName, module.Id, false, searchLog,
                                                       false);
            });

            if (!string.IsNullOrWhiteSpace(modulePath))
            {
                var sbModule = lldbTarget.AddModule(modulePath, "", module.Id.ToUUIDString());
                Trace.WriteLine($"Full dump load: found module {moduleName} with id {module.Id} " +
                                $"by path {modulePath}. Module preloaded: {sbModule != null}");

                if (sbModule?.SetPlatformFileSpec(moduleOriginPath, moduleName) == true)
                {
                    Trace.WriteLine($"Full dump load: module {moduleName} path set to " +
                                    $"{moduleOriginPath}.");
                    _moduleSearchLogHolder.AppendSearchLog(sbModule, searchLog.ToString());

                    return true;
                }
            }

            return false;
        }

        static void RecordModules(RemoteTarget lldbTarget,
                                  IModuleFileLoadMetricsRecorder moduleFileLoadRecorder)
        {
            var modules = Enumerable.Range(0, lldbTarget.GetNumModules())
                              .Select(lldbTarget.GetModuleAtIndex)
                              .Where(m => m != null)
                              .ToList();
            moduleFileLoadRecorder.RecordAfterLoad(modules);
        }

        bool GetRemoteProcessId(string executable, SbPlatform platform, out uint pid)
        {
            pid = 0;
            string cmd =
                $"for i in /proc/*/cmdline; do if cat $i | tr \"\\0\" \" \" | grep -q \"{executable}\"; then echo \"$i\"; break; fi; done | egrep -o \"[0-9]+\"";
            var shellCommand = _lldbPlatformShellCommandFactory.Create(cmd);
            if (platform == null)
            {
                Trace.WriteLine("Unable to find process, no platform selected");
                return false;
            }
            var error = platform.Run(shellCommand);
            if (error.Fail())
            {
                Trace.WriteLine($"Unable to find process: {error.GetCString()}");
                return false;
            }
            string output = shellCommand.GetOutput();
            if (string.IsNullOrEmpty(output))
            {
                Trace.WriteLine($"Unable to find process '{executable}'");
                return false;
            }
            string[] pids = output.Split(' ');
            if (pids.Length > 1)
            {
                Trace.WriteLine(
                    $"Unable to select process, multiple instances of '{executable}' are running");
                return false;
            }
            if (!uint.TryParse(pids[0], out pid))
            {
                Trace.WriteLine($"Unable to convert pid '{pids[0]}' to int");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Updates Natvis with additional default visualizers depending on how the binary was
        /// built. An example is enabling a workaround visualizer for std::string for binaries
        /// built with Clang version 10 or less, since the default LLDB formatter works as expected
        /// only since Clang version 11.
        /// </summary>
        void ApplyCompilerSpecificNatvis(RemoteTarget target)
        {
            if (target.GetNumModules() == 0)
            {
                return;
            }

            // Get the local executable path from the target modules.
            var executableFile = target.GetModuleAtIndex(0).GetFileSpec();
            var fullExecutablePath =
                Path.Combine(executableFile.GetDirectory(), executableFile.GetFilename());

            if (!ELFReader.TryLoad(fullExecutablePath, out IELF elf))
            {
                Trace.WriteLine($"Unable to read executable in ELF format.");
                return;
            }

            // Check Clang version used to build the exectuable.
            using (elf)
            {
                if (!elf.TryGetSection(".comment", out var commentSection))
                {
                    return;
                }

                try
                {
                    // Encoding of the .comment section is not defined.
                    // Assume UTF-8 and hope for the best.
                    var comment = Encoding.UTF8.GetString(commentSection.GetContents());
                    var clangVersionMatch = _clangVersionRegex.Match(comment);
                    if (!clangVersionMatch.Success || clangVersionMatch.Groups.Count != 2)
                    {
                        return;
                    }

                    // If the binary was built with LLVM version 10 or less, enable workaround
                    // Natvis solution for "std::string".
                    if (int.TryParse(clangVersionMatch.Groups[1].Value, out int clangVersion) &&
                        clangVersion < 11)
                    {
                        _natvisVisualizerScanner.EnableStringVisualizer();
                    }
                }
                catch (ArgumentException ex)
                {
                    Trace.WriteLine($"Error while reading comment section: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Run action repeatedly until it returns true, sleeping for the retry delay between
        /// retry attempts.
        /// </summary>
        /// <remarks>The action is attempted at least once, even if the timeout has been
        /// exceeded.
        /// </remarks>
        /// <exception cref="System.TimeoutException">Thrown when timer elapsed
        /// time exceeds the timeout.
        /// </exception>
        /// <exception  cref="OperationCanceledException">Thrown when the task is canceled while
        /// sleeping.
        /// </exception>
        static void RetryWithTimeout(ICancelable task, Func<bool> action, TimeSpan retryDelay,
                                     TimeSpan timeout, Stopwatch timer)
        {
            while (!action())
            {
                if (timer.Elapsed > timeout)
                {
                    throw new TimeoutException($"Timeout exceeded: {timeout.TotalMilliseconds}ms");
                }
                Trace.WriteLine($"Retrying in {retryDelay.TotalMilliseconds}ms");
                Thread.Sleep((int)retryDelay.TotalMilliseconds);
                task.ThrowIfCancellationRequested();
            }
        }
    }
}
