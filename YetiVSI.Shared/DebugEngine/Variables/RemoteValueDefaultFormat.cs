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

namespace YetiVSI.DebugEngine.Variables
{
    public class RemoteValueDefaultFormat : ISingleValueFormat
    {
        public static RemoteValueDefaultFormat DefaultFormatter = new RemoteValueDefaultFormat();

        public RemoteValueDefaultFormat()
        {
        }

        public virtual ValueFormat GetValueFormat(ValueFormat fallbackValueFormat)
            => fallbackValueFormat;

        public virtual string FormatValue(RemoteValue remoteValue, ValueFormat fallbackValueFormat)
        {
            string displayValue = remoteValue.GetDisplayValue(GetValueFormat(fallbackValueFormat));
            // TODO: Remove trailing zeros from ST registers summary.
            if (remoteValue.GetValueType() == ValueType.Register &&
                remoteValue.GetTypeName().Contains("ext_vector_type"))
            {
                // Ensure consistency across all the registers presented as vectors.
                return TrySwitchFromParenthesesToBrackets(displayValue);
            }
            return displayValue;
        }

        public virtual string FormatValueAsAddress(RemoteValue remoteValue)
        {
            TypeFlags flags = remoteValue?.GetTypeInfo()?.GetTypeFlags() ?? 0;
            if (flags.HasFlag(TypeFlags.IS_REFERENCE) || flags.HasFlag(TypeFlags.IS_POINTER))
            {
                return $"0x{remoteValue.GetValueAsUnsigned():x16}";
            }
            return string.Empty;
        }

        /// <summary>
        /// Returns an unquoted, unescaped version of the result from FormatValue() if that value
        /// looks like a quoted and escaped string literal. For instance, L"\"foo\\nbar\"" becomes
        /// "foo\nbar". Returns "" otherwise.
        /// </summary>
        public virtual string FormatStringView(
            RemoteValue remoteValue, ValueFormat fallbackValueFormat)
        {
            string value = FormatValue(remoteValue, fallbackValueFormat);
            return CStringEscapeHelper.Unescape(value);
        }

        public virtual string FormatExpressionForAssignment(
            RemoteValue remoteValue, string expression)
        {
            if (remoteValue.GetTypeInfo().GetTypeFlags().HasFlag(TypeFlags.IS_FLOAT))
            {
                return FloatRadixConversionHelper.TryConvertToFloatFromNumberString(
                    expression, new System.Lazy<int>(() => (int)remoteValue.GetByteSize()));
            }
            return expression;
        }

        public string GetValueForAssignment(
            RemoteValue remoteValue, ValueFormat fallbackValueFormat)
            => remoteValue.GetValueType() == ValueType.Register ?
            FormatValue(remoteValue, fallbackValueFormat) :
            remoteValue.GetValue(GetValueFormat(fallbackValueFormat));

        public virtual bool ShouldInheritFormatSpecifier() => false;

        static string TrySwitchFromParenthesesToBrackets(string val)
            => val.Length > 1 && val[0] == '(' && val[val.Length - 1] == ')' ?
            $"{{{val.Substring(1, val.Length - 2)}}}" : val;
    }
}
