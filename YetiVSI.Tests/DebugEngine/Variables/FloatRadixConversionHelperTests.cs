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
using System.Text.RegularExpressions;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    public class FloatRadixConversionHelperTests
    {
        static readonly Lazy<int> DoubleSize = new Lazy<int>(() => 8);

        [Test]
        public void NaN()
        {
            const string value = "0x7ff8000000000000";
            string convertedValue = FloatRadixConversionHelper.TryConvertToFloatFromNumberString(
                value, DoubleSize);
            Assert.That(ParseRatio(convertedValue), Is.EqualTo(double.NaN));
        }

        [Test]
        public void PositiveInfinity()
        {
            const string value = "0x7ff0000000000000";
            string convertedValue = FloatRadixConversionHelper.TryConvertToFloatFromNumberString(
                value, DoubleSize);
            Assert.That(ParseRatio(convertedValue), Is.EqualTo(double.PositiveInfinity));
        }

        [Test]
        public void NegativeInfinity()
        {
            const string value = "0xfff0000000000000";
            string convertedValue = FloatRadixConversionHelper.TryConvertToFloatFromNumberString(
                value, DoubleSize);
            Assert.That(ParseRatio(convertedValue), Is.EqualTo(double.NegativeInfinity));
        }

        [Test]
        public void HexDouble()
        {
            const string value = "0x7e457f48d8a8cd18";
            string convertedValue = FloatRadixConversionHelper.TryConvertToFloatFromNumberString(
                value, DoubleSize);
            Assert.That(convertedValue, Is.EqualTo("1.7995656645221401E+300"));
        }

        [Test]
        public void UpperHex()
        {
            const string value = "0X7E457F48D8A8CD18";
            string convertedValue = FloatRadixConversionHelper.TryConvertToFloatFromNumberString(
                value, DoubleSize);
            Assert.That(convertedValue, Is.EqualTo("1.7995656645221401E+300"));
        }

        [Test]
        public void HexFloat()
        {
            const string value = "0x71b5b5af";
            string convertedValue = FloatRadixConversionHelper.TryConvertToFloatFromNumberString(
                value, new Lazy<int>(() => 4));
            Assert.That(convertedValue, Is.EqualTo("1.79956572E+30"));
        }

        [Test]
        public void Binary()
        {
            const string value =
                "0b111111001000101011111110100100011011000101010001100110100011000";
            string convertedValue = FloatRadixConversionHelper.TryConvertToFloatFromNumberString(
                value, DoubleSize);
            Assert.That(convertedValue, Is.EqualTo("1.7995656645221401E+300"));
        }

        [Test]
        public void Octal()
        {
            const string value = "0771053764433052146430";
            string convertedValue = FloatRadixConversionHelper.TryConvertToFloatFromNumberString(
                value, DoubleSize);
            Assert.That(convertedValue, Is.EqualTo("1.7995656645221401E+300"));
        }

        [Test]
        public void WrongFormat()
        {
            const string value = "0xwrong";
            string convertedValue = FloatRadixConversionHelper.TryConvertToFloatFromNumberString(
                value, DoubleSize);
            Assert.That(convertedValue, Is.EqualTo(value));
        }

        static double ParseRatio(string str)
        {
            Match match = Regex.Match(str, @"(.*)/(.*)");
            var dividend = double.Parse(match.Groups[1].Value);
            var divisor = double.Parse(match.Groups[2].Value);
            return dividend / divisor;
        }
    }
}
