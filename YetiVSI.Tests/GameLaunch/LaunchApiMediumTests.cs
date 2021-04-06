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

using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Google.VisualStudioFake.API;
using Google.VisualStudioFake.Internal;
using Google.VisualStudioFake.Util;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Threading;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestsCommon.TestSupport;
using YetiCommon;
using YetiVSI.ProjectSystem.Abstractions;
using YetiVSI.Test.MediumTestsSupport;
using YetiVSI.Util;
using YetiVSITestsCommon;

namespace YetiVSI.Test.GameLaunch
{
    public class LaunchApiMediumTests
    {
        static LaunchApiMediumTests()
        {
            if (!Microsoft.Build.Locator.MSBuildLocator.IsRegistered)
            {
                Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
            }
        }

        static readonly string _sampleDir = Path.Combine(YetiConstants.RootDir, @"TestData\");
        const string _sampleName = "StubTestSample";

        NLogSpy _nLogSpy;
        MediumTestDebugEngineFactoryCompRoot _compRoot;
        JoinableTaskContext _taskContext;
        VSFakeCompRoot _vsFakeCompRoot;

        const string _instanceLocation = "location-east-4/instance-1";
        const string _applicationName = "Yeti Development Application";

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
            IVSFake vsFake = CreateVsFakeAndLoadProject(gameletClientFactory);

            _taskContext.RunOnMainThread(() => vsFake.LaunchSuspended());

            Assert.That(launches.Count, Is.EqualTo(1));
            Assert.That(launches[0].GameletName, Is.EqualTo(_instanceLocation));
            Assert.That(launches[0].ApplicationName, Is.EqualTo(_applicationName));
            Assert.That(launches[0].ExecutablePath, Does.Contain(_sampleName));
        }

        [Test]
        public void EnvironmentVariablesArePropagatedToLaunchRequest()
        {
            var launches = new List<LaunchGameRequest>();
            var gameletClientFactory = new GameletClientStub.Factory().WithSampleInstance()
                .WithLaunchRequestsTracker(launches);
            IVSFake vsFake = CreateVsFakeAndLoadProject(gameletClientFactory);

            (vsFake.ProjectAdapter as ProjectAdapter)?.SetGameletEnvironmentVariables(
                "v1=1;v2=stringValue");

            _taskContext.RunOnMainThread(() => vsFake.LaunchSuspended());

            Assert.That(launches.Count, Is.EqualTo(1));
            Assert.That(launches[0].EnvironmentVariablePairs,
                        Is.EqualTo(new Dictionary<string, string>
                                       { { "v1", "1" }, { "v2", "stringValue" } }));
        }

        [Test]
        public void EnvironmentVariablesArePropagatedWhenSpecifiedViaQueryParams()
        {
            var launches = new List<LaunchGameRequest>();
            var gameletClientFactory = new GameletClientStub.Factory().WithSampleInstance()
                .WithLaunchRequestsTracker(launches);
            IVSFake vsFake = CreateVsFakeAndLoadProject(gameletClientFactory);

            (vsFake.ProjectAdapter as ProjectAdapter)?.SetQueryParams(
                "stream_profile_preset=HIGH_VISUAL_QUALITY&streamer_fixed_fps=120");

            _taskContext.RunOnMainThread(() => vsFake.LaunchSuspended());

            Assert.That(launches.Count, Is.EqualTo(1));
            Assert.That(launches[0].StreamQualityPreset,
                        Is.EqualTo(StreamQualityPreset.HighVisualQuality));
            Assert.That(launches[0].StreamerFixedFps, Is.EqualTo(120));
        }

        [Test]
        public void RenderDocPropertyIsPropagatedToLaunchRequest()
        {
            var launches = new List<LaunchGameRequest>();
            var gameletClientFactory = new GameletClientStub.Factory().WithSampleInstance()
                .WithLaunchRequestsTracker(launches);
            IVSFake vsFake = CreateVsFakeAndLoadProject(gameletClientFactory);

            (vsFake.ProjectAdapter as ProjectAdapter)?.SetLaunchRenderDoc(true);

            _taskContext.RunOnMainThread(() => vsFake.LaunchSuspended());
            Assert.That(launches.Count, Is.EqualTo(1));
            Assert.That(launches[0].EnvironmentVariablePairs, Is.EqualTo(
                            new Dictionary<string, string>
                            {
                                { "ENABLE_VULKAN_RENDERDOC_CAPTURE", "1" },
                                { "RENDERDOC_TEMP", "/mnt/developer/ggp" },
                                { "RENDERDOC_DEBUG_LOG_FILE", "/var/game/RDDebug.log" }
                            }));
        }

        [Test]
        public void SurfaceEnforcementPropertyIsPropagatedToLaunchRequest()
        {
            var launches = new List<LaunchGameRequest>();
            var gameletClientFactory = new GameletClientStub.Factory().WithSampleInstance()
                .WithLaunchRequestsTracker(launches);
            IVSFake vsFake = CreateVsFakeAndLoadProject(gameletClientFactory);

            (vsFake.ProjectAdapter as ProjectAdapter)?.SetSurfaceEnforcement(
                SurfaceEnforcementSetting.Block);

            _taskContext.RunOnMainThread(() => vsFake.LaunchSuspended());
            Assert.That(launches.Count, Is.EqualTo(1));
            Assert.That(launches[0].SurfaceEnforcementMode,
                        Is.EqualTo(SurfaceEnforcementSetting.Block));
        }

        [Test]
        public void VulkanDriverVariantIsPropagatedToLaunchRequest()
        {
            var launches = new List<LaunchGameRequest>();
            var gameletClientFactory = new GameletClientStub.Factory().WithSampleInstance()
                .WithLaunchRequestsTracker(launches);
            IVSFake vsFake = CreateVsFakeAndLoadProject(gameletClientFactory);

            (vsFake.ProjectAdapter as ProjectAdapter)?.SetVulkanDriverVariant(
                "vulkan-driver-variant");

            _taskContext.RunOnMainThread(() => vsFake.LaunchSuspended());
            Assert.That(launches[0].EnvironmentVariablePairs, Is.EqualTo(
                            new Dictionary<string, string>
                            {
                                { "GGP_DEV_VK_DRIVER_VARIANT", "vulkan-driver-variant" }
                            }));
        }

        [Test]
        public void LaunchRgpIsPropagatedToLaunchRequest()
        {
            var launches = new List<LaunchGameRequest>();
            var gameletClientFactory = new GameletClientStub.Factory().WithSampleInstance()
                .WithLaunchRequestsTracker(launches);
            IVSFake vsFake = CreateVsFakeAndLoadProject(gameletClientFactory);

            (vsFake.ProjectAdapter as ProjectAdapter)?.SetLaunchRgp(true);

            _taskContext.RunOnMainThread(() => vsFake.LaunchSuspended());

            Assert.That(launches.Count, Is.EqualTo(1));
            Assert.That(launches[0].EnvironmentVariablePairs, Is.EqualTo(
                            new Dictionary<string, string>
                            {
                                { "GGP_INTERNAL_LOAD_RGP", "1" },
                                { "RGP_DEBUG_LOG_FILE", "/var/game/RGPDebug.log" },
                                { "LD_PRELOAD", "librgpserver.so" }
                            }));
        }

        [Test]
        public void TestAccountIsPropagatedToLaunchRequest()
        {
            var launches = new List<LaunchGameRequest>();
            var gameletClientFactory = new GameletClientStub.Factory().WithSampleInstance()
                .WithLaunchRequestsTracker(launches);
            IVSFake vsFake = CreateVsFakeAndLoadProject(gameletClientFactory);

            (vsFake.ProjectAdapter as ProjectAdapter)?.SetTestAccount("gamer#1234");
            _taskContext.RunOnMainThread(() => vsFake.LaunchSuspended());

            Assert.That(launches.Count, Is.EqualTo(1));
            Assert.That(launches[0].Parent, Does.Contain("testAccounts/gamer#1234"));
        }

        [Test]
        public void ErrorIsShownWhenPropertiesParsingFailed()
        {
            var gameletClientFactory = new GameletClientStub.Factory().WithSampleInstance();
            IVSFake vsFake = CreateVsFakeAndLoadProject(gameletClientFactory);

            (vsFake.ProjectAdapter as ProjectAdapter)?.SetQueryParams("cmd=wrongBinaryName");
            Assert.Throws<DialogUtilFake.DialogException>(
                () => _taskContext.RunOnMainThread(() => vsFake.LaunchSuspended()));

            DialogUtilFake.Message errorMessage =
                (_compRoot.GetDialogUtil() as DialogUtilFake)?.Messages.Last();
            Assert.That(errorMessage?.Text, Does.Contain("invalid binary name: 'wrongBinaryName'"));
        }

        IVSFake CreateVsFakeAndLoadProject(IGameletClientFactory gameletClientFactory)
        {
            IVSFake vsFake = CreateVsFake(gameletClientFactory);

            // For this test we don't need to launch / build binaries. The test assets contain
            // fake binaries in StubTestSample/GGP/Debug.
            vsFake.ProjectAdapter.Load(_sampleName);
            (vsFake.ProjectAdapter as ProjectAdapter)?.SetDeployOnLaunch(
                DeployOnLaunchSetting.FALSE);

            return vsFake;
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
            vsiOptions.LaunchGameApiFlow = LaunchGameApiFlowFlag.ENABLED;
            return vsiOptions;
        }
    }
}