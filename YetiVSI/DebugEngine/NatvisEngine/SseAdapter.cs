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

﻿using System.Collections.Generic;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    class SseAdapter : IChildAdapter
    {
        readonly int _count;
        readonly IChildAdapter _childAdapter;

        public SseAdapter(int count, IChildAdapter childAdapter)
        {
            _count = count;
            _childAdapter = childAdapter;
        }

        public Task<int> CountChildrenAsync() => Task.FromResult(_count);

        public async Task<IList<IVariableInformation>> GetChildrenAsync(int from, int count) =>
            await _childAdapter.GetChildrenAsync(from, count);
    }
}