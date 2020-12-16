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
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Globalization;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class RemoteValueLLDBVectorOfFloatFormatTests
    {
        RemoteValueLLDBVectorOfFloatFormat vectorFormat32;
        RemoteValueLLDBVectorOfFloatFormat vectorFormat64;
        RemoteValue mockValue;

        [SetUp]
        public void SetUp()
        {
            vectorFormat32 = new RemoteValueLLDBVectorOfFloatFormat(ValueFormat.VectorOfFloat32);
            vectorFormat64 = new RemoteValueLLDBVectorOfFloatFormat(ValueFormat.VectorOfFloat64);
            mockValue = Substitute.For<RemoteValue>();
        }

        [Test]
        public void FormatValueUnexpectedValue()
        {
            mockValue.GetDisplayValue(ValueFormat.VectorOfFloat32).Returns("0x00");

            Assert.AreEqual("0x00", vectorFormat32.FormatValue(mockValue, ValueFormat.Default));
        }

        [Test]
        public void FormatValueEmptyValue()
        {
            mockValue.GetDisplayValue(ValueFormat.VectorOfFloat32).Returns("");

            Assert.AreEqual("", vectorFormat32.FormatValue(mockValue, ValueFormat.Default));
        }

        [Test]
        public void FormatValueVector32([Values(" ", ", ")] string separator)
        {
            mockValue.GetDisplayValue(ValueFormat.VectorOfFloat32)
                .Returns($"(0{separator}1.24557444555557e35)");

            Assert.AreEqual("{0.00000E0, 1.24557E35}",
                vectorFormat32.FormatValue(mockValue, ValueFormat.Default));
        }

        [Test]
        public void FormatValueVector64([Values(" ", ", ")] string separator)
        {
            mockValue.GetDisplayValue(ValueFormat.VectorOfFloat64)
                .Returns($"(0{separator}1.24557444555557e35)");

            Assert.AreEqual("{0.00000000000000E0, 1.24557444555557E35}",
                vectorFormat64.FormatValue(mockValue, ValueFormat.Default));
        }

        [Test]
        public void FormatValueWhenCultureUsesCommaAsDecimalSeparator()
        {
            var cultureClone = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            cultureClone.NumberFormat.NumberDecimalSeparator = ",";
            CultureInfo.CurrentCulture = cultureClone;

            // LLDB always uses a '.' regardless of the locale.
            mockValue.GetDisplayValue(ValueFormat.VectorOfFloat32)
                .Returns("(0, 1.24557444555557e35)");

            // We should use a '.' despite local machine settings.
            Assert.AreEqual("{0.00000E0, 1.24557E35}",
                vectorFormat32.FormatValue(mockValue, ValueFormat.Default));
        }

        [Test]
        public void FormatExpressionForAssignmentInvalid()
        {
            var expression = "invalid";

            Assert.AreEqual(expression,
                vectorFormat32.FormatExpressionForAssignment(mockValue, expression));
        }

        [Test]
        public void FormatExpressionForAssignmentVector32()
        {
            var expression = "{1.41421E-20, 0.00000E0, -3.14159E5, 2.71828E10}";

            Assert.AreEqual(
                "{0x80, 0x91, 0x85, 0x1e, 0x00, 0x00, 0x00, 0x00, " +
                "0xe0, 0x65, 0x99, 0xc8, 0x14, 0x87, 0xca, 0x50}",
                vectorFormat32.FormatExpressionForAssignment(mockValue, expression));
        }

        [Test]
        public void FormatExpressionForAssignmentVector64()
        {
            var expression =
                "{1.41421356237309E-9, 0.00000000000000E0, " +
                "-3.14159265358979E0, 2.71828182845904E100}";

            Assert.AreEqual(
                "{0x71, 0xa9, 0x0a, 0xeb, 0xc6, 0x4b, 0x18, 0x3e, " +
                "0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, " +
                "0x11, 0x2d, 0x44, 0x54, 0xfb, 0x21, 0x09, 0xc0, " +
                "0x19, 0x68, 0x47, 0xd1, 0x0e, 0xdb, 0xc8, 0x54}",
                vectorFormat64.FormatExpressionForAssignment(mockValue, expression));
        }

        [Test]
        public void ShouldInheritFormatSpecifier()
        {
            Assert.That(vectorFormat32.ShouldInheritFormatSpecifier(), Is.True);
            Assert.That(vectorFormat64.ShouldInheritFormatSpecifier(), Is.True);
        }
    }
}
