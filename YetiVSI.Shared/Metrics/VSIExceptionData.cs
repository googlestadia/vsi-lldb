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

﻿using System.Collections.Generic;
using System.Linq;

namespace YetiVSI.Shared.Metrics
{
    /// <summary>
    /// This class is a stub for a proto. It uses a constant hash
    /// as it is only required to be able to override equality
    /// for testing purposes.
    /// </summary>
    public class VSIExceptionData
    {
        public class Types
        {
            public class Exception
            {
                public VSITypeInfo ExceptionType { get; set; }

                public override int GetHashCode()
                {
                    return 42;
                }

                public override bool Equals(object other) => Equals(other as Exception);

                public bool Equals(Exception other)
                {
                    if (ReferenceEquals(other, null))
                    {
                        return false;
                    }

                    if (ReferenceEquals(other, this))
                    {
                        return true;
                    }

                    if (!object.Equals(other.ExceptionType, ExceptionType))
                    {
                        return false;
                    }

                    return true;
                }

                public Exception Clone() => new Exception {ExceptionType = ExceptionType?.Clone()};
            }
        }

        public VSIMethodInfo CatchSite { get; set; }

        public List<Types.Exception> ExceptionsChain { get; set; }

        public VSIExceptionData()
        {
            ExceptionsChain = new List<Types.Exception>();
        }

        public override int GetHashCode()
        {
            return 42;
        }

        public override bool Equals(object other) => Equals(other as VSIExceptionData);

        public bool Equals(VSIExceptionData other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (!object.Equals(other.CatchSite, CatchSite))
            {
                return false;
            }

            if (other.ExceptionsChain != ExceptionsChain &&
                (other.ExceptionsChain == null || ExceptionsChain == null) ||
                !other.ExceptionsChain.SequenceEqual(ExceptionsChain))
            {
                return false;
            }

            return true;
        }

        public VSIExceptionData Clone()
        {
            var clone = new VSIExceptionData();
            clone.CatchSite = CatchSite?.Clone();
            clone.ExceptionsChain = ExceptionsChain?.Select(x => x.Clone()).ToList();
            return clone;
        }
    }
}
