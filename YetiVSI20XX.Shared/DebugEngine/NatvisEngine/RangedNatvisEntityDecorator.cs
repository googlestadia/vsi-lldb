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

﻿using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class RangedNatvisEntityDecorator : RangedChildAdapterDecorator, INatvisEntity
    {
        readonly INatvisEntity _entity;

        protected RangedNatvisEntityDecorator(int offset, int maxChildren, INatvisEntity entity) :
            base(entity, maxChildren, offset)
        {
            _entity = entity;
        }

        public static INatvisEntity First(int maxChildren, INatvisEntity entity) =>
            StartFrom(0, maxChildren, entity);

        public static INatvisEntity StartFrom(int offset, int maxChildren, INatvisEntity entity) =>
            new RangedNatvisEntityDecorator(offset, maxChildren, entity);

        public async Task<bool> IsValidAsync() => await _entity.IsValidAsync();

        protected override IChildAdapter More(int newOffset, int maxChildren) =>
            StartFrom(newOffset, maxChildren, _entity);
    }
}