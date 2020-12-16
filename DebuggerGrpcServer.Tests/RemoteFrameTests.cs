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

using DebuggerCommonApi;
using LldbApi;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;

namespace DebuggerGrpcServer.Tests
{
    [TestFixture]
    [Timeout(5000)]
    class RemoteFrameTests
    {
        const ulong TEST_PC = 0x123456789abcdef0;
        const ulong FUNCTION_ADDRESS_MIN = 10;
        const ulong FUNCTION_ADDRESS_MAX = 20;
        const ulong SYMBOL_ADDRESS_MIN = 30;
        const ulong SYMBOL_ADDRESS_MAX = 40;

        const string NAME = "DebugStackFrameTests";
        const string ARG1_TYPE_NAME = "type_name_1";
        const string ARG1_NAME = "name_1";
        const string ARG1_VALUE = "value_1";
        const string ARG2_TYPE_NAME = "type_name_2";
        const string ARG2_NAME = "name_2";
        const string ARG2_VALUE = "value_2";
        const string ARG_THIS_VALUE = "value_this";
        const string ARG_THIS_NAME = "this";

        RemoteFrame stackFrame;
        SbFrame mockDebuggerStackFrame;
        SbTarget mockTarget;

        SbFrame CreateMockStackFrame()
        {
            mockDebuggerStackFrame = Substitute.For<SbFrame>();
            mockDebuggerStackFrame.GetPC().Returns(TEST_PC);
            mockDebuggerStackFrame.GetThread().GetProcess().GetTarget().Returns(mockTarget);
            mockDebuggerStackFrame.GetFunction().GetStartAddress().GetLoadAddress(mockTarget)
                .Returns(FUNCTION_ADDRESS_MIN);
            mockDebuggerStackFrame.GetFunction().GetEndAddress().GetLoadAddress(mockTarget)
                .Returns(FUNCTION_ADDRESS_MAX);
            mockDebuggerStackFrame.GetSymbol().GetStartAddress().GetLoadAddress(mockTarget)
                .Returns(SYMBOL_ADDRESS_MIN);
            mockDebuggerStackFrame.GetSymbol().GetEndAddress().GetLoadAddress(mockTarget)
                .Returns(SYMBOL_ADDRESS_MAX);
            mockDebuggerStackFrame.GetFunctionName().Returns(NAME);
            return mockDebuggerStackFrame;
        }

        [SetUp]
        public void SetUp()
        {
            mockTarget = Substitute.For<SbTarget>();
            mockDebuggerStackFrame = CreateMockStackFrame();

            var mockArgument1 = Substitute.For<SbValue>();
            mockArgument1.GetValue().Returns(ARG1_VALUE);

            var mockArgument2 = Substitute.For<SbValue>();
            mockArgument2.GetValue().Returns(ARG2_VALUE);

            var functionArguments = new List<SbValue>();
            functionArguments.Add(mockArgument1);
            functionArguments.Add(mockArgument2);

            mockDebuggerStackFrame.GetVariables(true, false, false, false).Returns(
                functionArguments);

            mockDebuggerStackFrame.GetFunction().GetArgumentName(0).Returns(ARG1_NAME);
            mockDebuggerStackFrame.GetFunction().GetArgumentName(1).Returns(ARG2_NAME);

            var mockArgTypeList = Substitute.For<SbTypeList>();
            mockArgTypeList.GetSize().Returns(2u);
            mockArgTypeList.GetTypeAtIndex(0u).GetName().Returns(ARG1_TYPE_NAME);
            mockArgTypeList.GetTypeAtIndex(1u).GetName().Returns(ARG2_TYPE_NAME);

            var mockFunctionType = Substitute.For<SbType>();
            mockFunctionType.GetFunctionArgumentTypes().Returns(mockArgTypeList);

            mockDebuggerStackFrame.GetFunction().GetType().Returns(mockFunctionType);

            var optionsFactory = Substitute.For<ILldbExpressionOptionsFactory>();
            var valueFactory = new RemoteValueImpl.Factory(optionsFactory);
            stackFrame = new RemoteFrameImpl.Factory(valueFactory, optionsFactory)
                .Create(mockDebuggerStackFrame);
        }

        [Test]
        public void GetPhysicalStackRangePrioritizesFunctionAddressRange()
        {
            var result = stackFrame.GetPhysicalStackRange();
            Assert.NotNull(result);
            Assert.AreEqual(FUNCTION_ADDRESS_MIN, result.addressMin);
            Assert.AreEqual(FUNCTION_ADDRESS_MAX, result.addressMax);
        }

        [Test]
        public void GetPhysicalStackRangeChecksSymbolAddressRange()
        {
            mockDebuggerStackFrame.GetFunction().GetEndAddress().Returns((SbAddress)null);
            mockDebuggerStackFrame.GetFunction().GetStartAddress().Returns((SbAddress)null);
            var result = stackFrame.GetPhysicalStackRange();
            Assert.NotNull(result);
            Assert.AreEqual(SYMBOL_ADDRESS_MIN, result.addressMin);
            Assert.AreEqual(SYMBOL_ADDRESS_MAX, result.addressMax);
        }

        [Test]
        public void GetPhysicalStackRangeDefaultsToPC()
        {
            mockDebuggerStackFrame.GetFunction().GetStartAddress().Returns((SbAddress)null);
            mockDebuggerStackFrame.GetFunction().GetEndAddress().Returns((SbAddress)null);
            mockDebuggerStackFrame.GetSymbol().GetStartAddress().Returns((SbAddress)null);
            mockDebuggerStackFrame.GetSymbol().GetEndAddress().Returns((SbAddress)null);
            var result = stackFrame.GetPhysicalStackRange();
            Assert.NotNull(result);
            Assert.AreEqual(TEST_PC, result.addressMin);
            Assert.AreEqual(TEST_PC, result.addressMax);
        }

        [Test]
        public void GetPhysicalStackRangeFail()
        {
            mockDebuggerStackFrame.GetFunction().GetStartAddress().Returns((SbAddress)null);
            mockDebuggerStackFrame.GetFunction().GetEndAddress().Returns((SbAddress)null);
            mockDebuggerStackFrame.GetSymbol().GetStartAddress().Returns((SbAddress)null);
            mockDebuggerStackFrame.GetSymbol().GetEndAddress().Returns((SbAddress)null);
            mockDebuggerStackFrame.GetPC().Returns(ulong.MaxValue);
            var result = stackFrame.GetPhysicalStackRange();
            Assert.Null(result);
        }

        [Test]
        public void GetPhysicalStackRangeFailWithNullTarget()
        {
            mockDebuggerStackFrame.GetThread().GetProcess().GetTarget().Returns((SbTarget)null);
            mockDebuggerStackFrame.GetPC().Returns(ulong.MaxValue);
            var result = stackFrame.GetPhysicalStackRange();
            Assert.Null(result);
        }

        [Test]
        public void GetInfoEmpty()
        {
            var info = stackFrame.GetInfo(0);
            Assert.AreEqual((FrameInfoFlags)0, info.ValidFields);
        }

        [Test]
        public void GetInfoFunction()
        {
            var fields = FrameInfoFlags.FIF_FUNCNAME;
            var info = stackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(NAME, info.FuncName);
        }

        [Test]
        public void GetInfoFunctionModule()
        {
            const string moduleName = "module name";
            var module = Substitute.For<SbModule>();
            module.GetPlatformFileSpec().GetFilename().Returns(moduleName);
            mockDebuggerStackFrame.GetModule().Returns(module);

            var fields = FrameInfoFlags.FIF_FUNCNAME |
                FrameInfoFlags.FIF_FUNCNAME_MODULE;
            var info = stackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(moduleName + "!" + NAME, info.FuncName);
        }

        [Test]
        public void GetInfoModuleName()
        {
            const string moduleName = "module_name";
            var module = Substitute.For<SbModule>();
            module.GetPlatformFileSpec().GetFilename().Returns(moduleName);
            mockDebuggerStackFrame.GetModule().Returns(module);

            FrameInfo<SbModule> info = stackFrame.GetInfo(FrameInfoFlags.FIF_MODULE);
            Assert.That(info.ValidFields & FrameInfoFlags.FIF_MODULE,
                        Is.EqualTo(FrameInfoFlags.FIF_MODULE));
            Assert.That(info.ModuleName, Is.EqualTo(moduleName));
        }

        [Test]
        public void GetInfoFunctionLine()
        {
            uint lineNum = 17;
            var lineEntry = Substitute.For<SbLineEntry>();
            lineEntry.GetLine().Returns(lineNum);
            mockDebuggerStackFrame.GetLineEntry().Returns(lineEntry);

            var fields = FrameInfoFlags.FIF_FUNCNAME |
                FrameInfoFlags.FIF_FUNCNAME_LINES;
            var info = stackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(NAME + " Line " + lineNum, info.FuncName);
        }

        [Test]
        public void GetInfoFunctionGlobalScopeResolution([Values("::", "::::")] string prefix)
        {
            var name = prefix + NAME;
            mockDebuggerStackFrame.GetFunctionName().Returns(name);

            var fields = FrameInfoFlags.FIF_FUNCNAME;
            var info = stackFrame.GetInfo(fields);

            Assert.IsTrue(info.ValidFields.HasFlag(FrameInfoFlags.FIF_FUNCNAME));
            Assert.AreEqual(NAME, info.FuncName);
        }

        [Test]
        public void GetInfoTypes()
        {
            var fields = FrameInfoFlags.FIF_FUNCNAME |
                FrameInfoFlags.FIF_FUNCNAME_ARGS |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_TYPES;
            var info = stackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(string.Format("{0}({1}, {2})", NAME, ARG1_TYPE_NAME, ARG2_TYPE_NAME),
                info.FuncName);
        }

        [Test]
        public void GetInfoTypesAndNames()
        {
            var fields = FrameInfoFlags.FIF_FUNCNAME |
                FrameInfoFlags.FIF_FUNCNAME_ARGS |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_TYPES |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_NAMES;
            var info = stackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(
                string.Format("{0}({1} {2}, {3} {4})", NAME, ARG1_TYPE_NAME,
                    ARG1_NAME, ARG2_TYPE_NAME, ARG2_NAME),
                info.FuncName);
        }

        [Test]
        public void GetInfoTypesAndValues()
        {
            var fields = FrameInfoFlags.FIF_FUNCNAME |
                FrameInfoFlags.FIF_FUNCNAME_ARGS |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_TYPES |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_VALUES;
            var info = stackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(
                string.Format("{0}({1} = {2}, {3} = {4})", NAME, ARG1_TYPE_NAME,
                    ARG1_VALUE, ARG2_TYPE_NAME, ARG2_VALUE),
                info.FuncName);
        }

        [Test]
        public void GetInfoTypesAndNamesAndValues()
        {
            var fields = FrameInfoFlags.FIF_FUNCNAME |
                FrameInfoFlags.FIF_FUNCNAME_ARGS |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_TYPES |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_NAMES |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_VALUES;
            var info = stackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(
                string.Format("{0}({1} {2} = {3}, {4} {5} = {6})", NAME, ARG1_TYPE_NAME,
                    ARG1_NAME, ARG1_VALUE, ARG2_TYPE_NAME, ARG2_NAME, ARG2_VALUE),
                info.FuncName);
        }

        [Test]
        public void GetInfoNames()
        {
            var fields = FrameInfoFlags.FIF_FUNCNAME |
                FrameInfoFlags.FIF_FUNCNAME_ARGS |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_NAMES;
            var info = stackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(string.Format("{0}({1}, {2})", NAME, ARG1_NAME, ARG2_NAME),
                info.FuncName);
        }

        [Test]
        public void GetInfoNamesAndValues()
        {
            var fields = FrameInfoFlags.FIF_FUNCNAME |
                FrameInfoFlags.FIF_FUNCNAME_ARGS |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_NAMES |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_VALUES;
            var info = stackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(
                string.Format("{0}({1} = {2}, {3} = {4})", NAME, ARG1_NAME,
                    ARG1_VALUE, ARG2_NAME, ARG2_VALUE),
                info.FuncName);
        }

        [Test]
        public void GetInfoValues()
        {
            var fields = FrameInfoFlags.FIF_FUNCNAME |
                FrameInfoFlags.FIF_FUNCNAME_ARGS |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_VALUES;
            var info = stackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(string.Format("{0}({1}, {2})", NAME, ARG1_VALUE, ARG2_VALUE),
                info.FuncName);
        }

        RemoteFrame CreateMethodStackFrame()
        {
            var mockDebuggerMethodStackFrame = CreateMockStackFrame();

            var mockArgument0 = Substitute.For<SbValue>();
            mockArgument0.GetValue().Returns(ARG_THIS_VALUE);

            var mockArgument1 = Substitute.For<SbValue>();
            mockArgument1.GetValue().Returns(ARG1_VALUE);

            var mockArgument2 = Substitute.For<SbValue>();
            mockArgument2.GetValue().Returns(ARG2_VALUE);

            var functionArguments = new List<SbValue>();
            functionArguments.Add(mockArgument0);
            functionArguments.Add(mockArgument1);
            functionArguments.Add(mockArgument2);

            mockDebuggerMethodStackFrame.GetVariables(true, false, false, false).Returns(
                functionArguments);

            mockDebuggerMethodStackFrame.GetFunction().GetArgumentName(0).Returns(ARG_THIS_NAME);
            mockDebuggerMethodStackFrame.GetFunction().GetArgumentName(1).Returns(ARG1_NAME);
            mockDebuggerMethodStackFrame.GetFunction().GetArgumentName(2).Returns(ARG2_NAME);

            var mockArgTypeList = Substitute.For<SbTypeList>();
            mockArgTypeList.GetSize().Returns(2u);
            mockArgTypeList.GetTypeAtIndex(0u).GetName().Returns(ARG1_TYPE_NAME);
            mockArgTypeList.GetTypeAtIndex(1u).GetName().Returns(ARG2_TYPE_NAME);

            var mockFunctionType = Substitute.For<SbType>();
            mockFunctionType.GetFunctionArgumentTypes().Returns(mockArgTypeList);

            mockDebuggerMethodStackFrame.GetFunction().GetType().Returns(mockFunctionType);

            var optionsFactory = Substitute.For<ILldbExpressionOptionsFactory>();
            return new RemoteFrameImpl.Factory(
                new RemoteValueImpl.Factory(optionsFactory), optionsFactory)
                    .Create(mockDebuggerMethodStackFrame);
        }

        [Test]
        public void GetInfoTypesForMethod()
        {
            RemoteFrame methodStackFrame = CreateMethodStackFrame();

            var fields = FrameInfoFlags.FIF_FUNCNAME |
                FrameInfoFlags.FIF_FUNCNAME_ARGS |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_TYPES;
            var info = methodStackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(string.Format("{0}({1}, {2})", NAME, ARG1_TYPE_NAME, ARG2_TYPE_NAME),
                info.FuncName);
        }

        [Test]
        public void GetInfoTypesAndNamesAndValuesForMethod()
        {
            RemoteFrame methodStackFrame = CreateMethodStackFrame();

            var fields = FrameInfoFlags.FIF_FUNCNAME |
                FrameInfoFlags.FIF_FUNCNAME_ARGS |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_TYPES |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_NAMES |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_VALUES;
            var info = methodStackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(
                string.Format("{0}({1} {2} = {3}, {4} {5} = {6})", NAME, ARG1_TYPE_NAME,
                    ARG1_NAME, ARG1_VALUE, ARG2_TYPE_NAME, ARG2_NAME, ARG2_VALUE),
                info.FuncName);
        }

        [Test]
        public void GetInfoTypesAndNamesWithMissingValues()
        {
            mockDebuggerStackFrame.GetVariables(true, false, false, false).Returns(
                new List<SbValue>());
            var fields = FrameInfoFlags.FIF_FUNCNAME |
                FrameInfoFlags.FIF_FUNCNAME_ARGS |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_TYPES |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_NAMES |
                FrameInfoFlags.FIF_FUNCNAME_ARGS_VALUES;
            var info = stackFrame.GetInfo(fields);
            Assert.AreEqual(FrameInfoFlags.FIF_FUNCNAME,
                info.ValidFields & FrameInfoFlags.FIF_FUNCNAME);
            Assert.AreEqual(
                string.Format("{0}({1} {2}, {3} {4})", NAME, ARG1_TYPE_NAME,
                    ARG1_NAME, ARG2_TYPE_NAME, ARG2_NAME),
                info.FuncName);
        }
    }
}