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

﻿using DebuggerApi;
using NUnit.Framework;
using System.Collections.Generic;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    public class RemoteValueLLDBFormatTests
    {
        [Test]
        public void UpdateValueFormatSetsFormatAndIgnoresFallbackFormat()
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("test", 1);
            var format = new RemoteValueLLDBFormat(ValueFormat.Hex);
            Assert.AreEqual(remoteValue.GetFormat(), ValueFormat.Default);
            format.FormatValue(remoteValue, ValueFormat.Decimal /* This is ignored! */);
            Assert.AreEqual(remoteValue.GetFormat(), ValueFormat.Hex);
        }

        [Test]
        public void ShouldInheritFormatSpecifier()
        {
            string[] formats = { "B", "b", "y", "Y", "c", "C", "F", "s",   "d", "i", "E", "en", "x",
                                 "h", "X", "H", "f", "o", "O", "U", "U32", "u", "p", "I", "a" };

            foreach (string formatSpecifier in formats)
            {
                IRemoteValueFormat format = RemoteValueFormatProvider.Get(formatSpecifier);
                Assert.That(format, Is.Not.Null);
                Assert.That(format.ShouldInheritFormatSpecifier(), Is.True);
            }
        }

        [Test]
        public void FormatPointerAsAddress()
        {
            CheckPointerFormat("x", 255, $"0x{255:x16}");
            CheckPointerFormat("X", 255, $"0x{255:X16}");
            CheckPointerFormat("h", 255, $"0x{255:x16}");
            CheckPointerFormat("H", 255, $"0x{255:X16}");
            CheckPointerFormat("d", 255, $"0x{255:x16}");
            CheckPointerFormat("b", 255, $"0x{255:x16}");
        }

        void CheckPointerFormat(string formatSpecifier, int valueInt, string expectedValue)
        {
            var remoteValue = RemoteValueFakeUtil.CreatePointer("int*", "int", valueInt.ToString());
            IRemoteValueFormat format = RemoteValueFormatProvider.Get(formatSpecifier);
            Assert.AreEqual(format.FormatValueAsAddress(remoteValue), expectedValue);
            Assert.IsTrue(format.ShouldInheritFormatSpecifier());
        }
    }
}
