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

using GgpGrpc;
using GgpGrpc.Cloud;
using GgpGrpc.Models;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.Cloud;
using YetiCommon.VSProject;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSITestsCommon;

namespace YetiVSI.Test
{
    [TestFixture]
    class GgpDebugQueryTargetTests
    {
        const string TEST_GAMELET_ID1 = "gameletid1";
        const string TEST_GAMELET_ID2 = "gameletid2";
        const string TEST_GAMELET_IP1 = "1.2.3.4";
        const string TEST_GAMELET_IP2 = "1.2.3.5";
        const string TEST_PROJECT_DIR = "test/project/dir";
        const string TEST_APPLICATION_ID = "test_application_id";
        const string TEST_APPLICATION_NAME = "test application name";
        const string TEST_ACCOUNT = "test account";
        const string TEST_DEBUG_SESSION_ID = "abc123";
        const string TEST_PROJECT_ID = "project123";
        const string TEST_ORGANIZATION_ID = "organization123";
        const string TEST_TEST_ACCOUNT_ID = "testaccount123";
        const string SDK_VERSION_STRING = "1.22.1.7456";
        const string CUSTOM_QUERY_PARAMS = "test1=5&test2=10";
        readonly Versions.SdkVersion SDK_VERSION = Versions.SdkVersion.Create("7456.1.22.1");

        IGameletClient gameletClient;
        IDialogUtil dialogUtil;
        IRemoteDeploy remoteDeploy;
        ServiceManager serviceManager;
        YetiVSIService yetiVsiService;
        IMetrics metrics;
        IApplicationClient applicationClient;
        TestAccountClient.Factory testAccountClientFactory;
        IGameletSelector gameletSelector;
        ChromeClientLaunchCommandFormatter launchCommandFormatter;
        IExtensionOptions options;
        YetiVSI.DebugEngine.DebugEngine.Params.Factory paramsFactory;
        readonly int _outVariableIndex = 4;
        IAsyncProject project;
        string targetPath;
        string outputDirectory;
        GgpDebugQueryTarget ggpDebugQueryTarget;

        [SetUp]
        public void SetUp()
        {
            targetPath = "/any/old/target/path";
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(targetPath, new MockFileData(""));

            outputDirectory = Path.GetTempPath();

            project = Substitute.For<IAsyncProject>();
            project.GetTargetPathAsync().Returns(targetPath);
            project.GetTargetDirectoryAsync().Returns(Path.GetDirectoryName(targetPath));
            project.GetTargetFileNameAsync().Returns(Path.GetFileName(targetPath));
            project.GetOutputDirectoryAsync().Returns(outputDirectory);
            project.GetAbsoluteRootPathAsync().Returns(TEST_PROJECT_DIR);
            project.GetApplicationAsync().Returns(TEST_APPLICATION_NAME);
            project.GetQueryParamsAsync().Returns(CUSTOM_QUERY_PARAMS);

            var sdkConfigFactory = Substitute.For<SdkConfig.Factory>();
            var sdkConfig = new SdkConfig();
            sdkConfig.OrganizationId = TEST_ORGANIZATION_ID;
            sdkConfig.ProjectId = TEST_PROJECT_ID;
            sdkConfigFactory.LoadOrDefault().Returns(sdkConfig);

            gameletClient = Substitute.For<IGameletClient>();
            var gameletClientFactory = Substitute.For<GameletClient.Factory>();
            gameletClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(gameletClient);

            var remoteCommand = Substitute.For<IRemoteCommand>();
            remoteDeploy = Substitute.For<IRemoteDeploy>();
            dialogUtil = Substitute.For<IDialogUtil>();

            var credentialManager = Substitute.For<YetiCommon.ICredentialManager>();
            credentialManager.LoadAccount().Returns(TEST_ACCOUNT);

            var cancelableTaskFactory =
                FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false);

            applicationClient = Substitute.For<IApplicationClient>();
            var application = new Application() { Id = TEST_APPLICATION_ID };
            applicationClient.LoadByNameOrIdAsync(TEST_APPLICATION_NAME)
                .Returns(Task.FromResult(application));
            var applicationClientFactory = Substitute.For<ApplicationClient.Factory>();
            applicationClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(applicationClient);

            testAccountClientFactory = Substitute.For<TestAccountClient.Factory>();
            var testAccount = new TestAccount()
            {
                Name = $"organizations/{TEST_ORGANIZATION_ID}" +
                       $"/projects/{TEST_PROJECT_ID}/testAccounts/{TEST_TEST_ACCOUNT_ID}"
            };
            var testAccountClient = Substitute.For<ITestAccountClient>();
            testAccountClient
                .LoadByIdOrGamerTagAsync(TEST_ORGANIZATION_ID, TEST_PROJECT_ID,
                                         TEST_TEST_ACCOUNT_ID)
                .Returns(new List<TestAccount> { testAccount });
            testAccountClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(testAccountClient);

            options = Substitute.For<IExtensionOptions>();

            serviceManager = Substitute.For<ServiceManager>();
            yetiVsiService = new YetiVSIService(null);
            serviceManager.GetGlobalService(typeof(YetiVSIService)).Returns(yetiVsiService);
            metrics = Substitute.For<IMetrics>();
            metrics.NewDebugSessionId().Returns(TEST_DEBUG_SESSION_ID);
            var cloudRunner = new CloudRunner(sdkConfigFactory, credentialManager,
                                              new CloudConnection(), new GgpSDKUtil());
            gameletSelector = Substitute.For<IGameletSelector>();
            var serializer = new JsonUtil();
            launchCommandFormatter = new ChromeClientLaunchCommandFormatter(serializer);
            paramsFactory = new YetiVSI.DebugEngine.DebugEngine.Params.Factory(serializer);
            ggpDebugQueryTarget = new GgpDebugQueryTarget(
                fileSystem, sdkConfigFactory, gameletClientFactory, applicationClientFactory,
                options, cancelableTaskFactory, dialogUtil, remoteDeploy, metrics, serviceManager,
                credentialManager, testAccountClientFactory, gameletSelector, cloudRunner,
                SDK_VERSION, launchCommandFormatter, paramsFactory);
        }

        [Test]
        public async Task LaunchNoDebugAsync([Values(false, true)] bool renderdoc,
                                             [Values(false, true)] bool rgp,
                                             [Values(null,
                                                     "optprintasserts")] string vulkanDriverVariant)
        {
            project.GetLaunchRenderDocAsync().Returns(renderdoc);
            project.GetLaunchRgpAsync().Returns(rgp);
            project.GetVulkanDriverVariantAsync().Returns(vulkanDriverVariant);

            var gamelets = new List<Gamelet> { new Gamelet {
                Id = TEST_GAMELET_ID1,
                IpAddr = TEST_GAMELET_IP1,
                State = GameletState.Reserved,
            } };
            gameletClient.ListGameletsAsync().Returns(Task.FromResult(gamelets));

            Gamelet gamelet;
            gameletSelector
                .TrySelectAndPrepareGamelet(Arg.Any<string>(), Arg.Any<DeployOnLaunchSetting>(),
                                            Arg.Any<ActionRecorder>(), gamelets, out gamelet)
                .Returns(x => {
                    x[_outVariableIndex] = gamelets[0];
                    return true;
                });

            var launchSettings = await QueryDebugTargetsAsync(DebugLaunchOptions.NoDebug);
            Assert.AreEqual(1, launchSettings.Count);
            Assert.AreEqual(DebugLaunchOptions.NoDebug | DebugLaunchOptions.MergeEnvironment,
                            launchSettings[0].LaunchOptions);
            Assert.AreEqual(TEST_PROJECT_DIR, launchSettings[0].CurrentDirectory);
            Assert.AreEqual(Environment.SystemDirectory + "\\cmd.exe",
                            launchSettings[0].Executable);

            var launchParams = launchCommandFormatter.Parse(launchSettings[0].Arguments);
            Assert.AreEqual(await project.GetTargetFileNameAsync(), launchParams.Cmd);
            Assert.AreEqual(renderdoc, launchParams.RenderDoc);
            Assert.AreEqual(rgp, launchParams.Rgp);
            Assert.AreEqual(TEST_APPLICATION_ID, launchParams.ApplicationId);
            Assert.AreEqual(TEST_GAMELET_ID1, launchParams.GameletId);
            Assert.AreEqual(TEST_ACCOUNT, launchParams.Account);
            Assert.IsFalse(launchParams.Debug);
            Assert.AreEqual(SDK_VERSION_STRING, launchParams.SdkVersion);
            Assert.AreEqual(launchParams.VulkanDriverVariant, vulkanDriverVariant);
            Assert.AreEqual(launchParams.QueryParams, CUSTOM_QUERY_PARAMS);

            await remoteDeploy.Received().DeployGameExecutableAsync(
                project, gamelets[0], Arg.Any<ICancelable>(), Arg.Any<IAction>());

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDeployBinary,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task LaunchNoDebugDeployFailsAsync()
        {
            var gamelets = new List<Gamelet> { new Gamelet {
                Id = TEST_GAMELET_ID1,
                IpAddr = TEST_GAMELET_IP1,
                State = GameletState.Reserved,
            } };
            gameletClient.ListGameletsAsync().Returns(Task.FromResult(gamelets));

            Gamelet gamelet;
            gameletSelector
                .TrySelectAndPrepareGamelet(Arg.Any<string>(), Arg.Any<DeployOnLaunchSetting>(),
                                            Arg.Any<ActionRecorder>(), gamelets, out gamelet)
                .Returns(x => {
                    x[_outVariableIndex] = gamelets[0];
                    return true;
                });

            remoteDeploy
                .DeployGameExecutableAsync(project, gamelets[0], Arg.Any<ICancelable>(),
                                           Arg.Any<IAction>())
                .Returns(x => {
                    throw new DeployException("deploy exception",
                                              new ProcessException("ssh failed"));
                });

            await QueryDebugTargetsAsync(DebugLaunchOptions.NoDebug);
            dialogUtil.Received().ShowError("deploy exception", Arg.Any<string>());

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDeployBinary,
                                 DeveloperEventStatus.Types.Code.ExternalToolUnavailable);
        }

        [Test]
        public async Task LaunchDebugNullServerApplicationAsync()
        {
            Application application = null;
            applicationClient.LoadByNameOrIdAsync(TEST_APPLICATION_NAME).Returns(application);

            await QueryDebugTargetsAsync(0);
            dialogUtil.Received().ShowError(Arg.Any<string>(), Arg.Any<string>());

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.InvalidObjectState);
        }

        [Test]
        public async Task LaunchDebugNoApplicationAsync()
        {
            project.GetApplicationAsync().Returns("");

            await QueryDebugTargetsAsync(0);
            dialogUtil.Received().ShowError(Arg.Any<string>(), Arg.Any<string>());

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.InvalidConfiguration);
        }

        [Test]
        public async Task LaunchDebugAsync([Values(false, true)] bool renderdoc,
                                           [Values(false, true)] bool rgp,
                                           [Values(null,
                                                   "optprintasserts")] string vulkanDriverVariant)
        {
            project.GetLaunchRenderDocAsync().Returns(renderdoc);
            project.GetLaunchRgpAsync().Returns(rgp);
            project.GetVulkanDriverVariantAsync().Returns(vulkanDriverVariant);

            var gamelets = new List<Gamelet> { new Gamelet {
                Id = TEST_GAMELET_ID1,
                IpAddr = TEST_GAMELET_IP1,
                State = GameletState.Reserved,
            } };
            gameletClient.ListGameletsAsync().Returns(Task.FromResult(gamelets));

            Gamelet gamelet;
            gameletSelector
                .TrySelectAndPrepareGamelet(Arg.Any<string>(), Arg.Any<DeployOnLaunchSetting>(),
                                            Arg.Any<ActionRecorder>(), gamelets, out gamelet)
                .Returns(x => {
                    x[_outVariableIndex] = gamelets[0];
                    return true;
                });

            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(1, launchSettings.Count);
            Assert.AreEqual(DebugLaunchOptions.MergeEnvironment, launchSettings[0].LaunchOptions);
            Assert.AreEqual(YetiConstants.DebugEngineGuid, launchSettings[0].LaunchDebugEngineGuid);
            Assert.AreEqual(TEST_PROJECT_DIR, launchSettings[0].CurrentDirectory);
            var parameters = paramsFactory.Deserialize(launchSettings[0].Options);
            Assert.AreEqual(parameters.TargetIp, $"{TEST_GAMELET_IP1}:44722");
            Assert.AreEqual(parameters.DebugSessionId, TEST_DEBUG_SESSION_ID);
            Assert.AreEqual(await project.GetTargetPathAsync(), launchSettings[0].Executable);

            var launchParams =
                launchCommandFormatter.DecodeLaunchParams(launchSettings[0].Arguments);
            Assert.AreEqual(await project.GetTargetFileNameAsync(), launchParams.Cmd);
            Assert.AreEqual(renderdoc, launchParams.RenderDoc);
            Assert.AreEqual(rgp, launchParams.Rgp);
            Assert.AreEqual(TEST_APPLICATION_ID, launchParams.ApplicationId);
            Assert.AreEqual(TEST_GAMELET_ID1, launchParams.GameletId);
            Assert.AreEqual(TEST_ACCOUNT, launchParams.Account);
            Assert.IsTrue(launchParams.Debug);
            Assert.AreEqual(SDK_VERSION_STRING, launchParams.SdkVersion);
            Assert.AreEqual(launchParams.VulkanDriverVariant, vulkanDriverVariant);
            Assert.AreEqual(launchParams.QueryParams, CUSTOM_QUERY_PARAMS);

            await remoteDeploy.Received().DeployGameExecutableAsync(
                project, gamelets[0], Arg.Any<ICancelable>(), Arg.Any<IAction>());

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDeployBinary,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task LaunchTestAccountAsync()
        {
            project.GetTestAccountAsync().Returns(TEST_TEST_ACCOUNT_ID);
            var gamelets = new List<Gamelet> { new Gamelet {
                Id = TEST_GAMELET_ID1,
                IpAddr = TEST_GAMELET_IP1,
                State = GameletState.Reserved,
            } };
            gameletClient.ListGameletsAsync().Returns(Task.FromResult(gamelets));

            Gamelet gamelet;
            gameletSelector
                .TrySelectAndPrepareGamelet(Arg.Any<string>(), Arg.Any<DeployOnLaunchSetting>(),
                                            Arg.Any<ActionRecorder>(), gamelets, out gamelet)
                .Returns(x => {
                    x[_outVariableIndex] = gamelets[0];
                    return true;
                });

            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(1, launchSettings.Count);

            var gameLaunchParams =
                launchCommandFormatter.DecodeLaunchParams(launchSettings[0].Arguments);
            Assert.That(
                gameLaunchParams.TestAccount,
                Is.EqualTo($"organizations/{TEST_ORGANIZATION_ID}" +
                           $"/projects/{TEST_PROJECT_ID}/testAccounts/{TEST_TEST_ACCOUNT_ID}"));

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task LaunchIncorrectTestAccountAsync()
        {
            project.GetTestAccountAsync().Returns("wrong");
            var gamelet = new Gamelet
            {
                Id = TEST_GAMELET_ID1,
                IpAddr = TEST_GAMELET_IP1,
                State = GameletState.Reserved,
            };
            gameletClient.ListGameletsAsync().Returns(
                Task.FromResult(new List<Gamelet> { gamelet }));

            var testAccountClient = Substitute.For<ITestAccountClient>();
            testAccountClient
                .LoadByIdOrGamerTagAsync(TEST_ORGANIZATION_ID, TEST_PROJECT_ID, "wrong")
                .Returns(Task.FromResult(new List<TestAccount>()));
            testAccountClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(testAccountClient);
            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(0, launchSettings.Count);

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.InvalidConfiguration);
        }

        [Test]
        public async Task LaunchTestAccountGamerTagAsync()
        {
            const string TEST_TEST_ACCOUNT_GAMER_TAG = "test#123";
            project.GetTestAccountAsync().Returns(TEST_TEST_ACCOUNT_GAMER_TAG);
            var testAccount = new TestAccount()
            {
                Name = $"organizations/{TEST_ORGANIZATION_ID}" +
                       $"/projects/{TEST_PROJECT_ID}/testAccounts/{TEST_TEST_ACCOUNT_ID}",
                GamerTagName = "test",
                GamerTagSuffix = 123
            };
            var testAccountClient = Substitute.For<ITestAccountClient>();
            testAccountClient
                .LoadByIdOrGamerTagAsync(TEST_ORGANIZATION_ID, TEST_PROJECT_ID,
                                         TEST_TEST_ACCOUNT_GAMER_TAG)
                .Returns(new List<TestAccount> { testAccount });
            testAccountClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(testAccountClient);
            var gamelets = new List<Gamelet> { new Gamelet {
                Id = TEST_GAMELET_ID1,
                IpAddr = TEST_GAMELET_IP1,
                State = GameletState.Reserved,
            } };
            gameletClient.ListGameletsAsync().Returns(Task.FromResult(gamelets));

            Gamelet gamelet;
            gameletSelector
                .TrySelectAndPrepareGamelet(Arg.Any<string>(), Arg.Any<DeployOnLaunchSetting>(),
                                            Arg.Any<ActionRecorder>(), gamelets, out gamelet)
                .Returns(x => {
                    x[_outVariableIndex] = gamelets[0];
                    return true;
                });

            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(1, launchSettings.Count);
            var launchParams =
                launchCommandFormatter.DecodeLaunchParams(launchSettings[0].Arguments);
            Assert.That(
                launchParams.TestAccount,
                Is.EqualTo($"organizations/{TEST_ORGANIZATION_ID}" +
                           $"/projects/{TEST_PROJECT_ID}/testAccounts/{TEST_TEST_ACCOUNT_ID}"));

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task LaunchTestAccountGamerTagNameAmbiguousAsync()
        {
            const string TEST_TEST_ACCOUNT_GAMER_TAG_NAME = "test";
            const string TEST_TEST_ACCOUNT_ID_1 = "testid456";
            project.GetTestAccountAsync().Returns(TEST_TEST_ACCOUNT_GAMER_TAG_NAME);
            var testAccount = new TestAccount()
            {
                Name = $"organizations/{TEST_ORGANIZATION_ID}" +
                       $"/projects/{TEST_PROJECT_ID}/testAccounts/{TEST_TEST_ACCOUNT_ID}",
                GamerTagName = "test",
                GamerTagSuffix = 123
            };
            var testAccount1 = new TestAccount()
            {
                Name = $"organizations/{TEST_ORGANIZATION_ID}" +
                       $"/projects/{TEST_PROJECT_ID}/testAccounts/{TEST_TEST_ACCOUNT_ID_1}",
                GamerTagName = "test",
                GamerTagSuffix = 456
            };
            var testAccountClient = Substitute.For<ITestAccountClient>();
            testAccountClient
                .LoadByIdOrGamerTagAsync(TEST_ORGANIZATION_ID, TEST_PROJECT_ID,
                                         TEST_TEST_ACCOUNT_GAMER_TAG_NAME)
                .Returns(new List<TestAccount> { testAccount, testAccount1 });
            testAccountClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(testAccountClient);
            var gamelet = new Gamelet
            {
                Id = TEST_GAMELET_ID1,
                IpAddr = TEST_GAMELET_IP1,
                State = GameletState.Reserved,
            };
            gameletClient.ListGameletsAsync().Returns(
                Task.FromResult(new List<Gamelet> { gamelet }));

            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(0, launchSettings.Count);

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.InvalidConfiguration);
        }

        [Test]
        public async Task LaunchGameletPreparationFailsAsync(
            [Values(DebugLaunchOptions.NoDebug, 0)] DebugLaunchOptions debugLaunchOptions)
        {
            var gamelets = new List<Gamelet> { new Gamelet {
                Id = TEST_GAMELET_ID1,
                IpAddr = TEST_GAMELET_IP1,
                State = GameletState.InUse,
            } };
            gameletClient.ListGameletsAsync().Returns(Task.FromResult(gamelets));

            Gamelet gamelet;
            gameletSelector
                .TrySelectAndPrepareGamelet(Arg.Any<string>(), Arg.Any<DeployOnLaunchSetting>(),
                                            Arg.Any<ActionRecorder>(), gamelets, out gamelet)
                .Returns(false);

            var result = await QueryDebugTargetsAsync(debugLaunchOptions);

            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task LaunchGameletPreparationThrowsAsync(
            [Values(DebugLaunchOptions.NoDebug, 0)] DebugLaunchOptions debugLaunchOptions)
        {
            var gamelets = new List<Gamelet> { new Gamelet {
                Id = TEST_GAMELET_ID1,
                IpAddr = TEST_GAMELET_IP1,
                State = GameletState.InUse,
            } };
            gameletClient.ListGameletsAsync().Returns(Task.FromResult(gamelets));

            Gamelet gamelet;
            gameletSelector
                .When(g => g.TrySelectAndPrepareGamelet(
                          Arg.Any<string>(), Arg.Any<DeployOnLaunchSetting>(),
                          Arg.Any<ActionRecorder>(), gamelets, out gamelet))
                .Throw(c => new Exception("Oops!"));

            var result = await QueryDebugTargetsAsync(debugLaunchOptions);
            dialogUtil.Received().ShowError("Oops!", Arg.Any<string>());
            Assert.That(result.Count, Is.EqualTo(0));
        }

        Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(
            DebugLaunchOptions launchOptions)
        {
            return ggpDebugQueryTarget.QueryDebugTargetsAsync(project, launchOptions);
        }

        void AssertMetricRecorded(DeveloperEventType.Types.Type type,
                                          DeveloperEventStatus.Types.Code status)
        {
            metrics.Received().RecordEvent(
                type, Arg.Is<DeveloperLogEvent>(p => p.StatusCode == status &&
                                                     p.DebugSessionIdStr == TEST_DEBUG_SESSION_ID));
        }
    }
}