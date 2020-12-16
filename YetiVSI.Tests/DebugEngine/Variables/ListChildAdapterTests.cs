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

﻿using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class ListChildAdapterTests
    {
        List<IVariableInformation> _allChildren;

        [SetUp]
        public void SetUp()
        {
            var child1 = Substitute.For<IVariableInformation>();
            var child2 = Substitute.For<IVariableInformation>();
            var child3 = Substitute.For<IVariableInformation>();

            child1.DisplayName.Returns("child1");
            child2.DisplayName.Returns("child2");
            child3.DisplayName.Returns("child3");

            _allChildren = new List<IVariableInformation>() {child1, child2, child3};
        }

        [Test]
        public async Task GetChildrenReturnsChildrenInTheRequestedRangeAsync()
        {
            var childAdapter = new ListChildAdapter.Factory().Create(_allChildren);

            CollectionAssert.AreEqual(new[] {"child1", "child2"},
                                      await GetChildNamesAsync(childAdapter, 0, 2));

            CollectionAssert.AreEqual(new[] {"child2", "child3"},
                                      await GetChildNamesAsync(childAdapter, 1, 5));

            CollectionAssert.AreEqual(new[] {"child1", "child2", "child3"},
                                      await GetAllChildNamesAsync(childAdapter));
        }

        [Test]
        public async Task CountChildrenReturnsTheNumberOfChildrenAsync()
        {
            var childAdapter = new ListChildAdapter.Factory().Create(_allChildren);

            Assert.That(await childAdapter.CountChildrenAsync(), Is.EqualTo(_allChildren.Count));
        }

        async Task<string[]> GetChildNamesAsync(IChildAdapter childAdapter, int offset, int count)
        {
            return (await childAdapter.GetChildrenAsync(offset, count))
                .Select(child => child.DisplayName)
                .ToArray();
        }

        async Task<string[]> GetAllChildNamesAsync(IChildAdapter childAdapter)
        {
            return (await childAdapter.GetChildrenAsync(0, await childAdapter.CountChildrenAsync()))
                .Select(child => child.DisplayName)
                .ToArray();
        }
    }
}