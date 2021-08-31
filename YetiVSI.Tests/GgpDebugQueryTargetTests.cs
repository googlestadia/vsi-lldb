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
using YetiCommon.SSH;
using YetiVSI.DebugEngine;
using YetiVSI.GameLaunch;
using YetiVSI.Metrics;
using YetiVSI.ProjectSystem.Abstractions;
using YetiVSI.Shared.Metrics;
using YetiVSITestsCommon;

namespace YetiVSI.Test
{
    [TestFixture]
    class GgpDebugQueryTargetTests
    {
        const string _testGameletId = "gamelet/id";
        const string _testGameletIp = "1.2.3.4";
        const string _testGameletName = "test/gamelet/name";
        const string _testProjectDir = "test/project/dir";
        const string _testApplicationId = "test_application_id";
        const string _testApplicationName = "test/application/name";
        const string _platformName = "platform_name";
        const string _testAccount = "test@account.com";
        const string _testDebugSessionId = "abc123";
        const string _testProjectId = "project123";
        const string _testOrganizationId = "organization123";
        const string _testTestAccountId = "testaccount123";
        const string _externalAccount = "external-id";
        const string _externalAccountId = "12345";
        const string _sdkVersionString = "1.22.1.7456";
        const string _customQueryParams = "test1=5&test2=10";
        readonly Versions.SdkVersion _sdkVersion = Versions.SdkVersion.Create("7456.1.22.1");

        IGameletClient _gameletClient;
        IDialogUtil _dialogUtil;
        IRemoteDeploy _remoteDeploy;
        IYetiVSIService _yetiVsiService;
        IMetrics _metrics;
        IApplicationClient _applicationClient;
        IIdentityClient _identityClient;
        TestAccountClient.Factory _testAccountClientFactory;
        IGameletSelector _gameletSelector;
        IGameletSelectorFactory _gameletSelectorFactory;
        ChromeClientLaunchCommandFormatter _launchCommandFormatter;
        YetiVSI.DebugEngine.DebugEngine.Params.Factory _paramsFactory;
        readonly int _outVariableIndex = 5;
        IAsyncProject _project;
        string _targetPath;
        string _outputDirectory;
        GgpDebugQueryTarget _ggpDebugQueryTarget;
        IGameLauncher _gameLauncher;
        IVsiGameLaunch _gameLaunch;
        IProjectPropertiesMetricsParser _projectPropertiesParser;

        [SetUp]
        public void SetUp()
        {
            _targetPath = "/any/old/target/path";
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(_targetPath, new MockFileData(""));

            _outputDirectory = Path.GetTempPath();

            _project = Substitute.For<IAsyncProject>();
            _project.GetTargetPathAsync().Returns(_targetPath);
            _project.GetTargetDirectoryAsync().Returns(Path.GetDirectoryName(_targetPath));
            _project.GetTargetFileNameAsync().Returns(Path.GetFileName(_targetPath));
            _project.GetOutputDirectoryAsync().Returns(_outputDirectory);
            _project.GetAbsoluteRootPathAsync().Returns(_testProjectDir);
            _project.GetApplicationAsync().Returns(_testApplicationName);
            _project.GetQueryParamsAsync().Returns(_customQueryParams);

            var sdkConfigFactory = Substitute.For<SdkConfig.Factory>();
            var sdkConfig = new SdkConfig();
            sdkConfig.OrganizationId = _testOrganizationId;
            sdkConfig.ProjectId = _testProjectId;
            sdkConfigFactory.LoadOrDefault().Returns(sdkConfig);

            _gameletClient = Substitute.For<IGameletClient>();
            var gameletClientFactory = Substitute.For<GameletClient.Factory>();
            gameletClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(_gameletClient);

            _identityClient = Substitute.For<IIdentityClient>();

            var remoteCommand = Substitute.For<IRemoteCommand>();
            _remoteDeploy = Substitute.For<IRemoteDeploy>();
            _dialogUtil = Substitute.For<IDialogUtil>();

            var credentialManager = Substitute.For<YetiCommon.ICredentialManager>();
            credentialManager.LoadAccount().Returns(_testAccount);

            var taskContext = new JoinableTaskContext();
            var cancelableTaskFactory =
                FakeCancelableTask.CreateFactory(taskContext, false);

            _applicationClient = Substitute.For<IApplicationClient>();
            var application = new Application
            {
                Id = _testApplicationId, Name = _testApplicationName, PlatformName = _platformName
            };
            _applicationClient.LoadByNameOrIdAsync(_testApplicationName)
                .Returns(Task.FromResult(application));

            var player = new Player() { Name = _externalAccountId, ExternalId = _externalAccount };
            _identityClient.SearchPlayers(_platformName, _externalAccount)
                .Returns(new List<Player>() { player });
            var applicationClientFactory = Substitute.For<ApplicationClient.Factory>();
            applicationClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(_applicationClient);

            _testAccountClientFactory = Substitute.For<TestAccountClient.Factory>();
            var testAccount = new TestAccount()
            {
                Name = $"organizations/{_testOrganizationId}" +
                    $"/projects/{_testProjectId}/testAccounts/{_testTestAccountId}"
            };
            var testAccountClient = Substitute.For<ITestAccountClient>();
            testAccountClient
                .LoadByIdOrGamerTagAsync(_testOrganizationId, _testProjectId, _testTestAccountId)
                .Returns(new List<TestAccount> { testAccount });
            _testAccountClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(testAccountClient);

            _yetiVsiService = Substitute.For<IYetiVSIService>();
            var options = Substitute.For<IExtensionOptions>();
            var debuggerOptions = new YetiVSI.DebuggerOptions.DebuggerOptions();
            _yetiVsiService.DebuggerOptions.Returns(debuggerOptions);
            options.LaunchGameApiFlow.Returns(LaunchGameApiFlow.ENABLED);
            _yetiVsiService.Options.Returns(options);
            _metrics = Substitute.For<IMetrics>();
            _metrics.NewDebugSessionId().Returns(_testDebugSessionId);
            var debugMetrics = new DebugSessionMetrics(_metrics);
            var cloudRunner = new CloudRunner(sdkConfigFactory, credentialManager,
                                              new CloudConnection(), new GgpSDKUtil());
            _gameletSelector = Substitute.For<IGameletSelector>();
            _gameletSelectorFactory = Substitute.For<IGameletSelectorFactory>();
            _gameletSelectorFactory.Create(Arg.Any<bool>(), Arg.Any<ActionRecorder>())
                .Returns(_gameletSelector);
            var serializer = new JsonUtil();
            _launchCommandFormatter = new ChromeClientLaunchCommandFormatter(serializer);
            _paramsFactory = new YetiVSI.DebugEngine.DebugEngine.Params.Factory(serializer);

            _gameLauncher = Substitute.For<IGameLauncher>();
            _gameLauncher.LaunchGameApiEnabled.Returns(false);
            _gameLaunch = Substitute.For<IVsiGameLaunch>();
            _gameLaunch.LaunchName.Returns("launch_name");

            _projectPropertiesParser = Substitute.For<IProjectPropertiesMetricsParser>();
            _projectPropertiesParser.GetStadiaProjectPropertiesAsync(Arg.Any<IAsyncProject>())
                .Returns(Task.FromResult((VSIProjectProperties)null));

            _ggpDebugQueryTarget = new GgpDebugQueryTarget(fileSystem, sdkConfigFactory,
                                                           gameletClientFactory,
                                                           applicationClientFactory,
                                                           cancelableTaskFactory, _dialogUtil,
                                                           _remoteDeploy, debugMetrics,
                                                           credentialManager,
                                                           _testAccountClientFactory,
                                                           _gameletSelectorFactory, cloudRunner,
                                                           _sdkVersion, _launchCommandFormatter,
                                                           _paramsFactory, _yetiVsiService,
                                                           _gameLauncher, taskContext,
                                                           _projectPropertiesParser,
                                                           _identityClient);
        }

        [Test]
        public async Task LaunchNoDebugLegacyFlowAsync([Values(false, true)] bool renderdoc,
                                                       [Values(false, true)] bool rgp,
                                                       [Values(false, true)] bool dive,
                                                       [Values(null, "optprintasserts")]
                                                       string vulkanDriverVariant)
        {
            _project.GetLaunchRenderDocAsync().Returns(renderdoc);
            _project.GetLaunchRgpAsync().Returns(rgp);
            _project.GetLaunchDiveAsync().Returns(dive);
            _project.GetVulkanDriverVariantAsync().Returns(vulkanDriverVariant);
            Gamelet gamelet = SetupReservedGamelet();

            var launchSettings = await QueryDebugTargetsAsync(DebugLaunchOptions.NoDebug);
            Assert.AreEqual(1, launchSettings.Count);
            Assert.AreEqual(DebugLaunchOptions.NoDebug | DebugLaunchOptions.MergeEnvironment,
                            launchSettings[0].LaunchOptions);
            Assert.AreEqual(_testProjectDir, launchSettings[0].CurrentDirectory);
            Assert.AreEqual(Environment.SystemDirectory + "\\cmd.exe",
                            launchSettings[0].Executable);

            _launchCommandFormatter.Parse(launchSettings[0].Arguments,
                                          out LaunchParams launchParams, out _);
            Assert.AreEqual(await _project.GetTargetFileNameAsync(), launchParams.Cmd);
            Assert.AreEqual(renderdoc, launchParams.RenderDoc);
            Assert.AreEqual(rgp, launchParams.Rgp);
            Assert.AreEqual(dive, launchParams.Dive);
            Assert.AreEqual(_testApplicationName, launchParams.ApplicationName);
            Assert.AreEqual(_testGameletName, launchParams.GameletName);
            Assert.AreEqual(_testAccount, launchParams.Account);
            Assert.IsFalse(launchParams.Debug);
            Assert.AreEqual(_sdkVersionString, launchParams.SdkVersion);
            Assert.AreEqual(launchParams.VulkanDriverVariant, vulkanDriverVariant);
            Assert.AreEqual(launchParams.QueryParams, _customQueryParams);

            await _remoteDeploy.Received().DeployGameExecutableAsync(
                _project, new SshTarget(gamelet), Arg.Any<ICancelable>(), Arg.Any<IAction>());

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDeployBinary,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task LaunchNoDebugAsync([Values(false, true)] bool renderdoc,
                                             [Values(false, true)] bool rgp,
                                             [Values(false, true)] bool dive,
                                             [Values(null, "optprintasserts")]
                                             string vulkanDriverVariant,
                                             [Values(StadiaEndpoint.PlayerEndpoint,
                                                     StadiaEndpoint.TestClient,
                                                     StadiaEndpoint.AnyEndpoint)]
                                             StadiaEndpoint endpoint)
        {
            _project.GetLaunchRenderDocAsync().Returns(renderdoc);
            _project.GetLaunchRgpAsync().Returns(rgp);
            _project.GetLaunchDiveAsync().Returns(dive);
            _project.GetVulkanDriverVariantAsync().Returns(vulkanDriverVariant);
            _project.GetEndpointAsync().Returns(endpoint);
            Gamelet gamelet = SetupReservedGamelet();

            _gameLauncher.LaunchGameApiEnabled.Returns(true);
            _gameLauncher.CreateLaunch(Arg.Any<LaunchParams>())
                .Returns(_gameLaunch);

            var launchSettings = await QueryDebugTargetsAsync(DebugLaunchOptions.NoDebug);
            Assert.That(launchSettings.Count, Is.EqualTo(1));
            Assert.That(DebugLaunchOptions.NoDebug | DebugLaunchOptions.MergeEnvironment,
                        Is.EqualTo(launchSettings[0].LaunchOptions));
            Assert.That(launchSettings[0].CurrentDirectory, Is.EqualTo(_testProjectDir));
            Assert.That(launchSettings[0].Executable,
                        Is.EqualTo(Environment.SystemDirectory + "\\cmd.exe"));

            if (endpoint == StadiaEndpoint.AnyEndpoint)
            {
                Assert.That(launchSettings[0].Arguments, Contains.Substring("exit"));
            }
            else
            {
                _launchCommandFormatter.Parse(launchSettings[0].Arguments,
                                              out LaunchParams launchParams,
                                              out string launchName);
                Assert.That(launchParams.Account, Is.EqualTo(_testAccount));
                Assert.That(launchParams.SdkVersion, Is.EqualTo(_sdkVersionString));
                Assert.That(launchParams.Endpoint, Is.EqualTo(endpoint));
                Assert.That(launchName, Is.EqualTo(_gameLaunch.LaunchName));
            }

            await _remoteDeploy.Received()
                .DeployGameExecutableAsync(_project, new SshTarget(gamelet),
                                           Arg.Any<ICancelable>(),
                                           Arg.Any<IAction>());

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDeployBinary,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task LaunchNoDebugWithNullLaunchNameAsync()
        {
            _project.GetLaunchRenderDocAsync().Returns(false);
            _project.GetLaunchRgpAsync().Returns(false);
            _project.GetLaunchDiveAsync().Returns(false);
            _project.GetVulkanDriverVariantAsync().Returns("optprintasserts");

            SetupReservedGamelet();

            _gameLauncher.LaunchGameApiEnabled.Returns(true);
            _gameLauncher.CreateLaunch(Arg.Any<LaunchParams>())
                .Returns((IVsiGameLaunch) null);

            var launchSettings = await QueryDebugTargetsAsync(DebugLaunchOptions.NoDebug);
            Assert.That(launchSettings.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task LaunchNoDebugDeployFailsAsync()
        {
            Gamelet gamelet = SetupReservedGamelet();

            _remoteDeploy
                .DeployGameExecutableAsync(_project, new SshTarget(gamelet),
                                           Arg.Any<ICancelable>(),
                                           Arg.Any<IAction>())
                .Returns(x => throw new DeployException("deploy exception",
                                                        new ProcessException("ssh failed")));

            await QueryDebugTargetsAsync(DebugLaunchOptions.NoDebug);
            _dialogUtil.Received().ShowError("deploy exception", Arg.Any<string>());

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDeployBinary,
                                 DeveloperEventStatus.Types.Code.ExternalToolUnavailable);
        }

        [Test]
        public async Task LaunchDebugNullServerApplicationAsync()
        {
            Application application = null;
            _applicationClient.LoadByNameOrIdAsync(_testApplicationName).Returns(application);

            await QueryDebugTargetsAsync(0);
            _dialogUtil.Received().ShowError(Arg.Any<string>(), Arg.Any<string>());

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.InvalidObjectState);
        }

        [Test]
        public async Task LaunchDebugNoApplicationAsync()
        {
            _project.GetApplicationAsync().Returns("");

            await QueryDebugTargetsAsync(0);
            _dialogUtil.Received().ShowError(Arg.Any<string>(), Arg.Any<string>());

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.InvalidConfiguration);
        }

        [Test]
        public async Task LaunchDebugAsync([Values(false, true)] bool renderdoc,
                                           [Values(false, true)] bool rgp,
                                           [Values(false, true)] bool dive,
                                           [Values(null, "optprintasserts")]
                                           string vulkanDriverVariant)
        {
            _project.GetLaunchRenderDocAsync().Returns(renderdoc);
            _project.GetLaunchRgpAsync().Returns(rgp);
            _project.GetLaunchDiveAsync().Returns(dive);
            _project.GetVulkanDriverVariantAsync().Returns(vulkanDriverVariant);
            Gamelet gamelet = SetupReservedGamelet();

            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(1, launchSettings.Count);
            Assert.AreEqual(DebugLaunchOptions.MergeEnvironment, launchSettings[0].LaunchOptions);
            Assert.AreEqual(YetiConstants.DebugEngineGuid, launchSettings[0].LaunchDebugEngineGuid);
            Assert.AreEqual(_testProjectDir, launchSettings[0].CurrentDirectory);
            var parameters = _paramsFactory.Deserialize(launchSettings[0].Options);
            Assert.AreEqual(parameters.TargetIp, $"{_testGameletIp}:44722");
            Assert.AreEqual(parameters.DebugSessionId, _testDebugSessionId);
            Assert.AreEqual(await _project.GetTargetPathAsync(), launchSettings[0].Executable);

            var launchParams =
                _launchCommandFormatter.DecodeLaunchParams(launchSettings[0].Arguments);
            Assert.AreEqual(await _project.GetTargetFileNameAsync(), launchParams.Cmd);
            Assert.AreEqual(renderdoc, launchParams.RenderDoc);
            Assert.AreEqual(rgp, launchParams.Rgp);
            Assert.AreEqual(dive, launchParams.Dive);
            Assert.AreEqual(_testApplicationName, launchParams.ApplicationName);
            Assert.AreEqual(_testGameletName, launchParams.GameletName);
            Assert.AreEqual(_testAccount, launchParams.Account);
            Assert.IsTrue(launchParams.Debug);
            Assert.AreEqual(_sdkVersionString, launchParams.SdkVersion);
            Assert.AreEqual(launchParams.VulkanDriverVariant, vulkanDriverVariant);
            Assert.AreEqual(launchParams.QueryParams, _customQueryParams);

            await _remoteDeploy.Received().DeployGameExecutableAsync(
                _project, new SshTarget(gamelet), Arg.Any<ICancelable>(), Arg.Any<IAction>());

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.Success);
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDeployBinary,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task LaunchTestAccountAsync()
        {
            _project.GetTestAccountAsync().Returns(_testTestAccountId);
            SetupReservedGamelet();

            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(1, launchSettings.Count);

            var gameLaunchParams =
                _launchCommandFormatter.DecodeLaunchParams(launchSettings[0].Arguments);
            Assert.That(gameLaunchParams.TestAccount,
                        Is.EqualTo($"organizations/{_testOrganizationId}" +
                                   $"/projects/{_testProjectId}/testAccounts/{_testTestAccountId}"));

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task LaunchIncorrectTestAccountAsync()
        {
            _project.GetTestAccountAsync().Returns("wrong");
            SetupReservedGamelet();

            var testAccountClient = Substitute.For<ITestAccountClient>();
            testAccountClient.LoadByIdOrGamerTagAsync(_testOrganizationId, _testProjectId, "wrong")
                .Returns(Task.FromResult(new List<TestAccount>()));
            _testAccountClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(testAccountClient);
            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(0, launchSettings.Count);

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.InvalidConfiguration);
        }

        [Test]
        public async Task LaunchTestAccountGamerTagAsync()
        {
            const string testTestAccountGamerTag = "test#123";
            _project.GetTestAccountAsync().Returns(testTestAccountGamerTag);
            var testAccount = new TestAccount()
            {
                Name = $"organizations/{_testOrganizationId}" +
                    $"/projects/{_testProjectId}/testAccounts/{_testTestAccountId}",
                GamerTagName = "test",
                GamerTagSuffix = 123
            };
            var testAccountClient = Substitute.For<ITestAccountClient>();
            testAccountClient
                .LoadByIdOrGamerTagAsync(_testOrganizationId, _testProjectId,
                                         testTestAccountGamerTag)
                .Returns(new List<TestAccount> { testAccount });
            _testAccountClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(testAccountClient);
            SetupReservedGamelet();

            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(1, launchSettings.Count);
            var launchParams =
                _launchCommandFormatter.DecodeLaunchParams(launchSettings[0].Arguments);
            Assert.That(launchParams.TestAccount,
                        Is.EqualTo($"organizations/{_testOrganizationId}" +
                                   $"/projects/{_testProjectId}/testAccounts/{_testTestAccountId}"));

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task LaunchTestAccountGamerTagNameAmbiguousAsync()
        {
            const string testTestAccountGamerTagName = "test";
            const string testTestAccountId1 = "testid456";
            _project.GetTestAccountAsync().Returns(testTestAccountGamerTagName);
            var testAccount = new TestAccount()
            {
                Name = $"organizations/{_testOrganizationId}" +
                    $"/projects/{_testProjectId}/testAccounts/{_testTestAccountId}",
                GamerTagName = "test",
                GamerTagSuffix = 123
            };
            var testAccount1 = new TestAccount()
            {
                Name = $"organizations/{_testOrganizationId}" +
                    $"/projects/{_testProjectId}/testAccounts/{testTestAccountId1}",
                GamerTagName = "test",
                GamerTagSuffix = 456
            };
            var testAccountClient = Substitute.For<ITestAccountClient>();
            testAccountClient
                .LoadByIdOrGamerTagAsync(_testOrganizationId, _testProjectId,
                                         testTestAccountGamerTagName)
                .Returns(new List<TestAccount> { testAccount, testAccount1 });
            _testAccountClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(testAccountClient);
            SetupReservedGamelet();

            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(0, launchSettings.Count);

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.InvalidConfiguration);
        }

        [TestCase(StadiaEndpoint.PlayerEndpoint, TestName = "PlayerEndpoint")]
        [TestCase(StadiaEndpoint.AnyEndpoint, TestName = "AnyEndpoint")]
        public async Task LaunchTestAccountNotSupportedWithEndpointAsync(StadiaEndpoint endpoint)
        {
            const string testTestAccountGamerTagName = "test";
            _project.GetTestAccountAsync().Returns(testTestAccountGamerTagName);
            _project.GetEndpointAsync().Returns(endpoint);
            var testAccount = new TestAccount()
            {
                Name = $"organizations/{_testOrganizationId}" +
                    $"/projects/{_testProjectId}/testAccounts/{_testTestAccountId}",
                GamerTagName = testTestAccountGamerTagName,
                GamerTagSuffix = 123
            };
            var testAccountClient = Substitute.For<ITestAccountClient>();
            testAccountClient
                .LoadByIdOrGamerTagAsync(_testOrganizationId, _testProjectId,
                                         testTestAccountGamerTagName)
                .Returns(new List<TestAccount> { testAccount });
            _testAccountClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(testAccountClient);
            SetupReservedGamelet();

            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(1, launchSettings.Count);
            _dialogUtil.Received(1).ShowWarning(Arg.Is<string>(
                s => s.Contains("Test accounts are not supported")));
            LaunchParams launchParams = _launchCommandFormatter.DecodeLaunchParams(
                launchSettings[0].Arguments);
            Assert.IsNull(launchParams.TestAccount);
            Assert.IsNull(launchParams.TestAccountGamerName);

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task LaunchExternalIdAsync()
        {
            _project.GetExternalIdAsync().Returns(_externalAccount);
            SetupReservedGamelet();

            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.That(launchSettings.Count, Is.EqualTo(1));

            LaunchParams gameLaunchParams =
                _launchCommandFormatter.DecodeLaunchParams(launchSettings[0].Arguments);
            Assert.That(gameLaunchParams.ExternalAccount, Is.EqualTo(_externalAccountId));

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task LaunchWrongExternalIdReturnsErrorAsync()
        {
            _project.GetExternalIdAsync().Returns("wrongAccount");
            _identityClient.SearchPlayers(_platformName, "wrongAccount")
                .Returns(new List<Player>());

            var launchSettings = await QueryDebugTargetsAsync(0);

            Assert.That(launchSettings.Count, Is.EqualTo(0));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.InvalidConfiguration);
        }

        [Test]
        public async Task LaunchExternalIdMultipleAccountsAsync()
        {
            _project.GetExternalIdAsync().Returns("wrongAccount");
            _identityClient.SearchPlayers(_platformName, "wrongAccount")
                .Returns(new List<Player>() { new Player(), new Player() });

            var launchSettings = await QueryDebugTargetsAsync(0);

            Assert.That(launchSettings.Count, Is.EqualTo(0));
            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.InvalidConfiguration);
        }

        [Test]
        public async Task LaunchTestAccountNotSupportedWithExternalAccountAsync()
        {
            const string testTestAccountGamerTagName = "test";
            _project.GetTestAccountAsync().Returns(testTestAccountGamerTagName);
            _project.GetExternalIdAsync().Returns(_externalAccount);
            var testAccount = new TestAccount()
            {
                Name = $"organizations/{_testOrganizationId}" +
                    $"/projects/{_testProjectId}/testAccounts/{_testTestAccountId}",
                GamerTagName = testTestAccountGamerTagName,
                GamerTagSuffix = 123
            };
            var testAccountClient = Substitute.For<ITestAccountClient>();
            testAccountClient
                .LoadByIdOrGamerTagAsync(_testOrganizationId, _testProjectId,
                                         testTestAccountGamerTagName)
                .Returns(new List<TestAccount> { testAccount });
            _testAccountClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(testAccountClient);
            SetupReservedGamelet();

            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(1, launchSettings.Count);
            _dialogUtil.Received(1).ShowWarning(Arg.Is<string>(
                s => s.Contains("test accounts aren't compatible with external IDs")));
            LaunchParams launchParams = _launchCommandFormatter.DecodeLaunchParams(
                launchSettings[0].Arguments);
            Assert.IsNull(launchParams.TestAccount);
            Assert.IsNull(launchParams.TestAccountGamerName);

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.Success);
        }

        [Test]
        public async Task LaunchExternalAccountNotSupportedWithEndpointAsync()
        {
            _project.GetEndpointAsync().Returns(StadiaEndpoint.PlayerEndpoint);
            _project.GetExternalIdAsync().Returns(_externalAccount);
            SetupReservedGamelet();

            var launchSettings = await QueryDebugTargetsAsync(0);
            Assert.AreEqual(0, launchSettings.Count);
            _dialogUtil.Received(1)
                .ShowError(
                    Arg.Is<string>(s => s.Contains("web player endpoint option") &&
                                       s.Contains("it isn't compatible with external IDs")),
                    Arg.Any<string>());

            AssertMetricRecorded(DeveloperEventType.Types.Type.VsiDebugSetupQueries,
                                 DeveloperEventStatus.Types.Code.InvalidConfiguration);
        }

        [Test]
        public async Task LaunchGameletPreparationFailsAsync([Values(DebugLaunchOptions.NoDebug, 0)]
                                                             DebugLaunchOptions debugLaunchOptions)
        {
            var gamelets = new List<Gamelet>
            {
                new Gamelet
                {
                    Id = _testGameletId,
                    Name = _testGameletName,
                    IpAddr = _testGameletIp,
                    State = GameletState.InUse,
                }
            };
            _gameletClient.ListGameletsAsync().Returns(Task.FromResult(gamelets));

            Gamelet gamelet;
            _gameletSelector.TrySelectAndPrepareGamelet(Arg.Any<string>(),
                                                        Arg.Any<DeployOnLaunchSetting>(), gamelets,
                                                        Arg.Any<TestAccount>(), Arg.Any<string>(),
                                                        out gamelet).Returns(false);

            var result = await QueryDebugTargetsAsync(debugLaunchOptions);

            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task LaunchGameletPreparationThrowsAsync(
            [Values(DebugLaunchOptions.NoDebug, 0)]
            DebugLaunchOptions debugLaunchOptions)
        {
            var gamelets = new List<Gamelet>
            {
                new Gamelet
                {
                    Id = _testGameletId,
                    Name = _testGameletName,
                    IpAddr = _testGameletIp,
                    State = GameletState.InUse,
                }
            };
            _gameletClient.ListGameletsAsync().Returns(Task.FromResult(gamelets));

            Gamelet gamelet;
            _gameletSelector.When(g => g.TrySelectAndPrepareGamelet(
                                      Arg.Any<string>(), Arg.Any<DeployOnLaunchSetting>(), gamelets,
                                      Arg.Any<TestAccount>(), Arg.Any<string>(), out gamelet))
                .Throw(c => new Exception("Oops!"));

            var result = await QueryDebugTargetsAsync(debugLaunchOptions);
            _dialogUtil.Received().ShowError("Oops!", Arg.Any<string>());
            Assert.That(result.Count, Is.EqualTo(0));
        }

        Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(
            DebugLaunchOptions launchOptions)
        {
            return _ggpDebugQueryTarget.QueryDebugTargetsAsync(_project, launchOptions);
        }

        void AssertMetricRecorded(DeveloperEventType.Types.Type type,
                                  DeveloperEventStatus.Types.Code status)
        {
            _metrics.Received().RecordEvent(
                type,
                Arg.Is<DeveloperLogEvent>(p => p.StatusCode == status &&
                                              p.DebugSessionIdStr == _testDebugSessionId));
        }

        Gamelet SetupReservedGamelet()
        {
            var gamelet = new Gamelet
            {
                Id = _testGameletId,
                Name = _testGameletName,
                IpAddr = _testGameletIp,
                State = GameletState.Reserved,
            };
            _gameletClient.ListGameletsAsync().Returns(
                Task.FromResult(new List<Gamelet> { gamelet }));
            _gameletSelector.TrySelectAndPrepareGamelet(Arg.Any<string>(),
                                                       Arg.Any<DeployOnLaunchSetting>(),
                                                       Arg.Any<List<Gamelet>>(),
                                                       Arg.Any<TestAccount>(), Arg.Any<string>(),
                                                       out Gamelet _).Returns(x =>
                                                       {
                                                           x[_outVariableIndex] = gamelet;
                                                           return true;
                                                       });
            return gamelet;
        }
    }
}