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
using NUnit.Framework;

namespace YetiCommon.Tests
{
    [TestFixture]
    public class EnumValueAliasAttributeTests
    {
        enum TestType
        {
            [EnumValueAlias(DISABLED)]
            DEFAULT,

            ENABLED,
            DISABLED,
        }

        [Test]
        public void GetEnumAlias()
        {
            Assert.AreEqual(TestType.DISABLED,
                EnumValueAliasAttribute.GetAlias(typeof(TestType), TestType.DEFAULT));
            Assert.IsNull(EnumValueAliasAttribute.GetAlias(typeof(TestType), TestType.ENABLED));
            Assert.IsNull(EnumValueAliasAttribute.GetAlias(typeof(TestType), TestType.DISABLED));
        }

        [Test]
        public void GetEnumAliasOrValue()
        {
            Assert.AreEqual(TestType.DISABLED,
                EnumValueAliasAttribute.GetAliasOrValue(typeof(TestType), TestType.DEFAULT));
            Assert.AreEqual(TestType.ENABLED,
                EnumValueAliasAttribute.GetAliasOrValue(typeof(TestType), TestType.ENABLED));
            Assert.AreEqual(TestType.DISABLED,
                EnumValueAliasAttribute.GetAliasOrValue(typeof(TestType), TestType.DISABLED));
        }
    }
}
