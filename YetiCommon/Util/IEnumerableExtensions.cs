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
        /// Return a new enumerable which will yield all the values from the original, but caches
        /// each value as it is consumed. Consuming the resulting enumerable multiple times is
        /// guaranteed to enumerate the original enumerable at most once.
        /// </summary>
        public static IEnumerable<T> Memoize<T>(this IEnumerable<T> values)
        {
            return new MemoizedEnumerable<T>(values.GetEnumerator());
        }

        /// <summary>
        /// Peek returns an enumerable with the same values as the original. The given action
        /// is run against each value as the resulting enumerable is being consumed.
        /// </summary>
        public static IEnumerable<T> Peek<T>(this IEnumerable<T> values, Action<T> action)
        {
            foreach (var v in values)
            {
                action(v);
                yield return v;
            }
        }

        /// <summary>
        /// Returns a new enumerable that includes all the values from the original enumerable up
        /// to and including the first value for which 'f' returns true.
        /// </summary>
        public static IEnumerable<T> TakeUntil<T>(this IEnumerable<T> values, Func<T, bool> f)
        {
            foreach (var v in values)
            {
                yield return v;
                if (f(v))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Returns |first| if it is not null and not empty or |second()|.
        /// Defers evaluation until the enumerable is iterated.
        /// </summary>
        public static IEnumerable<T> OrIfEmpty<T>(this IEnumerable<T> values,
                                                  Func<IEnumerable<T>> fallbackIfEmpty)
        {
            var enumerator = values.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                // |values| is empty, use |fallbackIfEmpty|.
                IEnumerable<T> fallbackValues = fallbackIfEmpty();
                if (fallbackValues != null)
                {
                    foreach (T item in fallbackValues)
                    {
                        yield return item;
                    }
                }

                yield break;
            }

            do
            {
                yield return enumerator.Current;
            } while (enumerator.MoveNext());
        }

        /// <summary>
        /// Return the first value in the enumerable, or the given value, t, if the original
        /// enumerable is empty.
        /// </summary>
        public static T FirstOr<T>(this IEnumerable<T> values, T t) => values.FirstOr(() => t);

        /// <summary>
        /// Return the first value in the enumerable, or the value returned by f, if the original
        /// enumerable is empty.
        /// </summary>
        public static T FirstOr<T>(this IEnumerable<T> values, Func<T> f)
        {
            var enumerator = values.GetEnumerator();
            if (enumerator.MoveNext())
            {
                return enumerator.Current;
            }

            return f();
        }

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

        /// <summary>
        /// Applies an action to all items in an enumerable.
        /// </summary>
        public static void ForEach<T>(this IEnumerable<T> items, Action<T> action)
        {
            foreach (var item in items)
            {
                action(item);
            }
        }
    }

    public static class EnumerableHelpers
    {
        /// <summary>
        /// Create an enumerable that contains the given items.
        /// </summary>
        public static IEnumerable<T> EnumerableOf<T>(params T[] items) => new List<T>(items);

        /// <summary>
        /// Return an enumerable that will yield the given item infinitely.
        /// </summary>
        public static IEnumerable<object> InfiniteEnumerable(object item = null)
        {
            while (true)
            {
                yield return item;
            }
        }
    }
}