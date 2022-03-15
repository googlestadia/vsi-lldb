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
    public class VSIExceptionData
    {
        public class Types
        {
            public class Exception
            {
                public class Types
                {
                    public class StackTraceFrame
                    {
                        public bool? AllowedNamespace { get; set; }
                        public VSIMethodInfo Method { get; set; }
                        public string Filename { get; set; }
                        public uint? LineNumber { get; set; }

                        public override int GetHashCode()
                        {
                            return 42;
                        }

                        public override bool Equals(object other) =>
                            Equals(other as StackTraceFrame);

                        public bool Equals(StackTraceFrame other)
                        {
                            if (ReferenceEquals(other, null))
                            {
                                return false;
                            }

                            if (ReferenceEquals(other, this))
                            {
                                return true;
                            }

                            if (!object.Equals(other.AllowedNamespace, AllowedNamespace))
                            {
                                return false;
                            }

                            if (!object.Equals(other.Method, Method))
                            {
                                return false;
                            }

                            if (!object.Equals(other.Filename, Filename))
                            {
                                return false;
                            }

                            if (!object.Equals(other.LineNumber, LineNumber))
                            {
                                return false;
                            }

                            return true;
                        }

                        public StackTraceFrame Clone()
                        {
                            var clone = (StackTraceFrame) MemberwiseClone();
                            clone.Method = Method?.Clone();

                            return clone;
                        }
                    }
                }

                public VSITypeInfo ExceptionType { get; set; }
                public List<Types.StackTraceFrame> ExceptionStackTraceFrames { get; set; }

                public Exception()
                {
                    ExceptionStackTraceFrames = new List<Types.StackTraceFrame>();
                }

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

                    if (!Equals(other.ExceptionStackTraceFrames, ExceptionStackTraceFrames) &&
                        (other.ExceptionStackTraceFrames == null ||
                            ExceptionStackTraceFrames == null ||
                            !other.ExceptionStackTraceFrames.SequenceEqual(
                                ExceptionStackTraceFrames)))
                    {
                        return false;
                    }

                    return true;
                }

                public Exception Clone()
                {
                    var clone = new Exception
                    {
                        ExceptionType = ExceptionType.Clone(),
                        ExceptionStackTraceFrames = ExceptionStackTraceFrames
                            .Select(frame => frame.Clone())
                            .ToList()
                    };

                    return clone;
                }
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
