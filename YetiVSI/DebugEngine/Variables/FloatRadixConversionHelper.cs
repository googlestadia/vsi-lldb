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

﻿using System;

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// Helper class to convert hex/octal/bin strings representing floats into a
    /// float literal.
    /// </summary>
    public class FloatRadixConversionHelper
    {
        /// <summary>
        /// Tries to convert a hex/octal/bin string representing a float, e.g., "0x71b5b5af",
        /// into a float literal, e.g., 1.79956572E+30. This function guarantees that the resulting
        /// value rounds-trip back to the original string if it is normal, i.e., not NaN/inf.
        /// The width in bytes should be either 4 or 8.
        /// If that isn't met or the converstion fails, this function returns the input as is.
        /// </summary>
        public static string TryConvertToFloatFromNumberString(string str, Lazy<int> widthInBytes)
        {
            long longRep;
            if (!TryConvertToLongFromNumberString(str, out longRep) ||
                (widthInBytes.Value != 4 && widthInBytes.Value != 8))
            {
                return str;
            }

            return ConvertToStringFromBytes(BitConverter.GetBytes(longRep), widthInBytes.Value);
        }

        /// <summary>
        /// Tries to convert a string representation of a number in a certain base to a long that is
        /// formed by the same bits. Supports the following formats:
        /// Hex: "0xff"/"0xFF" => 255
        /// Octal: "012" => 10
        /// Binary: "0b1111"/"0B1111" => 16
        /// </summary>
        static bool TryConvertToLongFromNumberString(string str, out long res)
        {
            try
            {
                if (str.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                {
                    res = Convert.ToInt64(str.Substring(2), 16);
                    return true;
                }
                if (str.StartsWith("0b", StringComparison.InvariantCultureIgnoreCase))
                {
                    res = Convert.ToInt64(str.Substring(2), 2);
                    return true;
                }
                if (str.StartsWith("0"))
                {
                    res = Convert.ToInt64(str.Substring(1), 8);
                    return true;
                }
            }
            catch (FormatException)
            {
            }
            res = 0;
            return false;
        }

        static string ConvertToStringFromBytes(byte[] bytes, int totalWidthInBytes)
        {
            if (totalWidthInBytes == 4)
            {
                float singleVal = BitConverter.ToSingle(bytes, 0);
                return GetSpecialRepresentation(singleVal) ?? singleVal.ToString("G9");
            }
            double doubleVal = BitConverter.ToDouble(bytes, 0);
            return GetSpecialRepresentation(doubleVal) ?? doubleVal.ToString("G17");
        }

        static string GetSpecialRepresentation(double val)
        {
            if (double.IsNaN(val))
            {
                return "0./0";
            }
            if (double.IsPositiveInfinity(val))
            {
                return "1./0";
            }
            if (double.IsNegativeInfinity(val))
            {
                return "-1./0";
            }
            return null;
        }
    }
}
