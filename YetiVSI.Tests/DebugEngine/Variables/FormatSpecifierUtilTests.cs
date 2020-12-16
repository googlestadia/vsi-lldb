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

using NSubstitute;
using NUnit.Framework;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class FormatSpecifierUtilTests
    {
        [Test]
        public void HasRawFormatSpecifier()
        {
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier(null), Is.False);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier(string.Empty), Is.False);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier("!"), Is.True);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier(" !  "), Is.True);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier("!!"), Is.True);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier(" !!  "), Is.True);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier("!x"), Is.True);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier("  !x "), Is.True);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier("!!x"), Is.True);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier("  !!x "), Is.True);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier(" 3!!x "), Is.True);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier("!!view(a) "), Is.True);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier(" !expand(a) "), Is.True);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier(" [n]!x "), Is.True);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier(" [!n]x "), Is.False);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier("x"), Is.False);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier("x!"), Is.False);
            Assert.That(FormatSpecifierUtil.HasRawFormatSpecifier("x!!"), Is.False);
        }

        [Test]
        public void RemoveRawFormatSpecifier()
        {
            Assert.That(FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix(null),
                        Is.EqualTo(string.Empty));
            Assert.That(FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix(string.Empty),
                        Is.EqualTo(string.Empty));
            Assert.That(FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix("!"),
                        Is.EqualTo(string.Empty));
            Assert.That(FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix(" !  "),
                        Is.EqualTo(string.Empty));
            Assert.That(FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix("!!"),
                        Is.EqualTo(string.Empty));
            Assert.That(FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix(" !!  "),
                        Is.EqualTo(string.Empty));
            Assert.That(FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix("!x"), Is.EqualTo("x"));
            Assert.That(FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix("  !expand(3) "),
                        Is.EqualTo("expand(3)"));
            Assert.That(FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix("!!x"), Is.EqualTo("x"));
            Assert.That(FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix("  !!view(simple) "),
                        Is.EqualTo("view(simple)"));
            Assert.That(FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix("x"), Is.EqualTo("x"));
            Assert.That(FormatSpecifierUtil.RemoveRawFormatSpecifierPrefix("x!"), Is.EqualTo("x!"));
        }

        [Test]
        public void SuppressMemoryAddress()
        {
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress(null), Is.False);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress(string.Empty), Is.False);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress("!"), Is.False);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress("!!"), Is.False);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress("na"), Is.True);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress("!na"), Is.True);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress(" !na "), Is.True);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress("!!na"), Is.True);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress(" !!na "), Is.True);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress(" 3!na "), Is.True);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress(" [3]s "), Is.True);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress("!x"), Is.False);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress(" !x "), Is.False);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress("! x "), Is.False);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress("!!x"), Is.False);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress(" !!x "), Is.False);
            Assert.That(FormatSpecifierUtil.SuppressMemoryAddress("!! x "), Is.False);
        }

        [Test]
        public void GetChildFormatSpecifier_NotInherited()
        {
            var formatMock = Substitute.For<IRemoteValueFormat>();
            formatMock.ShouldInheritFormatSpecifier().Returns(false);

            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier("spec", formatMock),
                        Is.EqualTo(string.Empty));
            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier("!spec", formatMock),
                        Is.EqualTo(string.Empty));
            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier("!!spec", formatMock),
                        Is.EqualTo("!!"));
            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier(" !!spec ", formatMock),
                        Is.EqualTo("!!"));
        }

        [Test]
        public void TrySplitSpecifier_Empty()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier("", out string size, out string raw,
                                                                out string baseSpecifier);
            Assert.That(result, Is.True);
            Assert.That(size, Is.EqualTo(""));
            Assert.That(raw, Is.EqualTo(""));
            Assert.That(baseSpecifier, Is.EqualTo(""));
        }

        [Test]
        public void TrySplitSpecifier_BaseOnly()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier(
                "x", out string size, out string raw, out string baseSpecifier);
            Assert.That(result, Is.True);
            Assert.That(size, Is.EqualTo(""));
            Assert.That(raw, Is.EqualTo(""));
            Assert.That(baseSpecifier, Is.EqualTo("x"));
        }

        [Test]
        public void TrySplitSpecifier_RawBase()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier(
                "!Hb", out string size, out string raw, out string baseSpecifier);
            Assert.That(result, Is.True);
            Assert.That(size, Is.EqualTo(""));
            Assert.That(raw, Is.EqualTo("!"));
            Assert.That(baseSpecifier, Is.EqualTo("Hb"));
        }

        [Test]
        public void TrySplitSpecifier_RawOnly()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier(
                "!", out string size, out string raw, out string baseSpecifier);
            Assert.That(result, Is.True);
            Assert.That(size, Is.EqualTo(""));
            Assert.That(raw, Is.EqualTo("!"));
            Assert.That(baseSpecifier, Is.EqualTo(""));
        }

        [Test]
        public void TrySplitSpecifier_Size()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier(
                "10", out string size, out string raw, out string baseSpecifier);
            Assert.That(result, Is.True);
            Assert.That(size, Is.EqualTo("10"));
            Assert.That(raw, Is.EqualTo(""));
            Assert.That(baseSpecifier, Is.EqualTo(""));
        }

        [Test]
        public void TrySplitSpecifier_SizeBase()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier(
                "10o", out string size, out string raw, out string baseSpecifier);
            Assert.That(result, Is.True);
            Assert.That(size, Is.EqualTo("10"));
            Assert.That(raw, Is.EqualTo(""));
            Assert.That(baseSpecifier, Is.EqualTo("o"));
        }

        [Test]
        public void TrySplitSpecifier_SizeHexBase()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier(
                "0xabx", out string size, out string raw, out string baseSpecifier);
            Assert.That(result, Is.True);
            Assert.That(size, Is.EqualTo("0xab"));
            Assert.That(raw, Is.EqualTo(""));
            Assert.That(baseSpecifier, Is.EqualTo("x"));
        }

        [Test]
        public void TrySplitSpecifier_SizeExpression()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier(
                "[expr]", out string size, out string raw, out string baseSpecifier);
            Assert.That(result, Is.True);
            Assert.That(size, Is.EqualTo("[expr]"));
            Assert.That(raw, Is.EqualTo(""));
            Assert.That(baseSpecifier, Is.EqualTo(""));
        }

        [Test]
        public void TrySplitSpecifier_SizeExpressionBracket()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier(
                "[a[i]]", out string size, out string raw, out string baseSpecifier);
            Assert.That(result, Is.True);
            Assert.That(size, Is.EqualTo("[a[i]]"));
            Assert.That(raw, Is.EqualTo(""));
            Assert.That(baseSpecifier, Is.EqualTo(""));
        }

        [Test]
        public void TrySplitSpecifier_SizeExpressionBase()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier(
                "[a[i]]x", out string size, out string raw, out string baseSpecifier);
            Assert.That(result, Is.True);
            Assert.That(size, Is.EqualTo("[a[i]]"));
            Assert.That(raw, Is.EqualTo(""));
            Assert.That(baseSpecifier, Is.EqualTo("x"));
        }

        [Test]
        public void TrySplitSpecifier_SizeExpressionWithDoubleQuotesBase()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier(
                "[f(a, \"]c\")]Xb", out string size, out string raw, out string baseSpecifier);
            Assert.That(result, Is.True);
            Assert.That(size, Is.EqualTo("[f(a, \"]c\")]"));
            Assert.That(raw, Is.EqualTo(""));
            Assert.That(baseSpecifier, Is.EqualTo("Xb"));
        }

        [Test]
        public void TrySplitSpecifier_SizeExpressionRawBase()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier(
                "[a[i]]!!x", out string size, out string raw, out string baseSpecifier);
            Assert.That(result, Is.True);
            Assert.That(size, Is.EqualTo("[a[i]]"));
            Assert.That(raw, Is.EqualTo("!!"));
            Assert.That(baseSpecifier, Is.EqualTo("x"));
        }

        [Test]
        public void TrySplitSpecifier_InvalidRaw()
        {
            bool result = FormatSpecifierUtil.TrySplitSpecifier(
                "!!!", out string size, out string raw, out string baseSpecifier);
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetChildFormatSpecifier_Inherited()
        {
            var formatMock = Substitute.For<IRemoteValueFormat>();
            formatMock.ShouldInheritFormatSpecifier().Returns(true);

            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier("spec", formatMock),
                        Is.EqualTo("spec"));
            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier("!spec", formatMock),
                        Is.EqualTo("spec"));
            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier("!!spec", formatMock),
                        Is.EqualTo("!!spec"));
            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier(" !!spec ", formatMock),
                        Is.EqualTo("!!spec"));
        }

        [Test]
        public void GetChildFormatSpecifier_Expand_NotInherited()
        {
            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier("expand(1)", null),
                        Is.EqualTo(""));
            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier("!expand(1)", null),
                        Is.EqualTo(""));
            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier("!!expand(1)", null),
                        Is.EqualTo("!!"));
        }

        [Test]
        public void GetChildFormatSpecifier_View_Inherited()
        {
            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier("view(simple)", null),
                        Is.EqualTo("view(simple)"));
            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier("!view(simple)", null),
                        Is.EqualTo("view(simple)"));
            Assert.That(FormatSpecifierUtil.GetChildFormatSpecifier("!!view(simple)", null),
                        Is.EqualTo("!!view(simple)"));
        }
    }
}
