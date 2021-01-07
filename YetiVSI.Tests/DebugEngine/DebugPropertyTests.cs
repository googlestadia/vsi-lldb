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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Castle.DynamicProxy;
using Microsoft.VisualStudio.Threading;
using NSubstitute.ExceptionExtensions;
using TestsCommon.TestSupport;
using YetiCommon.CastleAspects;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.CastleAspects;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;
using YetiVSI.Test.TestSupport.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugPropertyTests
    {
        LogSpy logSpy;
        IVariableInformation mockVarInfo;
        DebugCodeContext.Factory mockCodeContextFactory;
        CreateDebugPropertyDelegate createPropertyDelegate;

        [SetUp]
        public void SetUp()
        {
            mockVarInfo = Substitute.For<IVariableInformation>();
            mockCodeContextFactory = Substitute.For<DebugCodeContext.Factory>();

            var taskExecutor = new TaskExecutor(new JoinableTaskContext().Factory);
            var enumFactory = new VariableInformationEnum.Factory(taskExecutor);

            var childrenProviderFactory = new ChildrenProvider.Factory();
            var debugPropertyFactory = new DebugAsyncProperty.Factory(
                enumFactory, childrenProviderFactory, mockCodeContextFactory,
                new VsExpressionCreator(), taskExecutor);

            createPropertyDelegate = debugPropertyFactory.Create;

            childrenProviderFactory.Initialize(createPropertyDelegate);

            logSpy = new LogSpy();
            logSpy.Attach();
        }

        [TearDown]
        public void Cleanup()
        {
            logSpy.Detach();
        }

        [Test]
        public void SetValueAsStringSuccess()
        {
            const string newValue = "newValue";
            string error;
            mockVarInfo.Assign(newValue, out error).Returns(true);
            var debugProperty = createPropertyDelegate.Invoke(mockVarInfo);

            var result = debugProperty.SetValueAsString(newValue, 0, 0);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
        }

        [Test]
        public void SetValueAsStringFailure()
        {
            const string newValue = "newValue";
            string error;
            mockVarInfo.Assign(newValue, out error).Returns(false);
            var debugProperty = createPropertyDelegate.Invoke(mockVarInfo);

            var result = debugProperty.SetValueAsString(newValue, 0, 0);

            Assert.That(result, Is.EqualTo(AD7Constants.E_SETVALUE_VALUE_CANNOT_BE_SET));
            Assert.That(logSpy.GetOutput(), Does.Contain(newValue));
            Assert.That(logSpy.GetOutput(), Does.Contain("Error"));
        }

        [Test]
        public void SetValueAsStringWithError()
        {
            const string newValue = "newValue";
            string error;
            mockVarInfo.Assign(newValue, out error)
                .Returns(x =>
                {
                    x[1] = "Something Bad Happened";
                    return false;
                });

            var debugProperty = createPropertyDelegate.Invoke(mockVarInfo);

            string actualError;
            var result = debugProperty.SetValueAsStringWithError(newValue, 0, 0, out actualError);

            Assert.That(result, Is.EqualTo(AD7Constants.E_SETVALUE_VALUE_CANNOT_BE_SET));
            Assert.That(logSpy.GetOutput(), Does.Contain(newValue));
            Assert.That(logSpy.GetOutput(), Does.Contain("Error"));
            Assert.That(actualError, Is.EqualTo("Something Bad Happened"));
        }

        [Test]
        public void GetMemoryContext()
        {
            const string VAR_NAME = "test";
            const ulong EXPECTED_ADDRESS = 0xdeadbeef;

            mockVarInfo.DisplayName.Returns(VAR_NAME);
            mockVarInfo.GetMemoryContextAddress().Returns<ulong?>(EXPECTED_ADDRESS);

            IDebugCodeContext2 mockCodeContext = Substitute.For<IDebugCodeContext2>();
            mockCodeContextFactory.Create(EXPECTED_ADDRESS, VAR_NAME, null, Guid.Empty)
                .Returns(mockCodeContext);

            var debugProperty = createPropertyDelegate.Invoke(mockVarInfo);

            IDebugMemoryContext2 memoryContext;
            Assert.AreEqual(VSConstants.S_OK, debugProperty.GetMemoryContext(out memoryContext));
            Assert.AreEqual(mockCodeContext, memoryContext);
        }

        [Test]
        public void GetMemoryContextInvalid()
        {
            const string VAR_NAME = "test";
            const string VAR_VALUE = "not an address";

            mockVarInfo.DisplayName.Returns(VAR_NAME);
            mockVarInfo.ValueAsync().Returns(VAR_VALUE);

            var debugProperty = createPropertyDelegate.Invoke(mockVarInfo);

            IDebugMemoryContext2 memoryContext;
            Assert.AreEqual(AD7Constants.S_GETMEMORYCONTEXT_NO_MEMORY_CONTEXT,
                            debugProperty.GetMemoryContext(out memoryContext));
        }

        [Test]
        public void TestEnumChildren()
        {
            var children = new List<IVariableInformation>()
            {
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>()
            };

            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.GetChildAdapter().Returns(new ListChildAdapter.Factory().Create(children));
            mockVarInfo.GetCachedView().Returns(mockVarInfo);
            var property = createPropertyDelegate.Invoke(mockVarInfo);

            IEnumDebugPropertyInfo2 propertyEnum;
            var guid = new System.Guid();
            var result = property.EnumChildren(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 10,
                                               ref guid,
                                               enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_ALL, "", 0,
                                               out propertyEnum);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));

            uint count;
            propertyEnum.GetCount(out count);
            Assert.That(count, Is.EqualTo(children.Count));
        }

        [Test]
        public void TestEnumChildrenHexadecimalDisplay()
        {
            mockVarInfo.MightHaveChildren().Returns(false);
            var property = createPropertyDelegate.Invoke(mockVarInfo);

            IEnumDebugPropertyInfo2 propertyEnum;
            var guid = new System.Guid();

            // For radix 10, mockVarInfo.UpdateValueFormat is called with ValueFormat.Default.
            var result = property.EnumChildren(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 10,
                                               ref guid,
                                               enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_ALL, "", 0,
                                               out propertyEnum);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.AreEqual(ValueFormat.Default, mockVarInfo.FallbackValueFormat);

            // For radix 16, mockVarInfo.UpdateValueFormat is called with ValueFormat.Hex.
            result = property.EnumChildren(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 16,
                                           ref guid, enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_ALL,
                                           "", 0, out propertyEnum);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.AreEqual(ValueFormat.Hex, mockVarInfo.FallbackValueFormat);
        }
    }

    [TestFixture]
    class DebugProperty_GetPropertyInfo_Tests
    {
        IVariableInformation mockVarInfo;
        LogSpy logSpy;
        DebugAsyncProperty.Factory propertyFactory;

        CreateDebugPropertyDelegate createPropertyDelegate;

        // Test target.
        IDebugProperty3 debugProperty;

        [SetUp]
        public void SetUp()
        {
            mockVarInfo = Substitute.For<IVariableInformation>();
            mockVarInfo.GetCachedView().Returns(mockVarInfo);

            var taskExecutor = new TaskExecutor(new JoinableTaskContext().Factory);
            var enumFactory = new VariableInformationEnum.Factory(taskExecutor);

            var childrenProviderFactory = new ChildrenProvider.Factory();
            propertyFactory =
                new DebugAsyncProperty.Factory(enumFactory, childrenProviderFactory, null,
                                               new VsExpressionCreator(), taskExecutor);

            createPropertyDelegate = propertyFactory.Create;

            childrenProviderFactory.Initialize(createPropertyDelegate);

            debugProperty = propertyFactory.Create(mockVarInfo);

            logSpy = new LogSpy();
            logSpy.Attach();
        }

        [TearDown]
        public void Cleanup()
        {
            logSpy.Detach();
        }

        // Helper function to exercise the test target.
        int GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS fields, out DEBUG_PROPERTY_INFO propertyInfo)
        {
            var result = debugProperty.GetPropertyInfo(fields, out propertyInfo);
            return result;
        }

        [Test]
        public void GetPropertyInfoNone()
        {
            var unexpectedCall = new Exception("Unexpected call");
            mockVarInfo.DisplayName.Throws(unexpectedCall);
            mockVarInfo.TypeName.Throws(unexpectedCall);
            mockVarInfo.AssignmentValue.Throws(unexpectedCall);
            mockVarInfo.ValueAsync().Throws(unexpectedCall);
            mockVarInfo.Error.Throws(unexpectedCall);
            mockVarInfo.MightHaveChildren().Throws(unexpectedCall);
            mockVarInfo.GetChildAdapter().Throws(unexpectedCall);
            mockVarInfo.FindChildByName(Arg.Any<string>()).ThrowsForAnyArgs(unexpectedCall);
            mockVarInfo.IsReadOnly.Throws(unexpectedCall);
            string error;
            mockVarInfo.Assign(Arg.Any<string>(), out error).ThrowsForAnyArgs(unexpectedCall);

            DEBUG_PROPERTY_INFO propertyInfo;
            int result = GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NONE,
                                         out propertyInfo);

            // Also verifies exceptions are not raised.

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(propertyInfo.dwFields,
                        Is.EqualTo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NONE));
        }

        [Test]
        public void GetPropertyInfoAll()
        {
            mockVarInfo = Substitute.For<IVariableInformation>();
            debugProperty = createPropertyDelegate.Invoke(mockVarInfo);

            DEBUG_PROPERTY_INFO propertyInfo;
            var result = GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ALL,
                                         out propertyInfo);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
        }

        [Test]
        public void GetPropertyInfoWhenFullNameExists()
        {
            var varName = "list.head->value";
            mockVarInfo.Fullname().Returns(varName);

            DEBUG_PROPERTY_INFO propertyInfo;
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME, out propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME));

            Assert.That(propertyInfo.bstrFullName, Is.EqualTo(varName));
        }

        [Test]
        public void GetPropertyInfoWhenFullNameDoesntExist()
        {
            mockVarInfo.Fullname().Returns("");

            DEBUG_PROPERTY_INFO propertyInfo;
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME, out propertyInfo);

            Assert.That(
                !propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME));
        }

        [Test]
        public void GetPropertyInfoName()
        {
            var varName = "myVar";
            mockVarInfo.DisplayName.Returns(varName);

            DEBUG_PROPERTY_INFO propertyInfo;
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, out propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME));

            Assert.That(propertyInfo.bstrName, Is.EqualTo(varName));
        }

        [Test]
        public void GetPropertyInfoTypeName()
        {
            var varType = "myType";
            mockVarInfo.TypeName.Returns(varType);

            DEBUG_PROPERTY_INFO propertyInfo;
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE, out propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE));

            Assert.That(propertyInfo.bstrType, Is.EqualTo(varType));
        }

        [Test]
        public void GetPropertyInfoValue()
        {
            var varValue = "randomValue";
            mockVarInfo.ValueAsync().Returns(varValue);

            // If the AUTOEXPAND flag is set, then display the value summary.
            DEBUG_PROPERTY_INFO propertyInfo;
            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE));

            Assert.That(propertyInfo.bstrValue, Is.EqualTo(varValue));
        }

        [Test]
        public void GetPropertyInfoCustomStringView()
        {
            var stringViewValue = "customStringViewValue";
            mockVarInfo.StringView.Returns(stringViewValue);

            DEBUG_PROPERTY_INFO propertyInfo;
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB, out propertyInfo);
            Assert.That(propertyInfo.dwAttrib.HasFlag(
                            enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_RAW_STRING));

            uint length;
            Assert.That(debugProperty.GetStringCharLength(out length),
                        Is.EqualTo(VSConstants.S_OK));

            Assert.That(length, Is.EqualTo(stringViewValue.Length));

            var stringViewArray = new ushort[stringViewValue.Length];
            uint eltsFetched;
            Assert.That(
                debugProperty.GetStringChars((uint) stringViewValue.Length, stringViewArray,
                                             out eltsFetched), Is.EqualTo(VSConstants.S_OK));

            Assert.That(eltsFetched, Is.EqualTo(stringViewValue.Length));
            Assert.That(stringViewArray, Is.EqualTo(FromStringToUshortArray(stringViewValue)));
        }

        [Test]
        public void GetPropertyInfoDefaultStringViewForStrings()
        {
            var stringViewValue = "randomValue";
            mockVarInfo.StringView.Returns(stringViewValue);

            DEBUG_PROPERTY_INFO propertyInfo;
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB, out propertyInfo);
            Assert.That(propertyInfo.dwAttrib.HasFlag(
                            enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_RAW_STRING));

            uint length;
            Assert.That(debugProperty.GetStringCharLength(out length),
                        Is.EqualTo(VSConstants.S_OK));

            Assert.That(length, Is.EqualTo(stringViewValue.Length));

            var stringViewArray = new ushort[stringViewValue.Length];
            uint eltsFetched;
            Assert.That(
                debugProperty.GetStringChars((uint) stringViewValue.Length, stringViewArray,
                                             out eltsFetched), Is.EqualTo(VSConstants.S_OK));

            Assert.That(eltsFetched, Is.EqualTo(stringViewValue.Length));
            Assert.That(stringViewArray, Is.EqualTo(FromStringToUshortArray(stringViewValue)));
        }

        ushort[] FromStringToUshortArray(string str)
        {
            var convertedValue = new ushort[str.Length];
            for (int i = 0; i < str.Length; ++i)
            {
                convertedValue[i] = str[i];
            }

            return convertedValue;
        }

        [Test]
        public void GetPropertyInfoAssigmentValue()
        {
            var varValue = "assignmentValue";
            mockVarInfo.AssignmentValue.Returns(varValue);

            // If the AUTOEXPAND flag is not set, then display the assignment value.
            DEBUG_PROPERTY_INFO propertyInfo;
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE, out propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE));

            Assert.That(propertyInfo.bstrValue, Is.EqualTo(varValue));
        }

        [Test]
        public void GetPropertyInfoPointerValueEqualsAssignmentValue()
        {
            IVariableInformation mockVarInfoChild = Substitute.For<IVariableInformation>();
            mockVarInfoChild.AssignmentValue.Returns("1");
            mockVarInfoChild.ValueAsync().Returns("1");
            mockVarInfoChild.GetCachedView().Returns(mockVarInfoChild);

            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.IsPointer.Returns(true);
            mockVarInfo.AssignmentValue.Returns("0xDEADBEEF");
            mockVarInfo.ValueAsync().Returns("0xDEADBEEF");
            mockVarInfo.GetMemoryAddressAsHex().Returns("0xDEADBEEF");
            mockVarInfo.GetChildAdapter()
                .Returns(new ListChildAdapter.Factory().Create(new List<IVariableInformation>()
                                                                   {mockVarInfoChild}));

            DEBUG_PROPERTY_INFO propertyInfo;

            // Display both the pointer value and the value representation.
            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.EqualTo("0xDEADBEEF {1}"));

            // Display only the pointer.
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE, out propertyInfo);
            Assert.That(propertyInfo.bstrValue, Is.EqualTo("0xDEADBEEF"));
        }

        [Test]
        public void GetPropertyInfoRecursivePointer()
        {
            const int numChildren = 3;
            var children = new IVariableInformation[]
            {
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>()
            };

            children[0].Error.Returns(false);
            children[0].AssignmentValue.Returns("0");
            children[0].GetMemoryAddressAsHex().Returns("0");
            children[0].ValueAsync().Returns("0");
            children[0].IsPointer.Returns(false);
            children[0].MightHaveChildren().Returns(false);
            children[0].GetCachedView().Returns(children[0]);
            for (int i = 1; i < numChildren; i++)
            {
                children[i].Error.Returns(false);
                children[i].AssignmentValue.Returns($"0x000{i}");
                children[i].GetMemoryAddressAsHex().Returns($"0x000{i}");
                children[i].ValueAsync().Returns($"0x000{i}");
                children[i].IsPointer.Returns(true);
                children[i].MightHaveChildren().Returns(true);
                children[i]
                    .GetChildAdapter()
                    .Returns(new ListChildAdapter.Factory().Create(new List<IVariableInformation>()
                                                                       {children[i - 1]}));

                children[i].GetCachedView().Returns(children[i]);
            }

            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.IsPointer.Returns(true);
            mockVarInfo.AssignmentValue.Returns("0x0000");
            mockVarInfo.GetMemoryAddressAsHex().Returns("0x0000");
            mockVarInfo.ValueAsync().Returns("0x0000");
            mockVarInfo.GetChildAdapter()
                .Returns(new ListChildAdapter.Factory().Create(new List<IVariableInformation>()
                                                                   {children[numChildren - 1]}));

            DEBUG_PROPERTY_INFO propertyInfo;

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.EqualTo("0x0000 {0x0002 {0x0001 {0}}}"));
        }

        [Test]
        public void GetPropertyInfoRecursivePointerMoreThanEightyCharacters()
        {
            const string value = "1234567891011121314151617181920212223242526";
            const int numChildren = 3;
            var children = new IVariableInformation[]
            {
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>()
            };

            children[0].Error.Returns(false);
            children[0].AssignmentValue.Returns(value);
            children[0].GetMemoryAddressAsHex().Returns(value);
            children[0].ValueAsync().Returns(value);
            children[0].IsPointer.Returns(false);
            children[0].MightHaveChildren().Returns(false);
            children[0].GetCachedView().Returns(children[0]);
            for (int i = 1; i < numChildren; i++)
            {
                children[i].Error.Returns(false);
                children[i].AssignmentValue.Returns($"{value}");
                children[i].GetMemoryAddressAsHex().Returns($"{value}");
                children[i].ValueAsync().Returns($"{value}");
                children[i].IsPointer.Returns(true);
                children[i].MightHaveChildren().Returns(true);
                children[i]
                    .GetChildAdapter()
                    .Returns(new ListChildAdapter.Factory().Create(new List<IVariableInformation>()
                                                                       {children[i - 1]}));

                children[i].GetCachedView().Returns(children[i]);
            }

            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.IsPointer.Returns(true);
            mockVarInfo.AssignmentValue.Returns($"{value}");
            mockVarInfo.GetMemoryAddressAsHex().Returns($"{value}");
            mockVarInfo.ValueAsync().Returns($"{value}");
            mockVarInfo.GetChildAdapter()
                .Returns(new ListChildAdapter.Factory().Create(new List<IVariableInformation>()
                                                                   {children[numChildren - 1]}));

            DEBUG_PROPERTY_INFO propertyInfo;

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.EqualTo($"{value} {{{value} {{...}}}}"));
        }

        [Test]
        public void GetPropertyInfoNoValueLessThanThreeChildren()
        {
            const int numChildren = 3;
            var children = new List<IVariableInformation>
            {
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>()
            };

            for (int i = 0; i < numChildren; i++)
            {
                children[i].DisplayName.Returns($"test{i + 1}");
                children[i].AssignmentValue.Returns($"{i + 1}");
                children[i].ValueAsync().Returns($"{i + 1}");
                children[i].GetCachedView().Returns(children[i]);
            }

            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.IsPointer.Returns(false);
            mockVarInfo.AssignmentValue.Returns("");
            mockVarInfo.ValueAsync().Returns("");
            mockVarInfo.GetChildAdapter().Returns(new ListChildAdapter.Factory().Create(children));

            DEBUG_PROPERTY_INFO propertyInfo;

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.EqualTo("{test1=1 test2=2 test3=3}"));
        }

        [Test]
        public void GetPropertyInfoNoValueMoreThanThreeChildren()
        {
            const int numChildren = 4;
            var children = new List<IVariableInformation>()
            {
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>()
            };

            for (int i = 0; i < numChildren; i++)
            {
                children[i].DisplayName.Returns($"test{i + 1}");
                children[i].AssignmentValue.Returns($"{i + 1}");
                children[i].ValueAsync().Returns($"{i + 1}");
                children[i].GetCachedView().Returns(children[i]);
            }

            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.IsPointer.Returns(false);
            mockVarInfo.AssignmentValue.Returns("");
            mockVarInfo.ValueAsync().Returns("");
            mockVarInfo.GetChildAdapter().Returns(new ListChildAdapter.Factory().Create(children));

            DEBUG_PROPERTY_INFO propertyInfo;

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.EqualTo("{test1=1 test2=2 test3=3 ...}"));
        }

        [Test]
        public void GetPropertyInfoNoValueMoreThanEightyCharacters()
        {
            const string value = "1234567891011121314151617181920212223242526";
            const int numChildren = 3;
            var children = new List<IVariableInformation>()
            {
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>(),
                Substitute.For<IVariableInformation>()
            };

            for (int i = 0; i < numChildren; i++)
            {
                children[i].DisplayName.Returns($"testVariable{i + 1}");
                children[i].AssignmentValue.Returns(value);
                children[i].ValueAsync().Returns(value);
                children[i].GetCachedView().Returns(children[i]);
            }

            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.AssignmentValue.Returns("");
            mockVarInfo.ValueAsync().Returns("");
            mockVarInfo.GetChildAdapter().Returns(new ListChildAdapter.Factory().Create(children));

            DEBUG_PROPERTY_INFO propertyInfo;

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            string result = $"{{testVariable1={value} testVariable2={value} ...}}";
            Assert.That(propertyInfo.bstrValue, Is.EqualTo(result));
        }

        [Test]
        public void NoPreviewForNatvisVariablesWithoutDisplayString()
        {
            var natvisChild = Substitute.For<IVariableInformation>();
            natvisChild.DisplayName.Returns("natvisName");
            natvisChild.AssignmentValue.Returns("natvisVal");
            natvisChild.GetMemoryAddressAsHex().Returns("natvisVal");
            natvisChild.ValueAsync().Returns("natvisVal");
            natvisChild.GetCachedView().Returns(natvisChild);

            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.IsPointer.Returns(false);
            mockVarInfo.AssignmentValue.Returns("");
            mockVarInfo.GetMemoryAddressAsHex().Returns("");
            mockVarInfo.ValueAsync().Returns("");
            mockVarInfo.DisplayName.Returns("name");

            var adapter = Substitute.For<INatvisEntity>();
            adapter.GetChildrenAsync(0, 1).Returns(new List<IVariableInformation>() {natvisChild});
            adapter.CountChildrenAsync().Returns(1);

            mockVarInfo.GetChildAdapter().Returns(adapter);

            DEBUG_PROPERTY_INFO propertyInfo;

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.Empty);
        }

        [Test]
        public void PreviewShownForNatvisVariablesWithDisplayString()
        {
            var natvisChild = Substitute.For<IVariableInformation>();
            natvisChild.DisplayName.Returns("natvisName");
            natvisChild.AssignmentValue.Returns("natvisVal");
            natvisChild.ValueAsync().Returns("natvisVal");
            natvisChild.GetCachedView().Returns(natvisChild);

            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.IsPointer.Returns(false);
            mockVarInfo.AssignmentValue.Returns("DisplayString");
            mockVarInfo.ValueAsync().Returns("DisplayString");
            mockVarInfo.DisplayName.Returns("name");

            var adapter = Substitute.For<INatvisEntity>();
            adapter.GetChildrenAsync(0, 1).Returns(new List<IVariableInformation>() {natvisChild});
            adapter.CountChildrenAsync().Returns(1);

            mockVarInfo.GetChildAdapter().Returns(adapter);

            DEBUG_PROPERTY_INFO propertyInfo;

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.EqualTo("DisplayString"));
        }

        [Test]
        public void DisplayStringShownWhenVariableHasNatvisChildWithDisplayString()
        {
            var natvisVar = Substitute.For<IVariableInformation>();
            natvisVar.DisplayName.Returns("natvisVarName");
            natvisVar.AssignmentValue.Returns("DisplayString");
            natvisVar.ValueAsync().Returns("DisplayString");
            natvisVar.GetCachedView().Returns(natvisVar);

            var natvisChildInfo = Substitute.For<IVariableInformation>();
            natvisChildInfo.DisplayName.Returns("natvisChildName");
            natvisChildInfo.AssignmentValue.Returns("natvisChildVal");
            natvisChildInfo.ValueAsync().Returns("natvisChildVal");
            natvisChildInfo.GetCachedView().Returns(natvisChildInfo);

            var natvisChildAdapter = Substitute.For<INatvisEntity>();
            natvisChildAdapter.GetChildrenAsync(0, 1)
                .Returns(new List<IVariableInformation>() { natvisChildInfo });
            natvisChildAdapter.CountChildrenAsync().Returns(1);

            natvisVar.GetChildAdapter().Returns(natvisChildAdapter);

            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.IsPointer.Returns(false);
            mockVarInfo.AssignmentValue.Returns("");
            mockVarInfo.ValueAsync().Returns("");
            mockVarInfo.GetChildAdapter()
                .Returns(new ListChildAdapter.Factory().Create(new List<IVariableInformation>()
                                                                   {natvisVar}));

            DEBUG_PROPERTY_INFO propertyInfo;

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.EqualTo("{natvisVarName=DisplayString}"));
        }

        [Test]
        public void ValueShownForNonNatvisAndDotsForNatvis()
        {
            var natvisVar = Substitute.For<IVariableInformation>();
            natvisVar.DisplayName.Returns("natvisVarName");
            natvisVar.AssignmentValue.Returns("");
            natvisVar.ValueAsync().Returns("");
            natvisVar.GetCachedView().Returns(natvisVar);

            var natvisChildInfo = Substitute.For<IVariableInformation>();
            natvisChildInfo.DisplayName.Returns("natvisChildName");
            natvisChildInfo.AssignmentValue.Returns("natvisChildVal");
            natvisChildInfo.ValueAsync().Returns("natvisChildVal");
            natvisChildInfo.GetCachedView().Returns(natvisChildInfo);

            var natvisChildAdapter = Substitute.For<INatvisEntity>();
            natvisChildAdapter.GetChildrenAsync(0, 1)
                .Returns(new List<IVariableInformation>() {natvisChildInfo});
            natvisChildAdapter.CountChildrenAsync().Returns(1);

            natvisVar.GetChildAdapter().Returns(natvisChildAdapter);

            var nonNatvisVar = Substitute.For<IVariableInformation>();
            nonNatvisVar.DisplayName.Returns("nonNatvisName");
            nonNatvisVar.AssignmentValue.Returns("nonNatvisVal");
            nonNatvisVar.ValueAsync().Returns("nonNatvisVal");
            nonNatvisVar.GetCachedView().Returns(nonNatvisVar);

            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.IsPointer.Returns(false);
            mockVarInfo.AssignmentValue.Returns("");
            mockVarInfo.ValueAsync().Returns("");
            mockVarInfo.GetChildAdapter()
                .Returns(new ListChildAdapter.Factory().Create(new List<IVariableInformation>()
                                                                   {natvisVar, nonNatvisVar}));

            DEBUG_PROPERTY_INFO propertyInfo;

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            Assert.That(propertyInfo.bstrValue,
                        Is.EqualTo("{natvisVarName={...} nonNatvisName=nonNatvisVal}"));
        }

        [Test]
        public void GetPropertyInfoPointerPointerValueNotEqualsAssignmentValue()
        {
            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(false);
            mockVarInfo.IsReadOnly.Returns(false);
            mockVarInfo.IsPointer.Returns(true);
            mockVarInfo.AssignmentValue.Returns("0xDEADBEEF");
            mockVarInfo.GetMemoryAddressAsHex().Returns("0xDEADBEEF");
            mockVarInfo.ValueAsync().Returns("\"foobar\"");

            DEBUG_PROPERTY_INFO propertyInfo;

            // Display both the pointer value and the value representation.
            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.EqualTo("0xDEADBEEF \"foobar\""));

            // Display only the pointer.
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE, out propertyInfo);
            Assert.That(propertyInfo.bstrValue, Is.EqualTo("0xDEADBEEF"));
        }

        [Test]
        public void GetPropertyInfoPointerValueWithoutChildren()
        {
            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.IsPointer.Returns(true);
            mockVarInfo.AssignmentValue.Returns("0xDEADBEEF");
            mockVarInfo.GetMemoryAddressAsHex().Returns("0xDEADBEEF");
            mockVarInfo.ValueAsync().Returns("0xDEADBEEF");
            mockVarInfo.GetChildAdapter()
                .Returns(new ListChildAdapter.Factory().Create(new List<IVariableInformation>()));

            DEBUG_PROPERTY_INFO propertyInfo;

            // Display both the pointer value and the value representation, but without children the
            // child value is not displayable.
            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND, out propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.EqualTo("0xDEADBEEF {???}"));
        }

        [Test]
        public void GetPropertyInfoProp()
        {
            // Regression test for (internal). Use a decorated factory since
            // propertyInfo.pProperty should be the decorated object.
            var decoratorUtil = new DecoratorUtil(
                new ProxyGenerationOptions(new DebugEngineProxyHook()));

            // Needs at least one aspect or else decorator is not assigned to Self.
            IDecorator factoryDecorator = decoratorUtil.CreateFactoryDecorator(
                new ProxyGenerator(), new NoopAspect());

            DebugAsyncProperty.Factory decoratedPropertyFactory =
                factoryDecorator.Decorate(propertyFactory);

            IGgpAsyncDebugProperty decoratedDebugProperty =
                decoratedPropertyFactory.Create(mockVarInfo);

            DEBUG_PROPERTY_INFO propertyInfo;
            decoratedDebugProperty.GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP,
                                                   out propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP));

            Assert.That(propertyInfo.pProperty, Is.SameAs(decoratedDebugProperty));
        }

        [Test]
        public void GetPropertyInfoAllAttrib()
        {
            mockVarInfo.Error.Returns(true);
            mockVarInfo.MightHaveChildren().Returns(true);
            mockVarInfo.IsReadOnly.Returns(true);
            mockVarInfo.StringView.Returns("randomValue");

            DEBUG_PROPERTY_INFO propertyInfo;
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB, out propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB));

            Assert.That(
                propertyInfo.dwAttrib.HasFlag(enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_ERROR));

            Assert.That(
                propertyInfo.dwAttrib.HasFlag(enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_OBJ_IS_EXPANDABLE));

            Assert.That(
                propertyInfo.dwAttrib.HasFlag(enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_READONLY));

            Assert.That(
                propertyInfo.dwAttrib.HasFlag(enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_RAW_STRING));
        }

        [Test]
        public void GetPropertyInfoNoAttrib()
        {
            mockVarInfo.Error.Returns(false);
            mockVarInfo.MightHaveChildren().Returns(false);
            mockVarInfo.IsReadOnly.Returns(false);

            DEBUG_PROPERTY_INFO propertyInfo;
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB, out propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB));

            Assert.That(
                !propertyInfo.dwAttrib.HasFlag(enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_ERROR));

            Assert.That(
                !propertyInfo.dwAttrib.HasFlag(enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_OBJ_IS_EXPANDABLE));

            Assert.That(
                !propertyInfo.dwAttrib.HasFlag(enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_READONLY));

            Assert.That(
                !propertyInfo.dwAttrib.HasFlag(enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_RAW_STRING));
        }

        [Test]
        public void GetPropertyInfoHexadecimalDisplay()
        {
            // 175 is hex AF!
            int valueInt = 175;
            string valueHex = "0xaf";

            var childAdapterFactory = new RemoteValueChildAdapter.Factory();
            var remoteValue = RemoteValueFakeUtil.CreateSimpleInt("test", valueInt);
            var varInfo = new RemoteValueVariableInformation(
                null, "", RemoteValueFormat.Default, ValueFormat.Default, remoteValue, "test",
                CustomVisualizer.None, childAdapterFactory);

            var debugProperty = createPropertyDelegate.Invoke(varInfo);
            var propertyInfos = new DEBUG_PROPERTY_INFO[1];

            // Radix 16 -> Int should be formatted as hex.
            debugProperty.GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE, 16, 0,
                                          null, 0, propertyInfos);

            Assert.AreEqual(propertyInfos[0].bstrValue, valueHex);

            // Radix 10 -> Int should be formatted as decimal.
            debugProperty.GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE, 10, 0,
                                          null, 0, propertyInfos);

            Assert.AreEqual(propertyInfos[0].bstrValue, valueInt.ToString());

            // Radix 8 -> Not supported, should fall back to decimal.
            debugProperty.GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE, 8, 0,
                                          null, 0, propertyInfos);

            Assert.AreEqual(propertyInfos[0].bstrValue, valueInt.ToString());
        }

        [Test]
        public void GetChildPropertyProviderTest()
        {
            var varInfo = Substitute.For<IVariableInformation>();
            var property = (IGgpAsyncDebugProperty) createPropertyDelegate.Invoke(varInfo);
            IAsyncDebugPropertyInfoProvider propertyInfoProvider;
            int status = property.GetChildPropertyProvider(0, 0, 0, out propertyInfoProvider);
            Assert.AreEqual(VSConstants.S_OK, status);
            Assert.NotNull(propertyInfoProvider);
        }
    }

    class NoopAspect : IInterceptor
    {
        public void Intercept(IInvocation invocation) => invocation.Proceed();
    }
}