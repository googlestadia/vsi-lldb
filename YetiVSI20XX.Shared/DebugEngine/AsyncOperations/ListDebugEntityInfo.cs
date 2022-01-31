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

﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Diagnostics;

namespace YetiVSI.DebugEngine.AsyncOperations
{
    public abstract class ListDebugEntityInfo<T> where T: struct
    {
        readonly T[] _entityInfos;

        protected ListDebugEntityInfo(T[] entityInfos)
        {
            _entityInfos = entityInfos;
        }

        // It is not quite clear why pCountReturned is ref and whether there are any situations
        // where fromIndex != 0. If they exist and count < itemsArray.Length, it is not obvious
        // where to put the result.
        public int GetItems(int fromIndex, int count, T[] itemsArray, ref int pCountReturned)
        {
            if (fromIndex != 0 || count != itemsArray.Length || pCountReturned != 0)
            {
                Trace.WriteLine(
                    $"Unexpected parameters set passed as an arguments of {GetType()}.GetItems: " +
                    $"{nameof(fromIndex)}={fromIndex}, " +
                    $"{nameof(count)}={count}, " +
                    $"{nameof(pCountReturned)}={pCountReturned}, " +
                    $"{nameof(itemsArray)}.Length={itemsArray.Length}");
            }

            if (fromIndex < 0 || fromIndex > _entityInfos.Length || count < 0 ||
                count > itemsArray.Length)
            {
                pCountReturned = 0;
                return VSConstants.S_FALSE;
            }

            int countToReturn = Math.Min(count, _entityInfos.Length - fromIndex);
            if (countToReturn < 0)
            {
                countToReturn = 0;
            }

            for (int i = 0; i < countToReturn; i++)
            {
                itemsArray[i] = _entityInfos[fromIndex + i];
            }

            pCountReturned = countToReturn;
            return VSConstants.S_OK;
        }

        public int Count => _entityInfos.Length;

        public T this[int lIndex] => _entityInfos[lIndex];
    }

    public class ListDebugFrameInfo : ListDebugEntityInfo<FRAMEINFO>, IListDebugFrameInfo
    {
        public ListDebugFrameInfo(FRAMEINFO[] frameInfos): base(frameInfos)
        {
        }
    }

    public class ListDebugPropertyInfo : 
        ListDebugEntityInfo<DEBUG_PROPERTY_INFO>, IListDebugPropertyInfo
    {
        public ListDebugPropertyInfo(DEBUG_PROPERTY_INFO[] propertyInfos) : base(propertyInfos)
        {
        }
    }
}
