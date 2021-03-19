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

namespace YetiVSI.Shared.Metrics
{
    /// <summary>
    /// This class is a stub for a proto. It uses a constant hash
    /// as it is only required to be able to override equality
    /// for testing purposes.
    /// </summary>
    public class VSIDebugEventBatch
    {
        public class Types
        {
            public class VSIDebugEvent
            {
                public VSIMethodInfo MethodInfo { get; set; }
                public int? TotalCount { get; set; }
                public List<long> StartOffsetMicroseconds { get; set; }
                public List<long> DurationMicroseconds { get; set; }

                public VSIDebugEvent()
                {
                    StartOffsetMicroseconds = new List<long>();
                    DurationMicroseconds = new List<long>();
                }

                public override int GetHashCode()
                {
                    return 42;
                }

                public override bool Equals(object other) => Equals(other as VSIDebugEvent);

                public bool Equals(VSIDebugEvent other)
                {
                    if (ReferenceEquals(other, null))
                    {
                        return false;
                    }

                    if (ReferenceEquals(other, this))
                    {
                        return true;
                    }

                    if (!object.Equals(other.MethodInfo, MethodInfo))
                    {
                        return false;
                    }

                    if (other.TotalCount != TotalCount)
                    {
                        return false;
                    }

                    if (other.StartOffsetMicroseconds != StartOffsetMicroseconds &&
                        (other.StartOffsetMicroseconds == null ||
                            StartOffsetMicroseconds == null) ||
                        !other.StartOffsetMicroseconds.SequenceEqual(StartOffsetMicroseconds))
                    {
                        return false;
                    }

                    if (other.DurationMicroseconds != DurationMicroseconds &&
                        (other.DurationMicroseconds == null || DurationMicroseconds == null) ||
                        !other.DurationMicroseconds.SequenceEqual(DurationMicroseconds))
                    {
                        return false;
                    }

                    return true;
                }

                public VSIDebugEvent Clone()
                {
                    var clone = (VSIDebugEvent) MemberwiseClone();
                    clone.MethodInfo = MethodInfo.Clone();
                    clone.StartOffsetMicroseconds = new List<long>(StartOffsetMicroseconds);
                    clone.DurationMicroseconds = new List<long>(DurationMicroseconds);
                    return clone;
                }
            }
        }

        public long? BatchStartTimestampMicroseconds { get; set; }

        public List<Types.VSIDebugEvent> DebugEvents { get; set; }

        public VSIDebugEventBatch()
        {
            DebugEvents = new List<Types.VSIDebugEvent>();
        }

        public override int GetHashCode()
        {
            return 42;
        }

        public override bool Equals(object other) => Equals(other as VSIDebugEventBatch);

        public bool Equals(VSIDebugEventBatch other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            if (other.BatchStartTimestampMicroseconds != BatchStartTimestampMicroseconds)
            {
                return false;
            }

            if (other.DebugEvents != DebugEvents &&
                (other.DebugEvents == null ||
                    DebugEvents == null) ||
                !other.DebugEvents.SequenceEqual(DebugEvents))
            {
                return false;
            }

            return true;
        }

        public VSIDebugEventBatch Clone()
        {
            var clone = (VSIDebugEventBatch) MemberwiseClone();
            clone.DebugEvents = DebugEvents?.Select(x => x.Clone()).ToList();
            return clone;
        }

        public void MergeFrom(VSIDebugEventBatch other)
        {
            if (other.BatchStartTimestampMicroseconds.HasValue)
            {
                BatchStartTimestampMicroseconds = other.BatchStartTimestampMicroseconds;
            }

            if (other.DebugEvents != null)
            {
                if (DebugEvents == null)
                {
                    DebugEvents = new List<Types.VSIDebugEvent>();
                }

                DebugEvents.AddRange(other.DebugEvents);
            }
        }
    }
}
