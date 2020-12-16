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
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class RemoteValueDefaultFormatTests
    {
        // Object under test.
        RemoteValueDefaultFormat format;

        [SetUp]
        public void SetUp()
        {
            format = RemoteValueDefaultFormat.DefaultFormatter;
        }

        [Test]
        public void FormatValue()
        {
            const string value = "test \t \n";
            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateSimpleString("tmp", value);

            Assert.That(format.FormatValue(remoteValue, ValueFormat.Default), Is.EqualTo(value));
        }

        [Test]
        public void FormatValueVectorRegister()
        {
            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "unsigned char __attribute__((ext_vector_type(3)))", "xmm0", "");
            remoteValue.SetValueType(ValueType.Register);
            remoteValue.SetSummary("(0x00, 0x01, 0x02)");

            Assert.That(format.FormatValue(remoteValue, ValueFormat.Default),
                Is.EqualTo("{0x00, 0x01, 0x02}"));
        }

        [Test]
        public void FormatStringView()
        {
            // Value has to be quoted, so that FormatStringView recognizes it as string.
            const string value = "\"test \\t \\n\"";
            const string unescapedValue = "test \t \n";
            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateSimpleString("tmp", value);

            Assert.That(format.FormatStringView(remoteValue, ValueFormat.Default),
                        Is.EqualTo(unescapedValue));
        }

        [Test]
        public void GetValueForAssignmentRegister()
        {
            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "customType", "var", "varValue");
            remoteValue.SetValueType(ValueType.Register);
            remoteValue.SetSummary("varSummary");

            Assert.That(format.GetValueForAssignment(remoteValue, ValueFormat.Default),
                Is.EqualTo("varSummary"));
        }

        [Test]
        public void GetValueForAssignmentNonRegister()
        {
            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "customType", "var", "varValue");
            remoteValue.SetValueType(ValueType.VariableLocal);
            remoteValue.SetSummary("varSummary");

            Assert.That(format.GetValueForAssignment(remoteValue, ValueFormat.Default),
                Is.EqualTo("varValue"));
        }

        [Test]
        public void ShouldNotInheritFormatSpecifier()
        {
            Assert.That(format.ShouldInheritFormatSpecifier(), Is.False);
        }
    }
}
