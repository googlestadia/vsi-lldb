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

﻿using NUnit.Framework;
using System;
using YetiVSI.DebugEngine;
using Microsoft.VisualStudio.Debugger.Interop;
using TestsCommon.TestSupport;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugEngineManagerTests
    {
        static Guid DEBUG_ENGINE_ID = Guid.Parse("deadc0de-dead-c0de-dead-c0dedeadc0de");

        private class DebugEngineStub : GgpDebugEngine
        {
            private Guid id;

            public DebugEngineStub() : base(null) { }

            public void EndSession()
            {
                RaiseSessionEnding(new EventArgs());
                RaiseSessionEnded(new EventArgs());
            }

            public override Guid Id
            {
                get
                {
                    return id;
                }
            }

            public void SetId(Guid id)
            {
                this.id = id;
            }

            #region NotImplemented

            public override IDebugEngineCommands DebugEngineCommands
            {
                get
                {
                    throw new NotImplementedTestDoubleException();
                }
            }

            public override int Attach(IDebugProgram2[] rgpPrograms,
                IDebugProgramNode2[] rgpProgramNodes, uint celtPrograms,
                IDebugEventCallback2 pCallback, enum_ATTACH_REASON dwReason)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int CanTerminateProcess(IDebugProcess2 pProcess)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int CauseBreak()
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int ContinueFromSynchronousEvent(IDebugEvent2 pEvent)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest,
                out IDebugPendingBreakpoint2 ppPendingBP)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int DestroyProgram(IDebugProgram2 pProgram)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int EnumPrograms(out IEnumDebugPrograms2 ppEnum)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int GetEngineId(out Guid pguidEngine)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int LaunchSuspended(string pszServer, IDebugPort2 pPort, string pszExe,
                string pszArgs, string pszDir, string bstrEnv, string pszOptions,
                enum_LAUNCH_FLAGS dwLaunchFlags, uint hStdInput, uint hStdOutput, uint hStdError,
                IDebugEventCallback2 pCallback, out IDebugProcess2 ppProcess)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int LoadSymbols()
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int RemoveAllSetExceptions(ref Guid guidType)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int RemoveSetException(EXCEPTION_INFO[] pException)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int ResumeProcess(IDebugProcess2 pProcess)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int SetAllExceptions(enum_EXCEPTION_STATE dwState)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int SetEngineGuidImpl(Guid guidEngine)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int SetException(EXCEPTION_INFO[] pException)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int SetJustMyCodeState(int fUpdate, uint dwModules,
                JMC_CODE_SPEC[] rgJMCSpec)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int SetLocale(ushort wLangID)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int SetMetric(string pszMetric, object varValue)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int SetRegistryRoot(string pszRegistryRoot)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int SetSymbolPath(string szSymbolSearchPath, string szSymbolCachePath,
                uint Flags)
            {
                throw new NotImplementedTestDoubleException();
            }

            public override int TerminateProcess(IDebugProcess2 pProcess)
            {
                throw new NotImplementedTestDoubleException();
            }

            #endregion
        }

        DebugEngineManager debugEngineManager;

        [SetUp]
        public void SetUp()
        {
            debugEngineManager = new DebugEngineManager();
        }

        [Test]
        public void SessionEndingRemovesDebugEngine()
        {
            Assert.That(debugEngineManager.GetDebugEngines().Count, Is.EqualTo(0));

            var debugEngine = new DebugEngineStub();
            debugEngine.SetId(DEBUG_ENGINE_ID);
            debugEngineManager.AddDebugEngine(debugEngine);

            Assert.That(debugEngineManager.GetDebugEngines().Count, Is.EqualTo(1));

            debugEngine.EndSession();
            Assert.That(debugEngineManager.GetDebugEngines().Count, Is.EqualTo(0));
        }
    }
}
