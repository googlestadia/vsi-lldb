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
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class ExpandFormatSpecifierUtilTests
    {
        [Test]
        public void ExpandFormatSpecifierUtilTest()
        {
            int result = 0;

            Assert.That(ExpandFormatSpecifierUtil.
                TryParseExpandFormatSpecifier(null, out result), Is.False);

            Assert.That(ExpandFormatSpecifierUtil.
                TryParseExpandFormatSpecifier("expand(X)", out result), Is.False);

            Assert.That(ExpandFormatSpecifierUtil.
                TryParseExpandFormatSpecifier("expand(1", out result), Is.False);
            Assert.That(result, Is.EqualTo(-1));

            Assert.That(ExpandFormatSpecifierUtil.
                TryParseExpandFormatSpecifier("xpand(1)", out result), Is.False);
            Assert.That(result, Is.EqualTo(-1));

            Assert.That(ExpandFormatSpecifierUtil.
                TryParseExpandFormatSpecifier("expand(1)", out result), Is.True);
            Assert.That(result, Is.EqualTo(1));

            Assert.That(ExpandFormatSpecifierUtil.
                TryParseExpandFormatSpecifier(" !expand(2)", out result), Is.True);
            Assert.That(result, Is.EqualTo(2));

            Assert.That(ExpandFormatSpecifierUtil.
                TryParseExpandFormatSpecifier("expand(-1)", out result), Is.False);
        }
    }
}
