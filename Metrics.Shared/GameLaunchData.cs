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

namespace Metrics.Shared
{
    /// <summary>
    /// This class is a stub for a proto. It uses a constant hash
    /// as it is only required to be able to override equality
    /// for testing purposes.
    /// </summary>
    public class GameLaunchData
    {
        /// <summary>
        /// A locally generated globally unique request identifier for every outgoing
        /// LaunchGame request. This is recorded only if the event generated a request.
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// The ID of a game launch, generated when a game launch is created.
        /// This should be recorded whenever the launch ID is available.
        /// </summary>
        public string LaunchId { get; set; }

        /// <summary>
        /// The reason a game session has ended. This should be recorded at most once
        /// per launch ID.
        /// </summary>
        public int? EndReason { get; set; }

        public override int GetHashCode()
        {
            return 42;
        }

        public override bool Equals(object other) => Equals(other as GameLaunchData);

        public bool Equals(GameLaunchData other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (other.RequestId != RequestId)
            {
                return false;
            }

            if (other.LaunchId != LaunchId)
            {
                return false;
            }

            if (other.EndReason != EndReason)
            {
                return false;
            }

            return true;
        }

        public GameLaunchData Clone() => (GameLaunchData)MemberwiseClone();

        public void MergeFrom(GameLaunchData other)
        {
            if (other.RequestId != null)
            {
                RequestId = other.RequestId;
            }

            if (other.LaunchId != null)
            {
                LaunchId = other.LaunchId;
            }

            if (other.EndReason.HasValue)
            {
                EndReason = other.EndReason;
            }
        }
    }
}
