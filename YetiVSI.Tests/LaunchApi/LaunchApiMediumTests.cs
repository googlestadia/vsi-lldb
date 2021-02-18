// Copyright 2021 Google LLC
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

using System.Collections.Generic;
using System.IO;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Google.VisualStudioFake.API;
using Google.VisualStudioFake.Internal;
using Google.VisualStudioFake.Util;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using NUnit.Framework;
using TestsCommon.TestSupport;
using YetiCommon;
using YetiCommon.VSProject;
using YetiVSI.Test.MediumTestsSupport;
using YetiVSI.Util;
using YetiVSITestsCommon;

namespace YetiVSI.Test.LaunchApi
{
    public class LaunchApiMediumTests
    {
        static readonly string _sampleDir = Path.Combine(YetiConstants.RootDir,
                                                         @"TestData\");
        const string _sampleName = "StubTestSample";

        NLogSpy _nLogSpy;
        MediumTestDebugEngineFactoryCompRoot _compRoot;
        JoinableTaskContext _taskContext;
        VSFakeCompRoot _vsFakeCompRoot;

        [SetUp]
        public void SetUp()
        {
            _nLogSpy = NLogSpy.CreateUnique(nameof(LaunchApiMediumTests));
            _nLogSpy.Attach();

            _taskContext = new FakeMainThreadContext().JoinableTaskContext;
        }

        [Test]
        public void LaunchRequestIsSentOnLaunchSuspended()
        {
            var launches = new List<LaunchGameRequest>();
            var gameletClientFactory = new GameletClientStub.Factory().WithSampleInstance()
                .WithLaunchRequestsTracker(launches);
            IVSFake vsFake = CreateVsFake(gameletClientFactory);

            // For this test we don't need to launch / build binaries. The test assets contain
            // fake binaries in StubTestSample/GGP/Debug.
            vsFake.ProjectAdapter.Load(_sampleName);
            (vsFake.ProjectAdapter as ProjectAdapter)?.SetDeployOnLaunch(
                DeployOnLaunchSetting.FALSE);

            _taskContext.RunOnMainThread(() => vsFake.LaunchSuspended());

            Assert.That(launches.Count, Is.EqualTo(1));
        }

        IVSFake CreateVsFake(IGameletClientFactory gameletClientFactory)
        {
            var serviceManager = new MediumTestServiceManager(_taskContext, CreateVsiOptions());

            _compRoot = new MediumTestDebugEngineFactoryCompRoot(
                serviceManager, _taskContext, gameletClientFactory,
                TestDummyGenerator.Create<IWindowsRegistry>());

            IDebugEngine2 CreateDebugEngine() =>
                _compRoot.CreateDebugEngineFactory().Create(null);

            InitVsFakeCompRoot(serviceManager, gameletClientFactory);
            return _vsFakeCompRoot.Create(CreateDebugEngine);
        }

        void InitVsFakeCompRoot(ServiceManager serviceManager,
                                IGameletClientFactory gameletClientFactory)
        {
            var config = new VSFakeCompRoot.Config { SamplesRoot = _sampleDir };
            var dialogUtil = new DialogUtilFake();

            var debugTargetCompRoot =
                new MediumTestGgpDebugQueryTargetCompRoot(serviceManager, dialogUtil,
                                                          gameletClientFactory);
            _taskContext.RunOnMainThread(() =>
            {
                var debugTargetWrapperFactory = new GgpDebugQueryTargetWrapperFactory(
                    debugTargetCompRoot.Create(), _taskContext, new ManagedProcess.Factory());
                _vsFakeCompRoot = new VSFakeCompRoot(config, debugTargetWrapperFactory,
                                                     _taskContext, _nLogSpy.GetLogger());
            });
        }

        OptionPageGrid CreateVsiOptions()
        {
            var vsiOptions = OptionPageGrid.CreateForTesting();
            vsiOptions.LunchGameApiFlow = LunchGameApiFlowFlag.ENABLED;
            return vsiOptions;
        }
    }
}