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
using Microsoft.VisualStudio.Threading;
using TestsCommon.TestSupport;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Exit;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.Test.TestSupport.DebugEngine.NatvisEngine;
using YetiVSI.Util;

namespace YetiVSI.Test.DebugEngine
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
        JoinableTaskContext _taskContext;

        NLogSpy _nLogSpy;

        NatvisDiagnosticLogger _natvisDiagnosticLogger;

        IVariableNameTransformer _variableNameTransformer;

        IFileSystem _fileSystem;

        YetiVSIService _vsiService;

        public IWindowsRegistry WindowsRegistry { get; set; }

        public ServiceManager ServiceManager { get; set; }

        // Constructor with option to inject a custom-configured |vsiService|. This can be useful
        // for changing option flags during unit test execution.
        public MediumTestDebugEngineFactoryCompRoot(YetiVSIService vsiService = null)
        {
            _vsiService = vsiService;
        }

        public override ServiceManager CreateServiceManager()
        {
            return ServiceManager;
        }

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
            if (WindowsRegistry == null)
            {
                WindowsRegistry = TestDummyGenerator.Create<IWindowsRegistry>();
            }
            return WindowsRegistry;
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
            if (_vsiService == null)
            {
                OptionPageGrid vsiServiceOptions = OptionPageGrid.CreateForTesting();
                vsiServiceOptions.NatvisLoggingLevel = NatvisLoggingLevelFeatureFlag.VERBOSE;

                _vsiService = new YetiVSIService(vsiServiceOptions);
            }

            return _vsiService;
        }

        public override JoinableTaskContext GetJoinableTaskContext()
        {
            if (_taskContext == null)
            {
                _taskContext = new JoinableTaskContext();
            }
            return _taskContext;
        }

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
    }
}
