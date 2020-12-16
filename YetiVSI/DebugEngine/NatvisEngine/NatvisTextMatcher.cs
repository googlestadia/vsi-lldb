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

﻿using System.Text.RegularExpressions;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    // Collection of helper functions to match text found in Natvis xml files.
    //
    // Extracted to it's own class for testability.
    public class NatvisTextMatcher
    {
        static readonly Regex _expressionPathMatcher;

        static NatvisTextMatcher()
        {
            _expressionPathMatcher = new Regex(@"\G(((\.|->)[a-zA-Z_][a-zA-Z_0-9]*)|\[[0-9]+\])+");
        }

        // Checks if |text| could be a subfield of a variable according to C++ syntax.
        //
        // True Examples:
        //   myVar
        //   _myVar
        //   [17]
        // False Examples:
        //   9myVar
        //   [i]
        public static bool IsExpressionPath(string text)
        {
            Match m = _expressionPathMatcher.Match(text);
            return m.Success && m.Length == text.Length;
        }
    }
}