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
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    public class RemoteValueNoPrefixIntegerFormatTests
    {
        [Test]
        public void HandlesNoPrefixHexFormats()
        {
            const int valueInt = 175;
            const string valueHex = "af";
            CheckFormat("xb", ValueFormat.Hex, valueInt, valueHex);
            CheckFormat("hb", ValueFormat.Hex, valueInt, valueHex);
            CheckPointerFormat("xb", 255, $"{255:x16}");
            CheckPointerFormat("hb", 255, $"{255:x16}");
        }

        [Test]
        public void HandlesNoPrefixHexUppercaseFormats()
        {
            const int valueInt = 175;
            const string valueHexUppercase = "AF";
            CheckFormat("Xb", ValueFormat.HexUppercase, valueInt, valueHexUppercase);
            CheckFormat("Hb", ValueFormat.HexUppercase, valueInt, valueHexUppercase);
            CheckPointerFormat("Xb", 255, $"{255:X16}");
            CheckPointerFormat("Hb", 255, $"{255:X16}");
        }

        [Test]
        public void HandlesNoPrefixBinFormats()
        {
            const int valueInt = 1282765946;
            const string valueBin = "1001100011101010111010001111010";
            CheckFormat("bb", ValueFormat.Binary, valueInt, valueBin);
            CheckPointerFormat("bb", 255, $"{255:x16}");
        }

        void CheckFormat(string formatSpecifier, ValueFormat expectedFormat, int valueInt,
                         string expectedValue)
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("test", valueInt);
            IRemoteValueFormat format = RemoteValueFormatProvider.Get(formatSpecifier);
            Assert.AreEqual(format.FormatValue(remoteValue, ValueFormat.Default), expectedValue);
            Assert.AreEqual(remoteValue.GetFormat(), expectedFormat);
            Assert.IsTrue(format.ShouldInheritFormatSpecifier());
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
