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
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class NatvisEntityStore
    {
        // ChildrenCount must be initialized before accessing.
        public int ChildrenCount
        {
            get => _childrenCount.Value;
            set => _childrenCount = value;
        }

        int? _childrenCount;

        public ErrorVariableInformation ValidationError { get; set; }

        readonly IDictionary<int, IVariableInformation> _cachedVarInfo =
            new Dictionary<int, IVariableInformation>();

        public async Task<IVariableInformation> GetOrEvaluateAsync(
            int index, Func<int, Task<IVariableInformation>> evaluator)
        {
            if (_cachedVarInfo.TryGetValue(index, out IVariableInformation varInfo))
            {
                return varInfo;
            }

            varInfo = await evaluator.Invoke(index);
            _cachedVarInfo.Add(index, varInfo);

            return varInfo;
        }

        public void SaveVariable(int index, IVariableInformation variable) =>
            _cachedVarInfo.Add(index, variable);

        public IVariableInformation GetVariable(int index) => _cachedVarInfo[index];

        public bool HasVariable(int index) => _cachedVarInfo.ContainsKey(index);
    }
}