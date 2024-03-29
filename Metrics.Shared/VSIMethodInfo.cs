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
    public class VSIMethodInfo
    {
        public string NamespaceName { get; set; }
        public string ClassName { get; set; }
        public string MethodName { get; set; }

        public override int GetHashCode()
        {
            return 42;
        }

        public override bool Equals(object other) => Equals(other as VSIMethodInfo);

        public bool Equals(VSIMethodInfo other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (other.NamespaceName != NamespaceName)
            {
                return false;
            }

            if (other.ClassName != ClassName)
            {
                return false;
            }

            if (other.MethodName != MethodName)
            {
                return false;
            }

            return true;
        }

        public VSIMethodInfo Clone() => (VSIMethodInfo) MemberwiseClone();

        public void MergeFrom(VSIMethodInfo other)
        {
            if (other.NamespaceName != null)
            {
                NamespaceName = other.NamespaceName;
            }

            if (other.ClassName != null)
            {
                ClassName = other.ClassName;
            }

            if (other.MethodName != null)
            {
                MethodName = other.MethodName;
            }
        }
    }
}
