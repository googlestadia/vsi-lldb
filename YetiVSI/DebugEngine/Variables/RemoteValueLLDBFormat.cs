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
using System.Linq;

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// Implementation for formats that are handled by LLDB.
    /// </summary>
    public class RemoteValueLLDBFormat : RemoteValueDefaultFormat
    {
        public ValueFormat ValueFormat { get; }

        public RemoteValueLLDBFormat(ValueFormat valueFormat)
        {
            ValueFormat = valueFormat;
        }

        public override ValueFormat GetValueFormat(ValueFormat fallbackValueFormat)
        {
            // Ignore |fallbackValueFormat|, |valueFormat| takes precedence.
            return ValueFormat;
        }

        public override string FormatValueAsAddress(RemoteValue remoteValue)
        {
            TypeFlags flags = remoteValue?.GetTypeInfo()?.GetTypeFlags() ?? 0;
            if (flags.HasFlag(TypeFlags.IS_REFERENCE) || flags.HasFlag(TypeFlags.IS_POINTER))
            {
                if (ValueFormat == ValueFormat.HexUppercase)
                {
                    return $"0x{remoteValue.GetValueAsUnsigned():X16}";
                }
                else
                {
                    return $"0x{remoteValue.GetValueAsUnsigned():x16}";
                }
            }
            return string.Empty;
        }

        public override bool ShouldInheritFormatSpecifier() => true;
    }
}
