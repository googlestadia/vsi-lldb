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

using System;
using System.Threading.Tasks;

namespace YetiVSI.GameLaunch
{
    public partial class GameLauncher
    {
        public async Task<object> GetLaunchStateAsync()
        {
            if (!CurrentLaunchExists)
            {
                // TODO: return Error status (internal).
            }

            // TODO: implement polling for state (internal).
            GgpGrpc.Models.GameLaunch state =
                await _gameletClient.GetGameLaunchStateAsync(_launchName);
            throw new NotImplementedException();
        }

        public async Task StopGameAsync()
        {
            await _gameletClient.DeleteGameLaunchAsync(_launchName);
        }
    }
}
