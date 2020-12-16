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

namespace YetiCommon
{
    public static class ProcessUtil
    {
        static readonly Regex ESCAPABLE_BACKSLASH_MATCHER = new Regex(@"\\*(?=""|$)");

        /// <summary>
        /// Escapes command line argument for the Microsoft command line parser in preparation for
        /// quoting.
        /// Double quotes are backslash-escaped. Literal backslashes are backslash-escaped if they
        /// are followed by a double quote, or if they are part of a sequence of backslashes that
        /// are followed by a double quote.
        /// </summary>
        static string EscapeForWindows(string argument)
        {
            // Escape sequences of backslashes that are followed by a double quote or end of string
            argument = ESCAPABLE_BACKSLASH_MATCHER.Replace(argument, "$0$0");
            // Escape double quotes
            return argument.Replace("\"", "\\\"");
        }

        /// <summary>
        /// Quotes and escapes a command line argument following the convention understood by
        /// the Microsoft command line parser.
        /// </summary>
        public static string QuoteArgument(string argument)
        {
            return $"\"{EscapeForWindows(argument)}\"";
        }

        /// <summary>
        /// Quotes and escapes a command line arguments for use in ssh command. The argument
        /// is first escaped and quoted for Linux using single quotes and then it is
        /// escaped to be used by the Microsoft command line parser.
        /// </summary>
        public static string QuoteAndEscapeArgumentForSsh(string argument)
        {
            // Quote and escape for bash.
            argument = $"'{argument.Replace("'", "'\\''")}'";
            return EscapeForWindows(argument);
        }
    }
}
