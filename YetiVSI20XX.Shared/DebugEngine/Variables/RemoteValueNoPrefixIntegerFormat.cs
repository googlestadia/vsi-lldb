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

﻿using DebuggerApi;
using System.Collections.Generic;

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// Implements prefix-less integer formats (e.g. "ff" instead of "0xff").
    /// LLDB only support the prefixed versions.
    /// </summary>
    public class RemoteValueNoPrefixIntegerFormat : RemoteValueLLDBFormat
    {
        /// <summary>
        /// Creates a new formatter for integer values.
        /// </summary>
        /// <param name="formatSpecifiers">Format specifiers that this class handles.</param>
        /// <param name="format">LLDB format.</param>
        public RemoteValueNoPrefixIntegerFormat(ValueFormat format) : base(format)
        {
        }

        /// <summary>
        /// Similar to base.FormatValue(), but removes the prefix for hex and binary integers.
        /// </summary>
        public override string FormatValue(RemoteValue remoteValue,
            ValueFormat fallbackValueFormat)
        {
            ValueFormat valueFormat = GetValueFormat(fallbackValueFormat);
            string summary = remoteValue.GetSummary(valueFormat);
            if (!string.IsNullOrEmpty(summary))
                return summary;

            string value = remoteValue.GetValue(valueFormat);
            if (value.StartsWith("0x") || value.StartsWith("0b"))
                return value.Substring(2);
            return value;
        }

        public override string FormatValueAsAddress(RemoteValue remoteValue)
        {
            TypeFlags flags = remoteValue?.GetTypeInfo()?.GetTypeFlags() ?? 0;
            if (flags.HasFlag(TypeFlags.IS_REFERENCE) || flags.HasFlag(TypeFlags.IS_POINTER))
            {
                if (ValueFormat == ValueFormat.HexUppercase)
                {
                    return $"{remoteValue.GetValueAsUnsigned():X16}";
                }
                else
                {
                    return $"{remoteValue.GetValueAsUnsigned():x16}";
                }
            }
            return string.Empty;
        }

        public override bool ShouldInheritFormatSpecifier() => true;
    }
}
