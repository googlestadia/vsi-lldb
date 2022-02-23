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
using System.Diagnostics;
using System.Threading.Tasks;
using YetiCommon;
using YetiCommon.SSH;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;

namespace YetiVSI
{
    // Used to enable SSH on a gamelet, by uploading a locally generated development SSH key.
    public interface ISshManager
    {
        // Generates a local development SSH key (if one does not exist) and pushes it to the
        // provided gamelet. Throws a SshKeyException or CloudException if these respective
        // operations fail. Also records additional information in the Metrics.Action.
        Task EnableSshAsync(Gamelet gamelet, Metrics.IAction action);
    }

    public class SshManager : SshManagerBase, ISshManager
    {
        readonly IGameletClientFactory gameletClientFactory;
        readonly ICloudRunner cloudRunner;

        public SshManager(IGameletClientFactory gameletClientFactory, ICloudRunner cloudRunner,
                          ISshKeyLoader sshKeyLoader, ISshKnownHostsWriter sshKnownHostsWriter,
                          IRemoteCommand remoteCommand)
            : base(sshKeyLoader, sshKnownHostsWriter, remoteCommand)
        {
            this.gameletClientFactory = gameletClientFactory;
            this.cloudRunner = cloudRunner;
        }

        public async Task EnableSshAsync(Gamelet gamelet, Metrics.IAction action)
        {
            action.UpdateEvent(new DeveloperLogEvent
            {
                GameletData = GameletData.FromGamelet(gamelet)
            });
            var gameletClient = gameletClientFactory.Create(cloudRunner.Intercept(action));
            await EnableSshForGameletAsync(gamelet, gameletClient);
        }
    }
}
