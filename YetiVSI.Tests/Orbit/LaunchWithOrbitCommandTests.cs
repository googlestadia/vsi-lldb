// Copyright 2022 Google LLC
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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NSubstitute;
using NUnit.Framework;
using YetiVSI.Orbit;
using YetiVSITestsCommon;
using Task = System.Threading.Tasks.Task;

namespace YetiVSI.Test.Orbit
{
    class LaunchWithOrbitCommandTests
    {
        FakeMainThreadContext _mainThreadContext;
        IVsSolutionBuildManager _solutionBuildManager;
        LaunchWithOrbitCommand _command;

        [SetUp]
        public void SetUp()
        {
            _mainThreadContext = new FakeMainThreadContext();
            _solutionBuildManager = Substitute.For<IVsSolutionBuildManager>();
            _command =
                LaunchWithOrbitCommand.CreateForTesting(_mainThreadContext.JoinableTaskContext,
                                                        _solutionBuildManager);
        }

        [Test]
        public async Task OnlyVisibleIfGgpPlatformAsync()
        {
            await _mainThreadContext.JoinableTaskContext.Factory.SwitchToMainThreadAsync();
            OleMenuCommand testCommand = new OleMenuCommand((sender, args) => { }, null);

            var hier = Substitute.For<IVsHierarchy>();
            var anyIVsHierarchy = Arg.Any<IVsHierarchy>();
            _solutionBuildManager.get_StartupProject(out anyIVsHierarchy).Returns(x =>
            {
                x[0] = hier;
                return VSConstants.S_OK;
            });

            var activeCfg = Substitute.For<IVsProjectCfg>();
            _solutionBuildManager
                .FindActiveProjectCfg(IntPtr.Zero, IntPtr.Zero, hier, Arg.Any<IVsProjectCfg[]>())
                .Returns(x =>
                {
                    ((IVsProjectCfg[]) x[3])[0] = activeCfg;
                    return VSConstants.S_OK;
                });

            // Mock a GGP project configuration.
            var anyString = Arg.Any<string>();
            activeCfg.get_CanonicalName(out anyString).Returns(x =>
            {
                x[0] = "Debug|GGP";
                return VSConstants.S_OK;
            });

            testCommand.Visible = false;
            _command.OnBeforeQueryStatus(testCommand, null);
            Assert.True(testCommand.Visible);

            // Mock a non-GGP project configuration.
            activeCfg.get_CanonicalName(out anyString).Returns(x =>
            {
                x[0] = "Debug|x86";
                return VSConstants.S_OK;
            });
            _command.OnBeforeQueryStatus(testCommand, null);
            Assert.False(testCommand.Visible);

            // Try to trick it into believing it's GGP.
            activeCfg.get_CanonicalName(out anyString).Returns(x =>
            {
                x[0] = "Debug|NonGGP";
                return VSConstants.S_OK;
            });
            _command.OnBeforeQueryStatus(testCommand, null);
            Assert.False(testCommand.Visible);
        }

        [Test]
        public async Task ExecuteAsync()
        {
            await _mainThreadContext.JoinableTaskContext.Factory.SwitchToMainThreadAsync();

            _command.Execute(null, null);
            var launchOptions = DebugLaunchOptions.NoDebug | DebugLaunchOptions.Profiling;
            _solutionBuildManager.Received().DebugLaunch((uint) launchOptions);
        }
    }
}