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
using System.Text;

namespace YetiVSI.DebugEngine.Variables
{
    public static class RemoteValueExtensions
    {
        /// <summary>
        /// Get the variable assignment expression as a string. Returns null if the value is
        /// constant or invalid.
        /// </summary>
        public static string GetVariableAssignExpression(this RemoteValue remoteValue)
        {
            var valueType = remoteValue.GetValueType();
            if (valueType == ValueType.Invalid)
            {
                return null;
            }

            if(valueType == ValueType.Register)
            {
                return $"${remoteValue.GetName()}";
            }

            TypeFlags? typeFlags = remoteValue.GetTypeInfo()?.GetTypeFlags();
            if (typeFlags.HasValue && typeFlags.Value.HasFlag(TypeFlags.IS_ARRAY))
            {
                return null;
            }

            string variableExpression = remoteValue.GetFullName();
            if (variableExpression == null)
            {
                variableExpression = remoteValue.GetMemoryAddressAssignExpression();
            }
            return variableExpression;
        }

        /// <summary>
        /// Return the expression path of the remoteValue. Don't return LLDB scratch variables like
        /// "$23" because they sometimes don't behave as expected when assigning values to them
        /// and they don't persist across debug sessions correctly.
        /// </summary>
        public static string GetFullName(this RemoteValue remoteValue)
        {
            string expressionPath;
            if (remoteValue.GetExpressionPath(out expressionPath) &&
                !string.IsNullOrEmpty(expressionPath) &&
                !expressionPath.Contains("$"))
            {
                return expressionPath;
            }
            return null;
        }

        /// <summary>
        /// Returns |remoteValue|'s summary if present and its value otherwise, formatted using
        /// |valueFormat|.
        /// </summary>
        public static string GetDisplayValue(this RemoteValue remoteValue, ValueFormat valueFormat)
        {
            var strValue = remoteValue.GetSummary(valueFormat);
            if (!string.IsNullOrEmpty(strValue))
            {
                return TryConvertToUtf8(strValue);
            }
            return remoteValue.GetValue(valueFormat);
        }

        /// <summary>
        /// Calls GetValue() with ValueFormat.Default as format.
        /// Useful for tests if you don't care about special formatting (e.g. hex).
        /// </summary>
        public static string GetDefaultValue(this RemoteValue remoteValue)
        {
            return remoteValue.GetValue(ValueFormat.Default);
        }

        /// <summary>
        /// Returns a casted raw address expression that can be used to build a value assign
        /// expression.
        ///
        /// Can return null.
        /// </summary>
        public static string GetMemoryAddressAssignExpression(this RemoteValue remoteValue)
        {
            if (!IsValidValue(remoteValue))
            {
                return null;
            }
            var flags = remoteValue.GetTypeInfo().GetTypeFlags();
            if (flags.HasFlag(TypeFlags.IS_REFERENCE))
            {
                // NOTE: This works for references implemented as a pointer. It is not known how
                // this will work for other implementations.
                var typeName = remoteValue.GetTypeName();
                typeName = typeName.Replace("&", "");
                return $"(*(({typeName}*){remoteValue.GetDefaultValue()}))";
            }

            RemoteValue addressOfValue;
            if (!TryGetAddressOf(remoteValue, out addressOfValue))
            {
                return null;
            }

            if (flags.HasFlag(TypeFlags.IS_POINTER))
            {
                return $"(({remoteValue.GetTypeName()}){remoteValue.GetDefaultValue()})";
            }

            if (flags.HasFlag(TypeFlags.IS_ARRAY))
            {
                return null;
            }

            return $"(*(({remoteValue.GetTypeName()}*){addressOfValue.GetDefaultValue()}))";
        }

        /// <summary>
        /// Returns the address to be used as memory context. This is used to get an address
        /// from an expression entered into Visual Studio's memory view's address text box.
        /// </summary>
        public static ulong? GetMemoryContextAddress(this RemoteValue remoteValue)
        {
            if (!IsValidValue(remoteValue))
            {
                return null;
            }

            TypeFlags GetTypeFlags(RemoteValue value) =>
                remoteValue.GetTypeInfo().GetCanonicalType().GetTypeFlags();

            // If the value is an array, try to obtain its address.
            TypeFlags flags = GetTypeFlags(remoteValue);
            if (flags.HasFlag(TypeFlags.IS_ARRAY))
            {
                remoteValue = remoteValue.AddressOf();
                if (!IsValidValue(remoteValue))
                {
                    return null;
                }
                flags = GetTypeFlags(remoteValue);
            }

            // Only interpret pointers, references and integers as addresses.
            if (!flags.HasFlag(TypeFlags.IS_REFERENCE) && !flags.HasFlag(TypeFlags.IS_POINTER) &&
                !flags.HasFlag(TypeFlags.IS_INTEGER))
            {
                return null;
            }

            return remoteValue.GetValueAsUnsigned();
        }

        /// <summary>
        /// Determines if a RemoteValue is a valid value. i.e. has correct type information and
        /// isn't an error.
        /// </summary>
        /// <param name="remoteValue"></param>
        /// <returns></returns>
        private static bool IsValidValue(RemoteValue remoteValue)
        {
            return remoteValue != null && remoteValue.GetTypeInfo() != null &&
                !remoteValue.GetError().Fail();
        }

        private static bool TryGetAddressOf(RemoteValue remoteValue, out RemoteValue addressOf)
        {
            addressOf = null;

            var addressOfValue = remoteValue.AddressOf();
            if (addressOfValue == null || addressOfValue.GetError().Fail())
            {
                return false;
            }
            addressOf = addressOfValue;
            return true;
        }

        // Note: The vanilla Encoding.UTF8 replaces invalid characters by �. However, we want it to
        // throw, so we create our own version. With Black Jack and exceptions.
        private static Encoding throwingUtf8Encoding = new UTF8Encoding(false, true);

        /// <summary>
        /// Tries to convert |defaultEncodedString| from Encoding.Default to Encoding.Utf8.
        /// If this is not possible, returns |defaultEncodedString|.
        /// </summary>
        private static string TryConvertToUtf8(string defaultEncodedString)
        {
            try
            {
                byte[] bytes = Encoding.Default.GetBytes(defaultEncodedString);
                return throwingUtf8Encoding.GetString(bytes);
            }
            catch
            {
                return defaultEncodedString;
            }
        }
    }
}
