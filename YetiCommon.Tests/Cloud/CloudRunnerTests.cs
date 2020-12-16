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
using Grpc.Core;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using YetiCommon.Cloud;

namespace YetiCommon.Tests.Cloud
{
    [TestFixture]
    class CloudRunnerTests
    {
        const string TEST_ACCOUNT = "test account";
        const string TEST_PROJECT_ID = "test project id";
        const string TEST_ORG_ID = "test org id";
        const string TEST_TITLE = "test title";

        SdkConfig.Factory sdkConfigFactory;
        ICredentialManager credentialManager;
        ICloudConnection cloudConnection;

        GrpcProjectFunction funcToRun;

        CloudRunner cloudRunner;

        [SetUp]
        public void SetUp()
        {
            sdkConfigFactory = Substitute.For<SdkConfig.Factory>();
            credentialManager = Substitute.For<ICredentialManager>();
            cloudConnection = Substitute.For<ICloudConnection>();
            funcToRun = Substitute.For<GrpcProjectFunction>();
            // Since a channel doesn't initialize the connection until an API is called, and since
            // we don't call any APIs we can use a real channel object. Use an empty host - if
            // this behaviour changes in the future, the channel will fail to connect.
            var channel = new Channel("", ChannelCredentials.Insecure);
            cloudConnection.CreateChannel(Arg.Any<string>(), Arg.Any<string>()).Returns(channel);
            cloudRunner = new CloudRunner(sdkConfigFactory, credentialManager, cloudConnection,
                                          new GgpSDKUtil());
        }

        [Test]
        public async Task RunAsync()
        {
            var sdkConfig = new SdkConfig
                {ProjectId = TEST_PROJECT_ID, OrganizationId = TEST_ORG_ID};
            sdkConfigFactory.LoadOrDefault().Returns(sdkConfig);
            credentialManager.LoadAccount().Returns(TEST_ACCOUNT);

            await cloudRunner.RunAsync(TEST_TITLE, funcToRun);
            await funcToRun.Received()
                .Invoke(Arg.Any<CallInvoker>(), TEST_ACCOUNT, TEST_PROJECT_ID);
        }

        [TestCase("", TEST_PROJECT_ID, TEST_ORG_ID)]
        [TestCase(TEST_ACCOUNT, "", TEST_ORG_ID)]
        [TestCase(TEST_ACCOUNT, TEST_PROJECT_ID, "")]
        public async Task RunMisconfigurationAsync(string account, string project, string org)
        {
            var sdkConfig = new SdkConfig {ProjectId = project, OrganizationId = org};
            sdkConfigFactory.LoadOrDefault().Returns(sdkConfig);
            credentialManager.LoadAccount().Returns(account);

            var exception = Assert.CatchAsync<CloudException>(
                () => cloudRunner.RunAsync(TEST_TITLE, funcToRun));
            Assert.That(exception, Is.AssignableTo<IGgpConfigurationError>());
            await funcToRun.DidNotReceiveWithAnyArgs().Invoke(null, null, null);
        }

        [Test]
        public async Task RunNoChannelAsync()
        {
            var sdkConfig = new SdkConfig
                {ProjectId = TEST_PROJECT_ID, OrganizationId = TEST_ORG_ID};
            sdkConfigFactory.LoadOrDefault().Returns(sdkConfig);
            credentialManager.LoadAccount().Returns(TEST_ACCOUNT);

            cloudConnection.CreateChannel(Arg.Any<string>(), Arg.Any<string>())
                .Returns(x => { throw new ChannelConfigurationException("Test exception"); });

            var exception = Assert.ThrowsAsync<CloudException>(
                () => cloudRunner.RunAsync(TEST_TITLE, funcToRun));
            Assert.That(exception, Has.Message.Contains("Test exception"));
            await funcToRun.DidNotReceiveWithAnyArgs().Invoke(null, null, null);
        }

        [Test]
        public void RunNoAccess()
        {
            var sdkConfig = new SdkConfig
                {ProjectId = TEST_PROJECT_ID, OrganizationId = TEST_ORG_ID};
            sdkConfigFactory.LoadOrDefault().Returns(sdkConfig);
            credentialManager.LoadAccount().Returns(TEST_ACCOUNT);

            funcToRun.WhenForAnyArgs(x => x.Invoke(null, null, null)).Throw(
                new RpcException(new Status(StatusCode.PermissionDenied, "access denied")));

            var exception = Assert.ThrowsAsync<CloudException>(
                () => cloudRunner.RunAsync(TEST_TITLE, funcToRun));
            Assert.That(exception, Has.Message.Contains("access denied"));
        }
    }
}