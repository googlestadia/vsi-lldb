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
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using YetiCommon;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.ProjectSystem.Abstractions;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    partial class DebugEngineTests
    {
        [Test]
        public async Task LaunchSuspendedWhenBothArgsAndOptionsCoreFilePathEmptyFailsAsync(
            [Values("", null)] string args,
            [Values("{}", "{\"CoreFilePath\":\"\"}")] string options)
        {
            await _mainThreadContext.JoinableTaskContext.Factory.SwitchToMainThreadAsync();

            var debugSessionLauncherFactory = Substitute.For<IDebugSessionLauncherFactory>();
            IGgpDebugEngine debugEngine = CreateGgpDebugEngine(debugSessionLauncherFactory);
            int result = debugEngine.LaunchSuspended("", null, _exePath, args, null, null, options,
                                                     enum_LAUNCH_FLAGS.LAUNCH_DEBUG, 0, 0, 0, null,
                                                     out var _);
            Assert.That(result, Is.EqualTo(VSConstants.E_FAIL));
        }

        [Test]
        public async Task
        LaunchSuspendedWhenOptionsNullAndArgsEmptySucceedsAsync([Values("", null)] string args)
        {
            await _mainThreadContext.JoinableTaskContext.Factory.SwitchToMainThreadAsync();

            var debugSessionLauncherFactory = Substitute.For<IDebugSessionLauncherFactory>();
            IGgpDebugEngine debugEngine = CreateGgpDebugEngine(debugSessionLauncherFactory);
            string options = null;
            var debugPort = Substitute.For<IDebugPort2>();
            int result = debugEngine.LaunchSuspended("", debugPort, _exePath, args, null, null,
                                                     options, enum_LAUNCH_FLAGS.LAUNCH_DEBUG, 0, 0,
                                                     0, null, out var _);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));

            ReleaseTransportSession(debugEngine);
        }

        [Test]
        public async Task LaunchSuspendedWhenEndpointSetSucceedsAsync(
            [Values(StadiaEndpoint.AnyEndpoint, StadiaEndpoint.PlayerEndpoint,
                    StadiaEndpoint.TestClient)] StadiaEndpoint endpoint)
        {
            await _mainThreadContext.JoinableTaskContext.Factory.SwitchToMainThreadAsync();

            var debugSessionLauncherFactory = Substitute.For<IDebugSessionLauncherFactory>();
            IGgpDebugEngine debugEngine = CreateGgpDebugEngine(debugSessionLauncherFactory);
            string options = null;
            var launchParams = new LaunchParams { Endpoint = endpoint };
            string args = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(new JsonUtil().Serialize(launchParams)));
            var debugPort = Substitute.For<IDebugPort2>();
            int result = debugEngine.LaunchSuspended("", debugPort, _exePath, args, null, null,
                                                     options, enum_LAUNCH_FLAGS.LAUNCH_DEBUG, 0, 0,
                                                     0, null, out var _);
            Assert.That(result, Is.EqualTo(VSConstants.S_OK));

            ReleaseTransportSession(debugEngine);
        }
    }

}
