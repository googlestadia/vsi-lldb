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

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// Adapter over a list of children.
    /// </summary>
    public class ListChildAdapter : IChildAdapter
    {
        public class Factory
        {
            public IChildAdapter Create(List<IVariableInformation> children) =>
                new ListChildAdapter(children);
        }

        readonly List<IVariableInformation> _children;

        ListChildAdapter(List<IVariableInformation> children)
        {
            _children = children;
        }

        public Task<int> CountChildrenAsync() => Task.FromResult(_children.Count);

        public Task<IList<IVariableInformation>> GetChildrenAsync(int from, int count)
        {
            int childFrom = Math.Max(from, 0);
            int childCount = Math.Min(count, _children.Count - childFrom);
            IList<IVariableInformation> result = childCount > 0
                ? _children.GetRange(childFrom, childCount)
                : new List<IVariableInformation>();

            return Task.FromResult(result);
        }
    }
}