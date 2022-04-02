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

﻿using DebuggerApi;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.LLDBShell;

namespace YetiVSI.Test
{
    [TestFixture]
    class LldbAttachedProgramTests
    {
        LldbAttachedProgram _attachedProgram;

        IGgpDebugProgram _debugProgram;

        IEventManager _eventManager;

        ILldbListenerSubscriber _listenerSubscriber;
        SbProcess _process;
        SbDebugger _debugger;

        IModuleFileLoader _moduleFileLoader;

        IDebugEngineHandler _debugEngineHandler;
        ITaskExecutor _taskExecutor;
        ILLDBShell _lldbShell;

        RemoteTarget _target;

        IBreakpointManager _breakpointManager;

        IDebugModuleCache _debugModuleCache;

        const int _remotePid = 12345;

        [SetUp]
        public void SetUp()
        {

            _lldbShell = Substitute.For<ILLDBShell>();
            _breakpointManager = Substitute.For<IBreakpointManager>();
            _moduleFileLoader = Substitute.For<IModuleFileLoader>();

            _debugger = Substitute.For<SbDebugger>();
            _target = Substitute.For<RemoteTarget>();
            _listenerSubscriber = Substitute.For<ILldbListenerSubscriber>();
            _process = Substitute.For<SbProcess>();

            _debugEngineHandler = Substitute.For<IDebugEngineHandler>();
            _taskExecutor = Substitute.For<ITaskExecutor>();
            _eventManager = Substitute.For<IEventManager>();

            var exceptionManagerFactory =
                new LldbExceptionManager.Factory(new Dictionary<int, YetiCommon.Signal>());
            var exceptionManager = exceptionManagerFactory.Create(_process);

            _debugModuleCache = Substitute.For<IDebugModuleCache>();
            _debugProgram = Substitute.For<IGgpDebugProgram>();

            _attachedProgram = new LldbAttachedProgram(
                _breakpointManager, _eventManager, _lldbShell, _moduleFileLoader,
                _debugEngineHandler, _taskExecutor, _debugProgram, _debugger, _target, _process,
                exceptionManager, _debugModuleCache, _listenerSubscriber, _remotePid);
        }

        [Test]
        public void Start()
        {
            var debugEngine = Substitute.For<IDebugEngine2>();
            _attachedProgram.Start(debugEngine);

            Predicate<EngineCreateEvent> enginesAreEqual = e =>
            {
                e.GetEngine(out IDebugEngine2 receivedEngine);
                return receivedEngine == debugEngine;
            };

            Received.InOrder(() => {
                _debugEngineHandler.SendEvent(Arg.Is<EngineCreateEvent>(e => enginesAreEqual(e)),
                                              _debugProgram);
                _debugEngineHandler.SendEvent(Arg.Any<ProgramCreateEvent>(), _debugProgram);
            });
        }

        [Test]
        public void ContinueFromSuspended()
        {
            _attachedProgram.ContinueFromSuspended();
            Received.InOrder(() => {
                _eventManager.StartListener();
                _process.Continue();
                _lldbShell.AddDebugger(_debugger);
            });
        }

        [Test]
        public void ContinueFromSuspendedWhenLldbShellIsNull()
        {
            _lldbShell = null;
            _attachedProgram.ContinueFromSuspended();
            Received.InOrder(() => {
                _eventManager.StartListener();
                _process.Continue();
                _debugProgram.Received().EnumModules(out _);
            });
        }

        [Test]
        public void ContinueInBreakModeSignal()
        {
            var selectedThread = Substitute.For<RemoteThread>();
            _process.GetSelectedThread().Returns(selectedThread);
            selectedThread.GetStopReason().Returns(StopReason.SIGNAL);
            selectedThread.GetStopReasonDataCount().Returns<uint>(1);
            ulong sigAbort = 6;
            selectedThread.GetStopReasonDataAtIndex(0).Returns(sigAbort);

            ExceptionEvent exceptionEvent = null;
            _debugEngineHandler.SendEvent(Arg.Do<ExceptionEvent>(e => exceptionEvent = e),
                                          _debugProgram, selectedThread);

            _attachedProgram.ContinueInBreakMode();

            Received.InOrder(() =>
            {
                _debugEngineHandler.SendEvent(
                    Arg.Any<ExceptionEvent>(), _debugProgram, selectedThread);
                _lldbShell.AddDebugger(_debugger);
                _debugProgram.Received().EnumModules(out _);
            });

            EXCEPTION_INFO[] info = new EXCEPTION_INFO[1];
            exceptionEvent.GetException(info);

            Assert.That(info[0].dwState,
                        Is.EqualTo(enum_EXCEPTION_STATE.EXCEPTION_STOP_ALL |
                                   enum_EXCEPTION_STATE.EXCEPTION_CANNOT_BE_CONTINUED));
            Assert.That(info[0].bstrExceptionName, Is.EqualTo("SIGABRT"));
        }

        [Test]
        public void ContinueInBreakMode()
        {
            var selectedThread = Substitute.For<RemoteThread>();
            _process.GetSelectedThread().Returns(selectedThread);

            _attachedProgram.ContinueInBreakMode();

            Predicate<ExceptionEvent> matchExceptionEvent = e =>
            {
                var info = new EXCEPTION_INFO[1];
                e.GetException(info);
                return info[0].dwState == enum_EXCEPTION_STATE.EXCEPTION_NONE;
            };

            Received.InOrder(() => {
                _debugEngineHandler.SendEvent(Arg.Is<ExceptionEvent>(e => matchExceptionEvent(e)),
                                              _debugProgram, selectedThread);
                _lldbShell.AddDebugger(_debugger);
                _debugProgram.Received().EnumModules(out _);
            });
        }

        [Test]
        public void ContinueInBreakModeWhenLldbShellIsNull()
        {
            _lldbShell = null;

            var selectedThread = Substitute.For<RemoteThread>();
            _process.GetSelectedThread().Returns(selectedThread);

            _attachedProgram.ContinueInBreakMode();

            Predicate<ExceptionEvent> matchExceptionEvent = e =>
            {
                var info = new EXCEPTION_INFO[1];
                e.GetException(info);
                return info[0].dwState == enum_EXCEPTION_STATE.EXCEPTION_NONE;
            };

            Received.InOrder(() => {
                _debugEngineHandler.SendEvent(Arg.Is<ExceptionEvent>(e => matchExceptionEvent(e)),
                                              _debugProgram, selectedThread);
            });
        }

        [Test]
        public void Stop()
        {
            _attachedProgram.Stop();

            Received.InOrder(() => {
                _eventManager.StopListener();
                _lldbShell.RemoveDebugger(_debugger);
            });
        }

        [Test]
        public void StopWhenLldbShellIsNull()
        {
            _lldbShell = null;
            _attachedProgram.Stop();

            Received.InOrder(() => { _eventManager.StopListener(); });
        }

        [Test]
        public void CreatePendingBreakpoint()
        {
            var breakpointRequest = Substitute.For<IDebugBreakpointRequest2>();
            var pendingBreakpointReturn = Substitute.For<IDebugPendingBreakpoint2>();

            _breakpointManager
                .When(bm => bm.CreatePendingBreakpoint(breakpointRequest, _target, out _))
                .Do(x => { x[2] = pendingBreakpointReturn; });

            Assert.That(_attachedProgram.CreatePendingBreakpoint(breakpointRequest),
                        Is.EqualTo(pendingBreakpointReturn));
        }

        [Test]
        public void NumLoadedModules()
        {
            _target.GetNumModules().Returns(10);
            Assert.That(_attachedProgram.NumLoadedModules, Is.EqualTo(10));
        }

        [Test]
        public async Task LoadSymbolsAsync()
        {
            var modules = new List<SbModule>() {
                Substitute.For<SbModule>(),
                Substitute.For<SbModule>(),
                Substitute.For<SbModule>(),
                Substitute.For<SbModule>(),
            };

            _target.GetNumModules().Returns(modules.Count);
            for (int i = 0; i < modules.Count; i++)
            {
                _target.GetModuleAtIndex(i).Returns(modules[i]);
            }

            var task = Substitute.For<ICancelable>();
            var symbolSettings =
                new SymbolInclusionSettings(true, new List<string>(), new List<string>());
            var moduleFileLoadRecorder = Substitute.For<IModuleFileLoadMetricsRecorder>();
            await _attachedProgram.LoadModuleFilesAsync(
                symbolSettings, true, true, task, moduleFileLoadRecorder);

            await _moduleFileLoader.Received(1).LoadModuleFilesAsync(
                Arg.Is<IList<SbModule>>(l => l.SequenceEqual(modules)), symbolSettings, true,
                true, task, moduleFileLoadRecorder);
        }

        [Test]
        public void GetModulesByNameReturnsAllModulesWithMatchingName()
        {
            const string searchForName = "module_name";

            var matchingDebugModule = Substitute.For<IDebugModule3>();
            var matchingSbModule = Substitute.For<SbModule>();
            matchingSbModule.GetFileSpec().GetFilename().Returns(searchForName);
            _debugModuleCache.GetOrCreate(matchingSbModule, _debugProgram)
                .Returns(matchingDebugModule);

            var notMatchingDebugModule = Substitute.For<IDebugModule3>();
            var notMatchingSbModule = Substitute.For<SbModule>();
            notMatchingSbModule.GetFileSpec().GetFilename().Returns("other_module_name");
            _debugModuleCache.GetOrCreate(notMatchingSbModule, _debugProgram)
                .Returns(notMatchingDebugModule);

            var sbModules = new List<SbModule>() {
                matchingSbModule,
                notMatchingSbModule,
                matchingSbModule,
                notMatchingSbModule,
            };

            _target.GetNumModules().Returns(sbModules.Count);
            for (int i = 0; i < sbModules.Count; i++)
            {
                _target.GetModuleAtIndex(i).Returns(sbModules[i]);
            }

            IDebugModule3[] modules = _attachedProgram.GetModulesByName(searchForName).ToArray();
            Assert.That(modules.Length, Is.EqualTo(2));
            Assert.That(modules[0], Is.EqualTo(matchingDebugModule));
            Assert.That(modules[1], Is.EqualTo(matchingDebugModule));
        }

        [TestCase(BreakpointEventType.LOCATIONS_REMOVED, TestName = "LocationsRemoved")]
        [TestCase(BreakpointEventType.LOCATIONS_ADDED, TestName = "LocationsAdded")]
        public void OnBreakpointChanged(BreakpointEventType eventType)
        {
            IPendingBreakpoint[] breakpoints = {
                GetBreakpointSubstitute(1), GetBreakpointSubstitute(2),
                GetBreakpointSubstitute(3)
            };
            _breakpointManager.GetPendingBreakpointById(Arg.Any<int>(), out IPendingBreakpoint _)
                .Returns(x =>
                {
                    x[1] = breakpoints.First(b => b.GetId() == (int) x[0]);
                    return true;
                });
            SbEvent evnt = Substitute.For<SbEvent>();
            IEventBreakpointData breakpointData = Substitute.For<IEventBreakpointData>();
            evnt.IsBreakpointEvent.Returns(true);
            evnt.BreakpointData.Returns(breakpointData);
            breakpointData.BreakpointId.Returns(2);
            breakpointData.EventType.Returns(eventType);

            _attachedProgram.Start(Substitute.For<IDebugEngine2>());
            _listenerSubscriber.BreakpointChanged +=
                Raise.EventWith(null, new BreakpointChangedEventArgs(evnt));

            breakpoints[0].DidNotReceive().UpdateLocations();
            breakpoints[1].Received(1).UpdateLocations();
            breakpoints[2].DidNotReceive().UpdateLocations();
        }

        public IPendingBreakpoint GetBreakpointSubstitute(int id)
        {
            var breakpoint = Substitute.For<IPendingBreakpoint>();
            breakpoint.GetId().Returns(id);
            return breakpoint;
        }
    }
}
