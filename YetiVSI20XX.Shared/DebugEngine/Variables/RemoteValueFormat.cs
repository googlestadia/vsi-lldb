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

using DebuggerApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// The RemoteValueFormat class combines the size specifier and value format specifier
    /// components of formatters.
    /// </summary>
    public class RemoteValueFormat : IRemoteValueFormat
    {
        public RemoteValueFormat(ISingleValueFormat valueFormat, uint? sizeSpecifier = null)
        {
            _format = valueFormat;
            _sizeSpecifier = sizeSpecifier;
        }

        public static RemoteValueFormat Default =
            new RemoteValueFormat(RemoteValueDefaultFormat.DefaultFormatter);

        const uint _maxChildBatchSize = 100;
        readonly ISingleValueFormat _format;
        readonly uint? _sizeSpecifier;

#region ISingleValueFormat implementation

        public ValueFormat GetValueFormat(ValueFormat fallbackValueFormat) =>
            _format.GetValueFormat(fallbackValueFormat);

        public string FormatValue(RemoteValue remoteValue, ValueFormat fallbackValueFormat)
        {
            if (_sizeSpecifier is uint size)
            {
                // If the base formatter supports sized format, use that.
                if (_format is IRemoteValueFormatWithSize sizeFormatter)
                {
                    return sizeFormatter.FormatValueWithSize(remoteValue, size);
                }
                // Otherwise, return empty value so that the caller computes the value from
                // the children.
                return "";
            }
            return _format.FormatValue(remoteValue, fallbackValueFormat);
        }

        public string FormatStringView(RemoteValue remoteValue, ValueFormat fallbackValueFormat)
        {
            if (_sizeSpecifier is uint size)
            {
                // If the base formatter supports sized format, use that.
                if (_format is IRemoteValueFormatWithSize sizeFormatter)
                {
                    return sizeFormatter.FormatStringViewWithSize(remoteValue, size);
                }
            }
            return _format.FormatStringView(remoteValue, fallbackValueFormat);
        }

        public string FormatExpressionForAssignment(RemoteValue remoteValue, string expression) =>
            _format.FormatExpressionForAssignment(remoteValue, expression);

        public string GetValueForAssignment(RemoteValue remoteValue,
                                            ValueFormat fallbackValueFormat) =>
            _format.GetValueForAssignment(remoteValue, fallbackValueFormat);

        public string FormatValueAsAddress(RemoteValue value) =>
            _format.FormatValueAsAddress(value);

        public bool ShouldInheritFormatSpecifier() => _format.ShouldInheritFormatSpecifier();

        #endregion

        public virtual uint GetNumChildren(RemoteValue remoteValue)
        {
            if (_sizeSpecifier is uint childCount)
            {
                return GetNumPointerOrArrayChildren(childCount, remoteValue);
            }
            return remoteValue.GetNumChildren();
        }

        public virtual IEnumerable<RemoteValue> GetChildren(RemoteValue remoteValue, int offset,
                                                            int count)
        {
            if (_sizeSpecifier == null)
            {
                return GetRemoteValueChildren(remoteValue, offset, count);
            }

            // If we have a size specifier, we adjust the size appropriately.
            int childCount = (int)GetNumPointerOrArrayChildren((uint)_sizeSpecifier, remoteValue);
            int adjustedCount = Math.Max(0, Math.Min(count, childCount - offset));
            // For pointers, we obtain the children by pointer arithmetic.
            if (remoteValue.TypeIsPointerType())
            {
                return GetPointerChildren(remoteValue, offset, adjustedCount);
            }
            return GetRemoteValueChildren(remoteValue, offset, adjustedCount);
        }

        static IEnumerable<RemoteValue> GetRemoteValueChildren(RemoteValue remoteValue, int offset,
                                                               int count)
        {
            var result = new List<RemoteValue>();
            uint childOffset = (uint)offset;
            uint endIndex = (uint)(offset + count);
            uint numChildren = remoteValue.GetNumChildren();
            while (childOffset < endIndex)
            {
                // Fetch children in batches for performance reasons.
                uint batchSize = System.Math.Min(endIndex - childOffset, _maxChildBatchSize);
                List<RemoteValue> currentBatch = remoteValue.GetChildren(childOffset, batchSize);

                for (int n = 0; n < batchSize; ++n)
                {
                    RemoteValue childValue = currentBatch[n];
                    if (childValue != null)
                    {
                        result.Add(childValue);
                    }
                    else if (n + childOffset < numChildren)
                    {
                        // There were times when LLDB was returning an error and thus a null child
                        // value. ex: Children[1] for a CustomType&* type.
                        Trace.WriteLine(
                            $"WARNING: No child found at index {n + childOffset} of " +
                            $"({remoteValue.GetTypeName()}){remoteValue.GetFullName()} even " +
                            $"though there are {numChildren} children.");
                    }
                }
                childOffset += batchSize;
            }
            return result;
        }

        static IEnumerable<RemoteValue> GetPointerChildren(RemoteValue value, int offset, int count)
        {
            var result = new List<RemoteValue>();
            SbType pointeeType = value.GetTypeInfo()?.GetPointeeType();
            if (pointeeType == null)
            {
                // If we cannot get the pointee type, just return the empty list.
                return result;
            }
            ulong byteSize = pointeeType.GetByteSize();
            ulong baseAddress = value.GetValueAsUnsigned() + (ulong)offset * byteSize;

            for (int n = 0; n < count; ++n)
            {
                ulong address = baseAddress + (ulong)n * byteSize;
                RemoteValue childValue =
                    value.CreateValueFromAddress($"[{offset + n}]", address, pointeeType);
                if (childValue != null)
                {
                    result.Add(childValue);
                }
            }
            return result;
        }

        uint GetNumPointerOrArrayChildren(uint size, RemoteValue remoteValue) =>
            remoteValue.TypeIsPointerType() ? size : Math.Min(size, remoteValue.GetNumChildren());
    }
}
