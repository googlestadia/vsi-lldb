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

﻿using GgpGrpc.Models;
using DebuggerGrpcClient;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NSubstitute.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestsCommon.TestSupport;
using YetiCommon.SSH;
using YetiVSI.DebugEngine;
using YetiVSI.LLDBShell;
using YetiVSI.LoadSymbols;
using YetiVSI.Metrics;
using YetiVSI.PortSupplier;
using YetiVSI.Shared.Metrics;
using YetiVSITestsCommon;
using YetiVSITestsCommon.Services;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    partial class DebugEngineTests
    {
        readonly string _debugSessionId = "42";
        readonly uint _celtPrograms = 1;
        readonly string _exePath = "disk:/path/file.exe";
        readonly string _gameletIp = "127.0.0.1";
        IMetrics _metrics;

        [Test]
        public void AttachNotImplementedForAutoAttach()
        {
            IGgpDebugEngine debugEngine = CreateGgpDebugEngine();

            int result = debugEngine.Attach(null, null, _celtPrograms, null,
                                            enum_ATTACH_REASON.ATTACH_REASON_AUTO);

            Assert.That(result, Is.EqualTo(VSConstants.E_NOTIMPL));
        }

        [Test]
        public void AttachNeedsNumProgramsToEqual1([Values] enum_ATTACH_REASON attachReason)
        {
            IGgpDebugEngine debugEngine = CreateGgpDebugEngine();

            int result = debugEngine.Attach(null, null, 2, null, attachReason);

            Assert.That(result, Is.EqualTo(VSConstants.E_INVALIDARG));
        }

        [Test]
        public void AttachToCoreWithEmptyTargetSucceeds()
        {
            int calls = 0;
            SshTarget expectedTarget = null;
            var attachReason = enum_ATTACH_REASON.ATTACH_REASON_LAUNCH;
            IDebugSessionLauncher debugSessionLauncher =
                CreateConfiguredDebugSessionLauncher(expectedTarget, x => {
                    calls++;
                    var attachedProgram = Substitute.For<ILldbAttachedProgram>();
                    return Task.FromResult(attachedProgram);
                });
            IDebugSessionLauncherFactory debugSessionLauncherFactory =
                CreateDebugSessionLauncherFactory(debugSessionLauncher);
            IGgpDebugEngine debugEngine = CreateGgpDebugEngine(debugSessionLauncherFactory);
            var debugPort = Substitute.For<IDebugPort2>();
            debugEngine.LaunchSuspended("", debugPort, _exePath, null, null, null, null,
                                        enum_LAUNCH_FLAGS.LAUNCH_DEBUG, 0, 0, 0, null,
                                        out IDebugProcess2 _);
            var rgpPrograms = new[] { Substitute.For<IDebugProgram2>() };
            var rgpProgramNodes = new[] { Substitute.For<IDebugProgramNode2>() };

            int result =
                debugEngine.Attach(rgpPrograms, rgpProgramNodes, _celtPrograms, null, attachReason);

            Assert.Multiple(() => {
                debugPort.Received().GetProcess(Arg.Any<AD_PROCESS_ID>(), out _);
                Assert.That(result, Is.EqualTo(VSConstants.S_OK));
                Assert.That(calls, Is.EqualTo(1));
            });
        }

        [Test]
        public void AttachToGameletWithUnknownTargetFailsAsUserAbortedOperation()
        {
            var gamelet = new Gamelet { IpAddr = "" };
            var attachReason = enum_ATTACH_REASON.ATTACH_REASON_USER;
            IGgpDebugEngine debugEngine = CreateGgpDebugEngine();
            debugEngine.LaunchSuspended("", null, "", null, null, null, null,
                                        enum_LAUNCH_FLAGS.LAUNCH_DEBUG, 0, 0, 0, null,
                                        out IDebugProcess2 _);
            IDebugPort2 debugPort = CreateDebugPort(gamelet);
            IDebugProgram2 program = CreateDebugProgram(debugPort);
            IDebugProgram2[] rgpPrograms = { program };
            var rgpProgramNodes = new[] { Substitute.For<IDebugProgramNode2>() };

            int result =
                debugEngine.Attach(rgpPrograms, rgpProgramNodes, _celtPrograms, null, attachReason);

            Assert.That(result, Is.EqualTo(VSConstants.E_ABORT));
        }

        // AttachToCore
        [TestCase(enum_ATTACH_REASON.ATTACH_REASON_LAUNCH)]
        // AttachToCore
        [TestCase(enum_ATTACH_REASON.ATTACH_REASON_USER)]
        public void RemoteDeployNotCalledDuringAttachToCore(enum_ATTACH_REASON attachReason)
        {
            var gamelet = new Gamelet { IpAddr = _gameletIp };
            var expectedTarget = new SshTarget(gamelet);
            IDebugSessionLauncher debugSessionLauncher =
                CreateConfiguredDebugSessionLauncher(expectedTarget, x => {
                    var attachedProgram = Substitute.For<ILldbAttachedProgram>();
                    return Task.FromResult(attachedProgram);
                });

            IDebugSessionLauncherFactory debugSessionLauncherFactory =
                CreateDebugSessionLauncherFactory(debugSessionLauncher);
            int calls = 0;
            var remoteDeploy = Substitute.For<IRemoteDeploy>();
            remoteDeploy.DeployLldbServerAsync(expectedTarget, Arg.Any<IAction>()).Returns(x => {
                calls++;
                return Task.CompletedTask;
            });
            IGgpDebugEngine debugEngine =
                CreateGgpDebugEngine(debugSessionLauncherFactory, remoteDeploy);
            IDebugPort2 debugPort = CreateDebugPort(gamelet);
            string options = null;
            debugEngine.LaunchSuspended("", debugPort, _exePath, null, null, null, options,
                                        enum_LAUNCH_FLAGS.LAUNCH_DEBUG, 0, 0, 0, null,
                                        out IDebugProcess2 _);
            IDebugProgram2 program = CreateDebugProgram(debugPort);
            IDebugProgram2[] rgpPrograms = { program };
            var rgpProgramNodes = new[] { Substitute.For<IDebugProgramNode2>() };

            debugEngine.Attach(rgpPrograms, rgpProgramNodes, _celtPrograms, null, attachReason);

            Assert.That(calls, Is.EqualTo(0));
        }

        IGgpDebugEngine CreateGgpDebugEngine(
            IDebugSessionLauncherFactory debugSessionLauncherFactory = null,
            IRemoteDeploy remoteDeploy = null)
        {
            DebugEngineFactoryCompRootStub compRoot = CreateEngineFactoryCompRoot(
                debugSessionLauncherFactory, remoteDeploy ?? Substitute.For<IRemoteDeploy>());
            IDebugEngineFactory factory = compRoot.CreateDebugEngineFactory();
            return factory.Create(null);
        }

        DebugEngineFactoryCompRootStub CreateEngineFactoryCompRoot(
            IDebugSessionLauncherFactory debugSessionLauncherFactory, IRemoteDeploy remoteDeploy)
        {
            var compRoot =
                new DebugEngineFactoryCompRootStub(debugSessionLauncherFactory, remoteDeploy);
            _metrics = Substitute.For<IMetrics>();
            _metrics.NewDebugSessionId().Returns(_debugSessionId);
            ISessionNotifier sessionNotifier = Substitute.For<ISessionNotifier>();
            SLLDBShell lldbShell = TestDummyGenerator.Create<SLLDBShell>();
            var vsiService = new YetiVSIService(OptionPageGrid.CreateForTesting());
            var vsOutputWindow = new OutputWindowStub();
            var symbolSettingsManager = Substitute.For<IVsDebuggerSymbolSettingsManager120A>();
            compRoot.ServiceManager =
                new ServiceManagerStub(_metrics, lldbShell, vsiService, vsOutputWindow,
                                       symbolSettingsManager, sessionNotifier);
            return compRoot;
        }

        IDebugProgram2 CreateDebugProgram(IDebugPort2 portToReturn)
        {
            var process = Substitute.For<IDebugProcess2>();
            process.GetPort(out IDebugPort2 _).Returns(x => {
                x[0] = portToReturn;
                return 0;
            });
            var program = Substitute.For<IDebugProgram2>();
            program.GetProcess(out IDebugProcess2 _).Returns(x => {
                x[0] = process;
                return 0;
            });
            return program;
        }

        IDebugPort2 CreateDebugPort(Gamelet gamelet)
        {
            var processFactory = Substitute.For<DebugProcess.Factory>();
            var dialogUtil = Substitute.For<IDialogUtil>();
            var sshManager = Substitute.For<ISshManager>();
            var processListRequest = Substitute.For<IProcessListRequest>();
            var processListRequestFactory = Substitute.For<ProcessListRequest.Factory>();
            processListRequestFactory.Create().Returns(processListRequest);
            CancelableTask.Factory cancelableTaskFactory =
                FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false);
            var portFactory =
                new DebugPort.Factory(processFactory, processListRequestFactory,
                                      cancelableTaskFactory, dialogUtil, sshManager, _metrics);
            return portFactory.Create(gamelet, null, "290");
        }

        /// <summary>
        /// Get IDebugSessionLauncherFactory instance that returns specified sessionLauncher when
        /// called Create() with any parameters.
        /// </summary>
        IDebugSessionLauncherFactory CreateDebugSessionLauncherFactory(
            IDebugSessionLauncher sessionLauncher)
        {
            var debugSessionLauncherFactory = Substitute.For<IDebugSessionLauncherFactory>();
            debugSessionLauncherFactory
                .Create(Arg.Any<IDebugEngine3>(),
                        Arg.Any<YetiVSI.DebugEngine.DebugEngine.LaunchOption>(), Arg.Any<string>(),
                        Arg.Any<string>(), Arg.Any<string>())
                .Returns((x) => sessionLauncher);
            return debugSessionLauncherFactory;
        }

        /// <summary>
        /// Get IDebugSessionLauncher instance that will execute `func` on LaunchAsync call
        /// (with specified targetIpAddress and targetIpPort (all other arguments ignored)).
        /// </summary>
        /// <param name="expectedTarget">Gamelet target used to check that LaunchAsync was called
        /// with expected values for IpAddress and Port of the Gamelet.</param>
        /// <param name="func">Function being called when LaunchAsync with specified arguments
        /// is called from the code.</param>
        IDebugSessionLauncher CreateConfiguredDebugSessionLauncher(
            SshTarget expectedTarget, Func<CallInfo, Task<ILldbAttachedProgram>> func = null)
        {
            var debugSessionLauncher = Substitute.For<IDebugSessionLauncher>();
            debugSessionLauncher
                .LaunchAsync(Arg.Any<ICancelableTask>(), Arg.Any<IDebugProcess2>(), Arg.Any<Guid>(),
                             Arg.Any<uint?>(), Arg.Any<YetiVSI.DebuggerOptions.DebuggerOptions>(),
                             Arg.Any<HashSet<string>>(), Arg.Any<GrpcConnection>(), Arg.Any<int>(),
                             Arg.Is(expectedTarget?.IpAddress), Arg.Is(expectedTarget?.Port ?? 0),
                             Arg.Any<IDebugEventCallback2>())
                .Returns(func);

            return debugSessionLauncher;
        }


    }
}
