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
    public abstract class RangedChildAdapterDecorator : IChildAdapter
    {
        readonly IChildAdapter _entity;
        readonly int _maxChildren;
        readonly int _offset;

        protected RangedChildAdapterDecorator(IChildAdapter entity, int maxChildren, int offset)
        {
            _entity = entity;
            _maxChildren = maxChildren;
            _offset = offset;
        }

        public async Task<int> CountChildrenAsync() =>
            await CountChildrenWithoutMoreAsync() + (await HasMoreAsync() ? 1 : 0);

        public async Task<IList<IVariableInformation>> GetChildrenAsync(int from, int count)
        {
            from = Math.Max(0, from);

            int entityFrom = _offset + from;
            int entityCount = Math.Min(await CountChildrenWithoutMoreAsync() - from, count);

            if (entityCount < 0)
            {
                return new List<IVariableInformation>();
            }

            IList<IVariableInformation> result =
                await _entity.GetChildrenAsync(entityFrom, entityCount);

            if (await HasMoreAsync() && count > entityCount)
            {
                result.Add(new MoreVariableInformation(More(_offset + _maxChildren, _maxChildren)));
            }

            return result;
        }

        protected abstract IChildAdapter More(int newOffset, int maxChildren);

        // Total number of children left, not capped by _maxChildren.
        async Task<int> CountChildrenUncappedAsync()
        {
            if (_entity is IHasChildrenLimit entityWithChildrenLimit)
            {
                entityWithChildrenLimit.ChildrenLimit = _offset + _maxChildren + 1;
            }

            return await _entity.CountChildrenAsync() - _offset;
        }

        async Task<int> CountChildrenWithoutMoreAsync() =>
            Math.Min(_maxChildren, await CountChildrenUncappedAsync());

        async Task<bool> HasMoreAsync() =>
            _offset + _maxChildren < await _entity.CountChildrenAsync();
    }
}