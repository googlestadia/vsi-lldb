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

using System.Collections.Generic;
using System.Linq;

namespace Metrics.Shared
{
    /// <summary>
    /// This class is a stub for a proto. It uses a constant hash
    /// as it is only required to be able to override equality
    /// for testing purposes.
    /// </summary>
    public class MountRemoteData
    {
        /// <summary>
        /// Randomly-generated whenever new host is mounted.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// A code which MountRemote.Program.OnRunMountRemote returns.
        /// </summary>
        public int? ProcessStartStatus { get; set; }

        public HostsUpdateData HostsUpdateData { get; set; }

        public ReconnectData ReconnectData { get; set; }

        /// <summary>
        /// Exception data. Can be sent with any MR_ event or
        /// with a generic MR_EXCEPTION event.
        /// </summary>
        public List<VSIExceptionData> ExceptionData { get; set; }

        public MountRemoteData()
        {
            ExceptionData = new List<VSIExceptionData>();
        }

        public override int GetHashCode() => 42;

        public override bool Equals(object other) =>
            Equals(other as MountRemoteData);

        public bool Equals(MountRemoteData other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (!Equals(other.SessionId, SessionId))
            {
                return false;
            }

            if (!Equals(other.ProcessStartStatus, ProcessStartStatus))
            {
                return false;
            }

            if (!Equals(other.HostsUpdateData, HostsUpdateData))
            {
                return false;
            }

            if (!Equals(other.ReconnectData, ReconnectData))
            {
                return false;
            }

            if (other.ExceptionData != ExceptionData &&
                (other.ExceptionData == null || ExceptionData == null) ||
                !other.ExceptionData.SequenceEqual(ExceptionData))
            {
                return false;
            }

            return true;
        }

        public MountRemoteData Clone()
        {
            var clone = (MountRemoteData) MemberwiseClone();
            clone.HostsUpdateData = HostsUpdateData?.Clone();
            clone.ReconnectData = ReconnectData?.Clone();
            clone.ExceptionData = ExceptionData?.Select(x => x.Clone()).ToList();

            return clone;
        }

        public void MergeFrom(MountRemoteData other)
        {
            if (other.ExceptionData != null)
            {
                if (ExceptionData == null)
                {
                    ExceptionData = new List<VSIExceptionData>();
                }

                ExceptionData.AddRange(other.ExceptionData);
            }

            if (other.SessionId != null)
            {
                SessionId = other.SessionId;
            }

            if (other.ProcessStartStatus != null)
            {
                ProcessStartStatus = other.ProcessStartStatus.Value;
            }

            if (other.HostsUpdateData != null)
            {
                if (HostsUpdateData == null)
                {
                    HostsUpdateData = new HostsUpdateData();
                }

                HostsUpdateData.MergeFrom(other.HostsUpdateData);
            }

            if (other.ReconnectData != null)
            {
                if (ReconnectData == null)
                {
                    ReconnectData = new ReconnectData();
                }

                ReconnectData.MergeFrom(other.ReconnectData);
            }
        }
    }
}
