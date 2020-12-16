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
    public class DeployBinaryData
    {
        public class Types
        {
            public enum DeploySettings
            {
                UnknownDeploySettings,
                DeployNone,
                DeployCopy,
                DeployCustom,
                DeployCopyAndCustom,
            }
        }
        public Types.DeploySettings? Settings { get; set; }
        public long? CopyBinaryBytes { get; set; }
        public int? CopyExitCode { get; set; }
        public int? SshChmodExitCode { get; set; }
        public bool? CustomDeploySuccess { get; set; }
        public double? CopyLatencyMs { get; set; }
        public double? CustomLatencyMs { get; set; }

        public override int GetHashCode()
        {
            return 42;
        }

        public override bool Equals(object other) => Equals(other as DeployBinaryData);

        public bool Equals(DeployBinaryData other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (other.Settings != Settings)
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

            if (other.CustomDeploySuccess != CustomDeploySuccess)
            {
                return false;
            }

            if (other.CopyLatencyMs != CopyLatencyMs)
            {
                return false;
            }

            if (other.CustomLatencyMs != CustomLatencyMs)
            {
                return false;
            }

            return true;
        }

        public DeployBinaryData Clone() => (DeployBinaryData) MemberwiseClone();

        public void MergeFrom(DeployBinaryData other)
        {
            if (other.Settings.HasValue)
            {
                Settings = other.Settings;
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

            if (other.CustomDeploySuccess.HasValue)
            {
                CustomDeploySuccess = other.CustomDeploySuccess;
            }

            if (other.CopyLatencyMs.HasValue)
            {
                CopyLatencyMs = other.CopyLatencyMs;
            }

            if (other.CustomLatencyMs.HasValue)
            {
                CustomLatencyMs = other.CustomLatencyMs;
            }
        }
    }
}
