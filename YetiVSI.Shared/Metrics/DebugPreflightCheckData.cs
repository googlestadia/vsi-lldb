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

﻿namespace YetiVSI.Shared.Metrics
{
    /// <summary>
    /// This class is a stub for a proto. It uses a constant hash
    /// as it is only required to be able to override equality
    /// for testing purposes.
    /// </summary>
    public class DebugPreflightCheckData
    {
        public class Types
        {
            public enum CheckType
            {
                AttachOnly,
                RunAndAttach,
            }
            public enum RemoteBuildIdCheckResult
            {
                ValidRemoteBuildId,
                InvalidRemoteBuildId,
                RemoteBinaryError,
                RemoteCommandError,
            }

            public enum LocalBinarySearchResult
            {
                NoCandidates,
                BinaryMismatch,
                BinaryMatch,
            }
        }

        public Types.CheckType? CheckType { get; set; }

        public Types.RemoteBuildIdCheckResult? RemoteBuildIdCheckResult { get; set; }

        public Types.LocalBinarySearchResult? LocalBinarySearchResult { get; set; }

        public override int GetHashCode()
        {
            return 42;
        }

        public override bool Equals(object other) => Equals(other as DebugPreflightCheckData);

        public bool Equals(DebugPreflightCheckData other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (other.CheckType != CheckType)
            {
                return false;
            }

            if (other.RemoteBuildIdCheckResult != RemoteBuildIdCheckResult)
            {
                return false;
            }

            if (other.LocalBinarySearchResult != LocalBinarySearchResult)
            {
                return false;
            }

            return true;
        }

        public DebugPreflightCheckData Clone() => (DebugPreflightCheckData) MemberwiseClone();

        public void MergeFrom(DebugPreflightCheckData other)
        {
            if (other.CheckType.HasValue)
            {
                CheckType = other.CheckType;
            }

            if (other.RemoteBuildIdCheckResult.HasValue)
            {
                RemoteBuildIdCheckResult = other.RemoteBuildIdCheckResult;
            }

            if (other.LocalBinarySearchResult.HasValue)
            {
                LocalBinarySearchResult = other.LocalBinarySearchResult;
            }
        }
    }
}
