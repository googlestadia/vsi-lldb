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
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using YetiCommon;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.CoreDumps;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.DebuggerOptions;
using YetiVSI.GameLaunch;
using YetiVSI.LLDBShell;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSI.Test.TestSupport.DebugEngine;
using YetiVSI.Test.TestSupport.Lldb;
using YetiVSI.Util;
using YetiVSITestsCommon;
using static YetiVSI.DebugEngine.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    /// <summary>
    /// These tests were set up as an integration smoke test that exercises the code and makes few
    /// asserts. The intention is exercise the previously untestable code and set up a fixture
    /// that can act as a launching pad for future integration tests.
    /// </summary>
    [TestFixture]
    class DebugSessionLauncherTests
    {
        const string _gameBinary = "myGame.exe";
        const int _pid = 22;
        const string _gameletIpAddress = "136.112.50.119";
        const int _gameletPort = 44722;
        const string _fastExpressionDisabledErrorMessage =
            "Fast expression evaluation setting was disabled.";

        IDebugEngine3 _debugEngine;
        GrpcConnection _grpcConnection;
        YetiVSI.DebuggerOptions.DebuggerOptions _debuggerOptions;
        HashSet<string> _libPaths;
        Guid _programId;
        IGameLauncher _gameLauncher;

        IDebugProcess2 _process;
        ICancelable _task;
        IDebugEventCallback2 _callback;
        GrpcDebuggerFactoryFake _debuggerFactory;
        GrpcPlatformFactoryFake _platformFactory;
        MockFileSystem _fileSystem;

        [SetUp]
        public void SetUp()
        {
            var taskContext = new JoinableTaskContext();
            PipeCallInvokerFactory callInvokerFactory = new PipeCallInvokerFactory();
            PipeCallInvoker callInvoker = callInvokerFactory.Create();
            _grpcConnection = new GrpcConnection(taskContext.Factory, callInvoker);
            _callback = Substitute.For<IDebugEventCallback2>();
            _debuggerOptions = new YetiVSI.DebuggerOptions.DebuggerOptions
                { [DebuggerOption.CLIENT_LOGGING] = DebuggerOptionState.DISABLED };
            _libPaths = new HashSet<string> { "some/path", "some/other/path", _gameBinary };
            _programId = Guid.Empty;
            _task = Substitute.For<ICancelable>();
            _process = new DebugProcessStub(enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM, _pid);
            _fileSystem = new MockFileSystem();
            _debugEngine = Substitute.For<IDebugEngine3>();
            _gameLauncher = Substitute.For<IGameLauncher>();
        }

        [Test]
        public async Task TestAttachToCoreAsync([Values] bool stadiaPlatformAvailable)
        {
            var launcherFactory = CreateLauncherFactory(stadiaPlatformAvailable);
            var launcher = launcherFactory.Create(_debugEngine, LaunchOption.AttachToCore,
                                                  "some/core/path", "", "", _gameLauncher);
            var attachedProgram = await LaunchAsync(launcher);
            Assert.That(attachedProgram, Is.Not.Null);
            Assert.IsFalse(
                _debuggerFactory.Debugger.CommandInterpreter.HandledCommands.Contains(
                    SbDebuggerExtensions.FastExpressionEvaluationCommand),
                _fastExpressionDisabledErrorMessage);
        }

        [Test]
        public async Task TestAttachToGameAsync([Values] bool stadiaPlatformAvailable)
        {
            var launcherFactory = CreateLauncherFactory(stadiaPlatformAvailable);
            var launcher = launcherFactory.Create(_debugEngine, LaunchOption.AttachToGame, "",
                                                  _gameBinary, _gameBinary, _gameLauncher);
            var attachedProgram = await LaunchAsync(launcher);
            Assert.That(attachedProgram, Is.Not.Null);
            Assert.IsFalse(
                _debuggerFactory.Debugger.CommandInterpreter.HandledCommands.Contains(
                    SbDebuggerExtensions.FastExpressionEvaluationCommand),
                _fastExpressionDisabledErrorMessage);
        }

        [Test]
        public void TestAttachToGameFail_AnotherTracer()
        {
            string parentPid = "1234";
            string tracerPid = "9876";
            string tracerName = "gdb";

            var launcherFactory = CreateLauncherFactory(false);
            var launcher = launcherFactory.Create(_debugEngine, LaunchOption.AttachToGame, "",
                                                  _gameBinary, _gameBinary, _gameLauncher);
            _debuggerFactory.SetTargetAttachError("Operation not permitted");
            _platformFactory.AddCommandOutput($"cat /proc/{_pid}/status",
                                              "Name:\tgame\n" + $"Pid:\t{_pid}\n" +
                                              $"PPid:\t{parentPid}\n" +
                                              $"TracerPid:\t{tracerPid}\n" + "FDSize:\t256\n");
            _platformFactory.AddCommandOutput($"cat /proc/{tracerPid}/comm", tracerName);

            AttachException e =
                Assert.ThrowsAsync<AttachException>(async () => await LaunchAsync(launcher));
            Assert.That(e.Message,
                        Is.EqualTo(
                            ErrorStrings
                                .FailedToAttachToProcessOtherTracer(tracerName, tracerPid)));
        }

        [Test]
        public void TestAttachToGameFail_SelfTrace()
        {
            string tracerParentPid = "1234";

            var launcherFactory = CreateLauncherFactory(false);
            var launcher = launcherFactory.Create(_debugEngine, LaunchOption.AttachToGame, "",
                                                  _gameBinary, _gameBinary, _gameLauncher);
            _debuggerFactory.SetTargetAttachError("Operation not permitted");
            _platformFactory.AddCommandOutput($"cat /proc/{_pid}/status",
                                              "Name:\tgame\n" + $"Pid:\t{_pid}\n" +
                                              $"PPid:\t{tracerParentPid}\n" +
                                              $"TracerPid:\t{tracerParentPid}\n" +
                                              "FDSize:\t256\n");

            AttachException e =
                Assert.ThrowsAsync<AttachException>(async () => await LaunchAsync(launcher));
            Assert.That(e.Message, Is.EqualTo(ErrorStrings.FailedToAttachToProcessSelfTrace));
        }

        [Test]
        public void TestAttachToGameFail_NoTracer()
        {
            string parentPid = "1234";
            string tracerPid = "0";

            var launcherFactory = CreateLauncherFactory(false);
            var launcher = launcherFactory.Create(_debugEngine, LaunchOption.AttachToGame, "",
                                                  _gameBinary, _gameBinary, _gameLauncher);
            _debuggerFactory.SetTargetAttachError("Operation not permitted");
            _platformFactory.AddCommandOutput($"cat /proc/{_pid}/status",
                                              "Name:\tgame\n" + $"Pid:\t{_pid}\n" +
                                              $"PPid:\t{parentPid}\n" +
                                              $"TracerPid:\t{tracerPid}\n" + "FDSize:\t256\n");

            AttachException e =
                Assert.ThrowsAsync<AttachException>(async () => await LaunchAsync(launcher));
            Assert.That(e.Message,
                        Is.EqualTo(
                            ErrorStrings.FailedToAttachToProcess("Operation not permitted")));
        }

        [Test]
        public void TestAttachToGameFail_CannotGetTracer()
        {
            var launcherFactory = CreateLauncherFactory(false);
            var launcher = launcherFactory.Create(_debugEngine, LaunchOption.AttachToGame, "",
                                                  _gameBinary, _gameBinary, _gameLauncher);
            _debuggerFactory.SetTargetAttachError("Operation not permitted");
            _platformFactory.AddCommandOutput($"cat /proc/{_pid}/status", null);

            AttachException e =
                Assert.ThrowsAsync<AttachException>(async () => await LaunchAsync(launcher));
            Assert.That(e.Message,
                        Is.EqualTo(
                            ErrorStrings.FailedToAttachToProcess("Operation not permitted")));
        }

        [Test]
        public async Task TestLaunchGameAsync([Values] bool stadiaPlatformAvailable)
        {
            var launcherFactory = CreateLauncherFactory(stadiaPlatformAvailable);
            var launcher = launcherFactory.Create(_debugEngine, LaunchOption.LaunchGame, "",
                                                  _gameBinary, _gameBinary, _gameLauncher);
            var attachedProgram = await LaunchAsync(launcher);
            Assert.That(attachedProgram, Is.Not.Null);
            Assert.IsFalse(
                _debuggerFactory.Debugger.CommandInterpreter.HandledCommands.Contains(
                    SbDebuggerExtensions.FastExpressionEvaluationCommand),
                _fastExpressionDisabledErrorMessage);
        }

        [Test]
        public async Task TestInitFilesSourcedAsync([Values] bool stadiaPlatformAvailable)
        {
            // Add ~/.lldbinit file, LLDB debugger should try loading it.
            var lldbinitPath = SbDebuggerExtensions.GetLLDBInitPath();
            _fileSystem.AddFile(lldbinitPath, MockFileData.NullObject);

            var launcherFactory = CreateLauncherFactory(stadiaPlatformAvailable);
            var launcher = launcherFactory.Create(_debugEngine, LaunchOption.AttachToCore,
                                                  "some/core/path", "", "", _gameLauncher);
            var attachedProgram = await LaunchAsync(launcher);
            Assert.IsTrue(_debuggerFactory.Debugger.IsInitFileSourced);
        }

        [Test]
        public async Task TestInitFilesNotSourcedAsync([Values] bool stadiaPlatformAvailable)
        {
            // Ensure that local ~/.lldbinit doesn't exist.
            var lldbinitPath = SbDebuggerExtensions.GetLLDBInitPath();
            Assert.IsFalse(_fileSystem.FileExists(lldbinitPath));

            var launcherFactory = CreateLauncherFactory(stadiaPlatformAvailable);
            var launcher = launcherFactory.Create(_debugEngine, LaunchOption.AttachToCore,
                                                  "some/core/path", "", "", _gameLauncher);
            var attachedProgram = await LaunchAsync(launcher);
            Assert.IsFalse(_debuggerFactory.Debugger.IsInitFileSourced);
        }

        [Test]
        public async Task TestConnectRemoteNotCalledWithAttachToCoreAsync(
            [Values] bool stadiaPlatformAvailable)
        {
            var connectRemoteRecorder = new PlatformFactoryFakeConnectRecorder();
            var launcherFactory =
                CreateLauncherFactory(stadiaPlatformAvailable, connectRemoteRecorder);
            var launcher = launcherFactory.Create(_debugEngine, LaunchOption.AttachToCore,
                                                  "some/core/path", "", "", _gameLauncher);
            await LaunchAsync(launcher);
            Assert.That(connectRemoteRecorder.InvocationCount, Is.EqualTo(0));
        }

        [TestCase(LaunchOption.AttachToGame)]
        [TestCase(LaunchOption.LaunchGame)]
        public async Task TestConnectRemoteCalledForLinuxWithCorrectUrlAsync(
            LaunchOption launchOption)
        {
            var connectRemoteRecorder = new PlatformFactoryFakeConnectRecorder();
            var launcherFactory = CreateLauncherFactory(false, connectRemoteRecorder);
            var launcher = launcherFactory.Create(_debugEngine, launchOption, "", _gameBinary,
                                                  _gameBinary, _gameLauncher);
            await LaunchAsync(launcher);
            Assert.That(connectRemoteRecorder.InvocationCount, Is.EqualTo(1));
            Assert.That(connectRemoteRecorder.InvocationOptions[0].GetUrl(),
                        Is.EqualTo("connect://localhost:10200"));
        }

        [TestCase(LaunchOption.AttachToGame)]
        [TestCase(LaunchOption.LaunchGame)]
        public async Task TestConnectRemoteCalledForStadiaWithScpCommandAsExtraArgAsync(
            LaunchOption launchOption)
        {
            var connectRemoteRecorder = new PlatformFactoryFakeConnectRecorder();
            var launcherFactory = CreateLauncherFactory(true, connectRemoteRecorder);
            var launcher = launcherFactory.Create(_debugEngine, launchOption, "", _gameBinary,
                                                  _gameBinary, _gameLauncher);
            await LaunchAsync(launcher);
            Assert.That(connectRemoteRecorder.InvocationCount, Is.EqualTo(1));
            Assert.That(connectRemoteRecorder.InvocationOptions.Count, Is.EqualTo(1));

            string connectRemoteUrl = connectRemoteRecorder.InvocationOptions[0].GetUrl();

            // argument for ConnectRemote should start with a standard value, ending with ';'
            // used as a separator between it and scp command.
            Assert.That(connectRemoteUrl, Does.StartWith("connect://localhost:10200;"));
            // path to the scp.exe should be quoted, to check this we add \" to the assertion.
            Assert.That(connectRemoteUrl, Does.Contain("scp.exe\""));
            Assert.That(connectRemoteUrl,
                        Does.Contain("-oStrictHostKeyChecking=yes -oUserKnownHostsFile"));
        }

        DebugSessionLauncher.Factory CreateLauncherFactory(bool stadiaPlatformAvailable,
                                                           PlatformFactoryFakeConnectRecorder
                                                               connectRecorder = null)
        {
            _debuggerFactory =
                new GrpcDebuggerFactoryFake(new TimeSpan(0), stadiaPlatformAvailable);
            _platformFactory = new GrpcPlatformFactoryFake(connectRecorder);
            var taskContext = new JoinableTaskContext();
            var listenerFactory = new GrpcListenerFactoryFake();

            // If stadiaPlatformAvailable is True the DebugSessionLauncher will connect
            // to the platform 'remote-stadia', otherwise it will use 'remote-linux'
            var platformName = stadiaPlatformAvailable
                ? "remote-stadia"
                : "remote-linux";
            _platformFactory.AddFakeProcess(platformName, _gameBinary, 44);
            var exceptionManagerFactory =
                new LldbExceptionManager.Factory(new Dictionary<int, Signal>());
            var connectOptionsFactory = new GrpcPlatformConnectOptionsFactory();
            var platformShellCommandFactory = new GrpcPlatformShellCommandFactory();

            var childAdapterFactory = new RemoteValueChildAdapter.Factory();
            var varInfoFactory = new LLDBVariableInformationFactory(childAdapterFactory);

            var taskExecutor = new TaskExecutor(taskContext.Factory);

            var debugCodeContextFactory =
                new DebugCodeContext.Factory(new DebugMemoryContext.Factory());

            var debugDocumentContextFactory = new DebugDocumentContext.Factory();
            var threadsEnumFactory = new ThreadEnumFactory();
            var moduleEnumFactory = new ModuleEnumFactory();
            var frameEnumFactory = new FrameEnumFactory();
            var codeContextEnumFactory = new CodeContextEnumFactory();

            var lldbModuleUtil = new LldbModuleUtil();
            var moduleFileFinder = Substitute.For<IModuleFileFinder>();
            var moduleFileLoadRecorderFactory =
                new ModuleFileLoadMetricsRecorder.Factory(lldbModuleUtil, moduleFileFinder);
            var mockBinaryFileUtil = Substitute.For<IBinaryFileUtil>();
            var lldbShell = Substitute.For<ILLDBShell>();
            var actionRecorder = new ActionRecorder(Substitute.For<IMetrics>());
            var symbolSettingsProvider = Substitute.For<ISymbolSettingsProvider>();
            var mockGameLauncher = Substitute.For<IGameLauncher>();
            mockGameLauncher.LaunchGameApiEnabled.Returns(false);
            var attachedProgramFactory = new LldbAttachedProgram.Factory(
                taskContext, new DebugEngineHandler.Factory(taskContext), taskExecutor,
                new LldbEventManager.Factory(new BoundBreakpointEnumFactory(), taskContext),
                new DebugProgram.Factory(taskContext,
                                         new DebugDisassemblyStream.Factory(
                                             debugCodeContextFactory, debugDocumentContextFactory),
                                         debugDocumentContextFactory, debugCodeContextFactory,
                                         threadsEnumFactory, moduleEnumFactory,
                                         codeContextEnumFactory, mockGameLauncher),
                new DebugModuleCache.Factory(new SynchronousDispatcher()),
                new DebugModule.Factory(
                    FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false),
                    actionRecorder, moduleFileLoadRecorderFactory, lldbModuleUtil,
                    symbolSettingsProvider),
                new DebugThread.Factory(frameEnumFactory, taskExecutor).Create,
                new DebugStackFrame.Factory(debugDocumentContextFactory,
                                            new ChildrenProvider.Factory(), debugCodeContextFactory,
                                            Substitute.For<DebugExpression.Factory>().Create,
                                            varInfoFactory,
                                            new VariableInformationEnum.Factory(taskExecutor),
                                            new RegisterSetsBuilder.Factory(varInfoFactory),
                                            taskExecutor).Create, lldbShell,
                new LldbBreakpointManager.Factory(taskContext,
                                                  new DebugPendingBreakpoint.Factory(
                                                      taskContext,
                                                      new DebugBoundBreakpoint.Factory(
                                                          debugDocumentContextFactory,
                                                          debugCodeContextFactory,
                                                          new DebugBreakpointResolution.Factory()),
                                                      new BreakpointErrorEnumFactory(),
                                                      new BoundBreakpointEnumFactory()),
                                                  new DebugWatchpoint.Factory(
                                                      taskContext,
                                                      new DebugWatchpointResolution.Factory(),
                                                      new BreakpointErrorEnumFactory(),
                                                      new BoundBreakpointEnumFactory())),
                new SymbolLoader.Factory(lldbModuleUtil, mockBinaryFileUtil, moduleFileFinder),
                new BinaryLoader.Factory(lldbModuleUtil, moduleFileFinder),
                Substitute.For<IModuleFileLoaderFactory>());

            var coreAttachWarningDialog = new CoreAttachWarningDialogUtil(
                taskContext, Substitute.For<IDialogUtil>());

            return new DebugSessionLauncher.Factory(taskContext, _debuggerFactory, listenerFactory,
                                                    _platformFactory, connectOptionsFactory,
                                                    platformShellCommandFactory,
                                                    attachedProgramFactory, actionRecorder,
                                                    moduleFileLoadRecorderFactory,
                                                    exceptionManagerFactory, _fileSystem, false,
                                                    moduleFileFinder,
                                                    new DumpModulesProvider(_fileSystem),
                                                    new ModuleSearchLogHolder(),
                                                    symbolSettingsProvider,
                                                    coreAttachWarningDialog);
        }

        Task<ILldbAttachedProgram> LaunchAsync(IDebugSessionLauncher launcher)
        {
            return launcher.LaunchAsync(_task, _process, _programId, _pid, _debuggerOptions,
                                        _libPaths, _grpcConnection, 10200, _gameletIpAddress,
                                        _gameletPort, _callback);
        }
    }
}