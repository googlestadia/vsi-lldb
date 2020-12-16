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
using NUnit.Framework;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine.NatvisEngine
{
    [TestFixture]
    class RangedNatvisEntityDecoratorTests
    {
        [Test]
        public async Task DelegatesChildrenCountingToWrappedEntityAsync()
        {
            const int maxChildren = 100;
            var rangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().WithCount(5));

            Assert.That(await rangedEntity.CountChildrenAsync(), Is.EqualTo(5));
        }

        [Test]
        public async Task LimitsCountToMaxChildrenCountAllowedPlusOneForMoreAsync()
        {
            const int maxChildren = 2;
            var rangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().WithCount(5));

            Assert.That(await rangedEntity.CountChildrenAsync(), Is.EqualTo(maxChildren + 1));
        }

        [Test]
        public async Task ReturnsChildrenCountForCurrentRangeAsync()
        {
            const int maxChildren = 100;
            var rangedEntity = RangedNatvisEntityDecorator.StartFrom(
                6, maxChildren, TestNatvisEntity.Create().WithCount(10));

            Assert.That(await rangedEntity.CountChildrenAsync(), Is.EqualTo(4));
        }

        [Test]
        public async Task LimitsCountToMaxChildrenCountAllowedWhenRangeIsWithOffsetAsync()
        {
            const int maxChildren = 2;
            var rangedEntity = RangedNatvisEntityDecorator.StartFrom(
                6, maxChildren, TestNatvisEntity.Create().WithCount(10));

            Assert.That(await rangedEntity.CountChildrenAsync(), Is.EqualTo(maxChildren + 1));
        }

        [Test]
        public async Task DelegatesGettingChildrenToWrappedEntityAsync()
        {
            const int maxChildren = 100;
            var rangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().WithCount(10));

            IList<IVariableInformation> children = await rangedEntity.GetChildrenAsync(0, 2);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
        }

        [Test]
        public async Task AddsMoreElementIfItIsInRangeAsync()
        {
            const int maxChildren = 2;
            var rangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().WithCount(10));

            IList<IVariableInformation> children = await rangedEntity.GetChildrenAsync(0, 3);
            Assert.That(children.Count, Is.EqualTo(3));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[More]"));
        }

        [Test]
        public async Task AddsMoreElementIfItIsInRangeWhenOnlyUpperPartRequestedAsync()
        {
            const int maxChildren = 5;
            var rangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().WithCount(10));

            IList<IVariableInformation> children = await rangedEntity.GetChildrenAsync(4, 2);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("4"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[More]"));
        }

        [Test]
        public async Task ReturnsMoreThatStartsFromTheNextItemAsync()
        {
            const int maxChildren = 2;
            var rangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().WithCount(10));

            IList<IVariableInformation> children = await rangedEntity.GetChildrenAsync(0, 3);
            var moreAdapter = children[2].GetChildAdapter();

            Assert.That(await moreAdapter.CountChildrenAsync(), Is.EqualTo(3));

            IList<IVariableInformation> moreChildren = await moreAdapter.GetChildrenAsync(0, 3);
            Assert.That(await moreChildren[0].ValueAsync(), Is.EqualTo("2"));
            Assert.That(await moreChildren[1].ValueAsync(), Is.EqualTo("3"));
            Assert.That(moreChildren[2].DisplayName, Is.EqualTo("[More]"));
        }

        [Test]
        public async Task DoesNotAddMoreElementIfItIsNotInRangeAsync()
        {
            const int maxChildren = 2;
            var rangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().WithCount(2));

            IList<IVariableInformation> children = await rangedEntity.GetChildrenAsync(0, 2);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
        }

        [Test]
        public async Task AddsMoreElementIfItInRangeForItemsWithOffsetAsync()
        {
            const int maxChildren = 2;
            var rangedEntity = RangedNatvisEntityDecorator.StartFrom(
                6, maxChildren, TestNatvisEntity.Create().WithCount(10));

            IList<IVariableInformation> children = await rangedEntity.GetChildrenAsync(1, 2);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("7"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[More]"));
        }

        [Test]
        public async Task DoesNotAddMoreElementIfItIsNotInRangeWithOffsetAsync()
        {
            const int maxChildren = 2;
            var rangedEntity = RangedNatvisEntityDecorator.StartFrom(
                6, maxChildren, TestNatvisEntity.Create().WithCount(10));

            IList<IVariableInformation> children = await rangedEntity.GetChildrenAsync(0, 2);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("6"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("7"));
        }

        [Test]
        public async Task DoesNotAddMoreElementIfAllElementsFitAsync()
        {
            const int maxChildren = 2;
            var rangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().WithCount(2));

            IList<IVariableInformation> children = await rangedEntity.GetChildrenAsync(0, 2);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
        }

        [Test]
        public async Task ReturnsMaxChildrenAllowedIfRequestedMoreAsync()
        {
            const int maxChildren = 2;
            var rangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().WithCount(10));

            IList<IVariableInformation> children = await rangedEntity.GetChildrenAsync(0, 5);
            Assert.That(children.Count, Is.EqualTo(3));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[More]"));
        }

        [Test]
        public async Task ReturnsAllChildrenIfLessThanMaxIfRequestedMoreAsync()
        {
            const int maxChildren = 100;
            var rangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().WithCount(2));

            IList<IVariableInformation> children = await rangedEntity.GetChildrenAsync(0, 5);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
        }

        [Test]
        public async Task ReturnsEmptyListIfRequestedRangeIsNotPresentAsync()
        {
            const int maxChildren = 2;
            var rangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().WithCount(10));

            IList<IVariableInformation> children = await rangedEntity.GetChildrenAsync(7, 2);
            Assert.That(children.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task DelegatesIsValidToWrappedEntityAsync()
        {
            const int maxChildren = 100;
            var validRangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().Valid(true));

            var invalidRangedEntity =
                RangedNatvisEntityDecorator.First(maxChildren,
                                                  TestNatvisEntity.Create().Valid(false));

            Assert.That(await validRangedEntity.IsValidAsync(), Is.True);
            Assert.That(await invalidRangedEntity.IsValidAsync(), Is.False);
        }

        [Test]
        public async Task SetChildrenLimitForWrappedEntityBeforeCountAsync()
        {
            const int maxChildren = 100;
            var entity = TestNatvisEntity.Create();
            var rangedEntity = RangedNatvisEntityDecorator.First(maxChildren, entity);

            Assert.That(entity.ChildrenLimit, Is.EqualTo(0));
            await rangedEntity.CountChildrenAsync();
            Assert.That(entity.ChildrenLimit, Is.EqualTo(maxChildren + 1));
        }
    }
}