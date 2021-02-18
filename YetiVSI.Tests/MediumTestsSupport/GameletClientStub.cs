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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GgpGrpc.Cloud;
using GgpGrpc.Cloud.Interceptors;
using GgpGrpc.Models;
using Grpc.Core;

namespace YetiVSI.Test.MediumTestsSupport
{
    public class GameletClientStub : IGameletClient
    {
        public class Factory : IGameletClientFactory
        {
            readonly List<Gamelet> _instances = new List<Gamelet>();
            List<LaunchGameRequest> _lastLaunches;

            public Factory WithInstance(Gamelet instance)
            {
                _instances.Add(instance);
                return this;
            }

            public Factory WithSampleInstance() => WithInstance(new Gamelet
            {
                IpAddr = "1.2.3.4",
                Id = "edge/location-east-4/55e068g9120723an31a9051d77r8c783drs2",
                Name = "location-east-4/instance-1",
                PoolId = "dev-pool",
                DisplayName = "instance-1",
                DevkitId = "devkit-id",
                PublicKeys = new List<SshHostPublicKey>() { new SshHostPublicKey() },
                State = GameletState.Reserved,
            });

            public Factory WithLaunchRequestsTracker(List<LaunchGameRequest> lastLaunches)
            {
                _lastLaunches = lastLaunches;
                return this;
            }

            public IGameletClient Create(ICloudRunner runner) =>
                new GameletClientStub(_instances, _lastLaunches);
        }

        readonly List<LaunchGameRequest> _lastLaunches;
        readonly List<Gamelet> _instances;
        readonly IDictionary<string, GgpGrpc.Models.GameLaunch> _launchesByInstance =
            new Dictionary<string, GgpGrpc.Models.GameLaunch>();

        GameletClientStub(List<Gamelet> instances, List<LaunchGameRequest> lastLaunches)
        {
            _instances = instances;
            _lastLaunches = lastLaunches;
        }

        public Task<Gamelet> LoadByNameOrIdAsync(string value) =>
            throw new NotImplementedException();

        public Task<Gamelet> GetGameletAsync(string gameletId) =>
            Task.FromResult(_instances.Find(i => i.Id == gameletId));

        public Task<Gamelet> GetGameletByNameAsync(string instanceName) =>
            Task.FromResult(_instances.Find(i => i.Name == instanceName));

        public Task<List<Gamelet>> ListGameletsAsync() => Task.FromResult(_instances);

        public Task EnableSshAsync(string gameletId, string publicKey) => Task.CompletedTask;

        public Task StopGameAsync(string gameletId) => throw new NotImplementedException();

        public async Task<LaunchGameResponse> LaunchGameAsync(LaunchGameRequest launchRequest)
        {
            var instance = await GetGameletByNameAsync(launchRequest.GameletName);
            instance.State = GameletState.InUse;

            var launch = new GgpGrpc.Models.GameLaunch
            {
                GameLaunchState = GameLaunchState.RunningGame,
                GameletName = launchRequest.GameletName,
                Name = Guid.NewGuid().ToString()
            };
            _launchesByInstance.Add(launch.GameletName, launch);
            _lastLaunches?.Add(launchRequest);
            return new LaunchGameResponse { GameLaunchName = launch.Name };
        }

        public Task<GgpGrpc.Models.GameLaunch> GetGameLaunchStateAsync(string gameLaunchName)
        {
            GgpGrpc.Models.GameLaunch launch = null;
            if (gameLaunchName.EndsWith("current"))
            {
                Gamelet instanceInUse = _instances.Find(i => i.State == GameletState.InUse);
                if (instanceInUse != null)
                {
                    launch = _launchesByInstance[instanceInUse.Name];
                }
            }
            else
            {
                launch = _launchesByInstance.Values.ToList().Find(l => l.Name == gameLaunchName);
            }

            if (launch == null)
            {
                var rpcException =
                    new RpcException(new Status(StatusCode.NotFound,
                                                "no launch with the specified name found"));
                throw new CloudException("error getting launch status", rpcException);
            }

            return Task.FromResult(launch);
        }

        public Task<GameletSdkCompatibility> CheckSdkCompatibilityAsync(
            string gameletName, string sdkVersion) =>
            Task.FromResult(new GameletSdkCompatibility
            {
                CompatibilityResult = GameletSdkCompatibilityResult.Compatible
            });

        public Task<GgpGrpc.Models.GameLaunch> DeleteGameLaunchAsync(string gameLaunchName) =>
            throw new NotImplementedException();

        public Task<LaunchGameResponse> LaunchGameAsync(LaunchGameRequest launchRequest,
                                                        RpcRecorder recorder) =>
            LaunchGameAsync(launchRequest);


        public Task<GgpGrpc.Models.GameLaunch> GetGameLaunchStateAsync(
            string gameLaunchName, RpcRecorder recorder) => GetGameLaunchStateAsync(gameLaunchName);

        public Task<GameletSdkCompatibility> CheckSdkCompatibilityAsync(
            string gameletName, string sdkVersion, RpcRecorder recorder) =>
            CheckSdkCompatibilityAsync(gameletName, sdkVersion);

        public Task<GgpGrpc.Models.GameLaunch> DeleteGameLaunchAsync(
            string gameLaunchName, RpcRecorder recorder) => DeleteGameLaunchAsync(gameLaunchName);
    }
}