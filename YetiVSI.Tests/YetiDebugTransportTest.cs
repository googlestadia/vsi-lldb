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
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Exit;
using LaunchOption = YetiVSI.DebugEngine.DebugEngine.LaunchOption;

namespace YetiVSI.Test
{

    class DisposingList<T> : List<T>, IDisposable where T : IDisposable
    {
        public void Dispose()
        {
            foreach (var item in this)
            {
                item?.Dispose();
            }
        }
    }

    [TestFixture]
    class YetiDebugTransportTest
    {
        const int _numGrpcPipePairs = 4;
        const int _remotePid = 1337;
        const string _targetString = "1.2.3.4:5";
        readonly TimeSpan _tailExitTimeout = TimeSpan.FromSeconds(1);

        // Mocks of dependencies
        JoinableTaskContext taskContext;
        PipeCallInvoker mockGrpcCallInvoker;
        PipeCallInvokerFactory mockGrpcCallInvokerFactory;
        GrpcConnectionFactory mockGrpcConnectionFactory;
        ManagedProcess.Factory mockManagedProcessFactory;
        IYetiVSIService service;
        IExtensionOptions optionPageGrid;
        IDialogUtil mockDialogUtil;

        // Exception get populated with error on abort
        Exception abortError;

        // Code under test
        YetiDebugTransport yetiDebugTransport;

        [SetUp]
        public void SetUp()
        {
            taskContext = new JoinableTaskContext();
            mockManagedProcessFactory = Substitute.For<ManagedProcess.Factory>();
            mockGrpcCallInvoker = Substitute.ForPartsOf<PipeCallInvoker>(_numGrpcPipePairs);
            mockGrpcCallInvokerFactory = Substitute.For<PipeCallInvokerFactory>();
            mockGrpcCallInvokerFactory.Create().Returns(mockGrpcCallInvoker);
            mockGrpcConnectionFactory = Substitute.For<GrpcConnectionFactory>();
            optionPageGrid = Substitute.For<IExtensionOptions>();
            service = new YetiVSIService(optionPageGrid);
            var mockVsOutputWindow = Substitute.For<IVsOutputWindow>();
            mockDialogUtil = Substitute.For<IDialogUtil>();
            yetiDebugTransport = new YetiDebugTransport(taskContext,
                                                        mockGrpcCallInvokerFactory,
                                                        mockGrpcConnectionFactory,
                                                        onAsyncRpcCompleted: null,
                                                        managedProcessFactory:
                                                        mockManagedProcessFactory,
                                                        dialogUtil: mockDialogUtil,
                                                        vsOutputWindow: mockVsOutputWindow,
                                                        yetiVSIService: service);

            abortError = null;
            yetiDebugTransport.OnStop += e => { abortError = e; };
        }

        [Test]
        public void StartPreGameAttachToCoreLocal()
        {
            SshTarget sshTargetNull = null;
            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPreGame(LaunchOption.AttachToCore, false, false, false,
                                            sshTargetNull, session);
            ExpectLocalProcessWithName(YetiConstants.DebuggerGrpcServerExecutable);
            Assert.IsNull(abortError);
        }

        [Test]
        public void StartGrpcServerNoSession()
        {
            using (var sessions = new DisposingList<LldbTransportSession>())
            {
                // Exhaust all available sessions.
                while (true)
                {
                    var session = new LldbTransportSession();
                    if (session.GetSessionId() == LldbTransportSession.INVALID_SESSION_ID)
                    {
                        break;
                    }
                    sessions.Add(session);

                    if (sessions.Count > 32)
                    {
                        Assert.Fail(
                            "There are too many LLDB sessions available, " +
                            "should be no more than ~10");
                    }
                }

                Assert.Throws<YetiDebugTransportException>(
                    () => yetiDebugTransport.StartGrpcServer());

                // Early errors don't cause aborts.
                Assert.IsNull(abortError);
            }
        }

        [Test]
        public void StartPreGameAttach()
        {
            SshTarget sshTarget = new SshTarget(_targetString);
            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPreGame(LaunchOption.AttachToGame, false, false, false,
                                            sshTarget, session);
            ExpectRemoteProcessWithArg("lldb-server", 1);
            ExpectRemoteProcessWithArg("-L", 1);
            ExpectLocalProcessWithName(YetiConstants.DebuggerGrpcServerExecutable);
            Assert.IsNull(abortError);
        }

        enum GrpcState
        {
            Initial,
            PipeCallInvokerCreated,
            DebuggerGrpcServerCreated,
            ClientPipeHandlesReleased
        }

        [Test]
        public void StartPreGameAttachNoGrpcPipeLeak()
        {
            // Verify that things happen in this order:
            // 1) Other processes are started (if any)
            // 2) PipeCallInvoker is created (and the pipes long with it)
            // 3) DebuggerGrpcServer is created
            // 4) Client pipe handles are released
            // 5) Other processes are started (if any)
            // Otherwise, other processes might get a copy of the pipes and cause the VSI to
            // freeze, see (internal).
            GrpcState state = GrpcState.Initial;

            mockGrpcCallInvokerFactory.When(x => x.Create()).Do(x =>
            {
                // 2)
                Assert.That(state, Is.EqualTo(GrpcState.Initial));
                state = GrpcState.PipeCallInvokerCreated;
            });

            mockGrpcCallInvoker.When(x => x.DisposeLocalCopyOfClientPipeHandles()).Do(x =>
            {
                // 4)
                Assert.That(state, Is.EqualTo(GrpcState.DebuggerGrpcServerCreated));
                state = GrpcState.ClientPipeHandlesReleased;
            });

            mockManagedProcessFactory.Create(Arg.Do<ProcessStartInfo>(x =>
            {
                if (Path.GetFileName(x.FileName) == YetiConstants.DebuggerGrpcServerExecutable)
                {
                    // 3)
                    Assert.That(state, Is.EqualTo(GrpcState.PipeCallInvokerCreated));
                    state = GrpcState.DebuggerGrpcServerCreated;
                }
                else
                {
                    // 1) or 5)
                    Assert.IsTrue(state == GrpcState.Initial ||
                                  state == GrpcState.ClientPipeHandlesReleased);
                }
            }));

            SshTarget sshTarget = new SshTarget(_targetString);
            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPreGame(LaunchOption.AttachToGame, true, true, true,
                                            sshTarget, session);

            Assert.That(state, Is.EqualTo(GrpcState.ClientPipeHandlesReleased));
        }

        [Test]
        public void StartPreGameLaunch()
        {
            SshTarget sshTarget = new SshTarget(_targetString);
            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPreGame(LaunchOption.LaunchGame, false, false, false,
                                            sshTarget, session);
            ExpectRemoteProcessWithArg("lldb-server", 1);
            ExpectRemoteProcessWithArg("-L", 1);
            ExpectLocalProcessWithName(YetiConstants.DebuggerGrpcServerExecutable);
            Assert.IsNull(abortError);
        }

        [Test]
        public void StartPreGameLaunchRenderDoc()
        {
            SshTarget sshTarget = new SshTarget(_targetString);

            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPreGame(LaunchOption.LaunchGame, false, false, true,
                                            sshTarget, session);
            ExpectRemoteProcessWithArg("lldb-server", 1);
            ExpectRemoteProcessWithArg("-L", 2);
            ExpectLocalProcessWithName(YetiConstants.DebuggerGrpcServerExecutable);
            Assert.IsNull(abortError);
        }

        [Test]
        public void StartPreGameLaunchRgp()
        {
            SshTarget sshTarget = new SshTarget(_targetString);
            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPreGame(LaunchOption.LaunchGame, true, false, false,
                                            sshTarget, session);
            ExpectRemoteProcessWithArg("lldb-server", 1);
            ExpectRemoteProcessWithArg("-L", 2);
            ExpectRemoteProcessWithArg("-L" + WorkstationPorts.RGP_LOCAL, 1);
            ExpectLocalProcessWithName(YetiConstants.DebuggerGrpcServerExecutable);
            Assert.IsNull(abortError);
        }

        [Test]
        public void StartPreGameLaunchDive()
        {
            SshTarget sshTarget = new SshTarget(_targetString);
            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPreGame(LaunchOption.LaunchGame, false, true, false,
                                            sshTarget, session);
            ExpectRemoteProcessWithArg("lldb-server", 1);
            ExpectRemoteProcessWithArg("-L", 2);
            ExpectRemoteProcessWithArg("-L" + WorkstationPorts.DIVE_LOCAL, 1);
            ExpectLocalProcessWithName(YetiConstants.DebuggerGrpcServerExecutable);
            Assert.IsNull(abortError);
        }

        [Test]
        public void StartPreGameLaunchAborted()
        {
            SshTarget sshTarget = new SshTarget(_targetString);

            var mockProcess = Substitute.For<IProcess>();
            ProcessStartInfo startInfo = null;
            mockManagedProcessFactory
                .Create(Arg.Is<ProcessStartInfo>(x => Path.GetFileName(x.FileName) ==
                                                     YetiConstants.DebuggerGrpcServerExecutable))
                .Returns(mockProcess).AndDoes(x => { startInfo = x.Arg<ProcessStartInfo>(); });

            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPreGame(LaunchOption.LaunchGame, false, false, false,
                                            sshTarget, session);

            int exitCode = 123;
            mockProcess.StartInfo.Returns(startInfo);
            mockProcess.ExitCode.Returns(exitCode);
            mockProcess.OnExit += Raise.EventWith(mockProcess, new EventArgs());
            Assert.IsInstanceOf(typeof(ProcessExecutionException), abortError);

            var processError = abortError as ProcessExecutionException;
            Assert.AreEqual(exitCode, ((ProcessExecutionException) abortError).ExitCode);
        }

        [Test]
        public void StartPostGameLaunchNoCaptureOutput()
        {
            SshTarget sshTarget = new SshTarget(_targetString);
            optionPageGrid.CaptureGameOutput.Returns(false);
            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPostGame(LaunchOption.AttachToGame, sshTarget, _remotePid);
            Assert.IsNull(abortError);
        }

        [Test]
        public void StartPostGameLaunchCaptureOutput()
        {
            SshTarget sshTarget = new SshTarget(_targetString);
            optionPageGrid.CaptureGameOutput.Returns(true);

            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPostGame(LaunchOption.AttachToGame, sshTarget, _remotePid);
            ExpectRemoteProcessWithArg($"tail --pid={_remotePid}", 1);
            Assert.IsNull(abortError);
        }

        [Test]
        public void TailExitsGracefulIfGameExits()
        {
            IProcess tailProcess = Substitute.For<IProcess>();
            SshTarget sshTarget = new SshTarget(_targetString);
            optionPageGrid.CaptureGameOutput.Returns(true);
            mockManagedProcessFactory.Create(Arg.Any<ProcessStartInfo>(), Arg.Any<int>())
                .Returns(tailProcess);

            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPostGame(LaunchOption.AttachToGame, sshTarget, _remotePid);

            tailProcess.WaitForExit(_tailExitTimeout).Returns(true);
            yetiDebugTransport.Stop(ExitReason.ProcessExited);
            tailProcess.Received().WaitForExit(_tailExitTimeout);
            tailProcess.Received().Kill();
        }

        [Test]
        public void TailKilledIfGracefulExitFailsAndGameExits()
        {
            IProcess tailProcess = Substitute.For<IProcess>();
            SshTarget sshTarget = new SshTarget(_targetString);
            optionPageGrid.CaptureGameOutput.Returns(true);
            mockManagedProcessFactory.Create(Arg.Any<ProcessStartInfo>(), Arg.Any<int>())
                .Returns(tailProcess);

            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPostGame(LaunchOption.AttachToGame, sshTarget, _remotePid);

            tailProcess.WaitForExit(_tailExitTimeout).Returns(false);
            yetiDebugTransport.Stop(ExitReason.ProcessExited);
            tailProcess.Received().WaitForExit(_tailExitTimeout);
            tailProcess.Received().Kill();
        }

        [Test]
        public void TailKilledIfOtherExitReason()
        {
            IProcess tailProcess = Substitute.For<IProcess>();
            SshTarget sshTarget = new SshTarget(_targetString);
            optionPageGrid.CaptureGameOutput.Returns(true);
            mockManagedProcessFactory.Create(Arg.Any<ProcessStartInfo>(), Arg.Any<int>())
                .Returns(tailProcess);

            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPostGame(LaunchOption.AttachToGame, sshTarget, _remotePid);

            yetiDebugTransport.Stop(ExitReason.Unknown);
            tailProcess.DidNotReceiveWithAnyArgs().WaitForExit(Arg.Any<TimeSpan>());
            tailProcess.Received().Kill();
        }

        // No pane - no gane!
        [Test]
        public void StartPostGameLaunchCaptionOutputNoPane()
        {
            yetiDebugTransport = new YetiDebugTransport(taskContext,
                                                        mockGrpcCallInvokerFactory,
                                                        mockGrpcConnectionFactory,
                                                        onAsyncRpcCompleted: null,
                                                        managedProcessFactory:
                                                        mockManagedProcessFactory,
                                                        dialogUtil: mockDialogUtil,
                                                        vsOutputWindow: null,
                                                        yetiVSIService: service);
            SshTarget sshTarget = new SshTarget(_targetString);
            optionPageGrid.CaptureGameOutput.Returns(true);
            YetiDebugTransport.GrpcSession session = yetiDebugTransport.StartGrpcServer();
            yetiDebugTransport.StartPostGame(LaunchOption.LaunchGame, sshTarget, _remotePid);
            ExpectRemoteProcessWithArg($"tail --pid={_remotePid}", 1);
            Assert.IsNull(abortError);
        }

        void ExpectLocalProcessWithName(string name)
        {
            mockManagedProcessFactory.Received(1)
                .Create(Arg.Is<ProcessStartInfo>(x => Path.GetFileName(x.FileName) == name));
        }

        void ExpectRemoteProcessWithArg(string name, int count)
        {
            mockManagedProcessFactory.Received(count).Create(
                Arg.Is<ProcessStartInfo>(
                    x => Path.GetFileName(x.FileName) == YetiConstants.SshWinExecutable &&
                        x.Arguments.Contains(name)));
        }
    }
}