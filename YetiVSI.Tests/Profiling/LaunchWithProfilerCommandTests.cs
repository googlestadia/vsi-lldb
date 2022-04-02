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
using YetiVSI.Profiling;
using YetiVSITestsCommon;
using Task = System.Threading.Tasks.Task;

namespace YetiVSI.Test.Profiling
{
    class LaunchWithProfilerCommandTests
    {
        const string _profilerName = "burke";

        FakeMainThreadContext _mainThreadContext;
        IVsSolutionBuildManager _solutionBuildManager;
        DebugLaunchOptions _debugLaunchOption;
        IDialogUtil _dialogUtil;
        LaunchWithProfilerCommand _command;

        [SetUp]
        public void SetUp()
        {
            _mainThreadContext = new FakeMainThreadContext();
            _solutionBuildManager = Substitute.For<IVsSolutionBuildManager>();
            _debugLaunchOption = DebugLaunchOptions.Profiling | DebugLaunchOptions.DetachOnStop;
            _dialogUtil = Substitute.For<IDialogUtil>();
            _command = new LaunchWithProfilerCommand(_solutionBuildManager, _debugLaunchOption,
                                                     _profilerName, _dialogUtil, null);
        }

        [TearDown]
        public void TeadDown()
        {
            _mainThreadContext.Dispose();
        }

        [Test]
        public async Task OnlyVisibleIfGgpPlatformAsync()
        {
            await _mainThreadContext.JoinableTaskContext.Factory.SwitchToMainThreadAsync();
            OleMenuCommand testCommand = new OleMenuCommand((sender, args) => { }, null);

            SetupMockConfig("Debug|GGP");
            testCommand.Visible = false;
            _command.OnBeforeQueryStatus(testCommand, null);
            Assert.True(testCommand.Visible);
            Assert.True(testCommand.Enabled);

            // Mock a non-GGP project configuration.
            SetupMockConfig("Debug|x86");
            _command.OnBeforeQueryStatus(testCommand, null);
            Assert.False(testCommand.Visible);
            Assert.False(testCommand.Enabled);

            // Try to trick it into believing it's GGP.
            SetupMockConfig("Debug|NonGGP");
            _command.OnBeforeQueryStatus(testCommand, null);
            Assert.False(testCommand.Visible);
            Assert.False(testCommand.Enabled);
        }

        [Test]
        public async Task ExecuteAsync()
        {
            await _mainThreadContext.JoinableTaskContext.Factory.SwitchToMainThreadAsync();

            _command.Execute(null, null);
            var launchOptions = DebugLaunchOptions.NoDebug | _debugLaunchOption;
            _solutionBuildManager.Received().DebugLaunch((uint) launchOptions);
        }

        [Test]
        public async Task ExecuteWarnsInDebugModeAsync()
        {
            await _mainThreadContext.JoinableTaskContext.Factory.SwitchToMainThreadAsync();

            string msg = YetiCommon.ErrorStrings.ProfilingInDebugMode;
            string caption = YetiCommon.ErrorStrings.ProfilingInDebugModeCaption(_profilerName);
            var launchOptions = DebugLaunchOptions.NoDebug | _debugLaunchOption;

            // Config name is Debug -> dialog, respond yes -> launch.
            SetupMockConfig("Debug|GGP");
            _dialogUtil.ShowYesNoWarning(msg, caption).Returns(true);
            _command.Execute(null, null);
            _dialogUtil.Received().ShowYesNoWarning(msg, caption);
            _dialogUtil.ClearReceivedCalls();
            _solutionBuildManager.Received().DebugLaunch((uint)launchOptions);
            _solutionBuildManager.ClearReceivedCalls();

            // Config name is Debug -> dialog, respond no -> no launch.
            _dialogUtil.ShowYesNoWarning(msg, caption).Returns(false);
            _command.Execute(null, null);
            _solutionBuildManager.DidNotReceive().DebugLaunch((uint)launchOptions);

            // Config name is Debug + something -> dialog.
            SetupMockConfig("DebugWithoutAsserts|GGP");
            _command.Execute(null, null);
            _dialogUtil.Received().ShowYesNoWarning(msg, caption);
            _dialogUtil.Received().ClearReceivedCalls();

            // Config name is something + Debug -> no dialog.
            SetupMockConfig("NoDebug|GGP");
            _command.Execute(null, null);
            _dialogUtil.DidNotReceive().ShowYesNoWarning(Arg.Any<string>(), Arg.Any<string>());
            _dialogUtil.Received().ClearReceivedCalls();

            // Config name is Release -> no dialog.
            SetupMockConfig("Release|GGP");
            _command.Execute(null, null);
            _dialogUtil.DidNotReceive().ShowYesNoWarning(Arg.Any<string>(), Arg.Any<string>());
            _dialogUtil.Received().ClearReceivedCalls();
        }

        /// <summary>
        /// Sets up the _solutionBuildManager to return the given name as active configuration name
        /// of the startup project.
        /// </summary>
        void SetupMockConfig(string name)
        {
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
                    ((IVsProjectCfg[])x[3])[0] = activeCfg;
                    return VSConstants.S_OK;
                });

            // Mock a GGP project configuration.
            var anyString = Arg.Any<string>();
            activeCfg.get_CanonicalName(out anyString).Returns(x =>
            {
                x[0] = name;
                return VSConstants.S_OK;
            });

        }
    }
}