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
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class RemoteValueChildAdapterTests
    {
        RemoteValueFake remoteValue;
        VarInfoBuilder varInfoBuilder;

        static readonly string noFormatSpecifier = string.Empty;

        [SetUp]
        public void SetUp()
        {
            remoteValue = RemoteValueFakeUtil.CreateClass("int[]", "array", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleString("child1", "value1"));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleString("child2", "value2"));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleString("child3", "value3"));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleString("child4", "value4"));

            var childAdapterFactory = new RemoteValueChildAdapter.Factory();
            var varInfoFactory = new LLDBVariableInformationFactory(childAdapterFactory);

            varInfoBuilder = new VarInfoBuilder(varInfoFactory);
            varInfoFactory.SetVarInfoBuilder(varInfoBuilder);
        }

        [Test]
        public async Task GetChildrenReturnsChildrenInTheSelectedRangeAsync()
        {
            var childAdapter = new RemoteValueChildAdapter.Factory().Create(
                remoteValue, RemoteValueFormat.Default, varInfoBuilder, noFormatSpecifier);

            CollectionAssert.AreEqual(new[] {"child1", "child2"},
                                      await GetChildNamesAsync(childAdapter, 0, 2));

            CollectionAssert.AreEqual(new[] {"child2"},
                                      await GetChildNamesAsync(childAdapter, 1, 1));

            CollectionAssert.AreEqual(new[] {"child1", "child2", "child3", "child4"},
                                      await GetAllChildNamesAsync(childAdapter));

            CollectionAssert.AreEqual(new[] {"child1", "child2", "child3", "child4"},
                                      await GetChildNamesAsync(childAdapter, 0, 100));
        }

        [Test]
        public async Task GetChildrenReturnsMoreElementWhenMoreThanRangeSizeRequestedAsync()
        {
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleString("child5", "value5"));

            int _countPerRange = 2;
            var childAdapter = new RemoteValueChildAdapter.Factory().CreateForTesting(
                remoteValue, RemoteValueFormat.Default, varInfoBuilder, noFormatSpecifier,
                _countPerRange);

            IList<IVariableInformation> children = await childAdapter.GetChildrenAsync(0, 5);
            Assert.That(children.Count, Is.EqualTo(3));
            Assert.That(children[2].DisplayName, Is.EqualTo("[More]"));

            IVariableInformation more = children[2];
            CollectionAssert.AreEqual(new[] {"child3", "child4", "[More]"},
                                      await GetAllChildNamesAsync(more.GetChildAdapter()));

            IVariableInformation nextMore =
                (await more.GetChildAdapter().GetChildrenAsync(2, 1))[0];

            CollectionAssert.AreEqual(new[] {"child5"},
                                      await GetAllChildNamesAsync(nextMore.GetChildAdapter()));
        }

        [Test]
        public async Task InheritableFormatSpecifiersAreInheritedAsync()
        {
            IRemoteValueFormat lldbFormat = RemoteValueFormatProvider.Get("x");
            Assert.That(lldbFormat, Is.Not.Null);
            Assert.That(lldbFormat.ShouldInheritFormatSpecifier(), Is.True);

            var childAdapter = new RemoteValueChildAdapter.Factory().Create(
                remoteValue, lldbFormat, varInfoBuilder, formatSpecifier: "x");

            CollectionAssert.AreEqual(new[] {"x", "x", "x", "x"},
                                      await GetAllChildFormatSpecifiersAsync(childAdapter));
        }

        [Test]
        public async Task UninheritableFormatSpecifiersAreNotInheritedAsync()
        {
            Assert.That(RemoteValueDefaultFormat.DefaultFormatter.ShouldInheritFormatSpecifier(),
                        Is.False);

            var childAdapter = new RemoteValueChildAdapter.Factory().Create(
                remoteValue, RemoteValueFormat.Default, varInfoBuilder, formatSpecifier: "na");

            CollectionAssert.AreEqual(
                new[] {string.Empty, string.Empty, string.Empty, string.Empty},
                await GetAllChildFormatSpecifiersAsync(childAdapter));
        }

        [Test]
        public async Task RawFormatSpecifierIsRemovedAsync()
        {
            IRemoteValueFormat lldbFormat = RemoteValueFormatProvider.Get("!x");
            Assert.That(lldbFormat, Is.Not.Null);
            Assert.That(lldbFormat.ShouldInheritFormatSpecifier(), Is.True);

            var childAdapter = new RemoteValueChildAdapter.Factory().Create(
                remoteValue, lldbFormat, varInfoBuilder, formatSpecifier: "!x");

            CollectionAssert.AreEqual(new[] {"x", "x", "x", "x"},
                                      await GetAllChildFormatSpecifiersAsync(childAdapter));
        }

        [Test]
        public async Task InheritableRawFormatSpecifierIsNotRemovedAsync()
        {
            IRemoteValueFormat lldbFormat = RemoteValueFormatProvider.Get("!!x");
            Assert.That(lldbFormat, Is.Not.Null);
            Assert.That(lldbFormat.ShouldInheritFormatSpecifier(), Is.True);

            var childAdapter = new RemoteValueChildAdapter.Factory().Create(
                remoteValue, lldbFormat, varInfoBuilder, formatSpecifier: "!!x");

            CollectionAssert.AreEqual(new[] {"!!x", "!!x", "!!x", "!!x"},
                                      await GetAllChildFormatSpecifiersAsync(childAdapter));
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

        async Task<string[]> GetAllChildFormatSpecifiersAsync(IChildAdapter childAdapter)
        {
            return (await childAdapter.GetChildrenAsync(0, await childAdapter.CountChildrenAsync()))
                .Select(child => child.FormatSpecifier)
                .ToArray();
        }
    }
}