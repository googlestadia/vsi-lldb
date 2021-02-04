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
using GgpGrpc.Models;

namespace YetiVSI.GameLaunch
{
    public static class LaunchUtils
    {
        // Some of the statuses might not be applicable for the dev flow.
        public static string GetEndReason(GameLaunchEnded gameLaunchEnded)
        {
            switch (gameLaunchEnded.EndReason)
            {
                case EndReason.Unspecified:
                    return ErrorStrings.LaunchEndedUnspecified;
                case EndReason.ExitedByUser:
                    return ErrorStrings.LaunchEndedExitedByUser;
                case EndReason.InactivityTimeout:
                    return ErrorStrings.LaunchEndedInactivityTimeout;
                case EndReason.ClientNeverConnected:
                    return ErrorStrings.LaunchEndedClientNeverConnected;
                case EndReason.GameExitedWithSuccessfulCode:
                    return ErrorStrings.LaunchEndedGameExitedWithSuccessfulCode;
                case EndReason.GameExitedWithErrorCode:
                    return ErrorStrings.LaunchEndedGameExitedWithErrorCode;
                case EndReason.GameShutdownBySystem:
                    return ErrorStrings.LaunchEndedGameShutdownBySystem;
                case EndReason.UnexpectedGameShutdownBySystem:
                    return ErrorStrings.LaunchEndedUnexpectedGameShutdownBySystem;
                case EndReason.GameBinaryNotFound:
                    return ErrorStrings.LaunchEndedGameBinaryNotFound;
                case EndReason.QueueAbandonedByUser:
                    return ErrorStrings.LaunchEndedQueueAbandonedByUser;
                case EndReason.QueueReadyTimeout:
                    return ErrorStrings.LaunchEndedQueueReadyTimeout;
                default:
                    throw new ArgumentOutOfRangeException(nameof(gameLaunchEnded.EndReason),
                                                          gameLaunchEnded.EndReason,
                                                          "Unexpected end reason received.");
            }
        }
    }
}
