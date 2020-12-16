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

﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace YetiCommon.Util
{
    // A MemoizedEnumerable is guaranteed to enumerate the source numerable no more than once.
    public class MemoizedEnumerable<T> : IEnumerable<T>
    {
        readonly IEnumerator<T> source;
        readonly List<T> list = new List<T>();

        public MemoizedEnumerable(IEnumerator<T> source)
        {
            this.source = source;
        }

        #region IEnumerable functions

        public IEnumerator<T> GetEnumerator()
        {
            // Concatenate the cached items with the remaining items in the source.
            // After all items have been read by from the source ExhaustSource will
            // return an empty enumerator and all items will be read directly from the
            // list.
            return list.Concat(ExhaustSource()).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        private IEnumerable<T> ExhaustSource()
        {
            // Cache items in the list as they are extracted from the source.
            while (source.MoveNext())
            {
                list.Add(source.Current);
                yield return source.Current;
            }
        }
    }
}
