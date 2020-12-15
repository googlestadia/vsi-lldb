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

using NUnit.Framework;
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
        public void TestSizeExpressionIsValidFormat()
        {
            Assert.That(RemoteValueFormatProvider.IsValidFormat("[anyExpression]!sub"));
        }

        [Test]
        public void SizeSpecifierIsNegativeDecimal()
        {
            uint? size = RemoteValueFormatProvider.TryParseSizeFormat("-99", null);
            Assert.That(size, Is.Null);
        }

        [Test]
        public void SizeSpecifierIsUnsignedHexadecimal()
        {
            Assert.That(RemoteValueFormatProvider.TryParseSizeFormat("0xffffffff", null),
                        Is.EqualTo(4294967295));
        }

        [Test]
        public void SizeSpecifierIsZero()
        {
            Assert.That(RemoteValueFormatProvider.TryParseSizeFormat("0", null), Is.Null);
        }

        [Test]
        public void WhenSizeSpecifierIsNotInteger()
        {
            Assert.That(RemoteValueFormatProvider.TryParseSizeFormat("mySpecifier", null), Is.Null);
        }

        [Test]
        public void SizeSpecifierIsPositiveDecimal()
        {
            Assert.That(RemoteValueFormatProvider.TryParseSizeFormat("3", null), Is.EqualTo(3));
        }

        [Test]
        public void SizeSpecifierIsPositiveHexadecimal()
        {
            Assert.That(RemoteValueFormatProvider.TryParseSizeFormat("0x12", null), Is.EqualTo(18));
        }

        [Test]
        public void SizeSpecifierIsNotFormattedCorrectly(
            [Values("myExpression]", "[myExpression")] string expression)
        {
            Assert.That(RemoteValueFormatProvider.TryParseSizeFormat(expression, null), Is.Null);
        }

        [Test]
        public void SizeIsProvidedAsArgument()
        {
            Assert.That(RemoteValueFormatProvider.TryParseSizeFormat("[anyExpression]", 5),
                        Is.EqualTo(5));
        }

        [Test]
        public void SizeIsNotProvidedForValidExpression()
        {
            LogSpy traceLogSpy = new LogSpy();
            traceLogSpy.Attach();

            Assert.That(RemoteValueFormatProvider.TryParseSizeFormat("[anyExpression]", null),
                        Is.Null);

            Assert.That(traceLogSpy.GetOutput(),
                        Does.Contain("ERROR: Evaluated size specifier isn't provided"));
        }

        [Test]
        public void ConstantSpecifierWithSizeProvided()
        {
            LogSpy traceLogSpy = new LogSpy();
            traceLogSpy.Attach();

            Assert.That(RemoteValueFormatProvider.TryParseSizeFormat("8", 5), Is.EqualTo(8));

            string log = traceLogSpy.GetOutput();
            Assert.That(log, Does.Contain("WARNING:"));
            Assert.That(log, Does.Contain("evaluated size will be ignored"));
        }
    }
}
