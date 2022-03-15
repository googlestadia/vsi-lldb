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
    public class VSIBoundBreakpointsData
    {
        public int? NumPendingBreakpoints { get; set; }
        public int? NumBoundBreakpoints { get; set; }

        public override int GetHashCode()
        {
            return 42;
        }

        public override bool Equals(object other) => Equals(other as VSIBoundBreakpointsData);

        public bool Equals(VSIBoundBreakpointsData other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (other.NumPendingBreakpoints != NumPendingBreakpoints)
            {
                return false;
            }

            if (other.NumBoundBreakpoints != NumBoundBreakpoints)
            {
                return false;
            }

            return true;
        }

        public VSIBoundBreakpointsData Clone() => (VSIBoundBreakpointsData) MemberwiseClone();

        public void MergeFrom(VSIBoundBreakpointsData other)
        {
            if (other.NumPendingBreakpoints.HasValue)
            {
                NumPendingBreakpoints = other.NumPendingBreakpoints;
            }

            if (other.NumBoundBreakpoints.HasValue)
            {
                NumBoundBreakpoints = other.NumBoundBreakpoints;
            }
        }
    }
}
