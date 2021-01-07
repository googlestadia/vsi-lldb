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

using DebuggerApi;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestsCommon.TestSupport;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;

namespace YetiVSI.Test.DebugEngine.Variables
{
    class RemoteValueFakeWithoutExpressionPath : RemoteValueFake
    {
        public RemoteValueFakeWithoutExpressionPath(string name, string value)
            : base(name, value) { }

        public override bool GetExpressionPath(out string path)
        {
            path = null;
            return false;
        }
    }

    [TestFixture]
    class RemoteValueVariableInformationTests
    {
        private RemoteValueChildAdapter.Factory childAdapterFactory;
        private VarInfoBuilder varInfoBuilder;
        private LogSpy logSpy;

        [SetUp]
        public void SetUp()
        {
            childAdapterFactory = new RemoteValueChildAdapter.Factory();
            var varInfoFactory = new LLDBVariableInformationFactory(childAdapterFactory);
            varInfoBuilder = new VarInfoBuilder(varInfoFactory);
            varInfoFactory.SetVarInfoBuilder(varInfoBuilder);

            logSpy = new LogSpy();
            logSpy.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            logSpy.Detach();
        }

        RemoteValueVariableInformation CreateVarInfo(RemoteValue remoteValue, string displayName)
        {
            return new RemoteValueVariableInformation(varInfoBuilder, "", RemoteValueFormat.Default,
                                                      ValueFormat.Default, remoteValue, displayName,
                                                      CustomVisualizer.None, childAdapterFactory);
        }

        [Test]
        public void GetAllInheritedTypesWithNoBaseClass()
        {
            var remoteValue= new RemoteValueFake("dummyVar", "dummyValue");
            remoteValue.SetTypeInfo(new SbTypeStub("MyType", TypeFlags.NONE));
            var varInfo = CreateVarInfo(remoteValue, "dummyDisplayName");

            var inheritedTypes = varInfo.GetAllInheritedTypes();

            Assert.That(inheritedTypes, Is.EqualTo(new[] { "MyType" }));
        }

        [Test]
        public void GetAllInheritedTypesWithOneBaseClass()
        {
            var baseType = new SbTypeStub("BaseType", TypeFlags.NONE);
            var derivedType = new SbTypeStub("DerivedType", TypeFlags.NONE);
            derivedType.AddDirectBaseClass(baseType);

            var derivedVar = new RemoteValueFake("derivedVar", "derivedValue");
            derivedVar.SetTypeInfo(derivedType);

            var varInfo = CreateVarInfo(derivedVar, "dummyDisplayName");

            var inheritedTypes = varInfo.GetAllInheritedTypes();

            Assert.That(inheritedTypes, Is.EqualTo(new[] { "DerivedType", "BaseType" }));
        }

        [Test]
        public void GetAllInheritedTypesWhenTypeInfoIsNull()
        {
            var remoteValue = new RemoteValueFake("dummyVar", "dummyValue");
            remoteValue.SetTypeInfo(null);
            var varInfo = CreateVarInfo(remoteValue, "dummyDisplayName");

            var inheritedTypes = varInfo.GetAllInheritedTypes();
            Assert.That(inheritedTypes, Is.Empty);
        }

        [Test]
        public void GetAllInheritedTypesListCloserBaseClassesFirst()
        {
            // Class hierarchy:
            //
            // 111    211    121   221
            //   \    /        \   /
            //     011          021
            //       \         /
            //           001
            var type_001 = new SbTypeStub("type_001", TypeFlags.NONE);

            var type_011 = new SbTypeStub("type_011", TypeFlags.NONE);
            var type_021 = new SbTypeStub("type_021", TypeFlags.NONE);

            var type_111 = new SbTypeStub("type_111", TypeFlags.NONE);
            var type_211 = new SbTypeStub("type_211", TypeFlags.NONE);

            var type_121 = new SbTypeStub("type_121", TypeFlags.NONE);
            var type_221 = new SbTypeStub("type_221", TypeFlags.NONE);

            type_001.AddDirectBaseClass(type_011);
            type_001.AddDirectBaseClass(type_021);

            type_011.AddDirectBaseClass(type_111);
            type_011.AddDirectBaseClass(type_211);

            type_021.AddDirectBaseClass(type_121);
            type_021.AddDirectBaseClass(type_221);

            var remoteValue = new RemoteValueFake("leafVar", "leafValue");
            remoteValue.SetTypeInfo(type_001);

            var varInfo = CreateVarInfo(remoteValue, "dummyDisplayName");
            var inheritedTypes = varInfo.GetAllInheritedTypes().ToList();

            Func<string, int> indexOf = typeName => inheritedTypes.IndexOf(typeName);

            Assert.That(inheritedTypes.Count, Is.EqualTo(7));

            Assert.That(indexOf("type_111"), Is.GreaterThan(indexOf("type_011")));
            Assert.That(indexOf("type_111"), Is.GreaterThan(indexOf("type_021")));
            Assert.That(indexOf("type_111"), Is.GreaterThan(indexOf("type_001")));

            Assert.That(indexOf("type_211"), Is.GreaterThan(indexOf("type_011")));
            Assert.That(indexOf("type_211"), Is.GreaterThan(indexOf("type_021")));
            Assert.That(indexOf("type_211"), Is.GreaterThan(indexOf("type_001")));

            Assert.That(indexOf("type_121"), Is.GreaterThan(indexOf("type_011")));
            Assert.That(indexOf("type_121"), Is.GreaterThan(indexOf("type_021")));
            Assert.That(indexOf("type_121"), Is.GreaterThan(indexOf("type_001")));

            Assert.That(indexOf("type_221"), Is.GreaterThan(indexOf("type_011")));
            Assert.That(indexOf("type_221"), Is.GreaterThan(indexOf("type_021")));
            Assert.That(indexOf("type_221"), Is.GreaterThan(indexOf("type_001")));

            Assert.That(indexOf("type_011"), Is.GreaterThan(indexOf("type_001")));

            Assert.That(indexOf("type_021"), Is.GreaterThan(indexOf("type_001")));
        }

        [Test]
        public void ValueForEmptySummary()
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("x", 1);
            remoteValue.SetSummary("");

            var varInfo = CreateVarInfo(remoteValue, "x");

            Assert.That(varInfo.Value, Is.EqualTo("1"));
        }

        [Test]
        public void ValueForNonEmptySummary()
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("x", 17);
            remoteValue.SetSummary("Summary For X");

            var varInfo = CreateVarInfo(remoteValue, "x");

            Assert.That(varInfo.Value, Is.EqualTo("Summary For X"));
        }

        [Test]
        public async Task GetChildrenIsRobustToMissingChildrenAsync()
        {
            var remoteValue = Substitute.For<RemoteValue>();
            remoteValue.GetTypeName().Returns("CustomType");

            string expressionPath;
            remoteValue.GetExpressionPath(out expressionPath).Returns(outArgs =>
            {
                outArgs[0] = "displayName";
                return true;
            });

            remoteValue.GetNumChildren().Returns(1u);
            remoteValue.GetChildren(0, 1).Returns(
                new System.Collections.Generic.List<RemoteValue>() {null});

            RemoteValueVariableInformation varInfo = CreateVarInfo(remoteValue, "");
            IVariableInformation[] children = await varInfo.GetAllChildrenAsync();

            Assert.That(children, Is.Empty);
            Assert.That(logSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(logSpy.GetOutput(), Does.Contain("0")); // Index
            Assert.That(logSpy.GetOutput(), Does.Contain("displayName"));
            Assert.That(logSpy.GetOutput(), Does.Contain("CustomType"));
            Assert.That(logSpy.GetOutput(), Does.Contain("1")); // num children
        }

        [Test]
        public async Task GetNumChildrenReturnsProperCountAsync()
        {
            var remoteValue = Substitute.For<RemoteValue>();
            remoteValue.GetTypeName().Returns("CustomType");
            remoteValue.GetNumChildren().Returns(42u);

            RemoteValueVariableInformation varInfo = CreateVarInfo(remoteValue, "");

            Assert.That(await varInfo.GetChildAdapter().CountChildrenAsync(), Is.EqualTo(42u));
            Assert.That(logSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(logSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task GetNumChildrenReturnsValueFromFormatterAsync()
        {
            var remoteValue = Substitute.For<RemoteValue>();
            remoteValue.GetTypeName().Returns("CustomType");
            remoteValue.GetNumChildren().Returns(42u);

            var format = Substitute.For<IRemoteValueFormat>();
            format.GetNumChildren(remoteValue).Returns(5u);

            var varInfo = new RemoteValueVariableInformation(
                varInfoBuilder, "5", RemoteValueFormatProvider.Get("5"), ValueFormat.Default,
                remoteValue, "displayName", CustomVisualizer.None, childAdapterFactory);

            Assert.That(await varInfo.GetChildAdapter().CountChildrenAsync(), Is.EqualTo(5u));
            Assert.That(logSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(logSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void IsNullPointerReturnsFalseIfIsNotPointer()
        {
            var remoteValue = RemoteValueFakeUtil.CreateClass("MyType", "myVar", "0x0");
            var varInfo = CreateVarInfo(remoteValue, "myVar");
            Assert.That(varInfo.IsNullPointer(), Is.False);
        }

        [Test]
        public void IsNullPointerReturnsFalseIfHexValueIsNotZero(
            [Values("0x0000032234", "0x00F0A32234")] string address)
        {
            var remoteValue = RemoteValueFakeUtil.CreatePointer("MyType", "myType", address);
            var varInfo = CreateVarInfo(remoteValue, "myType");
            Assert.That(varInfo.IsNullPointer(), Is.False);
        }

        [Test]
        public void IsNullPointerReturnsTrueIfHexValueIsZero(
            [Values("0X0000000000", "0x0000000000", "000000000")] string address)
        {
            var remoteValue = RemoteValueFakeUtil.CreatePointer("MyType", "myType", address);
            var varInfo = CreateVarInfo(remoteValue, "myType");
            Assert.That(varInfo.IsNullPointer(), Is.True);
        }

        [Test]
        public async Task CreateValueFromExpressionAsyncReturnsNullIfValueIsNullAsync()
        {
            var remoteValue = Substitute.For<RemoteValue>();
            remoteValue
                .CreateValueFromExpressionAsync("someName", "someExpression")
                .Returns(Task.FromResult<RemoteValue>(null));

            RemoteValueVariableInformation remoteVarInfo =
                CreateVarInfo(remoteValue, "remoteValue");

            IVariableInformation varInfo = await remoteVarInfo.CreateValueFromExpressionAsync(
                "someName", new VsExpression("someExpression", FormatSpecifier.EMPTY));
        }

        [Test]
        public async Task CreateValueFromExpressionAsyncSuccessWhenRemoteValueNotNullAsync()
        {
            const string outValueName = "newName";
            const string format = "format";
            var outRemoteValue = Substitute.For<RemoteValue>();
            outRemoteValue.GetName()
                .Returns(outValueName);
            var remoteValue = Substitute.For<RemoteValue>();
            remoteValue
                .CreateValueFromExpressionAsync("someName", "someExpression")
                .Returns(Task.FromResult(outRemoteValue));

            RemoteValueVariableInformation remoteVarInfo =
                CreateVarInfo(remoteValue, "remoteValue");

            IVariableInformation varInfo = await remoteVarInfo.CreateValueFromExpressionAsync(
                "someName", new VsExpression("someExpression", new FormatSpecifier(format)));

            Assert.AreEqual(outValueName, varInfo.DisplayName);
            Assert.AreEqual(format, varInfo.FormatSpecifier);
        }

        [Test]
        public async Task GetValueForExpressionPathSuccessAsync()
        {
            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateSimpleArray(
                "test", "int", RemoteValueFakeUtil.CreateSimpleInt, 4, 7, 74, 47);
            var varInfo = CreateVarInfo(remoteValue, "remoteValue");

            var result =
                varInfo.GetValueForExpressionPath(new VsExpression("[0]", FormatSpecifier.EMPTY));
            Assert.That(result.Error, Is.False);
            Assert.That(await result.ValueAsync(), Is.EqualTo("4"));
            Assert.That(result.DisplayName, Is.EqualTo("[0]"));
        }

        [Test]
        public void GetValueForExpressionPathFailure()
        {
            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateSimpleArray(
                "test", "int", RemoteValueFakeUtil.CreateSimpleInt, 4);
            var varInfo = CreateVarInfo(remoteValue, "remoteValue");

            var result =
                varInfo.GetValueForExpressionPath(new VsExpression("[1]", FormatSpecifier.EMPTY));
            Assert.That(result.Error, Is.True);
        }

        [Test]
        public void ErrorWhenSbValueHasError()
        {
            var remoteValue = RemoteValueFakeUtil.CreateError("Oh no!");

            var varInfo = CreateVarInfo(remoteValue, "remoteValue");

            Assert.That(varInfo.Error, Is.True);
            Assert.That(varInfo.ErrorMessage, Is.EqualTo("Oh no!"));
        }

        [Test]
        public void NoErrorWhenSbValueHasNoError()
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("test", 22);

            var varInfo = CreateVarInfo(remoteValue, "remoteValue");

            Assert.That(varInfo.Error, Is.False);
            Assert.That(varInfo.ErrorMessage, Is.EqualTo(""));
        }

        [Test]
        public void StringViewUnescapesStringsAndRemovesBraces()
        {
            string unescapedString = "somes\\tring\"view";
            var remoteValue = RemoteValueFakeUtil.CreateSimpleString(
                "x", CStringEscapeHelper.Escape(unescapedString));

            var varInfo = CreateVarInfo(remoteValue, "x");
            Assert.That(varInfo.StringView, Is.EqualTo(unescapedString));
        }
    }

    [TestFixture]
    class RemoteValueVariableInformation_AssignValue_Tests
    {
        LLDBVariableInformationFactory varInfoFactory;

        [SetUp]
        public void SetUp()
        {
            var defaultRemoteValueFormat = RemoteValueFormat.Default;
            var childAdapterFactory = new RemoteValueChildAdapter.Factory();
            varInfoFactory = new LLDBVariableInformationFactory(childAdapterFactory);
            var varInfoBuilder = new VarInfoBuilder(varInfoFactory);
            varInfoFactory.SetVarInfoBuilder(varInfoBuilder);
        }

        [Test]
        public void ReadOnlyCannotBeAssignedTo()
        {
            var remoteValue = Substitute.For<RemoteValue>();
            remoteValue.AddressOf().Returns((RemoteValue)null);
            var varInfo = varInfoFactory.Create(remoteValue, "myVar");

            string dummy;
            remoteValue.GetExpressionPath(out dummy).Returns(false);
            Assert.That(varInfo.IsReadOnly, Is.True);

            string actualError;
            bool assignResult = varInfo.Assign("newValue", out actualError);

            Assert.That(assignResult, Is.False);
            Assert.That(actualError, Does.Contain("myVar"));
        }

        [Test]
        public void AssignSuccess()
        {
            var newValue = RemoteValueFakeUtil.CreateClass("DummyType", "myVar", "newValue");
            newValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            newValue.SetError(new SbErrorStub(true));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "DummyType", "myVar", "originalValue");
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetAddressOf(null);

            remoteValue.AddValueFromExpression("myVar = (newValue)", newValue);

            var varInfo = varInfoFactory.Create(remoteValue, "myVar");

            Assert.That(varInfo.IsReadOnly, Is.False);

            string actualError;
            bool assignResult = varInfo.Assign("newValue", out actualError);

            Assert.That(assignResult, Is.True);
            Assert.That(actualError, Is.Null);
        }

        [Test]
        public void AssignedFailed()
        {
            var newValue = RemoteValueFakeUtil.CreateClass("DummyType", "myVar", "newValue");
            newValue.SetError(new SbErrorStub(false, "Something Bad Happened"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "DummyType", "myVar", "originalValue");
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetAddressOf(null);

            remoteValue.AddValueFromExpression("myVar = (newValue)", newValue);

            var varInfo = varInfoFactory.Create(remoteValue, "myVar");

            Assert.That(varInfo.IsReadOnly, Is.False);

            string actualError;
            bool assignResult = varInfo.Assign("newValue", out actualError);

            Assert.That(assignResult, Is.False);
            Assert.That(actualError, Is.EqualTo("Something Bad Happened"));
        }
    }

    [TestFixture]
    class RemoteValueVariableInformation_ReadOnly_Tests
    {
        RemoteValueChildAdapter.Factory childAdapterFactory;
        VarInfoBuilder varInfoBuilder;

        [SetUp]
        public void SetUp()
        {
            childAdapterFactory = new RemoteValueChildAdapter.Factory();
            var varInfoFactory = new LLDBVariableInformationFactory(childAdapterFactory);
            varInfoBuilder = new VarInfoBuilder(varInfoFactory);
            varInfoFactory.SetVarInfoBuilder(varInfoBuilder);
        }

        public RemoteValueVariableInformation CreateVarInfo(
            RemoteValue remoteValue, string displayName)
        {
            return new RemoteValueVariableInformation(varInfoBuilder, "", RemoteValueFormat.Default,
                                                      ValueFormat.Default, remoteValue, displayName,
                                                      CustomVisualizer.None, childAdapterFactory);
        }

        [Test]
        public void ValueWithAFullnameIsEditable()
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("myVar", 17);
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetAddressOf(null);
            var varInfo = CreateVarInfo(remoteValue, null);

            Assert.That(varInfo.IsReadOnly, Is.False);
        }

        [Test]
        public void ValueWithAnEmptyFullnameIsReadonly()
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("", 17);
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetAddressOf(null);
            var varInfo = CreateVarInfo(remoteValue, null);

            Assert.That(varInfo.IsReadOnly, Is.True);
        }

        [Test]
        public void ValueWithAScratchVariableFullnameIsReadonly()
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("$3", 17);
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetAddressOf(null);
            var varInfo = CreateVarInfo(remoteValue, null);

            Assert.That(varInfo.IsReadOnly, Is.True);
        }

        [Test]
        public void ReferenceValueIsEditable()
        {
            var remoteValue = new RemoteValueFakeWithoutExpressionPath("myVar", "0xDEADBEEF");
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetAddressOf(null);
            remoteValue.SetTypeInfo(new SbTypeStub("int", TypeFlags.IS_REFERENCE));
            var varInfo = CreateVarInfo(remoteValue, null);

            Assert.That(varInfo.IsReadOnly, Is.False);
        }

        [Test]
        public void SimpleArrayValueIsNotEditable()
        {
            var remoteValue = new RemoteValueFakeWithoutExpressionPath("myVar", "0xDEADBEEF");
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetAddressOf(RemoteValueFakeUtil.Create(
                "int (*)[23]",
                TypeFlags.HAS_CHILDREN | TypeFlags.HAS_VALUE | TypeFlags.IS_POINTER,
                "", "0xCAB005E"));
            remoteValue.SetTypeInfo(new SbTypeStub(
                "int[23]",
                TypeFlags.HAS_CHILDREN | TypeFlags.IS_ARRAY));
            var varInfo = CreateVarInfo(remoteValue, null);

            Assert.That(varInfo.IsReadOnly, Is.True);
        }

        [Test]
        public void ValueWithAnAddressIsEditable()
        {
            var remoteValue = new RemoteValueFakeWithoutExpressionPath("myVar", "0xDEADBEEF");
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetAddressOf(RemoteValueFakeUtil.Create("int*",
                TypeFlags.IS_INTEGER | TypeFlags.IS_POINTER, "", "0xCAB005E"));
            remoteValue.SetTypeInfo(new SbTypeStub("int[23]", TypeFlags.NONE));
            var varInfo = CreateVarInfo(remoteValue, null);

            Assert.That(varInfo.IsReadOnly, Is.False);
        }

        [Test]
        public void ValueWitNullAddressOfIsReadOnly()
        {
            var remoteValue = new RemoteValueFakeWithoutExpressionPath("myVar", "12358");
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetAddressOf(null);
            remoteValue.SetTypeInfo(new SbTypeStub("CustomType", TypeFlags.NONE));
            var varInfo = varInfoBuilder.Create(remoteValue);

            Assert.That(varInfo.IsReadOnly, Is.True);
        }

        [Test]
        public void ValueWithAnErrorAddressOfIsReadOnly()
        {
            var sbAddress = RemoteValueFakeUtil.Create("", TypeFlags.NONE, "$10", "");
            sbAddress.SetError(new SbErrorStub(false));

            var remoteValue = new RemoteValueFakeWithoutExpressionPath("myVar", "0xDEADBEEF");
            remoteValue.SetValueType(DebuggerApi.ValueType.VariableLocal);
            remoteValue.SetAddressOf(sbAddress);
            remoteValue.SetTypeInfo(new SbTypeStub("int*", TypeFlags.NONE));

            var varInfo = varInfoBuilder.Create(remoteValue);

            Assert.That(varInfo.IsReadOnly, Is.True);
        }
    }

    [TestFixture]
    class RemoteValueVariableInformation_IsTruthyTests
    {
        // We can't use the real min/max doubles for our tests because when we transform doubles
        // into a string rounding is done that makes the min/max values not fit in a double type
        // so we can't parse them as doubles later on. For the purposes of our tests, we just want
        // a couple values at opposite ends of the spectrum.
        const double TestMaxDouble = float.MaxValue * 2;
        const double TestMinDouble = float.MinValue * 2;

        VarInfoBuilder varInfoBuilder;

        [SetUp]
        public void SetUp()
        {
            var childAdapterFactory = new RemoteValueChildAdapter.Factory();
            var varInfoFactory = new LLDBVariableInformationFactory(childAdapterFactory);
            varInfoBuilder = new VarInfoBuilder(varInfoFactory);
            varInfoFactory.SetVarInfoBuilder(varInfoBuilder);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BoolValues(bool value)
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleBool("myVar", value);
            var varInfo = varInfoBuilder.Create(remoteValue);

            Assert.That(varInfo.IsTruthy, Is.EqualTo(value));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CanonicalBoolTypeValues(bool value)
        {
            var remoteValue = new RemoteValueFake("myVar", value ? "true" : "false");
            var boolType = new SbTypeStub("MyBoolType", TypeFlags.IS_TYPEDEF);
            boolType.SetCanonicalType(new SbTypeStub("bool", TypeFlags.IS_SCALAR));
            remoteValue.SetTypeInfo(boolType);
            var varInfo = varInfoBuilder.Create(remoteValue);

            Assert.That(varInfo.IsTruthy, Is.EqualTo(value));
        }

        [TestCase("0x000330", true)]
        [TestCase("0x000000", false)]
        public void PointerValues(string address, bool isTruthy)
        {
            var remoteValue = RemoteValueFakeUtil.CreatePointer("MyType*", "myVar", address);
            var varInfo = varInfoBuilder.Create(remoteValue);

            Assert.That(varInfo.IsTruthy, Is.EqualTo(isTruthy));
        }

        [TestCase(int.MinValue, true)]
        [TestCase(int.MaxValue, true)]
        [TestCase(0, false)]
        public void IntValues(int value, bool isTruthy)
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("myVar", value);
            var varInfo = varInfoBuilder.Create(remoteValue);

            Assert.That(varInfo.IsTruthy, Is.EqualTo(isTruthy));
        }

        [TestCase(long.MinValue, true)]
        [TestCase(long.MaxValue, true)]
        [TestCase(0, false)]
        public void LongValues(long value, bool isTruthy)
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleLong("myVar", value);
            var varInfo = varInfoBuilder.Create(remoteValue);

            Assert.That(varInfo.IsTruthy, Is.EqualTo(isTruthy));
        }

        [TestCase(float.MinValue, true)]
        [TestCase(0.203f, true)]
        [TestCase(float.MaxValue, true)]
        [TestCase(0, false)]
        public void FloatValues(float value, bool isTruthy)
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleFloat("myVar", value);
            var varInfo = varInfoBuilder.Create(remoteValue);

            Assert.That(varInfo.IsTruthy, Is.EqualTo(isTruthy));
        }

        [TestCase(TestMinDouble, true)]
        [TestCase(292.0202, true)]
        [TestCase(TestMaxDouble, true)]
        [TestCase(0, false)]
        public void DoubleValues(double value, bool isTruthy)
        {
            var remoteValue = RemoteValueFakeUtil.CreateSimpleDouble("myVar", value);
            var varInfo = varInfoBuilder.Create(remoteValue);

            Assert.That(varInfo.IsTruthy, Is.EqualTo(isTruthy));
        }
    }
}
