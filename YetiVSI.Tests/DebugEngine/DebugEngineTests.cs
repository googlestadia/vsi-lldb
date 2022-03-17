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

using NUnit.Framework;
using YetiCommon.Logging;
using System;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using TestsCommon.TestSupport;
using YetiVSI.LLDBShell;
using YetiVSI.Test.MediumTestsSupport;
using YetiVSI.Util;
using YetiVSITestsCommon.Services;
using YetiVSITestsCommon;
using System.Threading.Tasks;
using Metrics.Shared;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Exit;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    partial class DebugEngineTests
    {
        FakeMainThreadContext _mainThreadContext;

        [SetUp]
        public void SetUp()
        {
            // TODO: Don't output an actual log file to disk.
            YetiLog.Initialize(nameof(DebugEngineTests), DateTime.Now);

            _mainThreadContext = new FakeMainThreadContext();
        }

        [TearDown]
        public void TearDown()
        {
            YetiLog.Uninitialize();
            _mainThreadContext.Dispose();
            AssertAllSessionsReleased();
        }

        void AssertAllSessionsReleased()
        {
            using (var session = new LldbTransportSession())
            {
                Assert.That(session.GetSessionId(), Is.EqualTo(0));
            }
        }

        /// <summary>
        /// Verifies DebugEngine construction does not throw any exceptions.
        ///
        /// The most likely candidate to raise an exception is the application of the Castle
        /// aspects.  This test verifies that all factories can be decorated successfully.
        /// </summary>
        [Test]
        public async Task TestDebugEngineConstructorAsync()
        {
            await _mainThreadContext.JoinableTaskContext.Factory.SwitchToMainThreadAsync();

            var lldbShell = TestDummyGenerator.Create<SLLDBShell>();
            var vsiService = new YetiVSIService(OptionPageGrid.CreateForTesting());
            var metrics = TestDummyGenerator.Create<IVsiMetrics>();
            var vsOutputWindow = new OutputWindowStub();
            var symbolSettingsManager = Substitute.For<IVsDebuggerSymbolSettingsManager120A>();
            var serviceManager = new ServiceManagerStub(metrics, lldbShell, vsiService,
                                                        vsOutputWindow, symbolSettingsManager);
            var compRoot = new MediumTestDebugEngineFactoryCompRoot(
                serviceManager, new JoinableTaskContext(), new GameletClientStub.Factory(),
                TestDummyGenerator.Create<IWindowsRegistry>());

            var factory = compRoot.CreateDebugEngineFactory();
            factory.Create(null);
        }

        void ReleaseTransportSession(IGgpDebugEngine debugEngine)
        {
            debugEngine.ContinueFromSynchronousEvent(
                new ProgramDestroyEvent(ExitInfo.Normal(ExitReason.ProcessExited)));
        }
    }
}
