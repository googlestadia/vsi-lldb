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
using System.Linq;
using System.Text;

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// Helps C-strings to escape from the prison of special characters.
    /// </summary>
    public class CStringEscapeHelper
    {
        static readonly Dictionary<char, string> escapeCharacters = new Dictionary<char, string>
        {
            { '\a', "\\a" },
            { '\b', "\\b" },
            { '\f', "\\f" },
            { '\n', "\\n" },
            { '\r', "\\r" },
            { '\t', "\\t" },
            { '\v', "\\v" },
            { '\\', "\\\\" },
            { '"', "\\\"" },
            { '\0', "\\0" },
        };

        static readonly Dictionary<char, char> unescapeCharacters = new Dictionary<char, char>
        {
            { 'a', '\a' },
            { 'b', '\b' },
            { 'f', '\f' },
            { 'n', '\n' },
            { 'r', '\r' },
            { 't', '\t' },
            { 'v', '\v' },
            { '\\', '\\' },
            { '"', '"' },
            { '0', '\0' },
        };

        static readonly List<string> stringLiteralPrefixes = new List<string>()
        {
            "\"",
            "L\"",
            "u8\"",
            "u\"",
            "U\"",
        };

        /// <summary>
        /// Escapes special characters like \n and \t, and encloses the string in quotes.
        /// Example: "my\tstring" -> "\"my\\tstring\"".
        /// The enclosing quotes can be customized by changing prefix and postfix.
        /// </summary>
        public static string Escape(string str, string prefix = "\"", string postfix = "\"")
        {
            if (str == null)
            {
                return null;
            }

            StringBuilder result = new StringBuilder();
            for (int n = 0; n < str.Length; ++n)
            {
                string escapedChar;
                if (escapeCharacters.TryGetValue(str[n], out escapedChar))
                {
                    result.Append(escapedChar);
                }
                else
                {
                    result.Append(str[n]);
                }
            }

            return $"{prefix}{result}{postfix}";
        }

        /// <summary>
        /// Removes enclosing quotes and unescapes special characters like \n and \t. If |str| has
        /// a prefix, e.g. L"foo", u8"foo", that prefix is removed as well. Bad escape sequences
        /// are kept.
        /// Returns null if the string does not look like a C string literal, i.e. if it's not
        /// enclosed with quotes or has an unknown prefix.
        /// Example: "\"my\\tstring\"" -> "my\tstring", L"foo" -> "foo".
        /// </summary>
        public static string Unescape(string str)
        {
            if (string.IsNullOrEmpty(str) || str[str.Length - 1] != '"')
            {
                return null;
            }

            string prefix = stringLiteralPrefixes.FirstOrDefault(pf => str.StartsWith(pf));
            if (prefix == null || str.Length <= prefix.Length)
            {
                return null;
            }
            str = str.Substring(prefix.Length, str.Length - 1 - prefix.Length);

            StringBuilder result = new StringBuilder();
            for (int n = 0; n < str.Length; ++n)
            {
                // Append any non-backslash or a backslash at the end.
                if (str[n] != '\\' || n == str.Length - 1)
                {
                    result.Append(str[n]);
                    continue;
                }

                char unescapedChar;
                if (unescapeCharacters.TryGetValue(str[++n], out unescapedChar))
                {
                    result.Append(unescapedChar);
                }
                else
                {
                    // Bad escape sequence -> keep escaped string.
                    result.Append('\\');
                    result.Append(str[n]);
                    break;
                }
            }

            return result.ToString();
        }
    }
}
