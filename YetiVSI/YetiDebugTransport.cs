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
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.DebugEngine;
using YetiVSI.DebuggerOptions;
using LaunchOption = YetiVSI.DebugEngine.DebugEngine.LaunchOption;
using Microsoft.VisualStudio.Threading;
using YetiVSI.Util;
using System.Text;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.DebugEngine.Interfaces;

namespace YetiVSI
{
    public class YetiDebugTransportException : Exception, IUserVisibleError
    {
        public string UserDetails => null;

        public YetiDebugTransportException(string message) : base(message)
        {
        }
    }

    // Used by the debug engine in order to initialize Yeti-specific components.
    public class YetiDebugTransport
    {
        // An event indicating that the transprot has stopped.
        public event Action<Exception> OnStop;

        readonly object thisLock = new object();
        readonly JoinableTaskContext taskContext;
        readonly PipeCallInvokerFactory grpcCallInvokerFactory;
        readonly GrpcConnectionFactory grpcConnectionFactory;
        readonly Action onAsyncRpcCompleted;
        readonly LldbTransportSession.Factory transportSessionFactory;
        readonly ManagedProcess.Factory managedProcessFactory;
        readonly IVsOutputWindowPane debugPane;
        readonly IDialogUtil dialogUtil;
        readonly IYetiVSIService yetiVSIService;

        PipeCallInvoker grpcCallInvoker;
        GrpcConnection grpcConnection;
        ITransportSession transportSession;
        ProcessManager processManager;

        public YetiDebugTransport(JoinableTaskContext taskContext,
                                  LldbTransportSession.Factory transportSessionFactory,
                                  PipeCallInvokerFactory grpcCallInvokerFactory,
                                  GrpcConnectionFactory grpcConnectionFactory,
                                  Action onAsyncRpcCompleted,
                                  ManagedProcess.Factory managedProcessFactory,
                                  IDialogUtil dialogUtil, IVsOutputWindow vsOutputWindow,
                                  IYetiVSIService yetiVSIService)
        {
            taskContext.ThrowIfNotOnMainThread();

            this.taskContext = taskContext;
            this.grpcCallInvokerFactory = grpcCallInvokerFactory;
            this.grpcConnectionFactory = grpcConnectionFactory;
            this.onAsyncRpcCompleted = onAsyncRpcCompleted;
            this.managedProcessFactory = managedProcessFactory;
            this.transportSessionFactory = transportSessionFactory;
            this.dialogUtil = dialogUtil;

            Guid debugPaneGuid = VSConstants.GUID_OutWindowDebugPane;
            vsOutputWindow?.GetPane(ref debugPaneGuid, out debugPane);
            this.yetiVSIService = yetiVSIService;
        }

        public void StartPreGame(LaunchOption launchOption, bool rgpEnabled, bool renderdocEnabled,
                                 SshTarget target, out GrpcConnection grpcConnection,
                                 out ITransportSession transportSession)
        {
            lock (thisLock)
            {
                grpcConnection = null;
                this.transportSession = transportSession = transportSessionFactory.Create();
                if (transportSession == null)
                {
                    Trace.WriteLine("Unable to start the debug transport, invalid session.");
                    throw new YetiDebugTransportException(ErrorStrings.FailedToStartTransport);
                }

                if (!LaunchPreGameProcesses(launchOption, rgpEnabled, renderdocEnabled, target))
                {
                    Stop(ExitReason.Unknown);
                    throw new YetiDebugTransportException(
                        "Failed to launch all needed pre-game processes");
                }

                Trace.WriteLine("Started debug transport.  Session ID: " +
                                transportSession.GetSessionId());

                // The grpcConnection is created during the launch of one of the processes.
                grpcConnection = this.grpcConnection;
            }
        }

        public void StartPostGame(LaunchOption launchOption, SshTarget target, uint remotePid)
        {
            lock (thisLock)
            {
                if (!LaunchPostGameProcesses(launchOption, target, remotePid))
                {
                    Stop(ExitReason.Unknown);
                    throw new YetiDebugTransportException(
                        "Failed to launch all needed post-game processes");
                }
            }
        }

        // Exits and disposes of all processes, the TransportSession, and GrpcConnection.
        public void Stop(ExitReason exitReason)
        {
            lock (thisLock)
            {
                // Stop the grpc connection first because it depends on the the grpc server process
                // that is managed by the process manager.
                grpcConnection?.Shutdown();
                grpcConnection = null;
                grpcCallInvoker?.Dispose();
                grpcCallInvoker = null;
                transportSession?.Dispose();
                transportSession = null;
                processManager?.StopAll(exitReason);
                processManager = null;
            }
        }

        void StopWithException(Exception ex)
        {
            // Synchronously pass the execution exception to the on stop handlers before calling
            // stop. This is done because stopping processes can trigger other exceptions and
            // we would like this to be the first one handled.
            OnStop?.Invoke(ex);
            Stop(ExitReason.Unknown);
        }

        // Launches a process, configured using Yeti project properties.
        // If |monitorExit| is true, when the process exits the debugger will be stopped.
        // Throws a ProcessException if the process fails to launch.
        void LaunchYetiProcess(ProcessStartData startData)
        {
            startData.BeforeStart?.Invoke(startData.StartInfo);

            Trace.WriteLine(
                $"Command: {startData.StartInfo.FileName} {startData.StartInfo.WorkingDirectory}");
            Trace.WriteLine($"Working Dir: {startData.StartInfo.WorkingDirectory}");

            var process = managedProcessFactory.Create(startData.StartInfo);

            if (startData.OutputToConsole)
            {
                process.OutputDataReceived += (sender, data) =>
                {
                    if (data != null)
                    {
                        taskContext.Factory.Run(async () =>
                        {
                            await taskContext.Factory.SwitchToMainThreadAsync();
                            debugPane?.OutputString(data.Text + "\n");
                        });
                    }
                };
            }

            if (startData.MonitorExit)
            {
                process.OnExit += processManager.OnExit;
            }

            process.Start();

            startData.AfterStart?.Invoke();

            processManager.AddProcess(process, startData.StopHandler);
        }

        /// <summary>
        /// Launches all needed processes that can be launched before the game.
        /// </summary>
        /// <param name="launchOption">How the game will be launched</param>
        /// <param name="rgpEnabled">Whether RPG is enabled</param>
        /// <param name="renderdocEnabled">Whether Renderdoc is enabled</param>
        /// <param name="target">Remote instance</param>
        /// <returns>
        /// True if all processes launched successfully and false otherwise and we should abort.
        /// </returns>
        bool LaunchPreGameProcesses(LaunchOption launchOption, bool rgpEnabled,
                                    bool renderdocEnabled, SshTarget target)
        {
            var processes = new List<ProcessStartData>();
            if (launchOption == LaunchOption.LaunchGame ||
                launchOption == LaunchOption.AttachToGame)
            {
                processes.Add(CreatePortForwardingProcessStartData(target));
                processes.Add(CreateLldbServerProcessStartData(target));
            }

            if (launchOption == LaunchOption.LaunchGame)
            {
                if (renderdocEnabled)
                {
                    processes.Add(CreateRenderDocPortForwardingProcessStartData(target));
                }

                if (rgpEnabled)
                {
                    processes.Add(CreateRgpPortForwardingProcessStartData(target));
                }
            }

            processes.Add(CreateDebuggerGrpcServerProcessStartData());

            return LaunchProcesses(processes, "pre-game");
        }

        /// <summary>
        /// Launches all needed processes that can be launched after the game.
        /// </summary>
        /// <param name="launchOption">How the game will be launched</param>
        /// <param name="target">Remote instance</param>
        /// <param name="remotePid">Id of the remote process</param>
        /// <returns>
        /// True if all processes launched successfully and false otherwise and we should abort.
        /// </returns>
        bool LaunchPostGameProcesses(LaunchOption launchOption, SshTarget target, uint remotePid)
        {
            var processes = new List<ProcessStartData>();
            if ((launchOption == LaunchOption.LaunchGame ||
                    launchOption == LaunchOption.AttachToGame) &&
                yetiVSIService.Options.CaptureGameOutput)
            {
                processes.Add(CreateTailLogsProcessStartData(target, remotePid));
            }

            return LaunchProcesses(processes, "post-game");
        }

        bool LaunchProcesses(List<ProcessStartData> processes, string name)
        {
            if (processManager == null)
            {
                processManager = new ProcessManager();
                processManager.OnProcessExit += (processName, exitCode) =>
                    StopWithException(new ProcessExecutionException(
                                          $"{processName} exited with exit code {exitCode}",
                                          exitCode));
            }

            foreach (var item in processes)
            {
                try
                {
                    LaunchYetiProcess(item);
                }
                catch (ProcessException e)
                {
                    Trace.WriteLine($"Failed to start {item.Name}: {e.Message}");
                    dialogUtil.ShowError(ErrorStrings.FailedToStartRequiredProcess(e.Message),
                                         e.ToString());
                    return false;
                }
            }

            Trace.WriteLine($"Manager successfully started all {name} processes");
            return true;
        }

        ProcessStartData CreatePortForwardingProcessStartData(SshTarget target)
        {
            var ports = new List<ProcessStartInfoBuilder.PortForwardEntry>()
            {
                new ProcessStartInfoBuilder.PortForwardEntry
                {
                    LocalPort = transportSession.GetLocalDebuggerPort(),
                    RemotePort = transportSession.GetRemoteDebuggerPort(),
                },
                new ProcessStartInfoBuilder.PortForwardEntry
                {
                    // LLDB returns the GDB server port to the LLDB client, so the local
                    // and remote port must match.
                    LocalPort = transportSession.GetReservedLocalAndRemotePort(),
                    RemotePort = transportSession.GetReservedLocalAndRemotePort(),
                }
            };
            var startInfo = ProcessStartInfoBuilder.BuildForSshPortForward(ports, target);
            return new ProcessStartData("lldb port forwarding", startInfo);
        }

        ProcessStartData CreateRenderDocPortForwardingProcessStartData(SshTarget target)
        {
            var ports = new List<ProcessStartInfoBuilder.PortForwardEntry>()
            {
                new ProcessStartInfoBuilder.PortForwardEntry
                {
                    LocalPort = WorkstationPorts.RENDERDOC_LOCAL,
                    RemotePort = WorkstationPorts.RENDERDOC_REMOTE,
                }
            };
            var startInfo = ProcessStartInfoBuilder.BuildForSshPortForward(ports, target);
            return new ProcessStartData("renderdoc port forwarding", startInfo);
        }

        ProcessStartData CreateRgpPortForwardingProcessStartData(SshTarget target)
        {
            var ports = new List<ProcessStartInfoBuilder.PortForwardEntry>()
            {
                new ProcessStartInfoBuilder.PortForwardEntry
                {
                    LocalPort = WorkstationPorts.RGP_LOCAL,
                    RemotePort = WorkstationPorts.RGP_REMOTE,
                }
            };
            var startInfo = ProcessStartInfoBuilder.BuildForSshPortForward(ports, target);
            return new ProcessStartData("rgp port forwarding", startInfo);
        }

        ProcessStartData CreateLldbServerProcessStartData(SshTarget target)
        {
            string lldbServerCommand = string.Format(
                "{0} platform --listen 127.0.0.1:{1} --min-gdbserver-port={2} " +
                "--max-gdbserver-port={3}",
                Path.Combine(YetiConstants.LldbServerLinuxPath,
                             YetiConstants.LldbServerLinuxExecutable),
                transportSession.GetRemoteDebuggerPort(),
                transportSession.GetReservedLocalAndRemotePort(),
                transportSession.GetReservedLocalAndRemotePort() + 1);
            List<string> lldbServerEnvironment = new List<string>();
            if (yetiVSIService.DebuggerOptions[DebuggerOption.SERVER_LOGGING] ==
                DebuggerOptionState.ENABLED)
            {
                string channels = "lldb default:posix default:gdb-remote default";
                // gdb-server.log
                lldbServerEnvironment.Add(
                    "LLDB_DEBUGSERVER_LOG_FILE=/usr/local/cloudcast/log/gdb-server.log");
                lldbServerEnvironment.Add("LLDB_SERVER_LOG_CHANNELS=\\\"" + channels + "\\\"");
                // lldb-server.log
                lldbServerCommand += " --log-file=/usr/local/cloudcast/log/lldb-server.log " +
                    "--log-channels=\\\"" + channels + "\\\"";
            }

            var startInfo = ProcessStartInfoBuilder.BuildForSsh(
                lldbServerCommand, lldbServerEnvironment, target);
            return new ProcessStartData("lldb server", startInfo);
        }

        ProcessStartData CreateTailLogsProcessStartData(SshTarget target, uint remotePid)
        {
            // If the game process exits, give the tail process a chance to shut down gracefully.
            ProcessManager.ProcessStopHandler stopHandler = (process, reason) =>
            {
                // Did the game process, i.e. the process with pid |remotePid|, exit?
                if (reason != ExitReason.ProcessExited &&
                    reason != ExitReason.DebuggerTerminated)
                {
                    Trace.WriteLine("Game process did not exit, won't wait for tail process exit");
                    return;
                }

                // Give it a second to finish.
                bool exited = process.WaitForExit(TimeSpan.FromSeconds(1));

                // Emit a log message to help tracking down issues, just in case.
                Trace.WriteLine($"Tail process {(exited ? "exited" : "did not exit")} gracefully");
            };

            var startInfo = ProcessStartInfoBuilder.BuildForSsh(
                $"tail --pid={remotePid} -n +0 -F -q /var/game/stdout /var/game/stderr",
                new List<string>(), target);
            return new ProcessStartData("output tail", startInfo, monitorExit: false,
                                        outputToConsole: true, stopHandler: stopHandler);
        }

        ProcessStartData CreateDebuggerGrpcServerProcessStartData()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(YetiConstants.RootDir,
                                        YetiConstants.DebuggerGrpcServerExecutable),
            };

            // (internal): grpcCallInvoker must be created right before the process is created!
            // If it is created before the other processes are created, they'll hold on to the
            // pipes and they won't get closed if the GRPC server shuts down.
            Action<ProcessStartInfo> beforeStart = (processStartInfo) =>
            {
                grpcCallInvoker = grpcCallInvokerFactory.Create();
                grpcConnection = grpcConnectionFactory.Create(grpcCallInvoker);
                grpcConnection.RpcException += StopWithException;
                grpcConnection.AsyncRpcCompleted += onAsyncRpcCompleted;

                grpcCallInvoker.GetClientPipeHandles(out string[] inPipeHandles,
                                                     out string[] outPipeHandles);

                // Note: The server's input pipes are the client's output pipes and vice versa.
                processStartInfo.Arguments = $"-i {string.Join(",", outPipeHandles)} " +
                    $"-o {string.Join(",", inPipeHandles)}";
            };

            // Dispose client handles right after start. This is necessary so that pipes are closed
            // when the server exits.
            Action afterStart = () => { grpcCallInvoker.DisposeLocalCopyOfClientPipeHandles(); };

            var rootDir = YetiConstants.RootDir;
#if USE_LOCAL_PYTHON_AND_TOOLCHAIN
            // This is gated by the <DeployPythonAndToolchainDependencies> project setting to speed
            // up the build.
            var pythonRoot = File.ReadAllText(Path.Combine(rootDir, "local_python_dir.txt")).Trim();
            var toolchainDir = File.ReadAllText(Path.Combine(rootDir, "local_toolchain_dir.txt"))
                .Trim();
            var lldbSitePackagesDir = Path.Combine(toolchainDir, "windows", "lib", "site-packages");
            var libLldbDir = Path.Combine(toolchainDir, "windows", "bin");

            // Quick sanity check that all directories exist.
            var notFoundDir = !Directory.Exists(pythonRoot) ? pythonRoot :
                !Directory.Exists(lldbSitePackagesDir) ? lldbSitePackagesDir :
                !Directory.Exists(libLldbDir) ? libLldbDir : null;
            if (!string.IsNullOrEmpty(notFoundDir))
            {
                // Note: This error is only shown to internal VSI devs, not to external devs.
                throw new YetiDebugTransportException(
                    "You have set the <DeployPythonAndToolchainDependencies> project setting to " +
                    $"False to speed up deployment, but the Python/toolchain dir {notFoundDir} " +
                    "moved. Either fix the wrong directory (preferred) or set " +
                    "<DeployPythonAndToolchainDependencies> to False.");
            }
#else
            var pythonRoot = Path.Combine(rootDir, "Python3");
            var lldbSitePackagesDir = Path.Combine(YetiConstants.LldbDir, "site-packages");
            var libLldbDir = Path.Combine(YetiConstants.LldbDir, "bin");
#endif

            // Search paths for dLLs.
            startInfo.Environment["PATH"] = string.Join(";", new string[]
            {
                rootDir,
                pythonRoot,
                libLldbDir
            });

            // Search paths for Python files.
            startInfo.Environment["PYTHONPATH"] = string.Join(";", new string[]
            {
                Path.Combine(pythonRoot, "Lib"),
                lldbSitePackagesDir
            });

            // Do not display console windows when launching processes. In our case,
            // we do not want the scp process to show the console window.
            startInfo.Environment["LLDB_LAUNCH_INFERIORS_WITHOUT_CONSOLE"] = "true";

            Trace.WriteLine($"Starting {startInfo.FileName} with" + Environment.NewLine +
                            $"\tPATH={(startInfo.Environment["PATH"])}" + Environment.NewLine +
                            $"\tPYTHONPATH={(startInfo.Environment["PYTHONPATH"])}");

            // Use the directory with the binary as working directory. Normally it doesn't matter,
            // because the server doesn't load any files via relative paths and the dependent DLLs
            // are available via PATH and PYTHONPATH. However when debugging or just running the
            // extension from Visual Studio the working directory is set to the output build
            // directory. The working directory has precedence for loading DLLs, so liblldb.dll is
            // picked up from the build output directory, NOT the deployed extension.
            startInfo.WorkingDirectory = Path.GetDirectoryName(startInfo.FileName);

            // Uncomment to enable GRPC debug logging for DebuggerGrpcServer.exe.
            // This is currently very verbose, and you will most likely want to restrict logging to
            // a subset.
            //
            //lldbGrpcStartInfo.EnvironmentVariables["GRPC_TRACE"] = "all";
            //lldbGrpcStartInfo.EnvironmentVariables["GRPC_VERBOSITY"] = "DEBUG";
            return new ProcessStartData("lldb grpc server", startInfo, beforeStart: beforeStart,
                                        afterStart: afterStart);
        }

        static void LogCapturedOutput(string streamName, object sender, string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Trace.WriteLine(
                    ((IProcess) sender).ProcessName + " >" + streamName + "> " + message);
            }
        }

        static void LogStdOut(object sender, TextReceivedEventArgs data)
        {
            LogCapturedOutput("stdout", sender, data.Text);
        }

        static void LogStdErr(object sender, TextReceivedEventArgs data)
        {
            LogCapturedOutput("stderr", sender, data.Text);
        }

        class ProcessStartData
        {
            public ProcessStartData(string name, ProcessStartInfo startInfo,
                                    bool monitorExit = true, bool outputToConsole = false,
                                    Action<ProcessStartInfo> beforeStart = null,
                                    Action afterStart = null,
                                    ProcessManager.ProcessStopHandler stopHandler = null)
            {
                Name = name;
                StartInfo = startInfo;
                MonitorExit = monitorExit;
                OutputToConsole = outputToConsole;
                BeforeStart = beforeStart;
                AfterStart = afterStart;
                StopHandler = stopHandler;

                if (OutputToConsole)
                {
                    // Default to UTF8. If this is left null, it will default to the system's current
                    // code page, which is usually something like Windows-1252 and UTF8 won't render.
                    if (StartInfo.StandardOutputEncoding == null)
                    {
                        StartInfo.StandardOutputEncoding = Encoding.UTF8;
                    }

                    if (StartInfo.StandardErrorEncoding == null)
                    {
                        StartInfo.StandardErrorEncoding = Encoding.UTF8;
                    }
                }
            }

            public string Name { get; }

            public ProcessStartInfo StartInfo { get; }

            public bool MonitorExit { get; }

            public bool OutputToConsole { get; }

            /// <summary>
            /// Action called right before the process is started. May modify start info.
            /// </summary>
            public Action<ProcessStartInfo> BeforeStart { get; }

            /// <summary>
            /// Action called right after the process is started.
            /// </summary>
            public Action AfterStart { get; }

            /// <summary>
            /// Handler called when the process manager stops all processes. Gives the
            /// process a chance for custom stop handling based on the exit reason.
            /// If null, the process is killed.
            /// </summary>
            public ProcessManager.ProcessStopHandler StopHandler { get; }
        }
    }
}