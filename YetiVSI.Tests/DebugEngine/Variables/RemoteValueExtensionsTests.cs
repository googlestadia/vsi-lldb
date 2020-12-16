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
using System.Text;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class RemoteValueExtensionsTests
    {
        [Test]
        public void TestGetFullName_ReturnsNullForScratchVars()
        {
            var remoteValue = new RemoteValueFake("$myVar", "test123");
            Assert.That(remoteValue.GetFullName(), Is.Null);
        }

        [Test]
        public void TestGetFullName()
        {
            var remoteValue = new RemoteValueFake("myVar", "test123");
            Assert.That(remoteValue.GetFullName(), Is.EqualTo("myVar"));
        }
    }

    [TestFixture]
    class RemoteValueExtensions_GetVariableAssignExpressionTests
    {
        [Test]
        public void TestExpressionNameWhenFullnameExists()
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("myVar", 12358);
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetAddressOf(RemoteValueFakeUtil.CreateSimpleLong("$0", 0xDEADBEEF));

            Assert.That(remoteValue.GetVariableAssignExpression(), Is.EqualTo("myVar"));
        }

        [Test]
        public void TestExpressionNameWhenTypeInfoIsNull()
        {
            var remoteValue = new RemoteValueFakeWithoutExpressionPath("myVar", "12358");
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetTypeInfo(null);

            Assert.That(remoteValue.GetVariableAssignExpression(), Is.Null);
        }

        [Test]
        public void TestExpressionNameWhenRemoteValueHasError()
        {
            var remoteValue = new RemoteValueFakeWithoutExpressionPath("myVar", "12358");
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetTypeInfo(new SbTypeStub(
                "int", TypeFlags.HAS_VALUE | TypeFlags.IS_SCALAR));
            remoteValue.SetError(new SbErrorStub(false));

            Assert.That(remoteValue.GetVariableAssignExpression(), Is.Null);
        }

        [Test]
        public void TestExpressionNameWhenNoAddressOf()
        {
            var remoteValue = new RemoteValueFakeWithoutExpressionPath("myVar", "12358");
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetTypeInfo(new SbTypeStub(
                "int", TypeFlags.HAS_VALUE | TypeFlags.IS_SCALAR));
            remoteValue.SetAddressOf(null);

            Assert.That(remoteValue.GetVariableAssignExpression(), Is.Null);
        }

        [Test]
        public void TestExpressionNameForScalarType()
        {
            var remoteValue = new RemoteValueFakeWithoutExpressionPath("myVar", "12358");
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetTypeInfo(
                new SbTypeStub("CustomType", TypeFlags.HAS_VALUE | TypeFlags.IS_SCALAR));
            remoteValue.SetAddressOf(RemoteValueFakeUtil.CreateAddressOf(remoteValue, 0xDEADBEEF));

            Assert.That(remoteValue.GetVariableAssignExpression(),
                Is.EqualTo("(*((CustomType*)0xDEADBEEF))"));
        }

        [Test]
        public void TestExpressionNameForPointerType()
        {
            var remoteValue = new RemoteValueFakeWithoutExpressionPath("myVar", "0x0ddba11");
            remoteValue.SetValueType(ValueType.VariableLocal);
            remoteValue.SetTypeInfo(new SbTypeStub("CustomType*", TypeFlags.IS_POINTER));
            remoteValue.SetAddressOf(RemoteValueFakeUtil.CreateSimpleLong("$0", 0xDEADBEEF));

            Assert.That(remoteValue.GetVariableAssignExpression(),
                Is.EqualTo("((CustomType*)0x0ddba11)"));
        }

        [Test]
        public void TestExpressionNameForReferenceType()
        {
            var remoteValue = new RemoteValueFakeWithoutExpressionPath("myVar", "0x0ddba11");
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetTypeInfo(
                new SbTypeStub("CustomType&", TypeFlags.HAS_VALUE | TypeFlags.IS_REFERENCE));
            remoteValue.SetAddressOf(RemoteValueFakeUtil.CreateSimpleLong("$0", 0xDEADBEEF));

            Assert.That(remoteValue.GetVariableAssignExpression(),
                Is.EqualTo("(*((CustomType*)0x0ddba11))"));
        }

        [Test]
        public void TestExpressionNameForInvalidValueType()
        {
            var remoteValue = new RemoteValueFake("myVar", "12358");
            remoteValue.SetValueType(DebuggerApi.ValueType.Invalid);
            remoteValue.SetTypeInfo(
                new SbTypeStub("int", TypeFlags.HAS_VALUE | TypeFlags.IS_SCALAR));
            remoteValue.SetAddressOf(RemoteValueFakeUtil.CreateSimpleLong("$0", 0xDEADBEEF));

            Assert.That(remoteValue.GetVariableAssignExpression(), Is.Null);
        }

        /// <summary>
        /// Test that expressions like '&a' are not assignable during debugging.
        /// </summary>
        [Test]
        public void TestExpressionNameForReferenceOfVariable()
        {
            var remoteValue = new RemoteValueFake("$1", "0x0ddba11");
            remoteValue.SetValueType(DebuggerApi.ValueType.ConstResult);
            remoteValue.SetTypeInfo(new SbTypeStub(
                "CustomType*",
                TypeFlags.HAS_CHILDREN | TypeFlags.HAS_VALUE | TypeFlags.IS_POINTER));

            Assert.That(remoteValue.GetVariableAssignExpression(), Is.Null);
        }
    }

    [TestFixture]
    public class RemoteValueExtensions_GetMemoryAddressAssignExpressionTests
    {
        [Test]
        public void Reference()
        {
            var remoteValue = RemoteValueFakeUtil.Create("dummyType&",
                TypeFlags.IS_REFERENCE, "dummyName", "0xDEADC0DE");
            remoteValue.SetAddressOf(RemoteValueFakeUtil.CreateAddressOf(remoteValue, 0xAAAAAAAA));
            Assert.That(RemoteValueExtensions.GetMemoryAddressAssignExpression(remoteValue),
                Is.EqualTo("(*((dummyType*)0xDEADC0DE))"));
        }

        [Test]
        public void Pointer()
        {
            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreatePointer("dummyType*", "dummyName", "0xDEADC0DE");
            remoteValue.SetAddressOf(RemoteValueFakeUtil.CreateAddressOf(remoteValue, 0xDEAD0000));
            Assert.That(RemoteValueExtensions.GetMemoryAddressAssignExpression(remoteValue),
                Is.EqualTo("((dummyType*)0xDEADC0DE)"));
        }

        [Test]
        public void Array()
        {
            // We can not assign any value to array during debugging.
            var remoteValue = RemoteValueFakeUtil.CreateSimpleCharArray("dummyName",
                "dummyValue".ToCharArray());
            remoteValue.SetAddressOf(RemoteValueFakeUtil.CreateAddressOf(remoteValue, 0xDEADC0DE));
            Assert.That(RemoteValueExtensions.GetMemoryAddressAssignExpression(remoteValue),
                Is.EqualTo(null));
        }

        [Test]
        public void Stack()
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("dummyType", 123);
            remoteValue.SetAddressOf(RemoteValueFakeUtil.CreateAddressOf(remoteValue, 0xDEADC0DE));
            Assert.That(RemoteValueExtensions.GetMemoryAddressAssignExpression(remoteValue),
                Is.EqualTo("(*((int*)0xDEADC0DE))"));
        }
    }

    [TestFixture]
    public class RemoteValueExtensions_GetMemoryContextAddressTests
    {
        [Test]
        public void Reference()
        {
            var remoteValue = RemoteValueFakeUtil.Create("dummyType&",
                TypeFlags.IS_REFERENCE, "dummyName", "0xDEADC0DE");
            remoteValue.SetAddressOf(RemoteValueFakeUtil.CreateAddressOf(remoteValue, 0xAAAAAAAA));
            Assert.That(RemoteValueExtensions.GetMemoryContextAddress(remoteValue),
                        Is.EqualTo(0xDEADC0DE));
        }

        [Test]
        public void Pointer()
        {
            RemoteValue remoteValue =
                RemoteValueFakeUtil.CreatePointer("dummyType*", "dummyName", "0xDEADC0DE");
            Assert.That(RemoteValueExtensions.GetMemoryContextAddress(remoteValue),
                        Is.EqualTo(0xDEADC0DE));
        }

        [Test]
        public void Array()
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleCharArray("dummyName",
                "dummyValue".ToCharArray());
            remoteValue.SetAddressOf(RemoteValueFakeUtil.CreateAddressOf(remoteValue, 0xDEADC0DE));
            Assert.That(RemoteValueExtensions.GetMemoryContextAddress(remoteValue),
                        Is.EqualTo(0xDEADC0DE));
        }

        [Test]
        public void Stack()
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("dummyType", 123);
            remoteValue.SetAddressOf(RemoteValueFakeUtil.CreateAddressOf(remoteValue, 0xDEADC0DE));
            Assert.That(RemoteValueExtensions.GetMemoryContextAddress(remoteValue),
                        Is.EqualTo(123));
        }
    }
}
