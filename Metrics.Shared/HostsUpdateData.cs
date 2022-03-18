// Copyright 2022 Google LLC
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
    public class HostsUpdateData
    {
        public class Types
        {
            /// <summary>
            /// Status of the service invocation.
            /// </summary>
            public enum InvokeStatus
            {
                MrInvokeUnknown,
                MrInvokeSuccess,
                MrInvokeGrpcError,
                MrInvokeOtherError
            }
        }

        public Types.InvokeStatus? InvokeStatus { get; set; }

        /// <summary>
        /// Grpc error code in case when
        /// <see cref="InvokeStatus"/> == <see cref="Types.InvokeStatus.MrInvokeGrpcError"/>.
        /// </summary>
        public int? GrpcStatus { get; set; }

        /// <summary>
        /// Number of hosts after the update event.
        /// </summary>
        public uint? HostCountAfter { get; set; }

        /// <summary>
        /// Number of hosts before the update event.
        /// </summary>
        public uint? HostCountBefore { get; set; }

        public override int GetHashCode() => 42;

        public override bool Equals(object other) =>
            Equals(other as HostsUpdateData);

        public bool Equals(HostsUpdateData other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (!Equals(other.InvokeStatus, InvokeStatus))
            {
                return false;
            }

            if (!Equals(other.GrpcStatus, GrpcStatus))
            {
                return false;
            }

            if (!Equals(other.HostCountAfter, HostCountAfter))
            {
                return false;
            }

            if (!Equals(other.HostCountBefore, HostCountBefore))
            {
                return false;
            }

            return true;
        }

        public HostsUpdateData Clone()
        {
            var clone = (HostsUpdateData) MemberwiseClone();

            return clone;
        }

        public void MergeFrom(HostsUpdateData other)
        {
            if (other.HostCountBefore.HasValue)
            {
                HostCountBefore = other.HostCountBefore;
            }

            if (other.HostCountAfter.HasValue)
            {
                HostCountAfter = other.HostCountAfter;
            }

            if (other.GrpcStatus.HasValue)
            {
                GrpcStatus = other.GrpcStatus;
            }

            if (other.InvokeStatus.HasValue)
            {
                InvokeStatus = other.InvokeStatus;
            }
        }
    }
}
