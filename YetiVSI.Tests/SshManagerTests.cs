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

using System.IO.Abstractions.TestingHelpers;
using GgpGrpc.Models;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using GgpGrpc.Cloud;
using Metrics.Shared;
using YetiCommon.SSH;
using YetiCommon;
using YetiVSI.Metrics;

namespace YetiVSI.Test
{
    [TestFixture]
    class SshManagerTests
    {
        SshKey fakeKey = new SshKey { PublicKey = "12345" };

        Gamelet fakeGamelet = new Gamelet
        {
            Id = "abc",
            IpAddr = "127.0.0.1",
            State = GameletState.Reserved
        };

        IAction fakeAction = new Action(DeveloperEventType.Types.Type.VsiGameletsEnableSsh,
                                        Substitute.For<Timer.Factory>(),
                                        Substitute.For<IVsiMetrics>());

        IGameletClient gameletClient;
        GameletClient.Factory gameletClientFactory;
        ICloudRunner cloudRunner;
        ISshKeyLoader sshKeyLoader;
        ISshKnownHostsWriter sshKnownHostsWriter;
        IRemoteCommand remoteCommand;
        MockFileSystem _fakeFileSystem;

        SshManager sshManager;

        [SetUp]
        public void SetUp()
        {
            gameletClient = Substitute.For<IGameletClient>();
            gameletClientFactory = Substitute.For<GameletClient.Factory>();
            gameletClientFactory.Create(Arg.Any<ICloudRunner>()).Returns(gameletClient);

            cloudRunner = Substitute.For<ICloudRunner>();
            sshKeyLoader = Substitute.For<ISshKeyLoader>();
            sshKnownHostsWriter = Substitute.For<ISshKnownHostsWriter>();
            remoteCommand = Substitute.For<IRemoteCommand>();
            _fakeFileSystem = new MockFileSystem();

            sshManager = new SshManager(gameletClientFactory, cloudRunner, sshKeyLoader,
                                        sshKnownHostsWriter, remoteCommand, _fakeFileSystem);
        }

        [Test]
        public async Task EnableSshOptimisticCheckSuccessAsync()
        {
            sshKeyLoader.LoadOrCreateAsync().Returns(fakeKey);

            await sshManager.EnableSshAsync(fakeGamelet, fakeAction);

            sshKnownHostsWriter.Received().CreateOrUpdate(fakeGamelet);
            await remoteCommand.Received().RunWithSuccessAsync(
                new SshTarget(fakeGamelet), Arg.Any<string>());
            await gameletClient.DidNotReceiveWithAnyArgs().EnableSshAsync(null, null);
        }

        [Test]
        public async Task EnableSshOptimisticCheckFailSshCommandAsync()
        {
            sshKeyLoader.LoadOrCreateAsync().Returns(fakeKey);
            remoteCommand.RunWithSuccessAsync(null, null).ReturnsForAnyArgs(
                Task.FromException(new YetiCommon.ProcessException("SSH exception")));

            await sshManager.EnableSshAsync(fakeGamelet, fakeAction);

            await gameletClient.Received(1).EnableSshAsync(fakeGamelet.Id, fakeKey.PublicKey);
        }

        [Test]
        public void EnableSshKeyCreateFailed()
        {
            sshKeyLoader.LoadOrCreateAsync()
                .Returns(Task.FromException<SshKey>(new SshKeyException("create exception")));

            Assert.ThrowsAsync<SshKeyException>(
                () => sshManager.EnableSshAsync(fakeGamelet, fakeAction));
        }

        [Test]
        public void EnableSshWriteKnownHostsFailed()
        {
            sshKeyLoader.LoadOrCreateAsync().Returns(fakeKey);
            sshKnownHostsWriter.When(x => x.CreateOrUpdate(Arg.Any<Gamelet>()))
                .Do(x => { throw new SshKeyException("test exception"); });

            Assert.ThrowsAsync<SshKeyException>(
                () => sshManager.EnableSshAsync(fakeGamelet, fakeAction));
        }

        [Test]
        public void EnableSshRpcFailed()
        {
            sshKeyLoader.LoadOrCreateAsync().Returns(fakeKey);
            remoteCommand.RunWithSuccessAsync(null, null).ReturnsForAnyArgs(
                Task.FromException(new YetiCommon.ProcessException("SSH exception")));
            gameletClient.EnableSshAsync(null, null).ReturnsForAnyArgs(
                Task.FromException(new CloudException("cloud exception")));

            Assert.ThrowsAsync<CloudException>(
                () => sshManager.EnableSshAsync(fakeGamelet, fakeAction));
        }

        [Test]
        public async Task EnableSshCreatesConfigAsync()
        {
            Assert.False(_fakeFileSystem.File.Exists(SDKUtil.GetSshConfigFilePath()));
            sshKeyLoader.LoadOrCreateAsync().Returns(fakeKey);
            await sshManager.EnableSshAsync(fakeGamelet, fakeAction);
            Assert.True(_fakeFileSystem.File.Exists(SDKUtil.GetSshConfigFilePath()));
        }
    }
}