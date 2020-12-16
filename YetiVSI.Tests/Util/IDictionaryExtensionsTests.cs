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
using YetiVSI.Util;

namespace YetiVSI.Test.Util
{
    [TestFixture]
    public class IDictionaryExtensionsTests
    {
        IDictionary<string, int> dict;

        [SetUp]
        public void SetUp()
        {
            dict = new Dictionary<string, int>()
            {
                {"one", 1},
                {"two", 2},
                {"three", 3},
            };
        }

        [Test]
        public void GetOrAddValueWhenKeyIsPresent()
        {
            var value = dict.GetOrAddValue("two");
            Assert.AreEqual(2, value);
        }

        [Test]
        public void GetOrAddValueWhenKeyIsAbsent()
        {
            const string key = "four";
            var value = dict.GetOrAddValue(key);
            Assert.AreEqual(new int(), value);
            Assert.IsTrue(dict.ContainsKey(key));
        }
    }
}
