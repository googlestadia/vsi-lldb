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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// Implementation for VectorOfFloat formats. Those are handled by LLDB, but they might still
    /// require an extra post-processing step.
    /// </summary>
    public class RemoteValueLLDBVectorOfFloatFormat : RemoteValueLLDBFormat
    {
        static readonly IDictionary<ValueFormat, string> floatFormatMap =
            new Dictionary<ValueFormat, string>()
            {
                { ValueFormat.VectorOfFloat32, "0.00000E0" },
                { ValueFormat.VectorOfFloat64, "0.00000000000000E0" },
            };

        static readonly IDictionary<ValueFormat, Func<double, byte[]>> doubleToBytesMap =
            new Dictionary<ValueFormat, Func<double, byte[]>>()
            {
                { ValueFormat.VectorOfFloat32, d => BitConverter.GetBytes((float)d) },
                { ValueFormat.VectorOfFloat64, d => BitConverter.GetBytes(d) },
            };

        public RemoteValueLLDBVectorOfFloatFormat(ValueFormat valueFormat) : base(valueFormat)
        {
        }

        public override string FormatValue(RemoteValue remoteValue,
            ValueFormat fallbackValueFormat) =>
                TryMakeFloatFormatConsistent(base.FormatValue(remoteValue, fallbackValueFormat));

        /// <summary>
        /// Tries to convert from the vector representation of registers, e.g.,
        /// "{-1.76325e-17 -4.62538e-15}", to a C++ initialization list containing its bytes, e.g.,
        /// "{0xa0, 0xa1, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7}", that can be assigned to
        /// ext_vector_type variables.
        /// </summary>
        public override string FormatExpressionForAssignment(
            RemoteValue remoteValue, string expression)
        {
            List<double> entries;
            if (!TryParseVector(expression, out entries))
            {
                return expression;
            }

            expression = string.Join(", ", entries.SelectMany(
                num => DoubleToBytes(num).Select(b => $"0x{b.ToString("x2")}")));
            return $"{{{expression}}}";
        }

        public override bool ShouldInheritFormatSpecifier() => true;

        #region Helpers

        static string RemoveEnclosingBraces(string str) => str.Length >= 2 &&
            ((str[0] == '(' && str[str.Length - 1] == ')') ||
            (str[0] == '{' && str[str.Length - 1] == '}')) ?
            str.Substring(1, str.Length - 2) : str;

        /// <summary>
        /// Converts the string representation of a number to its double equivalent.
        /// </summary>
        /// <remarks>
        /// This method always expects a '.' to be used as decimal separator,
        /// despite local machine settings.
        /// </remarks>
        static bool TryParseDouble(string s, out double res) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out res);

        /// <summary>
        /// Converts the string representation of a vector, e.g., "(1.123, 3.45)", "(1.123 3.45)",
        /// to a list of doubles containing its elements.
        /// </summary>
        /// <remarks>
        /// This method always expects a '.' to be used as decimal separator,
        /// despite local machine settings.
        /// </remarks>
        static bool TryParseVector(string vector, out List<double> entries)
        {
            entries = new List<double>();
            Action errorLogger = () => Trace.WriteLine(
                $"WARNING: Could not parse ({vector}) as a vector of doubles.");

            if (string.IsNullOrEmpty(vector))
            {
                errorLogger.Invoke();
                return false;
            }
            foreach (string token in RemoveEnclosingBraces(vector).Split(
                new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                double res;
                if (!TryParseDouble(token, out res))
                {
                    errorLogger.Invoke();
                    return false;
                }
                entries.Add(res);
            }
            return true;
        }

        string FloatFormat => floatFormatMap[ValueFormat];

        Func<double, byte[]> DoubleToBytes => doubleToBytesMap[ValueFormat];

        /// <summary>
        /// Converts a double to its string equivalent according to the property FloatFormat.
        /// </summary>
        /// <remarks>
        /// The string produced by this method always uses a '.' as decimal separator,
        /// despite local machine settings.
        /// </remarks>
        string FormatDouble(double val) =>
            string.Format(CultureInfo.InvariantCulture, $"{{0:{FloatFormat}}}", val);

        /// <summary>
        /// Takes a comma-separated float |vector| formatted with variable number of digits,
        /// e.g., "(1.23, 2.3456)", and returns a consistently formatted,
        /// space-separated vector, e.g., "{1.23000E0  2.34560E0}", similar to what is outputted by
        /// `LLDB.Shell register read`. Note that similar to LLDB, this method always uses and
        /// expects '.' as the decimal separator.
        /// Returns the original |vector| if the vector does not have the appropriate format.
        /// </summary>
        string TryMakeFloatFormatConsistent(string vector)
        {
            List<double> entries;
            if (!TryParseVector(vector, out entries))
            {
                return vector;
            }

            return $"{{{string.Join(", ", entries.Select(d => FormatDouble(d)))}}}";
        }

        #endregion
    }
}
