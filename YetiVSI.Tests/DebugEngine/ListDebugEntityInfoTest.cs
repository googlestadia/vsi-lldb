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

﻿using Microsoft.VisualStudio;
using NUnit.Framework;
using YetiVSI.DebugEngine.AsyncOperations;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class ListDebugEntityInfoTest
    {
        struct TestEntity
        {
            public string bstrName;
        }

        class ListDebugTestInfo : ListDebugEntityInfo<TestEntity>
        {
            public ListDebugTestInfo(TestEntity[] testEntities) : base(testEntities)
            {
            }
        }

        TestEntity[] _testEntities;

        const string _firstElementName = "First";
        const string _secondElementName = "Second";

        [SetUp]
        public void SetUp()
        {
            _testEntities = new TestEntity[2];
            _testEntities[0] = new TestEntity { bstrName = _firstElementName};
            _testEntities[1] = new TestEntity { bstrName = _secondElementName};
        }

        [Test]
        public void CountNumberOfElements()
        {
            var list = new ListDebugTestInfo(_testEntities);
            Assert.AreEqual(_testEntities.Length, list.Count);
        }

        [Test]
        public void AccessElementByIndex()
        {
            var list = new ListDebugTestInfo(_testEntities);
            Assert.AreEqual(_firstElementName, list[0].bstrName);
            Assert.AreEqual(_secondElementName, list[1].bstrName);
        }

        [Test]
        public void GetItems()
        {
            var list = new ListDebugTestInfo(_testEntities);

            var outProperties = new TestEntity[_testEntities.Length];
            int returnedCount = 0;
            int status = list.GetItems(0, _testEntities.Length, outProperties, ref returnedCount);

            Assert.AreEqual(VSConstants.S_OK, status);
            Assert.AreEqual(_testEntities.Length, returnedCount);
            Assert.AreEqual(_firstElementName, outProperties[0].bstrName);
            Assert.AreEqual(_secondElementName, outProperties[1].bstrName);
        }

        [Test]
        public void GetItemsWhenFromIndexIsNotZero()
        {
            var list = new ListDebugTestInfo(_testEntities);

            var outProperties = new TestEntity[1];
            int returnedCount = 0;
            int status = list.GetItems(1, 1, outProperties, ref returnedCount);

            Assert.AreEqual(VSConstants.S_OK, status);
            Assert.AreEqual(1, returnedCount);
            Assert.AreEqual(_secondElementName, outProperties[0].bstrName);
        }

        [Test]
        public void FalseIsReturnedOnUnexpectedInput()
        {
            var list = new ListDebugTestInfo(_testEntities);

            var outProperties = new TestEntity[_testEntities.Length];
            int returnedCount = 0;

            int statusWhenFromIsNegative = list.GetItems(-1, _testEntities.Length, outProperties,
                ref returnedCount);
            int statusWhenFromMoreThanCount = list.GetItems(10, _testEntities.Length,
                outProperties, ref returnedCount);
            int statusWhenCountIsNegative = list.GetItems(0, -1, outProperties, ref returnedCount);
            int statusWhenCountIsMoreThanArrayLength = list.GetItems(0, outProperties.Length + 1,
                outProperties, ref returnedCount);

            Assert.AreEqual(VSConstants.S_FALSE, statusWhenFromIsNegative);
            Assert.AreEqual(VSConstants.S_FALSE, statusWhenFromMoreThanCount);
            Assert.AreEqual(VSConstants.S_FALSE, statusWhenCountIsNegative);
            Assert.AreEqual(VSConstants.S_FALSE, statusWhenCountIsMoreThanArrayLength);
        }
    }
}