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

using System.Collections.Generic;

namespace DebuggerApi
{
    // Enumeration of language types
    public enum LanguageType
    {
        UNKNOWN,
        C,
        C89,
        C99,
        C11,
        C_PLUS_PLUS,
        C_PLUS_PLUS_03,
        C_PLUS_PLUS_11,
        C_PLUS_PLUS_14
    }

    public class SbLanguageRuntime
    {
        private static Dictionary<LanguageType, string> languageNames =
            new Dictionary<LanguageType, string>()
        {
            { LanguageType.UNKNOWN, "unknown" },
            { LanguageType.C, "c" },
            { LanguageType.C11, "c11" },
            { LanguageType.C89, "c89" },
            { LanguageType.C99, "c99" },
            { LanguageType.C_PLUS_PLUS, "c++" },
            { LanguageType.C_PLUS_PLUS_03, "c++03" },
            { LanguageType.C_PLUS_PLUS_11, "c++11" },
            { LanguageType.C_PLUS_PLUS_14, "c++14" }
        };
        public static string GetNameFromLanguageType(LanguageType languageType)
        {
            string languageName = "unknown";
            languageNames.TryGetValue(languageType, out languageName);
            return languageName;
        }
    }
}
