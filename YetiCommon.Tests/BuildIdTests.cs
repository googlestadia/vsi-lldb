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
using YetiCommon;

namespace YetiCommon.Tests
{
    [TestFixture]
    class BuildIdTests
    {
        [TestCase("0102", "0102")]
        [TestCase("abcd", "ABCD")]
        [TestCase("", "")]
        public void Equality(string aStr, string bStr)
        {
            var a = new BuildId(aStr);
            var b = new BuildId(bStr);

            Assert.True(a.Equals(b));
            Assert.True(Equals(a, b));
            Assert.True(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestCase("0102", "0201")]
        [TestCase("0102", "010203")]
        [TestCase("0102", "")]
        public void Inequality(string aStr, string bStr)
        {
            var a = new BuildId(aStr);
            var b = new BuildId(bStr);

            Assert.False(a.Equals(b));
            Assert.False(Equals(a, b));
            Assert.True(a != b);
            Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equality_DefaultEqualsZeroLength()
        {
            BuildId defaultBuilId;
            BuildId zeroLength = new BuildId(new byte[] { });

            Assert.True(defaultBuilId.Equals(zeroLength));
            Assert.True(Equals(defaultBuilId, zeroLength));
            Assert.True(defaultBuilId == zeroLength);
            Assert.AreEqual(defaultBuilId.GetHashCode(), zeroLength.GetHashCode());
        }

        [TestCase(new byte[] { }, "")]
        [TestCase(new byte[] { 0x00 }, "00")]
        [TestCase(new byte[] { 0xF0, 0xE1, 0xD2, 0xC3 }, "F0E1D2C3")]
        [TestCase(new byte[] { 0xF0, 0xE1, 0xD2, 0xC3, 0xB4, 0xA5, 0x96, 0x87, 0x78, 0x69, 0x5A,
            0x4B, 0x3C, 0x2D, 0x1E, 0x0F, 0x01, 0x02, 0x03, 0x04},
                "F0E1D2C3-B4A5-9687-7869-5A4B3C2D1E0F-01020304")]
        public void FromBytesToUUIDString(byte[] bytes, string str)
        {
            var buildId = new BuildId(bytes);

            Assert.AreEqual(str, buildId.ToUUIDString());
        }

        [TestCase(new byte[] { }, "")]
        [TestCase(new byte[] { 0x00 }, "00")]
        [TestCase(new byte[] { 0xF0, 0xE1, 0xD2, 0xC3 }, "F0E1D2C3")]
        [TestCase(new byte[] { 0xF0, 0xE1, 0xD2, 0xC3, 0xB4, 0xA5, 0x96, 0x87, 0x78, 0x69, 0x5A,
            0x4B, 0x3C, 0x2D, 0x1E, 0x0F, 0x01, 0x02, 0x03, 0x04},
                "F0E1D2C3B4A5968778695A4B3C2D1E0F01020304")]
        public void FromBytesToHexString(byte[] bytes, string str)
        {
            var buildId = new BuildId(bytes);

            Assert.AreEqual(str, buildId.ToHexString());
        }

        [TestCase("", new byte[] { })]
        [TestCase("1A2b3C4d", new byte[] { 0x1A, 0x2B, 0x3C, 0x04D })]
        [TestCase("000110", new byte[] { 0x00, 0x01, 0x10 })]
        [TestCase("1A-2B-3C", new byte[] { 0x1A, 0x2B, 0x3C })]
        public void FromStringToBytes(string str, byte[] bytes)
        {
            var buildId = new BuildId(str);

            Assert.AreEqual(bytes, buildId.Bytes);
        }

        [TestCase(" ")]
        [TestCase("12xx")]
        [TestCase("123")]
        public void FromStringToBytes_FormatException(string str)
        {
            Assert.Throws(typeof(FormatException), () => new BuildId(str));
        }
    }
}
