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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DebuggerApi;
using DebuggerGrpcClient;
using GgpGrpc.Models;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using YetiCommon;
using YetiVSI.DebugEngine.CoreDumps;
using YetiVSI.DebuggerOptions;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using static YetiVSI.DebugEngine.DebugEngine;

namespace YetiVSI.DebugEngine
{

    public interface IDebugSessionLauncherFactory
    {
        IDebugSessionLauncher Create(IDebugEngine3 debugEngine, LaunchOption launchOption,
                                     string coreFilePath, string executableFileName,
                                     string executableFullPath, IVsiGameLaunch gameLaunch);
    }


    public interface IDebugSessionLauncher
    {
        Task<ILldbAttachedProgram> LaunchAsync(ICancelable task, IDebugProcess2 process,
                                               Guid programId, uint? attachPid,
                                               DebuggerOptions.DebuggerOptions debuggerOptions,
                                               HashSet<string> libPaths,
                                               GrpcConnection grpcConnection, int localDebuggerPort,
                                               string targetIpAddress, int targetPort,
                                               IDebugEventCallback2 callback);
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
            readonly GrpcDebuggerFactory _lldbDebuggerFactory;
            readonly GrpcListenerFactory _lldbListenerFactory;
            readonly GrpcPlatformConnectOptionsFactory _lldbPlatformConnectOptionsFactory;
            readonly GrpcPlatformFactory _lldbPlatformFactory;
            readonly GrpcPlatformShellCommandFactory _lldbPlatformShellCommandFactory;
            readonly LldbExceptionManager.Factory _exceptionManagerFactory;
            readonly IFileSystem _fileSystem;
            readonly bool _fastExpressionEvaluation;
            readonly CoreAttachWarningDialogUtil _warningDialog;

            readonly IModuleFileFinder _moduleFileFinder;
            readonly IDumpModulesProvider _dumpModulesProvider;
            readonly IModuleSearchLogHolder _moduleSearchLogHolder;
            readonly ISymbolSettingsProvider _symbolSettingsProvider;

            public Factory(JoinableTaskContext taskContext, GrpcDebuggerFactory lldbDebuggerFactory,
                           GrpcListenerFactory lldbListenerFactory,
                           GrpcPlatformFactory lldbPlatformFactory,
                           GrpcPlatformConnectOptionsFactory lldbPlatformConnectOptionsFactory,
                           GrpcPlatformShellCommandFactory lldbPlatformShellCommandFactory,
                           ILldbAttachedProgramFactory attachedProgramFactory,
                           ActionRecorder actionRecorder,
                           ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory,
                           LldbExceptionManager.Factory exceptionManagerFactory,
                           IFileSystem fileSystem, bool fastExpressionEvaluation,
                           IModuleFileFinder moduleFileFinder,
                           IDumpModulesProvider dumpModulesProvider,
                           IModuleSearchLogHolder moduleSearchLogHolder,
                           ISymbolSettingsProvider symbolSettingsProvider,
                           CoreAttachWarningDialogUtil warningDialog)
            {
                _taskContext = taskContext;
                _lldbDebuggerFactory = lldbDebuggerFactory;
                _lldbListenerFactory = lldbListenerFactory;
                _lldbPlatformFactory = lldbPlatformFactory;
                _lldbPlatformConnectOptionsFactory = lldbPlatformConnectOptionsFactory;
                _lldbPlatformShellCommandFactory = lldbPlatformShellCommandFactory;
                _attachedProgramFactory = attachedProgramFactory;
                _actionRecorder = actionRecorder;
                _moduleFileLoadRecorderFactory = moduleFileLoadRecorderFactory;
                _exceptionManagerFactory = exceptionManagerFactory;
                _fileSystem = fileSystem;
                _fastExpressionEvaluation = fastExpressionEvaluation;
                _moduleFileFinder = moduleFileFinder;
                _dumpModulesProvider = dumpModulesProvider;
                _moduleSearchLogHolder = moduleSearchLogHolder;
                _symbolSettingsProvider = symbolSettingsProvider;
                _warningDialog = warningDialog;
            }

            public IDebugSessionLauncher Create(IDebugEngine3 debugEngine,
                                                LaunchOption launchOption, string coreFilePath,
                                                string executableFileName,
                                                string executableFullPath,
                                                IVsiGameLaunch gameLaunch) =>
                new DebugSessionLauncher(_taskContext, _lldbDebuggerFactory, _lldbListenerFactory,
                                         _lldbPlatformFactory, _lldbPlatformConnectOptionsFactory,
                                         _lldbPlatformShellCommandFactory, _attachedProgramFactory,
                                         debugEngine, launchOption, _actionRecorder,
                                         _moduleFileLoadRecorderFactory, coreFilePath,
                                         executableFileName, executableFullPath,
                                         _exceptionManagerFactory, _fileSystem,
                                         _fastExpressionEvaluation, _moduleFileFinder,
                                         _dumpModulesProvider, _moduleSearchLogHolder,
                                         _symbolSettingsProvider, _warningDialog, gameLaunch);
        }

        const string _remoteLldbPlatformName = "remote-stadia";
        const string _fallbackRemoteLldbPlatformName = "remote-linux";
        bool _stadiaPlatformAvailable = false;

        const string _localLldbPlatformName = "host";
        const string _lldbConnectUrl = "connect://localhost";

        readonly TimeSpan _launchTimeout = TimeSpan.FromSeconds(60);
        readonly TimeSpan _launchRetryDelay = TimeSpan.FromMilliseconds(500);

        readonly JoinableTaskContext _taskContext;
        readonly GrpcDebuggerFactory _lldbDebuggerFactory;
        readonly GrpcListenerFactory _lldbListenerFactory;
        readonly GrpcPlatformFactory _lldbPlatformFactory;
        readonly GrpcPlatformConnectOptionsFactory _lldbPlatformConnectOptionsFactory;
        readonly GrpcPlatformShellCommandFactory _lldbPlatformShellCommandFactory;
        readonly ILldbAttachedProgramFactory _attachedProgramFactory;
        readonly IDebugEngine3 _debugEngine;
        readonly LldbExceptionManager.Factory _exceptionManagerFactory;
        readonly IFileSystem _fileSystem;
        readonly bool _fastExpressionEvaluation;

        readonly LaunchOption _launchOption;
        readonly ActionRecorder _actionRecorder;
        readonly ModuleFileLoadMetricsRecorder.Factory _moduleFileLoadRecorderFactory;

        readonly string _coreFilePath;
        readonly string _executableFileName;
        readonly string _executableFullPath;

        readonly IModuleFileFinder _moduleFileFinder;
        readonly IDumpModulesProvider _dumpModulesProvider;
        readonly IModuleSearchLogHolder _moduleSearchLogHolder;
        readonly ISymbolSettingsProvider _symbolSettingsProvider;
        readonly CoreAttachWarningDialogUtil _warningDialog;
        readonly IVsiGameLaunch _gameLaunch;

        public DebugSessionLauncher(
            JoinableTaskContext taskContext, GrpcDebuggerFactory lldbDebuggerFactory,
            GrpcListenerFactory lldbListenerFactory, GrpcPlatformFactory lldbPlatformFactory,
            GrpcPlatformConnectOptionsFactory lldbPlatformConnectOptionsFactory,
            GrpcPlatformShellCommandFactory lldbPlatformShellCommandFactory,
            ILldbAttachedProgramFactory attachedProgramFactory, IDebugEngine3 debugEngine,
            LaunchOption launchOption, ActionRecorder actionRecorder,
            ModuleFileLoadMetricsRecorder.Factory moduleFileLoadRecorderFactory,
            string coreFilePath, string executableFileName, string executableFullPath,
            LldbExceptionManager.Factory exceptionManagerFactory, IFileSystem fileSystem,
            bool fastExpressionEvaluation, IModuleFileFinder moduleFileFinder,
            IDumpModulesProvider dumpModulesProvider, IModuleSearchLogHolder moduleSearchLogHolder,
            ISymbolSettingsProvider symbolSettingsProvider,
            CoreAttachWarningDialogUtil warningDialog, IVsiGameLaunch gameLaunch)
        {
            _taskContext = taskContext;
            _lldbDebuggerFactory = lldbDebuggerFactory;
            _lldbListenerFactory = lldbListenerFactory;
            _lldbPlatformFactory = lldbPlatformFactory;
            _lldbPlatformConnectOptionsFactory = lldbPlatformConnectOptionsFactory;
            _lldbPlatformShellCommandFactory = lldbPlatformShellCommandFactory;
            _attachedProgramFactory = attachedProgramFactory;
            _debugEngine = debugEngine;
            _exceptionManagerFactory = exceptionManagerFactory;
            _fileSystem = fileSystem;
            _fastExpressionEvaluation = fastExpressionEvaluation;
            _launchOption = launchOption;
            _actionRecorder = actionRecorder;
            _moduleFileLoadRecorderFactory = moduleFileLoadRecorderFactory;
            _coreFilePath = coreFilePath;
            _executableFileName = executableFileName;
            _executableFullPath = executableFullPath;
            _moduleFileFinder = moduleFileFinder;
            _dumpModulesProvider = dumpModulesProvider;
            _moduleSearchLogHolder = moduleSearchLogHolder;
            _symbolSettingsProvider = symbolSettingsProvider;
            _warningDialog = warningDialog;
            _gameLaunch = gameLaunch;
        }

        public async Task<ILldbAttachedProgram> LaunchAsync(
            ICancelable task, IDebugProcess2 process, Guid programId, uint? attachPid,
            DebuggerOptions.DebuggerOptions debuggerOptions, HashSet<string> libPaths,
            GrpcConnection grpcConnection, int localDebuggerPort, string targetIpAddress,
            int targetPort, IDebugEventCallback2 callback)
        {
            var launchSucceeded = false;
            Stopwatch launchTimer = Stopwatch.StartNew();


            // This should be the first request to the DebuggerGrpcServer.  Providing a retry wait
            // time allows us to connect to a DebuggerGrpcServer that is slow to start. Note that
            // we postpone sourcing .lldbinit until we are done with our initialization so that
            // the users can override our defaults.
            var lldbDebugger =
                _lldbDebuggerFactory.Create(grpcConnection, false, TimeSpan.FromSeconds(10));

            if (lldbDebugger == null)
            {
                throw new AttachException(VSConstants.E_ABORT, ErrorStrings.FailedToCreateDebugger);
            }

            if (debuggerOptions[DebuggerOption.CLIENT_LOGGING] == DebuggerOptionState.ENABLED)
            {
                lldbDebugger.EnableLog("lldb", new List<string> { "default", "module" });

                // TODO: Disable 'dwarf' logs until we can determine why this
                // causes LLDB to hang.
                // lldbDebugger.EnableLog("dwarf", new List<string> { "default" });
            }

            if (_fastExpressionEvaluation)
            {
                lldbDebugger.EnableFastExpressionEvaluation();
            }

            lldbDebugger.SetDefaultLLDBSettings();

            // Apply .lldbinit after we set our settings so that the user can override our
            // defaults with a custom .lldbinit.
            LoadLocalLldbInit(lldbDebugger);

            // Add exec search paths, so that LLDB can find the executable and any dependent
            // libraries.  If LLDB is able to find the files locally, it won't try to download
            // them from the remote server, saving valuable time on attach.
            foreach (string path in libPaths)
            {
                lldbDebugger.SetLibrarySearchPath(path);
            }

            lldbDebugger.SetAsync(true);

            SbPlatform lldbPlatform;

            switch (_launchOption)
            {
            case LaunchOption.AttachToGame:
            // Fall through.
            case LaunchOption.LaunchGame:
                lldbPlatform = CreateRemotePlatform(grpcConnection, lldbDebugger);
                if (lldbPlatform == null)
                {
                    throw new AttachException(VSConstants.E_FAIL,
                                              ErrorStrings.FailedToCreateLldbPlatform);
                }
                task.ThrowIfCancellationRequested();
                Trace.WriteLine("Attempting to connect debugger");
                task.Progress.Report("Connecting to debugger");

                string connectRemoteUrl = $"{_lldbConnectUrl}:{localDebuggerPort}";
                string connectRemoteArgument =
                    CreateConnectRemoteArgument(connectRemoteUrl, targetIpAddress, targetPort);

                SbPlatformConnectOptions lldbConnectOptions =
                    _lldbPlatformConnectOptionsFactory.Create(connectRemoteArgument);

                IAction debugerWaitAction =
                    _actionRecorder.CreateToolAction(ActionType.DebugWaitDebugger);

                bool TryConnectRemote()
                {
                    if (lldbPlatform.ConnectRemote(lldbConnectOptions).Success())
                    {
                        return true;
                    }

                    VerifyGameIsReady(debugerWaitAction);
                    return false;
                }

                try
                {
                    debugerWaitAction.Record(() => RetryWithTimeout(
                                                 task, TryConnectRemote, _launchRetryDelay,
                                                 _launchTimeout, launchTimer));
                }
                catch (TimeoutException e)
                {
                    throw new AttachException(
                        VSConstants.E_ABORT,
                        ErrorStrings.FailedToConnectDebugger(lldbConnectOptions.GetUrl()), e);
                }
                Trace.WriteLine("LLDB successfully connected");
                break;

            case LaunchOption.AttachToCore:
                lldbPlatform = _lldbPlatformFactory.Create(_localLldbPlatformName, grpcConnection);
                if (lldbPlatform == null)
                {
                    throw new AttachException(VSConstants.E_FAIL,
                                              ErrorStrings.FailedToCreateLldbPlatform);
                }
                break;

            default:
                throw new AttachException(VSConstants.E_ABORT, ErrorStrings.InvalidLaunchOption(
                                                                   _launchOption.ToString()));
            }

            lldbDebugger.SetSelectedPlatform(lldbPlatform);

            task.ThrowIfCancellationRequested();
            task.Progress.Report("Debugger is attaching (this can take a while)");

            RemoteTarget lldbTarget = null;
            if (_launchOption == LaunchOption.LaunchGame &&
                !string.IsNullOrEmpty(_executableFullPath))
            {
                var createExecutableTargetAction =
                    _actionRecorder.CreateToolAction(ActionType.DebugCreateExecutableTarget);
                createExecutableTargetAction.Record(
                    () => lldbTarget = CreateTarget(lldbDebugger, _executableFullPath));
            }
            else
            {
                lldbTarget = CreateTarget(lldbDebugger, "");
            }

            var lldbListener = CreateListener(grpcConnection);
            var listenerSubscriber = new LldbListenerSubscriber(lldbListener);
            var eventHandler = new EventHandler<FileUpdateReceivedEventArgs>(
                (s, e) => ListenerSubscriberOnFileUpdateReceived(task, e));
            listenerSubscriber.FileUpdateReceived += eventHandler;
            listenerSubscriber.Start();

            try
            {
                if (_launchOption == LaunchOption.AttachToCore)
                {
                    var loadCoreAction = _actionRecorder.CreateToolAction(ActionType.DebugLoadCore);
                    SbProcess lldbDebuggerProcess = null;
                    loadCoreAction.Record(() =>
                        lldbDebuggerProcess = LoadCore(lldbTarget, loadCoreAction));

                    await _taskContext.Factory.SwitchToMainThreadAsync();
                    return _attachedProgramFactory.Create(
                        process, programId, _debugEngine, callback, lldbDebugger, lldbTarget,
                        listenerSubscriber, lldbDebuggerProcess,
                        lldbDebugger.GetCommandInterpreter(), true, new NullExceptionManager(),
                        _moduleSearchLogHolder, remotePid: 0);
                }

                // Get process ID.
                uint processId = 0;
                switch (_launchOption)
                {
                case LaunchOption.AttachToGame:
                    if (!attachPid.HasValue)
                    {
                        throw new AttachException(VSConstants.E_ABORT,
                                                  ErrorStrings.FailedToRetrieveProcessId);
                    }

                    processId = attachPid.Value;
                    break;
                case LaunchOption.LaunchGame:
                    // Since we have no way of knowing when the remote process actually
                    // starts, try a few times to get the pid.

                    IAction debugWaitAction =
                        _actionRecorder.CreateToolAction(ActionType.DebugWaitProcess);

                    bool TryGetRemoteProcessId()
                    {
                        if (GetRemoteProcessId(_executableFileName, lldbPlatform, out processId))
                        {
                            return true;
                        }

                        VerifyGameIsReady(debugWaitAction);
                        return false;
                    }

                    try
                    {
                        debugWaitAction.Record(() => RetryWithTimeout(
                                                   task, TryGetRemoteProcessId, _launchRetryDelay,
                                                   _launchTimeout, launchTimer));
                    }
                    catch (TimeoutException e)
                    {
                        throw new AttachException(VSConstants.E_ABORT,
                                                  ErrorStrings.FailedToRetrieveProcessId, e);
                    }

                    break;
                }

                Trace.WriteLine("Attaching to pid " + processId);
                var debugAttachAction = _actionRecorder.CreateToolAction(ActionType.DebugAttach);

                SbProcess debuggerProcess = null;
                debugAttachAction.Record(() => {
                    var moduleFileLoadRecorder =
                        _moduleFileLoadRecorderFactory.Create(debugAttachAction);
                    moduleFileLoadRecorder.RecordBeforeLoad(Array.Empty<SbModule>());

                    debuggerProcess = lldbTarget.AttachToProcessWithID(lldbListener, processId,
                                                                       out SbError lldbError);
                    if (lldbError.Fail())
                    {
                        throw new AttachException(
                            VSConstants.E_ABORT,
                            GetLldbAttachErrorDetails(lldbError, lldbPlatform, processId));
                    }

                    RecordModules(lldbTarget, moduleFileLoadRecorder);
                });

                var exceptionManager = _exceptionManagerFactory.Create(debuggerProcess);

                await _taskContext.Factory.SwitchToMainThreadAsync();
                ILldbAttachedProgram attachedProgram = _attachedProgramFactory.Create(
                    process, programId, _debugEngine, callback, lldbDebugger, lldbTarget,
                    listenerSubscriber, debuggerProcess, lldbDebugger.GetCommandInterpreter(),
                    false, exceptionManager, _moduleSearchLogHolder, processId);
                launchSucceeded = true;
                return attachedProgram;
            }
            finally
            {
                // clean up the SBListener subscriber
                listenerSubscriber.FileUpdateReceived -= eventHandler;
                // stop the SBListener subscriber completely if the game failed to launch
                if (!launchSucceeded)
                {
                    listenerSubscriber.Stop();
                }
            }
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
                        error = LaunchUtils.GetEndReason(state.GameLaunchEnded);
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

        SbPlatform CreateRemotePlatform(GrpcConnection grpcConnection, SbDebugger debugger)
        {
            string platformName;
            if (debugger.IsPlatformAvailable(_remoteLldbPlatformName))
            {
                _stadiaPlatformAvailable = true;
                platformName = _remoteLldbPlatformName;
            }
            else
            {
                platformName = _fallbackRemoteLldbPlatformName;
            }

            return _lldbPlatformFactory.Create(platformName, grpcConnection);
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
                                           int targetPort)
        {
            if (!_stadiaPlatformAvailable)
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
            string modulePath = _moduleFileFinder.FindFile(moduleName, module.Id, false, searchLog);

            if (!string.IsNullOrWhiteSpace(modulePath))
            {
                var sbModule = lldbTarget.AddModule(modulePath, "", module.Id.ToUUIDString());
                Trace.WriteLine($"Full dump load: found module {moduleName} with id {module.Id} " +
                                $"by path {modulePath}. Module preloaded: {sbModule != null}");

                if (sbModule?.SetPlatformFileSpec(moduleOriginPath, moduleName) == true)
                {
                    Trace.WriteLine($"Full dump load: module {moduleName} path set to " +
                                    $"{moduleOriginPath}.");
                    _moduleSearchLogHolder.SetSearchLog(sbModule, searchLog.ToString());

                    return true;
                }
            }

            return false;
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

        void ListenerSubscriberOnFileUpdateReceived(ICancelable task,
                                                    FileUpdateReceivedEventArgs args)
        {
            // Progress.Report uses SynchronizationContext and will take care about UI update.
            var update = args.Update;
            switch (update.Method)
            {
            case FileProcessingState.Read:
                task.Progress.Report(
                    $"Debugger is attaching:{Environment.NewLine}downloading {update.File}" +
                    $" ({ToMegabytes(update.Size):F1} MB)");
                break;
            case FileProcessingState.Close:
                task.Progress.Report("Debugger is attaching: loading modules");
                break;
            }

            double ToMegabytes(uint bytes) => ((double)bytes) / 1048576;
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

        /// <summary>
        /// LoadLocalLldbInit looks for a local LLDB config (~/.lldbinit), logs its contents and
        /// then issues RPCs to load it in LLDB.  Internally LLDB will try to load one of the
        /// following files: ~/.lldbinit-{PROGRAM_NAME}, ~/.lldbinit, {CWD}/.lldbinit (in that
        /// order).  We check only for ~/.lldbinit and don't call `SourceInitFileInHomeDirectory`
        /// if it doesn't exist.
        /// </summary>
        void LoadLocalLldbInit(SbDebugger debugger)
        {
            var lldbinitPath = SbDebuggerExtensions.GetLLDBInitPath();
            string lldbinit;
            try
            {
                lldbinit = _fileSystem.File.ReadAllText(lldbinitPath);
            }
            catch (FileNotFoundException)
            {
                Trace.WriteLine("No local ~/.lldbinit found, don't try loading it in LLDB.");
                return;
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Unexpected error while reading {lldbinitPath}: {e}");
                return;
            }

            Trace.WriteLine($"Found ~/.lldbinit ({lldbinitPath}), LLDB will try to load it:" +
                            $"{Environment.NewLine}{lldbinit}{Environment.NewLine}EOF");

            debugger.SkipLLDBInitFiles(false);
            debugger.GetCommandInterpreter().SourceInitFileInHomeDirectory();
        }

        RemoteTarget CreateTarget(SbDebugger lldbDebugger, string executable)
        {
            RemoteTarget lldbTarget = lldbDebugger.CreateTarget(executable);
            if (lldbTarget == null)
            {
                throw new AttachException(VSConstants.E_ABORT,
                                          ErrorStrings.FailedToCreateDebugTarget);
            }

            return lldbTarget;
        }

        bool GetRemoteProcessId(string executable, SbPlatform platform, out uint pid)
        {
            pid = 0;
            var shellCommand = _lldbPlatformShellCommandFactory.Create($"pidof \"{executable}\"");
            if (platform == null)
            {
                Trace.WriteLine("Unable to find process, no platform selected");
                return false;
            }
            var error = platform.Run(shellCommand);
            if (error.Fail())
            {
                Trace.WriteLine("Unable to find process: " + error.GetCString());
                return false;
            }
            string output = shellCommand.GetOutput();
            if (string.IsNullOrEmpty(output))
            {
                Trace.WriteLine("Unable to find process '" + executable + "'");
                return false;
            }
            string[] pids = output.Split(' ');
            if (pids.Length > 1)
            {
                Trace.WriteLine("Unable to select process, multiple instances of '" + executable +
                                "' are running");
                return false;
            }
            if (!uint.TryParse(pids[0], out pid))
            {
                Trace.WriteLine("Unable to convert pid '" + pids[0] + "' to int");
                return false;
            }
            return true;
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
