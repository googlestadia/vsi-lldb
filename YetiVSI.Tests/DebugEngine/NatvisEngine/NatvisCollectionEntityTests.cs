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
using NSubstitute;
using NUnit.Framework;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine.NatvisEngine
{
    [TestFixture]
    class NatvisCollectionEntityTests
    {
        NatvisCollectionEntity.Factory _natvisCollectionFactory;

        [SetUp]
        public void SetUp()
        {
            var logger = new NatvisDiagnosticLogger(Substitute.For<NLog.ILogger>(),
                                                    NatvisLoggingLevel.WARNING);

            _natvisCollectionFactory = new NatvisCollectionEntity.Factory(
                Substitute.For<ItemEntity.Factory>(), Substitute.For<SyntheticItemEntity.Factory>(),
                Substitute.For<ExpandedItemEntity.Factory>(),
                Substitute.For<IndexListItemsEntity.Factory>(),
                Substitute.For<ArrayItemsEntity.Factory>(),
                Substitute.For<LinkedListItemsEntity.Factory>(),
                Substitute.For<TreeItemsEntity.Factory>(),
                Substitute.For<CustomListItemsEntity.Factory>(), logger);
        }

        [Test]
        public async Task CountIsEqualToSumOfChildrenCountsPlusRawAsync()
        {
            INatvisEntity natvisCollection = CreateNatvisCollection(
                TestNatvisEntity.Create().WithCount(2), TestNatvisEntity.Create().WithCount(3));

            int count = await natvisCollection.CountChildrenAsync();
            Assert.That(count, Is.EqualTo(6));
        }

        [Test]
        public async Task CountIsEqualToSumOfChildrenCountsWhenRawViewIsHiddenAsync()
        {
            INatvisEntity natvisCollection = CreateNatvisCollectionWithHiddenRawView(
                TestNatvisEntity.Create().WithCount(2), TestNatvisEntity.Create().WithCount(3));

            int count = await natvisCollection.CountChildrenAsync();
            Assert.That(count, Is.EqualTo(5));
        }

        [Test]
        public async Task GetChildrenStartsFromTheFirstChildAsync()
        {
            INatvisEntity natvisCollection =
                CreateNatvisCollection(TestNatvisEntity.Create().WithCount(3));

            IList<IVariableInformation> children = await natvisCollection.GetChildrenAsync(0, 2);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
        }

        [Test]
        public async Task GetChildrenIsShiftedWhenOffsetSpecifiedAsync()
        {
            INatvisEntity natvisCollection =
                CreateNatvisCollection(TestNatvisEntity.Create().WithCount(3));

            IList<IVariableInformation> children = await natvisCollection.GetChildrenAsync(1, 2);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("1"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("2"));
        }

        [Test]
        public async Task GetChildrenIncludesRawViewAsync()
        {
            INatvisEntity natvisCollection =
                CreateNatvisCollection(TestNatvisEntity.Create().WithCount(2));

            IList<IVariableInformation> children = await natvisCollection.GetChildrenAsync(0, 3);
            Assert.That(children.Count, Is.EqualTo(3));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
            Assert.That(children[2].GetType(), Is.EqualTo(typeof(RawChildVariableInformation)));
        }

        [Test]
        public async Task GetChildrenDoesNotIncludeRawViewWhenAskedToHideAsync()
        {
            INatvisEntity natvisCollection =
                CreateNatvisCollectionWithHiddenRawView(TestNatvisEntity.Create().WithCount(2));

            IList<IVariableInformation> children = await natvisCollection.GetChildrenAsync(0, 3);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
        }

        [Test]
        public async Task GetChildrenIncludesRawViewWhenErrorEvenWhenAskedToHideRawViewAsync()
        {
            INatvisEntity natvisCollection =
                CreateNatvisCollectionWithHiddenRawView(
                    TestNatvisEntity.Create().WithCount(1).Valid(false));

            IList<IVariableInformation> children = await natvisCollection.GetChildrenAsync(0, 2);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(children[1].GetType(), Is.EqualTo(typeof(RawChildVariableInformation)));
        }

        [Test]
        public async Task GetChildrenReturnsMaxNumberOfElementsIfMoreRequestedAsync()
        {
            INatvisEntity natvisCollection =
                CreateNatvisCollectionWithHiddenRawView(TestNatvisEntity.Create().WithCount(2));

            IList<IVariableInformation> children = await natvisCollection.GetChildrenAsync(0, 5);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
        }

        [Test]
        public async Task GetChildrenCombinesElementFromChildrenAsync()
        {
            INatvisEntity natvisCollection = CreateNatvisCollection(
                TestNatvisEntity.Create().WithCount(1).WithId(1),
                TestNatvisEntity.Create().WithCount(1).WithId(2));

            IList<IVariableInformation> children = await natvisCollection.GetChildrenAsync(0, 2);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("1_0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("2_0"));
        }

        [Test]
        public async Task GetChildrenCombinesElementFromChildrenWithOffsetAsync()
        {
            INatvisEntity natvisCollection = CreateNatvisCollection(
                TestNatvisEntity.Create().WithCount(3).WithId(1),
                TestNatvisEntity.Create().WithCount(3).WithId(2));

            IList<IVariableInformation> children = await natvisCollection.GetChildrenAsync(2, 3);
            Assert.That(children.Count, Is.EqualTo(3));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("1_2"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("2_0"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("2_1"));
        }

        [Test]
        public async Task GetChildrenSkipsFirstChildIfOffsetIsMoreThanChildSizeAsync()
        {
            INatvisEntity natvisCollection = CreateNatvisCollection(
                TestNatvisEntity.Create().WithCount(1).WithId(1),
                TestNatvisEntity.Create().WithCount(2).WithId(2));

            IList<IVariableInformation> children = await natvisCollection.GetChildrenAsync(1, 2);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("2_0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("2_1"));
        }

        [Test]
        public async Task GetChildrenCalculatesOffsetForTheSecondChildAsync()
        {
            INatvisEntity natvisCollection = CreateNatvisCollection(
                TestNatvisEntity.Create().WithCount(2).WithId(1),
                TestNatvisEntity.Create().WithCount(5).WithId(2));

            IList<IVariableInformation> children = await natvisCollection.GetChildrenAsync(4, 2);
            Assert.That(children.Count, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("2_2"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("2_3"));
        }

        [Test]
        public async Task GetChildrenCombinesChildrenWhenMoreThanTwoAsync()
        {
            INatvisEntity natvisCollection = CreateNatvisCollection(
                TestNatvisEntity.Create().WithCount(3).WithId(1),
                TestNatvisEntity.Create().WithCount(3).WithId(2),
                TestNatvisEntity.Create().WithCount(3).WithId(3));

            IList<IVariableInformation> children = await natvisCollection.GetChildrenAsync(4, 4);
            Assert.That(children.Count, Is.EqualTo(4));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("2_1"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("2_2"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("3_0"));
            Assert.That(await children[3].ValueAsync(), Is.EqualTo("3_1"));
        }

        [Test]
        public async Task
            GetChildrenReturnsEmptyListWhenRequestedRangeDoesNotIntersectWithChildrenAsync()
        {
            INatvisEntity natvisCollection = CreateNatvisCollectionWithHiddenRawView(
                TestNatvisEntity.Create().WithCount(1), TestNatvisEntity.Create().WithCount(2));

            IList<IVariableInformation> children = await natvisCollection.GetChildrenAsync(3, 1);
            Assert.That(children.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task NatvisCollectionEntityIsValidWhenAllChildrenAreValidAsync()
        {
            INatvisEntity natvisCollection = CreateNatvisCollection(
                TestNatvisEntity.Create().Valid(true), TestNatvisEntity.Create().Valid(true));

            Assert.That(await natvisCollection.IsValidAsync(), Is.True);
        }

        [Test]
        public async Task NatvisCollectionEntityIsNotValidWhenAtLeastOneChildIsInvalidAsync()
        {
            INatvisEntity natvisCollection = CreateNatvisCollection(
                TestNatvisEntity.Create().Valid(true), TestNatvisEntity.Create().Valid(false));

            Assert.That(await natvisCollection.IsValidAsync(), Is.False);
        }

        INatvisEntity CreateNatvisCollection(params INatvisEntity[] children) =>
            _natvisCollectionFactory.CreateFromChildrenList(
                children, Substitute.For<IVariableInformation>(), false);

        INatvisEntity CreateNatvisCollectionWithHiddenRawView(params INatvisEntity[] children) =>
            _natvisCollectionFactory.CreateFromChildrenList(
                children, Substitute.For<IVariableInformation>(), true);
    }
}