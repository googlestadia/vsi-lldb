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

﻿using System.Linq;
using System.Text;
using DebuggerApi;
using NSubstitute;
using NUnit.Framework;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    public class RemoteValueStringFormatTests
    {
        // The fallback format should be unused.
        ValueFormat _fallback = ValueFormat.Default;

        const string _value = "text \t\n ☕";
        const string _quotedEscapedValueAscii = "\"text \\t\\n â˜•\"";
        const string _quotedEscapedValueUTF8 = "u8\"text \\t\\n ☕\"";
        const string _quotedEscapedValueUTF16 = "u\"text \\t\\n ☕\"";
        const string _quotedEscapedValueUTF32 = "U\"text \\t\\n ☕\"";
        const string _escapedValueAscii = "text \\t\\n â˜•";
        const string _escapedValue = "text \\t\\n ☕";
        const string _stringViewAscii = "text \t\n â˜•";
        const string _stringView = "text \t\n ☕";

        [Test]
        public void SupportsAllStringTypes()
        {
            Assert.NotNull(RemoteValueFormatProvider.Get("s"));
            Assert.NotNull(RemoteValueFormatProvider.Get("sb"));
            Assert.NotNull(RemoteValueFormatProvider.Get("s8"));
            Assert.NotNull(RemoteValueFormatProvider.Get("s8b"));
            Assert.NotNull(RemoteValueFormatProvider.Get("su"));
            Assert.NotNull(RemoteValueFormatProvider.Get("sub"));
            Assert.NotNull(RemoteValueFormatProvider.Get("s32"));
            Assert.NotNull(RemoteValueFormatProvider.Get("s32b"));
        }

        [Test]
        public void EncodesCStringsStrings()
        {
            var remoteValue = RemoteValueFor(_value, Encoding.UTF8, charSize: 1);

            IRemoteValueFormat format;

            format = RemoteValueFormatProvider.Get("s");
            Assert.AreEqual(_quotedEscapedValueAscii, format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual(_stringViewAscii, format.FormatStringView(remoteValue, _fallback));

            format = RemoteValueFormatProvider.Get("sb");
            Assert.AreEqual(_escapedValueAscii, format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual(_stringViewAscii, format.FormatStringView(remoteValue, _fallback));
        }

        [Test]
        public void EncodesUTF8Strings()
        {
            var remoteValue = RemoteValueFor(_value, Encoding.UTF8, charSize: 1);

            IRemoteValueFormat format;

            format = RemoteValueFormatProvider.Get("s8");
            Assert.AreEqual(_quotedEscapedValueUTF8, format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual(_stringView, format.FormatStringView(remoteValue, _fallback));

            format = RemoteValueFormatProvider.Get("s8b");
            Assert.AreEqual(_escapedValue, format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual(_stringView, format.FormatStringView(remoteValue, _fallback));
        }

        [Test]
        public void EncodesUTF16Strings()
        {
            var remoteValue = RemoteValueFor(_value, Encoding.Unicode, charSize: 2);

            IRemoteValueFormat format;

            format = RemoteValueFormatProvider.Get("su");
            Assert.AreEqual(_quotedEscapedValueUTF16, format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual(_stringView, format.FormatStringView(remoteValue, _fallback));

            format = RemoteValueFormatProvider.Get("sub");
            Assert.AreEqual(_escapedValue, format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual(_stringView, format.FormatStringView(remoteValue, _fallback));
        }

        [Test]
        public void EncodesUTF32Strings()
        {
            var remoteValue = RemoteValueFor(_value, Encoding.UTF32, charSize: 4);

            IRemoteValueFormat format;

            format = RemoteValueFormatProvider.Get("s32");
            Assert.AreEqual(_quotedEscapedValueUTF32, format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual(_stringView, format.FormatStringView(remoteValue, _fallback));

            format = RemoteValueFormatProvider.Get("s32b");
            Assert.AreEqual(_escapedValue, format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual(_stringView, format.FormatStringView(remoteValue, _fallback));
        }

        [Test]
        public void CStringWithSize()
        {
            var remoteValue = RemoteValueFor("text", Encoding.UTF8, charSize: 1);

            IRemoteValueFormat format;

            format = RemoteValueFormatProvider.Get("2s");
            Assert.AreEqual("\"te\"", format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual("te", format.FormatStringView(remoteValue, _fallback));

            format = RemoteValueFormatProvider.Get("2sb");
            Assert.AreEqual("te", format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual("te", format.FormatStringView(remoteValue, _fallback));
        }

        [Test]
        public void UTF8StringWithSize()
        {
            var remoteValue = RemoteValueFor("text", Encoding.UTF8, charSize: 1);

            IRemoteValueFormat format;

            format = RemoteValueFormatProvider.Get("2s8");
            Assert.AreEqual("u8\"te\"", format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual("te", format.FormatStringView(remoteValue, _fallback));

            format = RemoteValueFormatProvider.Get("2s8b");
            Assert.AreEqual("te", format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual("te", format.FormatStringView(remoteValue, _fallback));
        }

        [Test]
        public void UTF16StringWithSize()
        {
            var remoteValue = RemoteValueFor("text", Encoding.Unicode, charSize: 2);

            IRemoteValueFormat format;

            format = RemoteValueFormatProvider.Get("2su");
            Assert.AreEqual("u\"te\"", format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual("te", format.FormatStringView(remoteValue, _fallback));

            format = RemoteValueFormatProvider.Get("2sub");
            Assert.AreEqual("te", format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual("te", format.FormatStringView(remoteValue, _fallback));
        }

        [Test]
        public void UTF32StringWithSize()
        {
            var remoteValue = RemoteValueFor("text", Encoding.UTF32, charSize: 4);

            IRemoteValueFormat format;

            format = RemoteValueFormatProvider.Get("2s32");
            Assert.AreEqual("U\"te\"", format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual("te", format.FormatStringView(remoteValue, _fallback));

            format = RemoteValueFormatProvider.Get("2s32b");
            Assert.AreEqual("te", format.FormatValue(remoteValue, _fallback));
            Assert.AreEqual("te", format.FormatStringView(remoteValue, _fallback));
        }

        [Test]
        public void ReturnsError()
        {
            const string errorStr = "error";

            string error = null;
            var remoteValue = Substitute.For<RemoteValue>();
            remoteValue.GetPointeeAsByteString(1, Arg.Any<uint>(), out error)
                .Returns(x =>
                {
                    x[2] = errorStr;
                    return null;
                });

            IRemoteValueFormat format = RemoteValueFormatProvider.Get("s");
            Assert.AreEqual(errorStr, format.FormatValue(remoteValue, _fallback));
            Assert.IsNull(format.FormatStringView(remoteValue, _fallback));
        }

        [Test]
        public void ShouldInheritFormatSpecifier()
        {
            Assert.That(RemoteValueFormatProvider.Get("s").ShouldInheritFormatSpecifier(), Is.True);
            Assert.That(RemoteValueFormatProvider.Get("sb").ShouldInheritFormatSpecifier(),
                        Is.True);
            Assert.That(RemoteValueFormatProvider.Get("s8").ShouldInheritFormatSpecifier(),
                        Is.True);
            Assert.That(RemoteValueFormatProvider.Get("s8b").ShouldInheritFormatSpecifier(),
                        Is.True);
            Assert.That(RemoteValueFormatProvider.Get("su").ShouldInheritFormatSpecifier(),
                        Is.True);
            Assert.That(RemoteValueFormatProvider.Get("sub").ShouldInheritFormatSpecifier(),
                        Is.True);
            Assert.That(RemoteValueFormatProvider.Get("s32").ShouldInheritFormatSpecifier(),
                        Is.True);
            Assert.That(RemoteValueFormatProvider.Get("s32b").ShouldInheritFormatSpecifier(),
                        Is.True);
        }

        RemoteValue RemoteValueFor(string s, Encoding encoding, uint charSize)
        {
            string error;
            var remoteValue = Substitute.For<RemoteValue>();
            remoteValue.GetPointeeAsByteString(charSize, Arg.Any<uint>(), out error).Returns(x => {
                x[2] = null;
                return encoding.GetBytes(s).Take((int)(uint)x[1]).ToArray();
            });
            return remoteValue;
        }
    }
}
