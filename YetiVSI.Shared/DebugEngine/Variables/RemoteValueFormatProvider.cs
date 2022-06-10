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
using System.Text;

namespace YetiVSI.DebugEngine.Variables
{
    public delegate bool RemoteValueFormatSupplier(string formatSpecifier,
                                                   out IRemoteValueFormat format);

    /// <summary>
    /// RemoteValueFormatProvider is responsible for supplying a format given
    /// an expression.
    /// </summary>
    public static class RemoteValueFormatProvider
    {
        static readonly Dictionary<string, ISingleValueFormat> _formatLookup =
            new Dictionary<string, ISingleValueFormat>() {
                { "", RemoteValueDefaultFormat.DefaultFormatter },
                // LLDB formats.
                { "a", new RemoteValueLLDBFormat(ValueFormat.CharArray) },
                { "b", new RemoteValueLLDBFormat(ValueFormat.Binary) },
                { "B", new RemoteValueLLDBFormat(ValueFormat.Boolean) },
                { "c", new RemoteValueLLDBFormat(ValueFormat.Char) },
                { "C", new RemoteValueLLDBFormat(ValueFormat.CharPrintable) },
                { "d", new RemoteValueLLDBFormat(ValueFormat.Decimal) },
                { "en", new RemoteValueLLDBFormat(ValueFormat.Enum) },
                { "E", new RemoteValueLLDBFormat(ValueFormat.Enum) },
                { "f", new RemoteValueLLDBFormat(ValueFormat.Float) },
                { "F", new RemoteValueLLDBFormat(ValueFormat.ComplexFloat) },
                { "h", new RemoteValueLLDBFormat(ValueFormat.Hex) },
                { "H", new RemoteValueLLDBFormat(ValueFormat.HexUppercase) },
                { "i", new RemoteValueLLDBFormat(ValueFormat.Decimal) },
                { "I", new RemoteValueLLDBFormat(ValueFormat.ComplexInteger) },
                { "o", new RemoteValueLLDBFormat(ValueFormat.Octal) },
                { "O", new RemoteValueLLDBFormat(ValueFormat.OSType) },
                { "p", new RemoteValueLLDBFormat(ValueFormat.Pointer) },
                { "u", new RemoteValueLLDBFormat(ValueFormat.Unsigned) },
                { "U", new RemoteValueLLDBFormat(ValueFormat.Unicode16) },
                { "U32", new RemoteValueLLDBFormat(ValueFormat.Unicode32) },
                { "x", new RemoteValueLLDBFormat(ValueFormat.Hex) },
                { "X", new RemoteValueLLDBFormat(ValueFormat.HexUppercase) },
                { "y", new RemoteValueLLDBFormat(ValueFormat.Bytes) },
                { "Y", new RemoteValueLLDBFormat(ValueFormat.BytesWithASCII) },
                // String formats.
                { "s", new RemoteValueStringFormat(Encoding.Default, 1, "\"", "\"") },
                { "sb", new RemoteValueStringFormat(Encoding.Default, 1, "", "") },
                { "s8", new RemoteValueStringFormat(Encoding.UTF8, 1, "u8\"", "\"") },
                { "s8b", new RemoteValueStringFormat(Encoding.UTF8, 1, "", "") },
                { "su", new RemoteValueStringFormat(Encoding.Unicode, 2, "u\"", "\"") },
                { "sub", new RemoteValueStringFormat(Encoding.Unicode, 2, "", "") },
                { "s32", new RemoteValueStringFormat(Encoding.UTF32, 4, "U\"", "\"") },
                { "s32b", new RemoteValueStringFormat(Encoding.UTF32, 4, "", "") },
                // Prefix-less format.
                { "xb", new RemoteValueNoPrefixIntegerFormat(ValueFormat.Hex) },
                { "hb", new RemoteValueNoPrefixIntegerFormat(ValueFormat.Hex) },
                { "Hb", new RemoteValueNoPrefixIntegerFormat(ValueFormat.HexUppercase) },
                { "Xb", new RemoteValueNoPrefixIntegerFormat(ValueFormat.HexUppercase) },
                { "bb", new RemoteValueNoPrefixIntegerFormat(ValueFormat.Binary) },
                // Vector formats.
                { "vf32", new RemoteValueLLDBVectorOfFloatFormat(ValueFormat.VectorOfFloat32) },
                { "vf64", new RemoteValueLLDBVectorOfFloatFormat(ValueFormat.VectorOfFloat64) },
                // Unhandled formats.
                { "bstr", RemoteValueDefaultFormat.DefaultFormatter },
                { "e", RemoteValueDefaultFormat.DefaultFormatter },
                { "g", RemoteValueDefaultFormat.DefaultFormatter },
                { "l", RemoteValueDefaultFormat.DefaultFormatter },
                { "env", RemoteValueDefaultFormat.DefaultFormatter },
                { "hr", RemoteValueDefaultFormat.DefaultFormatter },
                { "hv", RemoteValueDefaultFormat.DefaultFormatter },
                { "m", RemoteValueDefaultFormat.DefaultFormatter },
                { "ma", RemoteValueDefaultFormat.DefaultFormatter },
                { "mb", RemoteValueDefaultFormat.DefaultFormatter },
                { "md", RemoteValueDefaultFormat.DefaultFormatter },
                { "mq", RemoteValueDefaultFormat.DefaultFormatter },
                { "mu", RemoteValueDefaultFormat.DefaultFormatter },
                { "mw", RemoteValueDefaultFormat.DefaultFormatter },
                { "na", RemoteValueDefaultFormat.DefaultFormatter },
                { "nd", RemoteValueDefaultFormat.DefaultFormatter },
                { "n", RemoteValueDefaultFormat.DefaultFormatter },
                { "nr", RemoteValueDefaultFormat.DefaultFormatter },
                { "nvo", RemoteValueDefaultFormat.DefaultFormatter },
                { "wc", RemoteValueDefaultFormat.DefaultFormatter },
                { "wm", RemoteValueDefaultFormat.DefaultFormatter },
            };

        /// <summary>
        /// Returns true if |formatSpecifier| is a valid format specifier, i.e. if it's handled
        /// by some base formatter (possibly with size and raw specifiers) or if it's a misc other
        /// format specifier like "view(foo)" that maps to RemoteValueFormat.Default.
        /// </summary>
        public static bool IsValidFormat(string formatSpecifier)
        {
            // TODO We should support more combinations of formatters, e.g.,
            // size specifier can be combined with 'view', etc.

            // Pass a dummy size to correctly resolve the case when size specifier is an expression.
            return ParseFormat(formatSpecifier, 0U) != null ||
                   ExpandFormatSpecifierUtil.TryParseExpandFormatSpecifier(formatSpecifier,
                                                                           out _) ||
                   !string.IsNullOrEmpty(NatvisViewsUtil.ParseViewFormatSpecifier(formatSpecifier));
        }

        /// <summary>
        /// Gets a remote value format given a formatSpecifier. If no appropriate format is found
        /// returns the default format which has no effect.
        /// </summary>
        public static IRemoteValueFormat Get(string formatSpecifier, uint? evaluatedSize = null)
        {
            return ParseFormat(formatSpecifier, evaluatedSize) ?? RemoteValueFormat.Default;
        }

        public static bool IsValidSizeSpecifierSuffix(string suffix)
        {
            suffix = FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix(suffix);
            return _formatLookup.ContainsKey(suffix);
        }

        static IRemoteValueFormat ParseFormat(string formatSpecifier, uint? evaluatedSize = null)
        {
            // Empty specifier is not accepted.
            if (string.IsNullOrEmpty(formatSpecifier))
            {
                return null;
            }

            // Split the expression into a size specifier, a raw formatter specifier and a value
            // format.
            if (!FormatSpecifierUtil.TrySplitSpecifier(formatSpecifier, out string size,
                                                       out string raw, out string baseSpecifier) ||
                !_formatLookup.ContainsKey(baseSpecifier))
            {
                return null;
            }
            ISingleValueFormat valueFormat = _formatLookup[baseSpecifier];
            if (string.IsNullOrEmpty(size))
            {
                return new RemoteValueFormat(valueFormat);
            }
            uint? parsedSize = TryParseSizeFormat(size, evaluatedSize);
            if (parsedSize != null)
            {
                return new RemoteValueFormat(valueFormat, parsedSize);
            }
            return null;
        }

        /// <summary>
        /// Try and parse the format specifier or return the given |size| in the case of expression
        /// in square brackets.
        /// </summary>
        public static uint? TryParseSizeFormat(string sizeExpression, uint? size)
        {
            if (sizeExpression != null && sizeExpression.StartsWith("[") &&
                sizeExpression.EndsWith("]"))
            {
                if (size == null)
                {
                    Trace.WriteLine("ERROR: Evaluated size specifier isn't provided.");
                }

                return size;
            }

            if (size != null)
            {
                Trace.WriteLine(
                    "WARNING: Size specifier isn't an expression, evaluated size will be ignored.");
            }

            if ((TryParseDecimal(sizeExpression, out uint numChildren) ||
                 TryParseHexadecimal(sizeExpression, out numChildren)) &&
                numChildren > 0)
            {
                return numChildren;
            }

            return null;
        }

        static bool TryParseDecimal(string formatSpecifier, out uint numChildren)
        {
            try
            {
                numChildren = uint.Parse(formatSpecifier, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
            {
            }

            numChildren = 0;
            return false;
        }

        static bool TryParseHexadecimal(string formatSpecifier, out uint numChildren)
        {
            if (formatSpecifier.StartsWith("0x"))
            {
                try
                {
                    numChildren = Convert.ToUInt32(formatSpecifier.Substring(2), 16);
                    return true;
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                {
                }
            }

            numChildren = 0;
            return false;
        }
    }
}
