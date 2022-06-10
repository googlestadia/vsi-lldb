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

﻿using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace YetiVSI.DebugEngine.Variables
{
    public class FormatSpecifierUtil
    {
        public const string RawFormatSpecifier = "!";
        public const string InheritableRawFormatSpecifier = "!!";

        /// <summary>
        /// All format specifiers that suppress the display of a variable's memory address, see
        /// https://docs.microsoft.com/en-us/visualstudio/debugger/format-specifiers-in-cpp?view=vs-2019.
        /// </summary>
        static readonly HashSet<string> _noMemoryAddressFormatSpecifiers = new HashSet<string>
        {
            "s", "sb", "s8", "s8b", "su", "sub", "s32", "s32b", "bstr", "env", "na", "I", "F"
        };

        // Size specifier can be a decimal number, hex number, '[expression]' or missing.
        const string _sizeSpecifierRegex = "([1-9][0-9]*|0x[0-9a-f]+|\\[.*\\]|)";
        const string _rawFormatRegex = "(!?!?)";
        const string _baseFormatRegex = "(|[a-zA-Z]+[a-zA-Z0-9]*)";

        static readonly Regex _formatRegex =
            new Regex($"^{_sizeSpecifierRegex}{_rawFormatRegex}{_baseFormatRegex}$");

        /// <summary>
        /// Removes raw format specifier prefix (! or !!). This is useful for detecting view and
        /// expand specifiers or for determining validity of size specifier suffix.
        /// </summary>
        public static string RemoveRawFormatSpecifierPrefix(string formatSpecifier)
        {
            formatSpecifier = Clean(formatSpecifier);

            if (formatSpecifier.StartsWith(InheritableRawFormatSpecifier))
            {
                return formatSpecifier.Substring(InheritableRawFormatSpecifier.Length);
            }

            if (formatSpecifier.StartsWith(RawFormatSpecifier))
            {
                return formatSpecifier.Substring(RawFormatSpecifier.Length);
            }

            return formatSpecifier;
        }

        public static bool HasRawFormatSpecifier(string formatSpecifier)
        {
            if (!TrySplitSpecifier(formatSpecifier, out _, out string rawFormatSpecifier, out _))
            {
                // If splitting fails, we can still have expand/view specifier with a raw suffix,
                // we handle that here.
                return Clean(formatSpecifier).StartsWith(RawFormatSpecifier);
            }
            return !string.IsNullOrEmpty(rawFormatSpecifier);
        }

        public static bool SuppressMemoryAddress(string formatSpecifier)
        {
            if (!TrySplitSpecifier(formatSpecifier, out _, out _, out string baseSpecifier))
            {
                return false;
            }
            return _noMemoryAddressFormatSpecifiers.Contains(baseSpecifier);
        }

        /// <summary>
        /// Splits composite format specifier into its size component, raw formatter component
        /// and base formatter component. Note that the base component is assumed to be
        /// only alphanumeric, so splitting "view()" and "expand()" specifiers will fail.
        ///
        /// The caller is required to handle "view()" and "expand()" separately.
        /// </summary>
        public static bool TrySplitSpecifier(string specifier, out string size, out string raw,
                                             out string baseSpecifier)
        {
            size = "";
            raw = "";
            baseSpecifier = "";
            if (specifier == null)
            {
                return false;
            }
            specifier = Clean(specifier);
            Match match = _formatRegex.Match(specifier);
            if (match.Success)
            {
                size = match.Groups[1].Value;
                raw = match.Groups[2].Value;
                baseSpecifier = match.Groups[3].Value;
                return true;
            }
            return false;
        }

        public static string GetChildFormatSpecifier(string formatSpecifier,
                                                     IRemoteValueFormat valueFormat)
        {
            formatSpecifier = Clean(formatSpecifier);
            if (!TrySplitSpecifier(formatSpecifier, out string size, out string raw,
                                   out string baseSpecifier))
            {
                // If we do not have the plain base formatter, let us inherit 'view' and
                // inheritable raw formatter (we ignore 'expand' as it is not inherited).
                raw = formatSpecifier.StartsWith(InheritableRawFormatSpecifier)
                          ? InheritableRawFormatSpecifier
                          : "";
                baseSpecifier = RemoveRawFormatSpecifierPrefix(formatSpecifier);
                if (string.IsNullOrEmpty(NatvisViewsUtil.ParseViewFormatSpecifier(baseSpecifier)))
                {
                    baseSpecifier = "";
                }
                return raw + baseSpecifier;
            }

            // Remove the non-inheritable ! specifier.
            if (raw == RawFormatSpecifier)
            {
                raw = "";
            }

            // Let the valueFormat decide whether to inherit the type. Note: Some types are not
            // represented as IRemoteValueFormat, e.g "na". These are not inherited, though.
            if (!valueFormat.ShouldInheritFormatSpecifier())
            {
                baseSpecifier = "";
            }

            return $"{raw}{baseSpecifier}";
        }

        static string Clean(string formatSpecifier) =>
            string.IsNullOrWhiteSpace(formatSpecifier) ? string.Empty : formatSpecifier.Trim();
    }
}