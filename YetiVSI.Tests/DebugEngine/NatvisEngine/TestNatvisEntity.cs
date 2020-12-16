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
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine.NatvisEngine
{
    public class TestNatvisEntity : INatvisEntity, IHasChildrenLimit
    {
        readonly int _count;
        readonly int _id;
        readonly bool _valid;

        public int ChildrenLimit { get; set; }

        TestNatvisEntity(int count, int id, bool valid)
        {
            _count = count;
            _id = id;
            _valid = valid;
        }

        public static TestNatvisEntity Create() => new TestNatvisEntity(0, 0, true);

        public TestNatvisEntity WithCount(int count) => new TestNatvisEntity(count, _id, _valid);

        public TestNatvisEntity WithId(int id) => new TestNatvisEntity(_count, id, _valid);

        public TestNatvisEntity Valid(bool valid) => new TestNatvisEntity(_count, _id, valid);

        public Task<int> CountChildrenAsync() =>
            Task.FromResult(ChildrenLimit > 0 ? Math.Min(_count, ChildrenLimit) : _count);

        public Task<IList<IVariableInformation>> GetChildrenAsync(int from, int count) =>
            Task.FromResult<IList<IVariableInformation>>(
                Enumerable.Range(from, count).Select(GetMockedVariable).ToList());

        public Task<bool> IsValidAsync() => Task.FromResult(_valid);

        IVariableInformation GetMockedVariable(int value)
        {
            var variable = Substitute.For<IVariableInformation>();
            variable.ValueAsync().Returns(_id == 0 ? value.ToString() : $"{_id}_{value}");
            return variable;
        }
    }
}