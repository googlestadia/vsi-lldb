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

﻿namespace YetiVSI.DebugEngine.Variables
{
    public class ExpandFormatSpecifierUtil
    {
        const string _expandStart = "expand(";
        const string _expandEnd = ")";

        /// <summary>
        /// Parses n and returns true if |formatSpecifier| is of the form "expand(n)"
        /// and false otherwise.
        /// </summary>
        public static bool TryParseExpandFormatSpecifier(string formatSpecifier,
            out int expandedIndex)
        {
            expandedIndex = -1;
            formatSpecifier = FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix(formatSpecifier);
            if (!formatSpecifier.StartsWith(_expandStart) || !formatSpecifier.EndsWith(_expandEnd))
            {
                return false;
            }

            int result;
            if (!int.TryParse(formatSpecifier.Substring(_expandStart.Length,
                formatSpecifier.Length - _expandStart.Length - _expandEnd.Length), out result))
            {
                return false;
            }

            if (result < 0)
            {
                return false;
            }
            expandedIndex = result;
            return true;
        }
    }
}