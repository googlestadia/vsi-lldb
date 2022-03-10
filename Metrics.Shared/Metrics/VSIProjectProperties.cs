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

namespace YetiVSI.Shared.Metrics
{
    /// <summary>
    /// This class is a stub for a proto. It uses a constant hash
    /// as it is only required to be able to override equality
    /// for testing purposes.
    /// </summary>
    public class VSIProjectProperties
    {
        /// <summary>
        /// Project properties from the Debugging section..
        /// </summary>
        public Types.Debugging Debugging { get; set; }

        public override int GetHashCode() => 42;

        public override bool Equals(object other) => Equals(other as VSIProjectProperties);

        public bool Equals(VSIProjectProperties other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            return Equals(other.Debugging, Debugging);
        }

        public VSIProjectProperties Clone() => (VSIProjectProperties)MemberwiseClone();

        public void MergeFrom(VSIProjectProperties other)
        {
            if (other.Debugging != null)
            {
                if (Debugging == null)
                {
                    Debugging = new Types.Debugging();
                }

                Debugging.MergeFrom(other.Debugging);
            }
        }

        public class Types
        {
            public class Debugging
            {
                public Types.DeployExecutableOnLaunch? DeployExecutableOnLaunch { get; set; }

                public Types.SurfaceEnforcement? SurfaceEnforcementMode { get; set; }

                public BoolValue LaunchWithRenderDoc { get; set; }

                public BoolValue LaunchWithRgp { get; set; }

                public Types.VulkanDriverVariant? VulkanDriverVariant { get; set; }

                public Types.StadiaEndpoint? StadiaEndpoint { get; set; }

                public override int GetHashCode() => 42;

                public override bool Equals(object other) => Equals(other as Debugging);

                public bool Equals(Debugging other)
                {
                    if (ReferenceEquals(other, null))
                    {
                        return false;
                    }

                    if (ReferenceEquals(other, this))
                    {
                        return true;
                    }

                    if (other.DeployExecutableOnLaunch != DeployExecutableOnLaunch)
                    {
                        return false;
                    }

                    if (other.SurfaceEnforcementMode != SurfaceEnforcementMode)
                    {
                        return false;
                    }

                    if (!Equals(other.LaunchWithRenderDoc, LaunchWithRenderDoc))
                    {
                        return false;
                    }

                    if (!Equals(other.LaunchWithRgp, LaunchWithRgp))
                    {
                        return false;
                    }

                    if (other.VulkanDriverVariant != VulkanDriverVariant)
                    {
                        return false;
                    }

                    if (other.StadiaEndpoint != StadiaEndpoint)
                    {
                        return false;
                    }

                    return true;
                }

                public Debugging Clone() => (Debugging) MemberwiseClone();

                public void MergeFrom(Debugging other)
                {
                    if (other.DeployExecutableOnLaunch != null)
                    {
                        DeployExecutableOnLaunch = other.DeployExecutableOnLaunch;
                    }

                    if (other.SurfaceEnforcementMode != null)
                    {
                        SurfaceEnforcementMode = other.SurfaceEnforcementMode;
                    }

                    if (other.LaunchWithRenderDoc != null)
                    {
                        if (LaunchWithRenderDoc == null)
                        {
                            LaunchWithRenderDoc = new BoolValue();
                        }

                        LaunchWithRenderDoc.MergeFrom(other.LaunchWithRenderDoc);
                    }

                    if (other.LaunchWithRgp != null)
                    {
                        if (LaunchWithRgp == null)
                        {
                            LaunchWithRgp = new BoolValue();
                        }

                        LaunchWithRgp.MergeFrom(other.LaunchWithRgp);
                    }

                    if (other.VulkanDriverVariant != null)
                    {
                        VulkanDriverVariant = other.VulkanDriverVariant;
                    }

                    if (other.StadiaEndpoint != null)
                    {
                        StadiaEndpoint = other.StadiaEndpoint;
                    }
                }

                public class Types
                {
                    public enum DeployExecutableOnLaunch
                    {
                        DeployDefault = 0,
                        DeployNo = 1,
                        YesWhenChanged = 2,
                        YesAlways = 3,
                        YesBinaryDiff = 4,
                        DeployOther = 5,
                    }

                    public enum SurfaceEnforcement
                    {
                        SurfaceDefault = 0,
                        SurfaceOff = 1,
                        SurfaceWarn = 2,
                        SurfaceBlock = 3,
                        SurfaceOther = 4,
                    }

                    public enum VulkanDriverVariant
                    {
                        VulkanDefault = 0,
                        Optimized = 1,
                        PrintingAssertions = 2,
                        TrappingAssertions = 3,
                        VulkanOther = 4,
                    }

                    public enum StadiaEndpoint
                    {
                        EndpointDefault = 0,
                        TestClient = 1,
                        PlayerWebEndpoint = 2,
                        AnyEndpoint = 3,
                        EndpointOther = 4,
                    }
                }
            }

            public class BoolValue
            {
                public Types.Value? Value { get; set; }

                public bool? IsDefault { get; set; }

                public override int GetHashCode() => 42;

                public override bool Equals(object other) => Equals(other as BoolValue);

                public bool Equals(BoolValue other)
                {
                    if (ReferenceEquals(other, null))
                    {
                        return false;
                    }

                    if (ReferenceEquals(other, this))
                    {
                        return true;
                    }

                    if (other.Value != Value)
                    {
                        return false;
                    }

                    if (other.IsDefault != IsDefault)
                    {
                        return false;
                    }

                    return true;
                }

                public BoolValue Clone() => (BoolValue)MemberwiseClone();

                public void MergeFrom(BoolValue other)
                {
                    if (other.Value != null)
                    {
                        Value = other.Value;
                    }

                    if (other.IsDefault != null)
                    {
                        IsDefault = other.IsDefault;
                    }
                }

                public class Types
                {
                    public enum Value
                    {
                        BoolYes = 0,
                        BoolNo = 1,
                        BoolOther = 2,
                    }
                }
            }
        }
    }
}
