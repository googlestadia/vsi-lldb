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

﻿using Grpc.Core;

namespace YetiVSI.Shared.Metrics
{
    /// <summary>
    /// This class is a stub for a proto. It uses a constant hash
    /// as it is only required to be able to override equality
    /// for testing purposes.
    /// </summary>
    public class GrpcServiceCallDetails
    {
        public Status? Status { get; set; }
        public string ServiceName { get; set; }
        public string ServiceMethod { get; set; }
        public long? RoundtripLatency { get; set; }

        public override int GetHashCode()
        {
            return 42;
        }

        public override bool Equals(object other) => Equals(other as GrpcServiceCallDetails);

        public bool Equals(GrpcServiceCallDetails other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (other.Status.HasValue != Status.HasValue ||
                other.Status.HasValue &&
                other.Status.Value.StatusCode != Status.Value.StatusCode)
            {
                return false;
            }

            if (other.ServiceName != ServiceName)
            {
                return false;
            }

            if (other.ServiceMethod != ServiceMethod)
            {
                return false;
            }

            if (other.RoundtripLatency != RoundtripLatency)
            {
                return false;
            }

            return true;
        }

        public GrpcServiceCallDetails Clone() => (GrpcServiceCallDetails) MemberwiseClone();

        public void MergeFrom(GrpcServiceCallDetails other)
        {
            if (other.Status.HasValue)
            {
                Status = other.Status;
            }

            if (other.ServiceName != null)
            {
                ServiceName = other.ServiceName;
            }

            if (other.ServiceMethod != null)
            {
                ServiceMethod = other.ServiceMethod;
            }

            if (other.RoundtripLatency.HasValue)
            {
                RoundtripLatency = other.RoundtripLatency;
            }
        }
    }
}
