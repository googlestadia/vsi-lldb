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
using System;
using System.Collections.Generic;
using System.Linq;
using YetiCommon.Util;
using static YetiCommon.Util.EnumerableHelpers;

namespace YetiCommon.Tests.Util
{
    [TestFixture]
    class IEnumerableExtensionsTests
    {
        readonly IEnumerable<int> list = EnumerableOf(1, 2, 3, 4, 5);

        [Test]
        public void PeekIsLazy()
        {
            Action<int> a = _ => Assert.Fail();
            list.Peek(a);
        }

        [Test]
        public void PeekCallsActionAsConsumed()
        {
            var consumed = new List<int>();
            list.Peek(consumed.Add).Take(3).ToList();
            Assert.That(consumed, Is.EquivalentTo(EnumerableOf(1, 2, 3)));
        }

        [Test]
        public void TakeUntilIncludesLastElement()
        {
            var result = list.TakeUntil(i => i == 4);
            Assert.That(result, Is.EquivalentTo(EnumerableOf(1, 2, 3, 4)));
        }

        [Test]
        public void FirstOrReturnsArgWhenEmptyList()
        {
            var result = EnumerableOf<int>().FirstOr(10);
            Assert.That(result, Is.EqualTo(10));
        }

        [Test]
        public void FirstOrReturnsFirstWhenNonEmptyList()
        {
            var result = list.FirstOr(10);
            Assert.That(result, Is.EqualTo(1));
        }

        [Test]
        public void FirstOrFuncReturnsArgWhenEmptyList()
        {
            var result = EnumerableOf<int>().FirstOr(() => 10);
            Assert.That(result, Is.EqualTo(10));
        }

        [Test]
        public void FirstOrFuncReturnsFirstWhenNonEmptyList()
        {
            var result = list.FirstOr(() => 10);
            Assert.That(result, Is.EqualTo(1));
        }

        [Test]
        public void ForEach()
        {
            string str = "";
            list.ForEach(item => str = str + item);
            Assert.That(str, Is.EqualTo("12345"));
        }

        [Test]
        public void OrIfEmptyDoesNotEvaluateSecondIfNotEmpty()
        {
            Func<IEnumerable<int>> explodeIfCalled =
                () => { throw new InvalidOperationException(); };
            Assert.That(list, Is.Not.Empty);
            Assert.That(list.OrIfEmpty(explodeIfCalled), Is.EqualTo(list));
        }

        IEnumerable<string> GenerateOnePingOnly()
        {
            yield return "ping";
            throw new InvalidOperationException("The Navy sunk your submarine");
        }

        [Test]
        public void OrIfEmptyYieldsSecondIfEmpty()
        {
            IEnumerable<string> emptyList = EnumerableOf<string>();
            IEnumerable<string> result = emptyList.OrIfEmpty(() => GenerateOnePingOnly());
            IEnumerator<string> enumerator = result.GetEnumerator();
            Assert.IsTrue(enumerator.MoveNext());
            Assert.IsTrue(enumerator.Current == "ping");
        }
    }
}
