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

﻿using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using YetiCommon.Util;

namespace YetiCommon.Tests.Util
{
    [TestFixture]
    public class MemoizedEnumerableTests
    {
        private TestEnumerableGenerator generator;

        [SetUp]
        public void SetUp()
        {
            generator = new TestEnumerableGenerator();
        }

        [Test]
        public void TestEnumerableGeneratorWorks()
        {
            var values = generator.GetEnumerable();
            var a1 = values.ToArray();
            var a2 = values.ToArray();

            Assert.That(generator.EnumerationCount, Is.EqualTo(2));
        }

        [Test]
        public void TestEnumeratesOriginalValuesOnce()
        {
            var values = generator.GetEnumerable().Memoize();
            Assert.That(values, Is.EquivalentTo(generator.Values));
            Assert.That(generator.EnumerationCount, Is.EqualTo(1));
        }

        [Test]
        public void TestDoesntEnumerateOriginalTwice()
        {
            var values = generator.GetEnumerable().Memoize();
            var a1 = values.ToArray();
            var a2 = values.ToArray();

            Assert.That(a1, Is.EquivalentTo(generator.Values));
            Assert.That(a2, Is.EquivalentTo(generator.Values));
            Assert.That(generator.EnumerationCount, Is.EqualTo(1));
        }

        [Test]
        public void TestPartialEnumerationDoesntEnumerateOriginalTwice()
        {
            var values = generator.GetEnumerable().Memoize();

            var a1 = values.Take(3).ToArray();
            Assert.That(a1, Is.EquivalentTo(Enumerable.Range(0, 3)));
            Assert.That(generator.EnumerationCount, Is.EqualTo(0));

            var a2 = values.ToArray();
            Assert.That(a2, Is.EquivalentTo(generator.Values));
            Assert.That(generator.EnumerationCount, Is.EqualTo(1));
        }

        private class TestEnumerableGenerator
        {
            int enumerationCount = 0;
            readonly int[] values = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            public IEnumerable<int> GetEnumerable()
            {
                foreach (var i in values)
                {
                    yield return i;
                }
                enumerationCount += 1;
            }

            public int EnumerationCount
            {
                get { return enumerationCount; }
            }

            public int[] Values
            {
                get { return values; }
            }
        }
    }
}
