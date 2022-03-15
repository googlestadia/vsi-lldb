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
    public class CopyBinaryData
    {
        public bool? CopyAttempted { get; set; }
        public BinarySignatureCheck.Types.Result? SignatureCheckResult { get; set; }
        public BinarySignatureCheck.Types.ErrorCode? SignatureCheckErrorCode { get; set; }
        public long? CopyBinaryBytes { get; set; }
        public int? CopyExitCode { get; set; }
        public int? SshChmodExitCode { get; set; }
        public double? CopyLatencyMs { get; set; }
        public long? TransferredBinaryBytes { get; set;}
        public CopyBinaryType.Types.DeploymentMode? DeploymentMode { get; set;}
        public double? BinaryDiffEncodingLatencyMs { get; set;}
        public double? BinaryDiffDecodingLatencyMs { get; set;}

        public override int GetHashCode()
        {
            return 42;
        }

        public override bool Equals(object other) => Equals(other as CopyBinaryData);

        public bool Equals(CopyBinaryData other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (other.CopyAttempted != CopyAttempted)
            {
                return false;
            }

            if (other.SignatureCheckResult != SignatureCheckResult)
            {
                return false;
            }

            if (other.SignatureCheckErrorCode != SignatureCheckErrorCode)
            {
                return false;
            }

            if (other.CopyBinaryBytes != CopyBinaryBytes)
            {
                return false;
            }

            if (other.CopyExitCode != CopyExitCode)
            {
                return false;
            }

            if (other.SshChmodExitCode != SshChmodExitCode)
            {
                return false;
            }

            if (other.CopyLatencyMs != CopyLatencyMs)
            {
                return false;
            }

            if (other.TransferredBinaryBytes != TransferredBinaryBytes)
            {
                return false;
            }

            if (other.DeploymentMode != DeploymentMode)
            {
                return false;
            }

            if (other.BinaryDiffEncodingLatencyMs != BinaryDiffEncodingLatencyMs)
            {
                return false;
            }

            if (other.BinaryDiffDecodingLatencyMs != BinaryDiffDecodingLatencyMs)
            {
                return false;
            }

            return true;
        }

        public CopyBinaryData Clone() => (CopyBinaryData) MemberwiseClone();

        public void MergeFrom(CopyBinaryData other)
        {
            if (other.CopyAttempted.HasValue)
            {
                CopyAttempted = other.CopyAttempted;
            }

            if (other.SignatureCheckResult.HasValue)
            {
                SignatureCheckResult = other.SignatureCheckResult;
            }

            if (other.SignatureCheckErrorCode.HasValue)
            {
                SignatureCheckErrorCode = other.SignatureCheckErrorCode;
            }

            if (other.CopyBinaryBytes.HasValue)
            {
                CopyBinaryBytes = other.CopyBinaryBytes;
            }

            if (other.CopyExitCode.HasValue)
            {
                CopyExitCode = other.CopyExitCode;
            }

            if (other.SshChmodExitCode.HasValue)
            {
                SshChmodExitCode = other.SshChmodExitCode;
            }

            if (other.CopyLatencyMs.HasValue)
            {
                CopyLatencyMs = other.CopyLatencyMs;
            }

            if (other.TransferredBinaryBytes.HasValue)
            {
                TransferredBinaryBytes = other.TransferredBinaryBytes;
            }

            if (other.DeploymentMode.HasValue)
            {
                DeploymentMode = other.DeploymentMode;
            }

            if (other.BinaryDiffEncodingLatencyMs.HasValue)
            {
                BinaryDiffEncodingLatencyMs = other.BinaryDiffEncodingLatencyMs;
            }

            if (other.BinaryDiffDecodingLatencyMs.HasValue)
            {
                BinaryDiffDecodingLatencyMs = other.BinaryDiffDecodingLatencyMs;
            }
        }

    }
}