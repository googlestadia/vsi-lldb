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
using System;
using System.Collections.Generic;
using TestsCommon.TestSupport;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class RemoteValueFormatProviderTests
    {
        const string SPECIFIER_1 = "x";
        const string SPECIFIER_2 = "s32";

        [Test]
        public void TestGetWithRawFormatSpecifier(
            [Values(FormatSpecifierUtil.RawFormatSpecifier + SPECIFIER_1,
                    FormatSpecifierUtil.InheritableRawFormatSpecifier + SPECIFIER_2)]
            string specifier)
        {
            bool validFormat = RemoteValueFormatProvider.IsValidFormat(specifier);
            IRemoteValueFormat format = RemoteValueFormatProvider.Get(specifier);
            Assert.That(validFormat, Is.True);
        }

        [Test]
        public void TestGetStandaloneRawFormatSpecifiers(
            [Values(FormatSpecifierUtil.RawFormatSpecifier,
                    FormatSpecifierUtil.InheritableRawFormatSpecifier)] string specifier)
        {
            bool validFormat = RemoteValueFormatProvider.IsValidFormat(specifier);
            ISingleValueFormat format = RemoteValueFormatProvider.Get(specifier);
            Assert.That(validFormat, Is.True);
            Assert.That(format is RemoteValueFormat);
        }

        [Test]
        public void TestFallBackOnDefaultFormat()
        {
            bool validFormat = RemoteValueFormatProvider.IsValidFormat("unsupportedSpecifier");
            IRemoteValueFormat format = RemoteValueFormatProvider.Get("unsupportedSpecifier");
            Assert.That(validFormat, Is.False);
            Assert.That(format, Is.Not.Null);
        }

        [Test]
        public void TestFallBackOnDefaultFormatWithRawSpecifier()
        {
            bool validFormat = RemoteValueFormatProvider.IsValidFormat("!unsupportedSpecifier");
            IRemoteValueFormat format = RemoteValueFormatProvider.Get("!unsupportedSpecifier");
            Assert.That(validFormat, Is.False);
            Assert.That(format, Is.Not.Null);
        }

        [Test]
        public void TestWhenFormatSpecifierIsEmpty()
        {
            bool validFormat = RemoteValueFormatProvider.IsValidFormat("");
            IRemoteValueFormat format = RemoteValueFormatProvider.Get("");

            Assert.That(validFormat, Is.False);
            Assert.That(format, Is.Not.Null);
        }

        [Test]
        public void TestViewIsValidFormat()
        {
            Assert.That(RemoteValueFormatProvider.IsValidFormat("view(simple)"));
        }

        [Test]
        public void TestExpandIsValidFormat()
        {
            Assert.That(RemoteValueFormatProvider.IsValidFormat("expand(2)"));
        }

        [Test]
        public void TestRawIsValidFormat()
        {
            Assert.That(RemoteValueFormatProvider.IsValidFormat("!"));
        }

        [Test]
        public void SizeSpecifierIsNegativeDecimal()
        {
            IRemoteValueNumChildrenProvider format;
            bool result = RemoteValueFormatProvider.TryParseSizeFormat("-99", null, out format);
            Assert.That(result, Is.False);
        }

        [Test]
        public void SizeSpecifierIsNegativeHexadecimal()
        {
            IRemoteValueNumChildrenProvider format;
            bool result =
                RemoteValueFormatProvider.TryParseSizeFormat("0xffffffff", null, out format);
            Assert.That(result, Is.False);
        }

        [Test]
        public void SizeSpecifierIsZero()
        {
            IRemoteValueNumChildrenProvider format;
            bool result = RemoteValueFormatProvider.TryParseSizeFormat("0", null, out format);
            Assert.That(result, Is.False);
        }

        [Test]
        public void WhenSizeSpecifierIsNotInteger()
        {
            IRemoteValueNumChildrenProvider format;
            bool result =
                RemoteValueFormatProvider.TryParseSizeFormat("mySpecifier", null, out format);
            Assert.That(result, Is.False);
        }

        [Test]
        public void SizeSpecifierIsPositiveDecimal()
        {
            IRemoteValueNumChildrenProvider format;
            bool result = RemoteValueFormatProvider.TryParseSizeFormat("3", null, out format);
            Assert.That(result, Is.True);
            Assert.That(format, Is.Not.Null);
        }

        [Test]
        public void SizeSpecifierIsPositiveHexadecimal()
        {
            IRemoteValueNumChildrenProvider format;
            bool result = RemoteValueFormatProvider.TryParseSizeFormat("0x3", null, out format);
            Assert.That(result, Is.True);
            Assert.That(format, Is.Not.Null);
        }

        [Test]
        public void SizeSpecifierIsNotFormattedCorrectly(
            [Values("myExpression]", "[myExpression")] string expression)
        {
            IRemoteValueNumChildrenProvider format;
            var result = RemoteValueFormatProvider.TryParseSizeFormat(expression, null, out format);
            Assert.That(result, Is.False);
        }

        [Test]
        public void SpecifierIsFormattedCorrectly()
        {
            IRemoteValueNumChildrenProvider format;
            var result =
                RemoteValueFormatProvider.TryParseSizeFormat("[anyExpression]", null, out format);
            Assert.That(result, Is.True);
            Assert.That(format, Is.Not.Null);
        }

        [Test]
        public void SizeIsProvidedAsArgument()
        {
            IRemoteValueNumChildrenProvider format;
            var result =
                RemoteValueFormatProvider.TryParseSizeFormat("[anyExpression]", 5, out format);
            Assert.That(result, Is.True);
            Assert.That(format, Is.Not.Null);
            Assert.IsTrue(format is ScalarNumChildrenProvider);
            Assert.That(format.GetNumChildren(null), Is.EqualTo(5));
        }

        [Test]
        public void ConstantSpecifierWithSizeProvided()
        {
            IRemoteValueNumChildrenProvider format;
            var result = RemoteValueFormatProvider.TryParseSizeFormat("8", 5, out format);
            Assert.That(result, Is.True);
            Assert.That(result, Is.Not.Null);
            Assert.IsTrue(format is ScalarNumChildrenProvider);
            Assert.That(format.GetNumChildren(null), Is.EqualTo(8));
        }
    }
}
