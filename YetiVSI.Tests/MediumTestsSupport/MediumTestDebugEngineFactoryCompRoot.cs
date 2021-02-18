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

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using GgpGrpc.Cloud;
using Microsoft.VisualStudio.Threading;
using TestsCommon.TestSupport;
using YetiCommon;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.Test.TestSupport.DebugEngine.NatvisEngine;
using YetiVSI.Util;
using YetiVSITestsCommon;

namespace YetiVSI.Test.MediumTestsSupport
{
    /// <summary>
    /// Defines the Medium test scope.
    /// </summary>
    /// <remarks>
    /// Quickly created to support a DebugEngine constructor test.  This deserves more thought as
    /// to how it should evolve.
    /// </remarks>
    public class MediumTestDebugEngineFactoryCompRoot : DebugEngineFactoryCompRoot
    {
        readonly JoinableTaskContext _taskContext;

        readonly ServiceManager _serviceManager;

        IGameletClientFactory _gameletClientFactory;

        NLogSpy _nLogSpy;

        NatvisDiagnosticLogger _natvisDiagnosticLogger;

        IVariableNameTransformer _variableNameTransformer;

        IFileSystem _fileSystem;

        YetiVSIService _vsiService;

        IChromeLauncher _chromeLauncher;

        CancelableTask.Factory _cancelableTaskFactory;

        IWindowsRegistry _windowsRegistry;

        public MediumTestDebugEngineFactoryCompRoot(JoinableTaskContext taskContext)
        {
            _taskContext = taskContext;
        }

        public MediumTestDebugEngineFactoryCompRoot(ServiceManager serviceManager,
                                                    JoinableTaskContext taskContext,
                                                    IGameletClientFactory gameletClientFactory,
                                                    IWindowsRegistry windowsRegistry)
        {
            _serviceManager = serviceManager;
            _taskContext = taskContext;
            _gameletClientFactory = gameletClientFactory;
            _windowsRegistry = windowsRegistry;
        }

        public override ServiceManager CreateServiceManager() => _serviceManager;

        public override IFileSystem GetFileSystem()
        {
            if (_fileSystem == null)
            {
                _fileSystem = new MockFileSystem();
            }

            return _fileSystem;
        }

        public override IWindowsRegistry GetWindowsRegistry()
        {
            if (_windowsRegistry == null)
            {
                _windowsRegistry = TestDummyGenerator.Create<IWindowsRegistry>();
            }

            return _windowsRegistry;
        }

        // TODO: Remove GetNatvisDiagnosticLogger
        public override NatvisDiagnosticLogger GetNatvisDiagnosticLogger()
        {
            if (_natvisDiagnosticLogger == null)
            {
                _natvisDiagnosticLogger = new NatvisDiagnosticLogger(
                    GetNatvisDiagnosticLogSpy().GetLogger(),
                    GetVsiService().Options.NatvisLoggingLevel);
            }

            return _natvisDiagnosticLogger;
        }

        public override YetiVSIService GetVsiService()
        {
            if (_vsiService != null)
            {
                return _vsiService;
            }

            if (_serviceManager != null)
            {
                _vsiService = base.GetVsiService();
            }

            if (_vsiService == null)
            {
                OptionPageGrid vsiServiceOptions = OptionPageGrid.CreateForTesting();
                vsiServiceOptions.NatvisLoggingLevel = NatvisLoggingLevelFeatureFlag.VERBOSE;

                _vsiService = new YetiVSIService(vsiServiceOptions);
            }

            return _vsiService;
        }

        public override JoinableTaskContext GetJoinableTaskContext() => _taskContext;

        public override DialogExecutionContext GetDialogExecutionContext()
        {
            return null;
        }

        public override NatvisLoggerOutputWindowListener GetNatvisLoggerOutputWindowListener()
        {
            return null;
        }

        public override IVariableNameTransformer GetVariableNameTransformer()
        {
            if (_variableNameTransformer == null)
            {
                _variableNameTransformer = new TestNatvisVariableNameTransformer();
            }

            return _variableNameTransformer;
        }

        public NLogSpy GetNatvisDiagnosticLogSpy()
        {
            if (_nLogSpy == null)
            {
                _nLogSpy = NLogSpy.CreateUnique("NatvisDiagnostic");
            }

            return _nLogSpy;
        }

        public override IChromeLauncher GetChromeLauncher(BackgroundProcess.Factory factory)
        {
            if (_chromeLauncher == null)
            {
                _chromeLauncher = new ChromeLauncherStub();
            }

            return _chromeLauncher;
        }

        public override CancelableTask.Factory GetCancelableTaskFactory()
        {
            if (_cancelableTaskFactory == null)
            {
                _cancelableTaskFactory =
                    FakeCancelableTask.CreateFactory(GetJoinableTaskContext(), false);
            }

            return _cancelableTaskFactory;
        }

        public override IGameletClientFactory GetGameletClientFactory()
        {
            if (_gameletClientFactory == null)
            {
                _gameletClientFactory = new GameletClientStub.Factory();
            }

            return _gameletClientFactory;
        }
    }
}