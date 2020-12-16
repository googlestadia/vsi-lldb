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
using System.Collections.Generic;

namespace Google.VisualStudioFake.API
{
    /// <summary>
    /// Maps enum values to TimeSpans.
    /// </summary>
    /// <typeparam name="T">Enforced to be an enum.</typeparam>
    // TODO: use 'where T : System.Enum' when upgrading to C# version that supports it.
    public class TimeoutSource<T> where T : struct, IConvertible
    {
        readonly Dictionary<T, TimeSpan> _timeouts = new Dictionary<T, TimeSpan>();

        public TimeoutSource()
        {
            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException("T must be an enumerated type.");
            }
        }

        public TimeoutSource(TimeSpan defaultTimeout) : this()
        {
            foreach (T value in typeof(T).GetEnumValues())
            {
                _timeouts[value] = defaultTimeout;
            }
        }

        public TimeSpan this[T key]
        {
            get
            {
                return _timeouts[key];
            }
            set
            {
                _timeouts[key] = value;
            }
        }
    }
}
