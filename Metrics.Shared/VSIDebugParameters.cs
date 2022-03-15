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
    public class VSIDebugParameters
    {
        public class Types
        {
            public class EnumOption
            {
                public string Name { get; set; }
                public uint? Value { get; set; }
                public bool? IsDefaultValue { get; set; }

                public override int GetHashCode()
                {
                    return 42;
                }

                public override bool Equals(object other) => Equals(other as EnumOption);

                public bool Equals(EnumOption other)
                {
                    if (ReferenceEquals(other, null))
                    {
                        return false;
                    }

                    if (ReferenceEquals(other, this))
                    {
                        return true;
                    }

                    if (other.Name != Name)
                    {
                        return false;
                    }

                    if (other.Value != Value)
                    {
                        return false;
                    }

                    if (other.IsDefaultValue != IsDefaultValue)
                    {
                        return false;
                    }

                    return true;
                }

                public EnumOption Clone() => (EnumOption) MemberwiseClone();
            }
        }

        public List<Types.EnumOption> ExtensionOptions { get; set; }
        public List<Types.EnumOption> ExperimentalOptions { get; set; }

        public VSIDebugParameters()
        {
            ExtensionOptions = new List<Types.EnumOption>();
            ExperimentalOptions = new List<Types.EnumOption>();
        }

        public override int GetHashCode()
        {
            return 42;
        }

        public override bool Equals(object other) => Equals(other as VSIDebugParameters);

        public bool Equals(VSIDebugParameters other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (other.ExtensionOptions != ExtensionOptions &&
                (other.ExtensionOptions == null || ExtensionOptions == null) ||
                !other.ExtensionOptions.SequenceEqual(ExtensionOptions))
            {
                return false;
            }

            if (other.ExperimentalOptions != ExperimentalOptions &&
                (other.ExperimentalOptions == null || ExperimentalOptions == null) ||
                !other.ExperimentalOptions.SequenceEqual(ExperimentalOptions))
            {
                return false;
            }

            return true;
        }

        public VSIDebugParameters Clone()
        {
           var clone = new VSIDebugParameters();
           clone.ExtensionOptions = ExtensionOptions?.Select(x => x.Clone()).ToList();
           clone.ExperimentalOptions = ExperimentalOptions?.Select(x => x.Clone()).ToList();
           return clone;
        }

        public void MergeFrom(VSIDebugParameters other)
        {
            if (other.ExtensionOptions != null)
            {
                if (ExtensionOptions == null)
                {
                    ExtensionOptions = new List<Types.EnumOption>();
                }

                ExtensionOptions.AddRange(other.ExtensionOptions);
            }

            if (other.ExperimentalOptions != null)
            {
                if (ExperimentalOptions == null)
                {
                    ExperimentalOptions = new List<Types.EnumOption>();
                }

                ExperimentalOptions.AddRange(other.ExperimentalOptions);
            }
        }
    }
}
