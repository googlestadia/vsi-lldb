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
using System.Threading.Tasks;

namespace YetiCommon.Util
{
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Return the first value in the enumerable that satisfies the specified condition. Returns
        /// default value if no element satisfies the condition.
        /// </summary>
        public static async Task<T> FirstOrDefaultAsync<T>(this IEnumerable<T> values,
                                                           Func<T, Task<bool>> predicate)
        {
            if (values == null)
            {
                return default(T);
            }

            using (IEnumerator<T> enumerator = values.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    T value = enumerator.Current;
                    if (await predicate.Invoke(value))
                    {
                        return value;
                    }
                }
            }

            return default(T);
        }
    }
}