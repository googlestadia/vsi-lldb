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
        RemoteTarget _mockTarget;
        LogSpy _logSpy;
        IVariableInformation _mockVarInfo;
        DebugCodeContext.Factory _mockCodeContextFactory;
        IGgpDebugPropertyFactory _propertyFactory;

        [SetUp]
        public void SetUp()
        {
            _mockTarget = Substitute.For<RemoteTarget>();
            _mockVarInfo = Substitute.For<IVariableInformation>();
            _mockCodeContextFactory = Substitute.For<DebugCodeContext.Factory>();

#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            var taskExecutor = new TaskExecutor(new JoinableTaskContext().Factory);
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
            var enumFactory = new VariableInformationEnum.Factory(taskExecutor);

            var childrenProviderFactory = new ChildrenProvider.Factory();
            _propertyFactory = new DebugAsyncProperty.Factory(
                enumFactory, childrenProviderFactory, _mockCodeContextFactory,
                new VsExpressionCreator(), taskExecutor);

            childrenProviderFactory.Initialize(_propertyFactory);

            _logSpy = new LogSpy();
            _logSpy.Attach();
        }

        [TearDown]
        public void Cleanup()
        {
            _logSpy.Detach();
        }

        [Test]
        public void SetValueAsStringSuccess()
        {
            const string newValue = "newValue";
            _mockVarInfo.Assign(newValue, out string _).Returns(true);
            var debugProperty = _propertyFactory.Create(_mockTarget, _mockVarInfo);

            var result = debugProperty.SetValueAsString(newValue, 0, 0);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
        }

        [Test]
        public void SetValueAsStringFailure()
        {
            const string newValue = "newValue";
            _mockVarInfo.Assign(newValue, out string _).Returns(false);
            var debugProperty = _propertyFactory.Create(_mockTarget, _mockVarInfo);

            int result = debugProperty.SetValueAsString(newValue, 0, 0);

            Assert.That(result, Is.EqualTo(AD7Constants.E_SETVALUE_VALUE_CANNOT_BE_SET));
            Assert.That(_logSpy.GetOutput(), Does.Contain(newValue));
            Assert.That(_logSpy.GetOutput(), Does.Contain("Error"));
        }

        [Test]
        public void SetValueAsStringWithError()
        {
            const string newValue = "newValue";
            _mockVarInfo.Assign(newValue, out string _).Returns(x =>
            {
                x[1] = "Something Bad Happened";
                return false;
            });

            var debugProperty = _propertyFactory.Create(_mockTarget, _mockVarInfo);

            int result =
                debugProperty.SetValueAsStringWithError(newValue, 0, 0, out string actualError);

            Assert.That(result, Is.EqualTo(AD7Constants.E_SETVALUE_VALUE_CANNOT_BE_SET));
            Assert.That(_logSpy.GetOutput(), Does.Contain(newValue));
            Assert.That(_logSpy.GetOutput(), Does.Contain("Error"));
            Assert.That(actualError, Is.EqualTo("Something Bad Happened"));
        }

        [Test]
        public void GetMemoryContext()
        {
            const ulong expectedAddress = 0xdeadbeef;
            _mockVarInfo.GetMemoryContextAddress().Returns(expectedAddress);

            var mockCodeContext = Substitute.For<IGgpDebugCodeContext>();
            _mockCodeContextFactory
                .Create(_mockTarget, expectedAddress, null, null)
                .Returns(mockCodeContext);

            var debugProperty = _propertyFactory.Create(_mockTarget, _mockVarInfo);

            Assert.AreEqual(VSConstants.S_OK,
                            debugProperty.GetMemoryContext(out IDebugMemoryContext2 memoryContext));
            Assert.AreEqual(mockCodeContext, memoryContext);
        }

        [Test]
        public void GetMemoryContextInvalid()
        {
            const string varName = "test";
            const string varValue = "not an address";

            _mockVarInfo.DisplayName.Returns(varName);
            _mockVarInfo.ValueAsync().Returns(varValue);

            var debugProperty = _propertyFactory.Create(_mockTarget, _mockVarInfo);

            Assert.AreEqual(AD7Constants.S_GETMEMORYCONTEXT_NO_MEMORY_CONTEXT,
                            debugProperty.GetMemoryContext(out IDebugMemoryContext2 _));
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

            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.GetChildAdapterAsync().Returns(
                new ListChildAdapter.Factory().Create(children));
            _mockVarInfo.GetCachedView().Returns(_mockVarInfo);
            var property = _propertyFactory.Create(_mockTarget, _mockVarInfo);

            var guid = new Guid();
            var result = property.EnumChildren(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 10,
                                               ref guid,
                                               enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_ALL, "", 0,
                                               out IEnumDebugPropertyInfo2 propertyEnum);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));

            propertyEnum.GetCount(out uint count);
            Assert.That(count, Is.EqualTo(children.Count));
        }

        [Test]
        public void TestEnumChildrenHexadecimalDisplay()
        {
            _mockVarInfo.MightHaveChildrenAsync().Returns(false);
            var property = _propertyFactory.Create(_mockTarget, _mockVarInfo);

            var guid = new Guid();

            // For radix 10, mockVarInfo.UpdateValueFormat is called with ValueFormat.Default.
            int result = property.EnumChildren(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 10,
                                               ref guid,
                                               enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_ALL, "", 0,
                                               out IEnumDebugPropertyInfo2 _);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.AreEqual(ValueFormat.Default, _mockVarInfo.FallbackValueFormat);

            // For radix 16, mockVarInfo.UpdateValueFormat is called with ValueFormat.Hex.
            result = property.EnumChildren(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME, 16,
                                           ref guid, enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_ALL,
                                           "", 0, out IEnumDebugPropertyInfo2 _);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.AreEqual(ValueFormat.Hex, _mockVarInfo.FallbackValueFormat);
        }
    }

    [TestFixture]
    class DebugPropertyGetPropertyInfoTests
    {
        RemoteTarget _mockTarget;
        IVariableInformation _mockVarInfo;
        LogSpy _logSpy;
        IGgpDebugPropertyFactory _propertyFactory;

        // Test target.
        IDebugProperty3 _debugProperty;

        [SetUp]
        public void SetUp()
        {
            _mockTarget = Substitute.For<RemoteTarget>();
            _mockVarInfo = Substitute.For<IVariableInformation>();
            _mockVarInfo.GetCachedView().Returns(_mockVarInfo);

#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            var taskExecutor = new TaskExecutor(new JoinableTaskContext().Factory);
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
            var enumFactory = new VariableInformationEnum.Factory(taskExecutor);

            var childrenProviderFactory = new ChildrenProvider.Factory();
            _propertyFactory = new DebugAsyncProperty.Factory(
                enumFactory, childrenProviderFactory, null, new VsExpressionCreator(),
                taskExecutor);

            childrenProviderFactory.Initialize(_propertyFactory);

            _debugProperty = _propertyFactory.Create(_mockTarget, _mockVarInfo);

            _logSpy = new LogSpy();
            _logSpy.Attach();
        }

        [TearDown]
        public void Cleanup()
        {
            _logSpy.Detach();
        }

        // Helper function to exercise the test target.
        int GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS fields,
                            out DEBUG_PROPERTY_INFO propertyInfo) =>
            _debugProperty.GetPropertyInfo(fields, out propertyInfo);

        [Test]
        public void GetPropertyInfoNone()
        {
            var unexpectedCall = new Exception("Unexpected call");
            _mockVarInfo.DisplayName.Throws(unexpectedCall);
            _mockVarInfo.TypeName.Throws(unexpectedCall);
            _mockVarInfo.AssignmentValue.Throws(unexpectedCall);
            _mockVarInfo.ValueAsync().Throws(unexpectedCall);
            _mockVarInfo.Error.Throws(unexpectedCall);
            _mockVarInfo.MightHaveChildrenAsync().Throws(unexpectedCall);
            _mockVarInfo.GetChildAdapterAsync().Throws(unexpectedCall);
            _mockVarInfo.FindChildByName(Arg.Any<string>()).ThrowsForAnyArgs(unexpectedCall);
            _mockVarInfo.IsReadOnly.Throws(unexpectedCall);
            _mockVarInfo.Assign(Arg.Any<string>(), out string _).ThrowsForAnyArgs(unexpectedCall);

            int result = GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NONE,
                                         out DEBUG_PROPERTY_INFO propertyInfo);

            // Also verifies exceptions are not raised.

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
            Assert.That(propertyInfo.dwFields,
                        Is.EqualTo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NONE));
        }

        [Test]
        public void GetPropertyInfoAll()
        {
            _mockVarInfo = Substitute.For<IVariableInformation>();
            _debugProperty = _propertyFactory.Create(_mockTarget, _mockVarInfo);

            int result = GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ALL,
                                         out DEBUG_PROPERTY_INFO _);

            Assert.That(result, Is.EqualTo(VSConstants.S_OK));
        }

        [Test]
        public void GetPropertyInfoWhenFullNameExists()
        {
            string varName = "list.head->value";
            _mockVarInfo.Fullname().Returns(varName);

            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME,
                            out DEBUG_PROPERTY_INFO propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME));

            Assert.That(propertyInfo.bstrFullName, Is.EqualTo(varName));
        }

        [Test]
        public void GetPropertyInfoWhenFullNameDoesntExist()
        {
            _mockVarInfo.Fullname().Returns("");

            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME,
                            out DEBUG_PROPERTY_INFO propertyInfo);

            Assert.That(
                !propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME));
        }

        [Test]
        public void GetPropertyInfoName()
        {
            string varName = "myVar";
            _mockVarInfo.DisplayName.Returns(varName);

            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME,
                            out DEBUG_PROPERTY_INFO propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME));

            Assert.That(propertyInfo.bstrName, Is.EqualTo(varName));
        }

        [Test]
        public void GetPropertyInfoTypeName()
        {
            string varType = "myType";
            _mockVarInfo.TypeName.Returns(varType);

            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE,
                            out DEBUG_PROPERTY_INFO propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE));

            Assert.That(propertyInfo.bstrType, Is.EqualTo(varType));
        }

        [Test]
        public void GetPropertyInfoValue()
        {
            string varValue = "randomValue";
            _mockVarInfo.ValueAsync().Returns(varValue);

            // If the AUTOEXPAND flag is set, then display the value summary.
            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE));

            Assert.That(propertyInfo.bstrValue, Is.EqualTo(varValue));
        }

        [Test]
        public void GetPropertyInfoCustomStringView()
        {
            string stringViewValue = "customStringViewValue";
            _mockVarInfo.StringViewAsync().Returns(stringViewValue);

            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB,
                            out DEBUG_PROPERTY_INFO propertyInfo);
            Assert.That(propertyInfo.dwAttrib.HasFlag(
                            enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_RAW_STRING));

            Assert.That(_debugProperty.GetStringCharLength(out uint length),
                        Is.EqualTo(VSConstants.S_OK));

            Assert.That(length, Is.EqualTo(stringViewValue.Length));

            var stringViewArray = new ushort[stringViewValue.Length];
            Assert.That(
                _debugProperty.GetStringChars((uint) stringViewValue.Length, stringViewArray,
                                              out uint eltsFetched), Is.EqualTo(VSConstants.S_OK));

            Assert.That(eltsFetched, Is.EqualTo(stringViewValue.Length));
            Assert.That(stringViewArray, Is.EqualTo(FromStringToUshortArray(stringViewValue)));
        }

        [Test]
        public void GetPropertyInfoDefaultStringViewForStrings()
        {
            string stringViewValue = "randomValue";
            _mockVarInfo.StringViewAsync().Returns(stringViewValue);

            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB,
                            out DEBUG_PROPERTY_INFO propertyInfo);
            Assert.That(propertyInfo.dwAttrib.HasFlag(
                            enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_RAW_STRING));

            Assert.That(_debugProperty.GetStringCharLength(out uint length),
                        Is.EqualTo(VSConstants.S_OK));

            Assert.That(length, Is.EqualTo(stringViewValue.Length));

            var stringViewArray = new ushort[stringViewValue.Length];
            Assert.That(
                _debugProperty.GetStringChars((uint) stringViewValue.Length, stringViewArray,
                                              out uint eltsFetched), Is.EqualTo(VSConstants.S_OK));

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
            _mockVarInfo.AssignmentValue.Returns(varValue);

            // If the AUTOEXPAND flag is not set, then display the assignment value.
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE,
                            out DEBUG_PROPERTY_INFO propertyInfo);

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

            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.IsPointer.Returns(true);
            _mockVarInfo.AssignmentValue.Returns("0xDEADBEEF");
            _mockVarInfo.ValueAsync().Returns("0xDEADBEEF");
            _mockVarInfo.GetMemoryAddressAsHex().Returns("0xDEADBEEF");
            _mockVarInfo.GetChildAdapterAsync().Returns(new ListChildAdapter.Factory().Create(
                new List<IVariableInformation>() { mockVarInfoChild }));

            // Display both the pointer value and the value representation.
            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.EqualTo("0xDEADBEEF {1}"));

            // Display only the pointer.
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE, out propertyInfo);
            Assert.That(propertyInfo.bstrValue, Is.EqualTo("0xDEADBEEF"));
        }

        [Test]
        public void GetPropertyInfoRecursivePointer()
        {
            const int numChildren = 3;
            var children = new[]
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
            children[0].MightHaveChildrenAsync().Returns(false);
            children[0].GetCachedView().Returns(children[0]);
            for (int i = 1; i < numChildren; i++)
            {
                children[i].Error.Returns(false);
                children[i].AssignmentValue.Returns($"0x000{i}");
                children[i].GetMemoryAddressAsHex().Returns($"0x000{i}");
                children[i].ValueAsync().Returns($"0x000{i}");
                children[i].IsPointer.Returns(true);
                children[i].MightHaveChildrenAsync().Returns(true);
                children[i].GetChildAdapterAsync().Returns(new ListChildAdapter.Factory().Create(
                    new List<IVariableInformation>() { children[i - 1] }));

                children[i].GetCachedView().Returns(children[i]);
            }

            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.IsPointer.Returns(true);
            _mockVarInfo.AssignmentValue.Returns("0x0000");
            _mockVarInfo.GetMemoryAddressAsHex().Returns("0x0000");
            _mockVarInfo.ValueAsync().Returns("0x0000");
            _mockVarInfo.GetChildAdapterAsync().Returns(new ListChildAdapter.Factory().Create(
                new List<IVariableInformation>() { children[numChildren - 1] }));

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.EqualTo("0x0000 {0x0002 {0x0001 {0}}}"));
        }

        [Test]
        public void GetPropertyInfoRecursivePointerMoreThanEightyCharacters()
        {
            const string value = "1234567891011121314151617181920212223242526";
            const int numChildren = 3;
            var children = new[]
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
            children[0].MightHaveChildrenAsync().Returns(false);
            children[0].GetCachedView().Returns(children[0]);
            for (int i = 1; i < numChildren; i++)
            {
                children[i].Error.Returns(false);
                children[i].AssignmentValue.Returns($"{value}");
                children[i].GetMemoryAddressAsHex().Returns($"{value}");
                children[i].ValueAsync().Returns($"{value}");
                children[i].IsPointer.Returns(true);
                children[i].MightHaveChildrenAsync().Returns(true);
                children[i].GetChildAdapterAsync().Returns(new ListChildAdapter.Factory().Create(
                    new List<IVariableInformation>() { children[i - 1] }));

                children[i].GetCachedView().Returns(children[i]);
            }

            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.IsPointer.Returns(true);
            _mockVarInfo.AssignmentValue.Returns($"{value}");
            _mockVarInfo.GetMemoryAddressAsHex().Returns($"{value}");
            _mockVarInfo.ValueAsync().Returns($"{value}");
            _mockVarInfo.GetChildAdapterAsync().Returns(new ListChildAdapter.Factory().Create(
                new List<IVariableInformation>() { children[numChildren - 1] }));

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

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

            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.IsPointer.Returns(false);
            _mockVarInfo.AssignmentValue.Returns("");
            _mockVarInfo.ValueAsync().Returns("");
            _mockVarInfo.GetChildAdapterAsync().Returns(
                new ListChildAdapter.Factory().Create(children));

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

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

            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.IsPointer.Returns(false);
            _mockVarInfo.AssignmentValue.Returns("");
            _mockVarInfo.ValueAsync().Returns("");
            _mockVarInfo.GetChildAdapterAsync().Returns(
                new ListChildAdapter.Factory().Create(children));

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

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

            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.AssignmentValue.Returns("");
            _mockVarInfo.ValueAsync().Returns("");
            _mockVarInfo.GetChildAdapterAsync().Returns(
                new ListChildAdapter.Factory().Create(children));

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

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

            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.IsPointer.Returns(false);
            _mockVarInfo.AssignmentValue.Returns("");
            _mockVarInfo.GetMemoryAddressAsHex().Returns("");
            _mockVarInfo.ValueAsync().Returns("");
            _mockVarInfo.DisplayName.Returns("name");

            var adapter = Substitute.For<INatvisEntity>();
            adapter.GetChildrenAsync(0, 1)
                .Returns(new List<IVariableInformation>() { natvisChild });
            adapter.CountChildrenAsync().Returns(1);

            _mockVarInfo.GetChildAdapterAsync().Returns(adapter);

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

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

            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.IsPointer.Returns(false);
            _mockVarInfo.AssignmentValue.Returns("DisplayString");
            _mockVarInfo.ValueAsync().Returns("DisplayString");
            _mockVarInfo.DisplayName.Returns("name");

            var adapter = Substitute.For<INatvisEntity>();
            adapter.GetChildrenAsync(0, 1)
                .Returns(new List<IVariableInformation>() { natvisChild });
            adapter.CountChildrenAsync().Returns(1);

            _mockVarInfo.GetChildAdapterAsync().Returns(adapter);

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

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

            natvisVar.GetChildAdapterAsync().Returns(natvisChildAdapter);

            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.IsPointer.Returns(false);
            _mockVarInfo.AssignmentValue.Returns("");
            _mockVarInfo.ValueAsync().Returns("");
            _mockVarInfo.GetChildAdapterAsync().Returns(new ListChildAdapter.Factory().Create(
                new List<IVariableInformation>() { natvisVar }));

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

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
                .Returns(new List<IVariableInformation>() { natvisChildInfo });
            natvisChildAdapter.CountChildrenAsync().Returns(1);

            natvisVar.GetChildAdapterAsync().Returns(natvisChildAdapter);

            var nonNatvisVar = Substitute.For<IVariableInformation>();
            nonNatvisVar.DisplayName.Returns("nonNatvisName");
            nonNatvisVar.AssignmentValue.Returns("nonNatvisVal");
            nonNatvisVar.ValueAsync().Returns("nonNatvisVal");
            nonNatvisVar.GetCachedView().Returns(nonNatvisVar);

            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.IsPointer.Returns(false);
            _mockVarInfo.AssignmentValue.Returns("");
            _mockVarInfo.ValueAsync().Returns("");
            _mockVarInfo.GetChildAdapterAsync().Returns(new ListChildAdapter.Factory().Create(
                new List<IVariableInformation>() { natvisVar, nonNatvisVar }));

            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

            Assert.That(propertyInfo.bstrValue,
                        Is.EqualTo("{natvisVarName={...} nonNatvisName=nonNatvisVal}"));
        }

        [Test]
        public void GetPropertyInfoPointerPointerValueNotEqualsAssignmentValue()
        {
            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(false);
            _mockVarInfo.IsReadOnly.Returns(false);
            _mockVarInfo.IsPointer.Returns(true);
            _mockVarInfo.AssignmentValue.Returns("0xDEADBEEF");
            _mockVarInfo.GetMemoryAddressAsHex().Returns("0xDEADBEEF");
            _mockVarInfo.ValueAsync().Returns("\"foobar\"");

            // Display both the pointer value and the value representation.
            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

            Assert.That(propertyInfo.bstrValue, Is.EqualTo("0xDEADBEEF \"foobar\""));

            // Display only the pointer.
            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE, out propertyInfo);
            Assert.That(propertyInfo.bstrValue, Is.EqualTo("0xDEADBEEF"));
        }

        [Test]
        public void GetPropertyInfoPointerValueWithoutChildren()
        {
            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.IsPointer.Returns(true);
            _mockVarInfo.AssignmentValue.Returns("0xDEADBEEF");
            _mockVarInfo.GetMemoryAddressAsHex().Returns("0xDEADBEEF");
            _mockVarInfo.ValueAsync().Returns("0xDEADBEEF");
            _mockVarInfo.GetChildAdapterAsync().Returns(
                new ListChildAdapter.Factory().Create(new List<IVariableInformation>()));

            // Display both the pointer value and the value representation, but without children the
            // child value is not displayable.
            GetPropertyInfo(
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND,
                out DEBUG_PROPERTY_INFO propertyInfo);

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

            IGgpDebugPropertyFactory decoratedPropertyFactory =
                factoryDecorator.Decorate(_propertyFactory);

            IGgpDebugProperty decoratedDebugProperty =
                decoratedPropertyFactory.Create(_mockTarget, _mockVarInfo);

            decoratedDebugProperty.GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP,
                                                   out DEBUG_PROPERTY_INFO propertyInfo);

            Assert.That(
                propertyInfo.dwFields.HasFlag(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP));

            Assert.That(propertyInfo.pProperty, Is.SameAs(decoratedDebugProperty));
        }

        [Test]
        public void GetPropertyInfoAllAttrib()
        {
            _mockVarInfo.Error.Returns(true);
            _mockVarInfo.MightHaveChildrenAsync().Returns(true);
            _mockVarInfo.IsReadOnly.Returns(true);
            _mockVarInfo.StringViewAsync().Returns("randomValue");

            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB,
                            out DEBUG_PROPERTY_INFO propertyInfo);

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
            _mockVarInfo.Error.Returns(false);
            _mockVarInfo.MightHaveChildrenAsync().Returns(false);
            _mockVarInfo.IsReadOnly.Returns(false);

            GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB,
                            out DEBUG_PROPERTY_INFO propertyInfo);

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

            var debugProperty = _propertyFactory.Create(_mockTarget, varInfo);
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
            var property = _propertyFactory.Create(_mockTarget, varInfo);
            int status =
                property.GetChildPropertyProvider(
                    0, 0, 0, out IAsyncDebugPropertyInfoProvider propertyInfoProvider);
            Assert.AreEqual(VSConstants.S_OK, status);
            Assert.NotNull(propertyInfoProvider);
        }
    }

    class NoopAspect : IInterceptor
    {
        public void Intercept(IInvocation invocation) => invocation.Proceed();
    }
}