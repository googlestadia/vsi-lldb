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

﻿using GgpGrpc.Models;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.Metrics
{
    public class GameletData
    {
        public static DeveloperLogEvent.Types.GameletData FromGamelet(Gamelet gamelet)
        {
            return new DeveloperLogEvent.Types.GameletData
            {
                State = (int) gamelet.State,
                Type = gamelet.DevkitId != ""
                    ? DeveloperLogEvent.Types.GameletData.Types.Type.GameletDevkit
                    : DeveloperLogEvent.Types.GameletData.Types.Type.GameletEdge
            };
        }
    }
}
