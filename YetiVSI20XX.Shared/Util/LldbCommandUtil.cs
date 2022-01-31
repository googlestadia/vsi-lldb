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

namespace YetiVSI.Util
{
    public class LldbCommandUtil
    {
        /// <summary>
        /// Quotes and escapes an argument for use with lldb's command interpreter.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if |argument| is null.</exception>
        public static string QuoteArgument(string argument)
        {
            if (argument == null) throw new ArgumentNullException(nameof(argument));
            return '"' + argument.Replace(@"\", @"\\").Replace(@"""", @"\""") + '"';
        }
    }
}
