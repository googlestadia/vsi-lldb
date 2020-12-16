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
    class CStringEscapeHelperTests
    {
        #region Escape

        [Test]
        public void EscapesString()
        {
            string str = "\a\big\fool\named\ryo\thanks\vladimyr\\\"";
            string escapedStr = "\"\\a\\big\\fool\\named\\ryo\\thanks\\vladimyr\\\\\\\"\"";
            Assert.That(CStringEscapeHelper.Escape(str), Is.EqualTo(escapedStr));
        }

        [Test]
        public void EscapesStringWithDifferentPrefixPostfix()
        {
            string prefix = "Lam";
            string str = "pe\nsch";
            string postfix = "irm";
            string escapedStr = $"{prefix}pe\\nsch{postfix}";
            Assert.That(CStringEscapeHelper.Escape(str, prefix, postfix), Is.EqualTo(escapedStr));
        }

        [Test]
        public void EscapeNullString()
        {
            Assert.That(CStringEscapeHelper.Escape(null), Is.Null);
        }

        [Test]
        public void EscapeEmptyString()
        {
            Assert.That(CStringEscapeHelper.Escape(""), Is.EqualTo("\"\""));
        }

        #endregion

        #region Unescape

        [Test]
        public void UnescapeString()
        {
            string str = "\a\big\fool\named\ryo\thanks\vladimyr\\\"";
            string escapedStr = "\"\\a\\big\\fool\\named\\ryo\\thanks\\vladimyr\\\\\\\"\"";
            Assert.That(CStringEscapeHelper.Unescape(escapedStr), Is.EqualTo(str));
        }

        [Test]
        public void UnescapeNullString()
        {
            Assert.That(CStringEscapeHelper.Unescape(null), Is.Null);
        }

        [Test]
        public void UnescapeEmptyString()
        {
            Assert.That(CStringEscapeHelper.Unescape(""), Is.Null);
        }

        [Test]
        public void UnescapeStringWithMissingQuotes()
        {
            Assert.That(CStringEscapeHelper.Unescape("str\\n"), Is.Null);
        }

        [Test]
        public void UnescapeStringWithOnlyOneQuote()
        {
            Assert.That(CStringEscapeHelper.Unescape("\""), Is.Null);
        }

        [Test]
        public void UnescapeStringWithTextAndOnlyOneQuote()
        {
            Assert.That(CStringEscapeHelper.Unescape("\"str\\n"), Is.Null);
            Assert.That(CStringEscapeHelper.Unescape("str\\n\""), Is.Null);
        }

        [Test]
        public void UnescapeBackslashAtEnd()
        {
            Assert.That(CStringEscapeHelper.Unescape("\"str\\\""), Is.EqualTo("str\\"));
        }

        [Test]
        public void UnescapeStringWithBadEscapeSequence()
        {
            Assert.That(CStringEscapeHelper.Unescape("\"\\g\""), Is.EqualTo("\\g"));
        }

        [Test]
        public void UnescapeCpp11StringLiterals()
        {
            string str = "foo";
            Assert.That(CStringEscapeHelper.Unescape("L\"" + str + "\""), Is.EqualTo(str));
            Assert.That(CStringEscapeHelper.Unescape("u\"" + str + "\""), Is.EqualTo(str));
            Assert.That(CStringEscapeHelper.Unescape("u8\"" + str + "\""), Is.EqualTo(str));
            Assert.That(CStringEscapeHelper.Unescape("U\"" + str + "\""), Is.EqualTo(str));
        }

        #endregion
    }
}
