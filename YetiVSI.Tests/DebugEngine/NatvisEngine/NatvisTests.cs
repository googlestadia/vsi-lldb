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

using Castle.DynamicProxy;
using DebuggerApi;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestsCommon.TestSupport;
using TestsCommon.TestSupport.CastleAspects;
using YetiVSI.DebugEngine.NatvisEngine;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Test.TestSupport;
using YetiVSI.Test.TestSupport.NUnitExtensions;
using Does = YetiVSI.Test.TestSupport.NUnitExtensions.Does;
using YetiVSI.DebuggerOptions;
using Microsoft.VisualStudio.Threading;
using YetiVSI.Test.MediumTestsSupport;

namespace YetiVSI.Test.DebugEngine.NatvisEngine
{
    #region Test Matcher

    [TestFixture]
    public class NatvisTextMatcherTests
    {
        [Test]
        public void IsExpressionTest()
        {
            Assert.True(NatvisTextMatcher.IsExpressionPath(".myVar"));
            Assert.True(NatvisTextMatcher.IsExpressionPath("->myVar"));
            Assert.True(NatvisTextMatcher.IsExpressionPath("[17]"));
            Assert.True(NatvisTextMatcher.IsExpressionPath("[17][23]"));
            Assert.True(NatvisTextMatcher.IsExpressionPath(".list[17]"));
            Assert.True(NatvisTextMatcher.IsExpressionPath(".list[0]->box.points[0]"));

            Assert.False(NatvisTextMatcher.IsExpressionPath("myVar"));
            Assert.False(NatvisTextMatcher.IsExpressionPath("."));
            Assert.False(NatvisTextMatcher.IsExpressionPath("->"));
            Assert.False(NatvisTextMatcher.IsExpressionPath("[index]"));
            Assert.False(NatvisTextMatcher.IsExpressionPath(" .myVar"));
            Assert.False(NatvisTextMatcher.IsExpressionPath(".myVar "));
            Assert.False(NatvisTextMatcher.IsExpressionPath(".myVar + 1"));
            Assert.False(NatvisTextMatcher.IsExpressionPath(".17myVar"));
        }
    }

    #endregion

    [TestFixture(ExpressionEvaluationEngineFlag.LLDB)]
    [TestFixture(ExpressionEvaluationEngineFlag.LLDB_EVAL)]
    [TestFixture(ExpressionEvaluationEngineFlag.LLDB_EVAL_WITH_FALLBACK)]
    [TestFixture]
    public class NatvisTests
    {
        #region Test Class Setup

        readonly ExpressionEvaluationEngineFlag _expressionEvaluationEngineFlag;
        MediumTestDebugEngineFactoryCompRoot compRoot;

        private NLogSpy nLogSpy;
        private LogSpy traceLogSpy;

        TestDoubleProxyHelper testDoubleHelper;

        private NatvisExpander _natvisExpander;

        private RemoteValueFake FALSE_REMOTE_VALUE;
        private RemoteValueFake TRUE_REMOTE_VALUE;

        const string MEM_ADDRESS1 = "0x0000000002260771";
        const string MEM_ADDRESS2 = "0x0000000002260772";
        const string MEM_ADDRESS3 = "0x0000000002260773";
        const string MEM_ADDRESS4 = "0x0000000002260774";
        const string MEM_ADDRESS_NULL = "0x0000000000000000";

        public NatvisTests(ExpressionEvaluationEngineFlag expressionEvaluationEngineFlag)
        {
            _expressionEvaluationEngineFlag = expressionEvaluationEngineFlag;
        }

        // Helper function to load natvis content from a string.  Verifies there were no errors.
        private void LoadFromString(string xml)
        {
            _natvisExpander.VisualizerScanner.LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [SetUp]
        public void SetUp()
        {
            traceLogSpy = new LogSpy();
            traceLogSpy.Attach();

            testDoubleHelper = new TestDoubleProxyHelper(new ProxyGenerator());

            compRoot = new MediumTestDebugEngineFactoryCompRoot(new JoinableTaskContext());

            _natvisExpander = compRoot.GetNatvis();
            compRoot.GetVsiService().DebuggerOptions[DebuggerOption.NATVIS_EXPERIMENTAL] =
                DebuggerOptionState.ENABLED;
            ((OptionPageGrid)compRoot.GetVsiService().Options).ExpressionEvaluationEngine =
                _expressionEvaluationEngineFlag;

            nLogSpy = compRoot.GetNatvisDiagnosticLogSpy();
            nLogSpy.Attach();

            FALSE_REMOTE_VALUE = RemoteValueFakeUtil.CreateSimpleBool("", false);
            FALSE_REMOTE_VALUE.SetError(new SbErrorStub(true));

            TRUE_REMOTE_VALUE = RemoteValueFakeUtil.CreateSimpleBool("", true);
            TRUE_REMOTE_VALUE.SetError(new SbErrorStub(true));
        }

        [TearDown]
        public void TearDown()
        {
            traceLogSpy.Detach();
            nLogSpy.Detach();
        }

        #endregion

        #region Misc: Empty, Version, LocalizedStrings, UiVisualizer, HResult, Intrinsic

        [Test]
        public async Task EmptyTypeDefinitionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");

            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("actualValue"));
            Assert.That(varInfo.MightHaveChildren, Is.EqualTo(false));
            Assert.That(varInfo.StringView, Is.EqualTo(""));
        }

        [Test]
        public async Task VersionTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Version Name=""Windows.UI.Xaml.dll"" Min=""1.0"" Max=""1.5""/>
  <Type Name=""CustomType"">
    <DisplayString>expectedValue</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<Version>"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "CustomType", "varName", "originalValue");

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("expectedValue"));
        }

        [Test]
        public async Task LocalizedStringTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <LocalizedStrings>
    <LocalizedString Id=""12358"">DummyValue</LocalizedString>
  </LocalizedStrings>
  <Type Name=""CustomType"">
    <DisplayString>expectedValue</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<LocalizedStrings>"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "CustomType", "varName", "originalValue");

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("expectedValue"));
        }

        [Test]
        public async Task UIVisualizerTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <UIVisualizer ServiceId=""{AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE}"" Id=""7"" MenuName="""" Description=""List Visualizer""/>
  <Type Name=""CustomType"">
    <DisplayString>expectedValue</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<UIVisualizer>"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "CustomType", "varName", "originalValue");

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("expectedValue"));
        }

        [Test]
        public async Task HResultTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <HResult Name=""Dummy Name"">
    <AlternativeHResult Name=""Dummy Name""/>
    <HRValue>0xABC0123</HRValue>
    <HRDescription>No elements in the collection.</HRDescription>
  </HResult>
  <Type Name=""CustomType"">
    <DisplayString>expectedValue</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<HResult>"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "CustomType", "varName", "originalValue");

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("expectedValue"));
        }

        [Test]
        public async Task IntrinsicTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Intrinsic Id=""12358"" LanguageId=""DummyLanguageId"" Name=""DummyName"" ReturnType=""DummyReturnType"" SourceId=""DummySourceId"">
  </Intrinsic>
  <Type Name=""CustomType"">
    <DisplayString>expectedValue</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<Intrinsic>"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "CustomType", "varName", "originalValue");

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("expectedValue"));
        }

        [Test]
        public async Task MatchTemplateArgumentsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""Tuple&lt;*&gt;"">
    <DisplayString>*</DisplayString>
  </Type>
  <Type Name=""Tuple&lt;*,*&gt;"">
    <DisplayString>*,*</DisplayString>
  </Type>
  <Type Name=""Tuple&lt;int&gt;"">
    <DisplayString>int</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var t1 = CreateVarInfo(RemoteValueFakeUtil.CreateClass("Tuple<int>", "t1", "1"));
            var t2 = CreateVarInfo(RemoteValueFakeUtil.CreateClass("Tuple<short>", "t2", "2"));
            var t3 =
                CreateVarInfo(RemoteValueFakeUtil.CreateClass("Tuple<int, short>", "t3", "value"));
            var t4 = CreateVarInfo(
                RemoteValueFakeUtil.CreateClass("Tuple<int, int, int>", "t4", "value"));

            Assert.That(await t1.ValueAsync(), Is.EqualTo("int"));
            Assert.That(await t2.ValueAsync(), Is.EqualTo("*"));
            Assert.That(await t3.ValueAsync(), Is.EqualTo("*,*"));
            Assert.That(await t4.ValueAsync(), Is.EqualTo("*,*"));
        }

#endregion

#region AlternativeType

        [Test]
        public async Task AlternativeTypeWithSimpleTypesAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""std::vector"">
    <AlternativeType Name=""boost::vector""/>
    <DisplayString>ExpectedValue</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var stdValue = RemoteValueFakeUtil.CreateClass(
                "boost::vector", "varName", "originalValue");
            var varInfo = CreateVarInfo(stdValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("ExpectedValue"));
        }

        [Test]
        public async Task AlternativeTypeWithTemplateTypesAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""std::tuple&lt;*,*,*&gt;"">
    <AlternativeType Name=""boost::tuple&lt;*,*,*&gt;""/>
    <DisplayString>ExpectedValue</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var stdValue = RemoteValueFakeUtil.CreateClass(
                "boost::tuple<int, char, long>", "varName",
                "originalValue");
            var varInfo = CreateVarInfo(stdValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("ExpectedValue"));
        }

        [Test]
        public async Task AlternativeTypeWithMixedTypesAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""SimpleType"">
    <AlternativeType Name=""TemplateType&lt;*,*,*&gt;""/>
    <DisplayString>ExpectedValue</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var stdValue = RemoteValueFakeUtil.CreateClass(
                "TemplateType<int, char, long>", "varName",
                "originalValue");
            var varInfo = CreateVarInfo(stdValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("ExpectedValue"));
        }

        [Test]
        public void AlternativeTypeWithPriorityAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <AlternativeType Name=""SimilarType"" Priority=""MediumLow""/>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("Priority"));
        }

        [Test]
        public void AlternativeTypeWithInheritableAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <AlternativeType Name=""SimilarType"" Inheritable=""false""/>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("Inheritable"));
        }

        #endregion

        #region MostDerivedType

        [Test]
        public void TypeMostDerivedType()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <MostDerivedType>expression</MostDerivedType>
    <DisplayString>ExpectedValue</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("MostDerivedType"));
        }

        #endregion

        #region CustomVisualizer

        [Test]
        public async Task TypeCustomVisualizerTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomType"">
    <CustomVisualizer VisualizerId=""AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE"" Optional=""true"" Condition=""true"" IncludeView="" ExcludeView=""/>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<CustomVisualizer>"));

            var childValue = RemoteValueFakeUtil.CreateClass("string", "childName", "a");

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "CustomType", "varName", "originalValue");
            remoteValue.AddChild(childValue);

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("originalValue"));

            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("childName"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
        }

        #endregion

        #region SmartPointer

        [Test]
        public async Task SmartPointerTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""SmartPointerType"">
    <SmartPointer>pointeeInstance</SmartPointer>
    <DisplayString>smartPointerDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake pointeeInstance =
                RemoteValueFakeUtil.CreateClass("string", "smartPointerType", "Z");
            pointeeInstance.AddChild(RemoteValueFakeUtil.CreateClass("string", "someVar_0", "a"));
            pointeeInstance.AddChild(RemoteValueFakeUtil.CreateClass("string", "someVar_1", "b"));

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "SmartPointerType", "smartPointerName", "smartPointerValue");
            remoteValue.AddValueFromExpression("pointeeInstance", pointeeInstance);

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("smartPointerDisplayString"));

            Assert.That(varInfo.MightHaveChildren());
            IVariableInformation[] children = await varInfo.GetAllChildrenAsync();
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("someVar_0"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("someVar_1"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
            // Note: There's no RawView for SmartPointer.

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task SmartPointerTypeIsNotShownWhenExpandTypeIsDeclaredAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""SmartPointerType"">
    <SmartPointer>ignored</SmartPointer>
    <Expand HideRawView=""true"">
      <Item>""MyItemValue""</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "SmartPointerType", "smartPointerName", "smartPointerValue");
            ;
            remoteValue.AddStringLiteral("MyItemValue");

            IVariableInformation varInfo = CreateVarInfo(remoteValue);

            Assert.That(varInfo.MightHaveChildren());
            IVariableInformation[] children = await varInfo.GetAllChildrenAsync();
            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("\"MyItemValue\""));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task SmartPointerTypeFallsBackToPointeeDisplayStringAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""SmartPointerType"">
    <SmartPointer>pointeeInstance</SmartPointer>
    <DisplayString Condition=""false"">smartPointerDisplayString</DisplayString>
  </Type>
  <Type Name=""PointeeType"">
    <DisplayString>pointeeDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "SmartPointerType", "smartPointerName", "smartPointerValue");
            remoteValue.AddValueFromExpression("pointeeInstance",
                                               RemoteValueFakeUtil.CreateClass(
                                                   "PointeeType", "pointeeInstance",
                                                   "pointeeValue"));

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("pointeeDisplayString"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task SmartPointerTypeFallsBackToRawIfOptionalAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""SmartPointerType"">
    <SmartPointer Optional=""true"">invalidExpression</SmartPointer>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "SmartPointerType", "smartPointerName", "smartPointerValue");
            remoteValue.AddChild(
                RemoteValueFakeUtil.CreateSimpleString("rawChildName", "rawChildValue"));

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo.MightHaveChildren());
            IVariableInformation[] children = await varInfo.GetAllChildrenAsync();
            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("rawChildName"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("rawChildValue"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void SmartPointerTypeWithIncludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""SmartPointerType"">
    <SmartPointer IncludeView=""MyIncludeView"">pointeeInstance</SmartPointer>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake pointeeInstance =
                RemoteValueFakeUtil.CreateClass("string", "pointeeName", "pointeeValue");
            pointeeInstance.AddChild(
                RemoteValueFakeUtil.CreateClass("string", "childName1", "childValue1"));
            pointeeInstance.AddChild(
                RemoteValueFakeUtil.CreateClass("string", "childName2", "childValue2"));

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "SmartPointerType", "smartPointerName", "smartPointerValue");
            remoteValue.AddValueFromExpression("pointeeInstance", pointeeInstance);
            remoteValue.AddChild(pointeeInstance);

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildWithValue("pointeeValue"));

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(varInfo, Does.HaveChildrenWithValues("childValue1", "childValue2"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void SmartPointerTypeWithExcludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""SmartPointerType"">
    <SmartPointer ExcludeView=""MyExcludeView"">pointeeInstance</SmartPointer>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake pointeeInstance =
                RemoteValueFakeUtil.CreateClass("string", "pointeeName", "pointeeValue");
            pointeeInstance.AddChild(
                RemoteValueFakeUtil.CreateClass("string", "childName1", "childValue1"));
            pointeeInstance.AddChild(
                RemoteValueFakeUtil.CreateClass("string", "childName2", "childValue2"));

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "SmartPointerType", "smartPointerName", "smartPointerValue");
            remoteValue.AddValueFromExpression("pointeeInstance", pointeeInstance);
            remoteValue.AddChild(pointeeInstance);

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildrenWithValues("childValue1", "childValue2"));

            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(varInfo, Does.HaveChildWithValue("pointeeValue"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        #endregion

        #region DisplayString

        [Test]
        public async Task SimpleDisplayStringAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString>MyFormattedDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("MyFormattedDisplayString"));
        }

        [Test]
        public async Task DisplayStringItemWithConditionAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString Condition=""false"">MyFormattedValue_1</DisplayString>
    <DisplayString Condition=""true"">MyFormattedValue_2</DisplayString>
    <DisplayString>MyFormattedValue_3</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("MyFormattedValue_2"));
        }

        [Test]
        public async Task DisplayStringItemWithInvalidConditionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString Condition=""invalidExpression"">MyFormattedValue_1</DisplayString>
    <DisplayString>MyFormattedValue_2</DisplayString>
    <Expand HideRawView=""true"">
      <Item Name=""ExpectedChild"">validExpression</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("unexpectedChild_0", 0));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("unexpectedChild_1", 1));

            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateClass("std::string", "$12", "expectedValue"));

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("actualValue"));

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("invalidExpression"));

            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("ExpectedChild"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("expectedValue"));
        }

        [Test]
        public async Task DisplayStringWithExpressionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString>The {expression_1} brown {expression_2}.</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue =
                RemoteValueFakeUtil.CreateClass("MyCustomType", "dummyVarName", "actualValue");
            remoteValue.AddValueFromExpression("expression_1",
                RemoteValueFakeUtil.CreateClass("string", "", "QUICK"));
            remoteValue.AddValueFromExpression("expression_2",
                RemoteValueFakeUtil.CreateClass("string", "", "FOX"));
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("The QUICK brown FOX."));
        }

        [Test]
        public async Task DisplayStringWithExpressionHexadecimalDisplayAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString>{255}</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue =
                RemoteValueFakeUtil.CreateClass("MyCustomType", "dummyVarName", "actualValue");
            remoteValue.AddValueFromExpression("255",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 255));
            var varInfo = CreateVarInfo(remoteValue);
            varInfo.FallbackValueFormat = ValueFormat.Hex;

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("0xff"));
        }

        [Test]
        public async Task DisplayStringWithEscapedExpressionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString>The {{expression_1}} brown {expression_2}.</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");
            remoteValue.AddValueFromExpression("expression_1",
                RemoteValueFakeUtil.CreateClass("string", "", "QUICK"));
            remoteValue.AddValueFromExpression("expression_2",
                RemoteValueFakeUtil.CreateClass("string", "", "FOX"));
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("The {expression_1} brown FOX."));
        }

        [Test]
        public async Task DisplayStringWithUnmatchedClosingBracesAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString>{{ {{ } num = {expression}} }</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");
            remoteValue.AddValueFromExpression("expression",
                RemoteValueFakeUtil.CreateSimpleInt("myint", 42));
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("{ { } num = 42} }"));
        }

        [Test]
        public async Task DisplayStringWithFalseOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString Optional=""false"">{invalidExpression}</DisplayString>
    <DisplayString Optional=""true"">{validExpression}</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "varName", "originalValue");
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateClass("string", "", "dummyValue"));
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("originalValue"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
        }

        [Test]
        public async Task DisplayStringWithTrueOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString Optional=""true"">{invalidExpression}</DisplayString>
    <DisplayString>{validExpression}</DisplayString>
    <DisplayString Optional=""true"">{invalidExpression}</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "varName", "originalValue");
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateClass("string", "", "ExpectedDisplayString"));
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("ExpectedDisplayString"));
        }

        [Test]
        public async Task DisplayStringWithExportAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString Export=""yes"">ExpectedDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("Export"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "CustomType", "varName", "originalValue");
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateClass("string", "", "ExpectedDisplayString"));

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("originalValue"));
        }

        [Test]
        public async Task DisplayStringWithEncodingAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString Encoding=""Utf8"">ExpectedDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("Encoding"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "CustomType", "varName", "originalValue");
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateClass("string", "", "ExpectedDisplayString"));

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("originalValue"));
        }

        [Test]
        public async Task DisplayStringWithIncludeViewAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString IncludeView=""MyIncludeView"">MyFormattedDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("actualValue"));

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("MyFormattedDisplayString"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public async Task DisplayStringWithExcludeViewAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString ExcludeView=""MyExcludeView"">MyFormattedDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("MyFormattedDisplayString"));

            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("actualValue"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        #endregion

        #region StringView

        [Test]
        public void SimpleStringView()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <StringView>""MyFormattedValue""</StringView>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");
            remoteValue.AddStringLiteral("MyFormattedValue");

            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.StringView, Is.EqualTo("MyFormattedValue"));
        }

        [Test]
        public void StringViewWithWideCharString()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <StringView>L""MyFormattedValue""</StringView>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");
            remoteValue.AddStringLiteral("MyFormattedValue", "L");

            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.StringView, Is.EqualTo("MyFormattedValue"));
        }

        [Test]
        public async Task UndefinedStringViewWithDisplayStringAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString>MyFormattedDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyName", "actualValue");

            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("MyFormattedDisplayString"));
            Assert.That(varInfo.StringView, Is.EqualTo(""));
        }

        [Test]
        public void StringViewItemWithConditionAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <StringView Condition=""false"">""MyFormattedValue_1""</StringView>
    <StringView Condition=""true"">""MyFormattedValue_2""</StringView>
    <StringView>""MyFormattedValue_3""</StringView>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);
            remoteValue.AddStringLiteral("MyFormattedValue_1");
            remoteValue.AddStringLiteral("MyFormattedValue_2");
            remoteValue.AddStringLiteral("MyFormattedValue_3");

            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.StringView, Is.EqualTo("MyFormattedValue_2"));
        }

        [Test]
        public async Task StringViewItemWithInvalidConditionAsync()
        {
            var xml = @"
        <AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
          <Type Name=""MyCustomType"">
            <StringView Condition=""invalidExpression"">""MyFormattedValue_1""</StringView>
            <StringView>""MyFormattedValue_2""</StringView>
            <Expand HideRawView=""true"">
              <Item Name=""ExpectedChild"">validExpression</Item>
            </Expand>
          </Type>
        </AutoVisualizer>
        ";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("unexpectedChild_0", 0));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("unexpectedChild_1", 1));

            remoteValue.AddStringLiteral("MyFormattedValue_1");
            remoteValue.AddStringLiteral("MyFormattedValue_2");
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateClass("std::string", "$12", "expectedValue"));

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(varInfo.StringView, Is.EqualTo(""));

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("invalidExpression"));

            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("ExpectedChild"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("expectedValue"));
        }

        [Test]
        public void StringViewWithExpressionTypeDefiningStringView()
        {
            var xml = @"
        <AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
          <Type Name=""StringViewType"">
            <StringView>""MyFormattedValue""</StringView>
          </Type>
          <Type Name=""MyCustomType"">
            <StringView>validExpression</StringView>
          </Type>
        </AutoVisualizer>
        ";
            LoadFromString(xml);

            var remoteValue1 = RemoteValueFakeUtil.CreateClass(
                "StringViewType", "dummyVarName", "dummyValue");
            remoteValue1.AddStringLiteral("MyFormattedValue");

            var remoteValue2 =
                RemoteValueFakeUtil.CreateClass("MyCustomType", "dummyVarName", "actualValue");
            remoteValue2.AddValueFromExpression("validExpression", remoteValue1);

            var varInfo = CreateVarInfo(remoteValue2);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.StringView, Is.EqualTo("MyFormattedValue"));
        }

        [Test]
        public void StringViewWithExpressionTypeNotDefiningStringView()
        {
            var xml = @"
        <AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
          <Type Name=""MyCustomType_1"">
          </Type>
          <Type Name=""MyCustomType_2"">
            <StringView>validExpression</StringView>
          </Type>
        </AutoVisualizer>
        ";
            LoadFromString(xml);

            var remoteValue =
                RemoteValueFakeUtil.CreateClass("MyCustomType_2", "dummyVarName", "dummyValue");
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateClass("MyCustomType_1", "dummyVarName", "dummyValue"));

            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.StringView, Is.EqualTo(""));
        }

        [Test]
        public void StringViewWithFalseOptionalAttribute()
        {
            var xml = @"
        <AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
          <Type Name=""MyCustomType"">
            <StringView Optional=""false"">invalidExpression</StringView>
            <StringView Optional=""true"">validExpression</StringView>
          </Type>
        </AutoVisualizer>
        ";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "varName", "originalValue");
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateClass("string", "", "dummyValue"));
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(varInfo.StringView, Is.EqualTo(""));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
        }

        [Test]
        public void StringViewWithTrueOptionalAttribute()
        {
            var xml = @"
        <AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
          <Type Name=""MyCustomType"">
            <StringView Optional=""true"">invalidExpression</StringView>
            <StringView>validExpression</StringView>
            <StringView Optional=""true"">invalidExpression</StringView>
          </Type>
        </AutoVisualizer>
        ";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "varName", "originalValue");
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateClass("StringType", "", "\"ExpectedStringViewString\""));
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("varName"));
            Assert.That(varInfo.StringView, Is.EqualTo("ExpectedStringViewString"));
        }

        [Test]
        public void StringViewUsesSmartPointer()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""SmartPointerType"">
    <SmartPointer>pointeeInstance</SmartPointer>
  </Type>
  <Type Name=""PointeeType"">
    <StringView>""pointeeStringViewText""</StringView>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake pointeeInstance =
                RemoteValueFakeUtil.CreateClass("PointeeType", "pointeeName", "pointeeValue");
            pointeeInstance.AddStringLiteral("pointeeStringViewText");

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "SmartPointerType", "smartPointerName", "smartPointerValue");
            remoteValue.AddValueFromExpression("pointeeInstance", pointeeInstance);

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo.StringView, Is.EqualTo("pointeeStringViewText"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void StringViewFallsBackToSmartPointerIfConditionFails()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""SmartPointerType"">
    <SmartPointer>pointeeInstance</SmartPointer>
    <StringView Condition=""false"">""smartPointerStringViewText""</StringView>
  </Type>
  <Type Name=""PointeeType"">
    <StringView>""pointeeStringViewText""</StringView>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake pointeeInstance =
                RemoteValueFakeUtil.CreateClass("PointeeType", "pointeeName", "pointeeValue");
            pointeeInstance.AddStringLiteral("pointeeStringViewText");

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "SmartPointerType", "smartPointerName", "smartPointerValue");
            remoteValue.AddValueFromExpression("pointeeInstance", pointeeInstance);
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo.StringView, Is.EqualTo("pointeeStringViewText"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void StringViewFallsBackToSmartPointerIfOptional()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""SmartPointerType"">
    <SmartPointer>pointeeInstance</SmartPointer>
    <StringView Optional=""true"">invalidExpression</StringView>
  </Type>
  <Type Name=""PointeeType"">
    <StringView>""pointeeStringViewText""</StringView>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake pointeeInstance =
                RemoteValueFakeUtil.CreateClass("PointeeType", "pointeeName", "pointeeValue");
            pointeeInstance.AddStringLiteral("pointeeStringViewText");

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "SmartPointerType", "smartPointerName", "smartPointerValue");
            remoteValue.AddValueFromExpression("pointeeInstance", pointeeInstance);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo.StringView, Is.EqualTo("pointeeStringViewText"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void StringViewDoesNotUseSmartPointerIfValid()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""SmartPointerType"">
    <SmartPointer>pointeeInstance</SmartPointer>
    <StringView>""smartPointerStringViewText""</StringView>
  </Type>
  <Type Name=""PointeeType"">
    <StringView>""pointeeStringViewText""</StringView>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake pointeeInstance =
                RemoteValueFakeUtil.CreateClass("PointeeType", "pointeeName", "pointeeValue");

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "SmartPointerType", "smartPointerName", "smartPointerValue");
            remoteValue.AddValueFromExpression("pointeeInstance", pointeeInstance);
            remoteValue.AddStringLiteral("smartPointerStringViewText");

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo.StringView, Is.EqualTo("smartPointerStringViewText"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void StringViewWithIncludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <StringView IncludeView=""MyIncludeView"">""MyFormattedValue""</StringView>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");
            remoteValue.AddStringLiteral("MyFormattedValue");

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo.StringView, Is.EqualTo(""));

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(varInfo.StringView, Is.EqualTo("MyFormattedValue"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void StringViewWithExcludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <StringView ExcludeView=""MyExcludeView"">""MyFormattedValue""</StringView>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");
            remoteValue.AddStringLiteral("MyFormattedValue");

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo.StringView, Is.EqualTo("MyFormattedValue"));

            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(varInfo.StringView, Is.EqualTo(""));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }


        [Test]
        public void StringViewExpressionWithNonStringType()
        {
            var xml = @"
        <AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
          <Type Name=""MyCustomType"">
            <StringView>123</StringView>
          </Type>
        </AutoVisualizer>
        ";
            LoadFromString(xml);

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("MyCustomType", "dummyVarName", "actualValue");

            RemoteValueFake childValue =
                RemoteValueFakeUtil.CreateClass("int", "myInt", "123");
            remoteValue.AddValueFromExpression("123", childValue);

            IVariableInformation varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.StringView, Is.EqualTo(""));
        }

        #endregion

        #region Expand

        [Test]
        public void StringViewUnescapesStringsAndRemovesBraces()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <StringView>""some\ns\tring\""view""</StringView>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "MyCustomType", "dummyVarName", "actualValue");
            string unescapedString = "some\ns\tring\"view";
            remoteValue.AddStringLiteral(unescapedString);

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo.StringView, Is.EqualTo(unescapedString));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task DefaultChildrenWhenNoExpandItemExistsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(
                RemoteValueFakeUtil.CreateClass("ChildType_0", "childName_0", "childValue_0"));
            remoteValue.AddChild(
                RemoteValueFakeUtil.CreateClass("ChildType_1", "childName_1", "childValue_1"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("childName_0"));
            Assert.That(children[1].DisplayName, Is.EqualTo("childName_1"));
        }

        [Test]
        public async Task EmptyExpandItemAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString>MyFormattedDisplayString</DisplayString>
    <Expand HideRawView=""true""></Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "", "actualValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("x", 1));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("x", 2));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("MyFormattedDisplayString"));
            Assert.That(children.Length, Is.EqualTo(0));
        }

        [Test]
        public async Task InvalidExpandConditionDoesntAffectDisplayStringAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString>ExpectedDisplayString</DisplayString>
    <Expand>
      <Item Name=""InvalidChild"">invalidExpression</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var varInfo = CreateVarInfo(
                RemoteValueFakeUtil.CreateClass("MyCustomType", "", ""));

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("invalidExpression"));

            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("ExpectedDisplayString"));
            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("invalidExpression"));

            var rawInfo = children[1];
            Assert.That((await rawInfo.GetAllChildrenAsync()).Length, Is.EqualTo(0));
        }

        [Test]
        public async Task ExpandWithTrueHideRawViewAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <Item Name=""Child"">expression</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateClass(
                "hiddenType", "hiddenVar", "hiddenValue"));
            remoteValue.AddValueFromExpression("expression",
                RemoteValueFakeUtil.CreateClass("dummyType", "dummyName", "expectedValue"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("Child"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("expectedValue"));
        }

        [Test]
        public async Task ExpandWithFalseHideRawViewAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""false"">
      <Item Name=""Child"">expression</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateClass(
                "hiddenType", "hiddenVar", "hiddenValue"));
            remoteValue.AddValueFromExpression("expression",
                RemoteValueFakeUtil.CreateClass("dummyType", "dummyName", "expectedValue"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("Child"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("expectedValue"));
        }

        #endregion

        #region RawView

        [Test]
        public async Task RawViewAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">

  <Type Name=""GrandparentType"">
    <DisplayString>GrandparentValue</DisplayString>
    <Expand>
      <Item Name=""[Visible Parent]"">_visibleParent</Item>
    </Expand>
  </Type>

  <Type Name=""ParentType"">
    <DisplayString>ParentValue</DisplayString>
    <Expand>
      <Item Name=""[Visible Child]"">_visibleChild</Item>
    </Expand>
  </Type>

  <Type Name=""ChildType"">
    <DisplayString>ChildValue</DisplayString>
    <Expand>
        <Item Name=""[Visible Int]"">_visibleInt</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var sbGrandparent =
                RemoteValueFakeUtil.CreateClass(
                    "GrandparentType", "_grandparent", "grandparentActualValue");

            var sbVisibleParent =
                RemoteValueFakeUtil.CreateClass(
                    "ParentType", "_visibleParent", "visibleParentActualValue");
            var sbHiddenParent =
                RemoteValueFakeUtil.CreateClass(
                    "ParentType", "_hiddenParent", "hiddenParentActualValue");

            sbGrandparent.AddChild(sbVisibleParent);
            sbGrandparent.AddChild(sbHiddenParent);

            {
                var sbVisibleChild =
                    RemoteValueFakeUtil.CreateClass(
                        "ChildType", "_visibleChild", "visibleChildActualValue");
                var sbHiddenChild =
                    RemoteValueFakeUtil.CreateClass(
                        "ChildType", "_hiddenChild", "hiddenChildActualValue");

                sbVisibleParent.AddChild(sbVisibleChild);
                sbVisibleParent.AddChild(sbHiddenChild);

                sbVisibleChild.AddChild(RemoteValueFakeUtil.CreateSimpleInt(
                    "_visibleInt", 17));
                sbVisibleChild.AddChild(RemoteValueFakeUtil.CreateSimpleInt(
                    "_hiddenInt", 13));
            }
            {
                var sbVisibleChild =
                    RemoteValueFakeUtil.CreateClass(
                        "ChildType", "_visibleChild", "visibleChildActualValue");
                var sbHiddenChild =
                    RemoteValueFakeUtil.CreateClass(
                        "ChildType", "_hiddenChild", "hiddenChildActualValue");

                sbHiddenParent.AddChild(sbVisibleChild);
                sbHiddenParent.AddChild(sbHiddenChild);

                sbVisibleChild.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_visibleInt", 27));
                sbVisibleChild.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_hiddenInt", 23));
            }


            var grandparentInfo = CreateVarInfo(sbGrandparent);

            Assert.That(grandparentInfo.DisplayName, Is.EqualTo("_grandparent"));
            Assert.That(await grandparentInfo.ValueAsync(), Is.EqualTo("GrandparentValue"));

            // Natvis GrandparentType expansion (_grandparent)
            {
                var grandparent = await grandparentInfo.GetAllChildrenAsync();

                Assert.That(grandparent.Length, Is.EqualTo(2));

                Assert.That(grandparent[0].DisplayName, Is.EqualTo("[Visible Parent]"));
                Assert.That(await grandparent[0].ValueAsync(), Is.EqualTo("ParentValue"));
                Assert.That(grandparent[0].TypeName, Is.EqualTo("ParentType"));

                Assert.That(grandparent[1].DisplayName, Is.EqualTo("[Raw View]"));
                Assert.That(await grandparent[1].ValueAsync(), Is.EqualTo("grandparentActualValue"));
                Assert.That(grandparent[1].TypeName, Is.EqualTo("GrandparentType"));
            }

            // Raw GrandparentType expansion
            // (_grandparent -> [Raw View])
            {
                var grandparent = await grandparentInfo.GetAllChildrenAsync();
                var granparentRawView = await grandparent[1].GetAllChildrenAsync();

                Assert.That(granparentRawView.Length, Is.EqualTo(2));

                Assert.That(granparentRawView[0].DisplayName, Is.EqualTo("_visibleParent"));
                Assert.That(await granparentRawView[0].ValueAsync(), Is.EqualTo("ParentValue"));
                Assert.That(granparentRawView[0].TypeName, Is.EqualTo("ParentType"));

                Assert.That(granparentRawView[1].DisplayName, Is.EqualTo("_hiddenParent"));
                Assert.That(await granparentRawView[1].ValueAsync(), Is.EqualTo("ParentValue"));
                Assert.That(granparentRawView[1].TypeName, Is.EqualTo("ParentType"));
            }

            // Natvis ParentType expansion
            // (_grandparent -> [Visible Parent])
            {
                var grandparent = await grandparentInfo.GetAllChildrenAsync();
                var visibleParent = await grandparent[0].GetAllChildrenAsync();

                Assert.That(visibleParent.Length, Is.EqualTo(2));

                Assert.That(visibleParent[0].DisplayName, Is.EqualTo("[Visible Child]"));
                Assert.That(await visibleParent[0].ValueAsync(), Is.EqualTo("ChildValue"));
                Assert.That(visibleParent[0].TypeName, Is.EqualTo("ChildType"));

                Assert.That(visibleParent[1].DisplayName, Is.EqualTo("[Raw View]"));
                Assert.That(await visibleParent[1].ValueAsync(),
                            Is.EqualTo("visibleParentActualValue"));
                Assert.That(visibleParent[1].TypeName, Is.EqualTo("ParentType"));
            }

            // Raw ParentType expansion
            // (_grandparent -> [Visible Parent] -> [Raw View])
            {
                var grandparent = await grandparentInfo.GetAllChildrenAsync();
                var visibleParent = await grandparent[0].GetAllChildrenAsync();
                var visibleParentRawView = await visibleParent[1].GetAllChildrenAsync();

                Assert.That(visibleParentRawView.Length, Is.EqualTo(2));

                Assert.That(visibleParentRawView[0].DisplayName, Is.EqualTo("_visibleChild"));
                Assert.That(await visibleParentRawView[0].ValueAsync(), Is.EqualTo("ChildValue"));
                Assert.That(visibleParentRawView[0].TypeName, Is.EqualTo("ChildType"));

                Assert.That(visibleParentRawView[1].DisplayName, Is.EqualTo("_hiddenChild"));
                Assert.That(await visibleParentRawView[1].ValueAsync(), Is.EqualTo("ChildValue"));
                Assert.That(visibleParentRawView[1].TypeName, Is.EqualTo("ChildType"));
            }

            // Natvis ChildType expansion
            // (_grandparent -> [Visible Parent] -> [Visible Chld])
            {
                var grandparent = await grandparentInfo.GetAllChildrenAsync();
                var visibleParent = await grandparent[0].GetAllChildrenAsync();
                var visibleChild = await visibleParent[0].GetAllChildrenAsync();

                Assert.That(visibleChild.Length, Is.EqualTo(2));

                Assert.That(visibleChild[0].DisplayName, Is.EqualTo("[Visible Int]"));
                Assert.That(await visibleChild[0].ValueAsync(), Is.EqualTo("17"));
                Assert.That(visibleChild[0].TypeName, Is.EqualTo("int"));

                Assert.That(visibleChild[1].DisplayName, Is.EqualTo("[Raw View]"));
                Assert.That(await visibleChild[1].ValueAsync(),
                            Is.EqualTo("visibleChildActualValue"));
                Assert.That(visibleChild[1].TypeName, Is.EqualTo("ChildType"));
            }

            // Raw ChildType expansion
            // (_grandparent -> [Visible Parent] -> [Visible Chld] -> [Raw View])
            {
                var grandparent = await grandparentInfo.GetAllChildrenAsync();
                var visibleParent = await grandparent[0].GetAllChildrenAsync();
                var visibleChild = await visibleParent[0].GetAllChildrenAsync();
                var visibleChildRawView = await visibleChild[1].GetAllChildrenAsync();

                Assert.That(visibleChildRawView.Length, Is.EqualTo(2));

                Assert.That(visibleChildRawView[0].DisplayName, Is.EqualTo("_visibleInt"));
                Assert.That(await visibleChildRawView[0].ValueAsync(), Is.EqualTo("17"));
                Assert.That(visibleChildRawView[0].TypeName, Is.EqualTo("int"));

                Assert.That(visibleChildRawView[1].DisplayName, Is.EqualTo("_hiddenInt"));
                Assert.That(await visibleChildRawView[1].ValueAsync(), Is.EqualTo("13"));
                Assert.That(visibleChildRawView[1].TypeName, Is.EqualTo("int"));
            }

            // Ensure [Raw View] children expand using Natvis
            // (_grandparent -> [Raw View] -> _visibleParent
            {
                var grandparent = await grandparentInfo.GetAllChildrenAsync();
                var grandparentRawView = await grandparent[1].GetAllChildrenAsync();
                var visibleParent = await grandparentRawView[0].GetAllChildrenAsync();

                Assert.That(visibleParent.Length, Is.EqualTo(2));

                Assert.That(visibleParent[0].DisplayName, Is.EqualTo("[Visible Child]"));
                Assert.That(await visibleParent[0].ValueAsync(), Is.EqualTo("ChildValue"));
                Assert.That(visibleParent[0].TypeName, Is.EqualTo("ChildType"));

                Assert.That(visibleParent[1].DisplayName, Is.EqualTo("[Raw View]"));
                Assert.That(await visibleParent[1].ValueAsync(),
                            Is.EqualTo("visibleParentActualValue"));
                Assert.That(visibleParent[1].TypeName, Is.EqualTo("ParentType"));
            }

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        #endregion

        #region ArrayItems

        [Test]
        public async Task ArrayItemsTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems>
        <Size>arraySize</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var arraySize = RemoteValueFakeUtil.CreateSimpleInt("arraySize", 2);
            remoteValue.AddChild(arraySize);

            remoteValue.AddValueFromExpression("(array)[0]",
                RemoteValueFakeUtil.CreateClass("string", "$11", "a"));
            remoteValue.AddValueFromExpression("(array)[1]",
                RemoteValueFakeUtil.CreateClass("string", "$12", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
        }

        [Test]
        public async Task ArrayItemsTypeWithConditionsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems Condition=""false"">
        <Size>arraySize_0</Size>
        <ValuePointer>array_0</ValuePointer>
      </ArrayItems>
      <ArrayItems Condition=""true"">
        <Size>arraySize_1</Size>
        <ValuePointer>array_1</ValuePointer>
      </ArrayItems>
      <ArrayItems>
        <Size>arraySize_2</Size>
        <ValuePointer>array_2</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var arraySize1 = RemoteValueFakeUtil.CreateSimpleInt("arraySize_1", 2);
            remoteValue.AddChild(arraySize1);
            remoteValue.AddValueFromExpression("(array_1)[0]",
                RemoteValueFakeUtil.CreateClass("string", "[0]", "a"));
            remoteValue.AddValueFromExpression("(array_1)[1]",
                RemoteValueFakeUtil.CreateClass("string", "[1]", "b"));

            var arraySize2 = RemoteValueFakeUtil.CreateSimpleInt("arraySize_2", 3);
            remoteValue.AddChild(arraySize2);
            remoteValue.AddValueFromExpression("(array_2)[0]",
                RemoteValueFakeUtil.CreateClass("string", "[0]", "x"));
            remoteValue.AddValueFromExpression("(array_2)[1]",
                RemoteValueFakeUtil.CreateClass("string", "[1]", "y"));
            remoteValue.AddValueFromExpression("(array_2)[2]",
                RemoteValueFakeUtil.CreateClass("string", "[2]", "z"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(5));

            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));

            Assert.That(children[2].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("x"));
            Assert.That(children[3].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[3].ValueAsync(), Is.EqualTo("y"));
            Assert.That(children[4].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[4].ValueAsync(), Is.EqualTo("z"));
        }

        [Test]
        public async Task ArrayItemsTypeWithInvalidConditionsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand>
      <ArrayItems Condition=""invalidExpression"">
        <Size>arraySize_0</Size>
        <ValuePointer>array_0</ValuePointer>
      </ArrayItems>
      <ArrayItems>
        <Size>arraySize_1</Size>
        <ValuePointer>array_1</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var arraySize1 = RemoteValueFakeUtil.CreateSimpleInt("arraySize_0", 2);
            remoteValue.AddChild(arraySize1);
            remoteValue.AddValueFromExpression("(array_0)[0]",
                RemoteValueFakeUtil.CreateClass("string", "[0]", "a"));
            remoteValue.AddValueFromExpression("(array_0)[1]",
                RemoteValueFakeUtil.CreateSimpleInt("[1]", 'b'));

            var arraySize2 = RemoteValueFakeUtil.CreateSimpleInt("arraySize_1", 3);
            remoteValue.AddChild(arraySize2);
            remoteValue.AddValueFromExpression("(array_1)[0]",
                RemoteValueFakeUtil.CreateClass("string", "[0]", "x"));
            remoteValue.AddValueFromExpression("(array_1)[1]",
                RemoteValueFakeUtil.CreateClass("string", "[1]", "y"));
            remoteValue.AddValueFromExpression("(array_1)[2]",
                RemoteValueFakeUtil.CreateClass("string", "[2]", "z"));

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("invalidExpression"));

            Assert.That(children.Length, Is.EqualTo(5));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("invalidExpression"));

            Assert.That(children[1].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("x"));

            Assert.That(children[2].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("y"));

            Assert.That(children[3].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[3].ValueAsync(), Is.EqualTo("z"));

            var raw = children[4];
            Assert.That(raw.DisplayName, Is.EqualTo("[Raw View]"));
            Assert.That(await raw.ValueAsync(), Is.EqualTo("parentValue"));

            var rawChildren = await raw.GetAllChildrenAsync();
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("arraySize_0"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("2"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("arraySize_1"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo("3"));
        }

        [Test]
        public async Task ArrayValuePointerTypeWithConditionsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems>
        <Size>arraySize</Size>
        <ValuePointer Condition=""false"">array_0</ValuePointer>
        <ValuePointer Condition=""true"">array_1</ValuePointer>
        <ValuePointer>array_2</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var arraySize = RemoteValueFakeUtil.CreateSimpleInt("arraySize", 2);
            remoteValue.AddChild(arraySize);

            remoteValue.AddValueFromExpression("(array_1)[0]",
                RemoteValueFakeUtil.CreateClass("string", "[0]", "a"));
            remoteValue.AddValueFromExpression("(array_1)[1]",
                RemoteValueFakeUtil.CreateClass("string", "[1]", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
        }

        [Test]
        public async Task ArrayValuePointerTypeWithInvalidConditionsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand>
      <ArrayItems>
        <Size>arraySize</Size>
        <ValuePointer Condition=""invalidExpression"">array_0</ValuePointer>
        <ValuePointer>array_1</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var arraySize = RemoteValueFakeUtil.CreateSimpleInt("arraySize", 2);
            remoteValue.AddChild(arraySize);

            remoteValue.AddValueFromExpression("(array_1)[0]",
                RemoteValueFakeUtil.CreateClass("string", "[0]", "a"));
            remoteValue.AddValueFromExpression("(array_1)[1]",
                RemoteValueFakeUtil.CreateClass("string", "[1]", "b"));

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("invalidExpression"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("invalidExpression"));

            var raw = children[1];
            Assert.That(raw.DisplayName, Is.EqualTo("[Raw View]"));
            Assert.That(await raw.ValueAsync(), Is.EqualTo("parentValue"));

            var rawChildren = await raw.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(1));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("arraySize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("2"));
        }

        [Test]
        public async Task ArrayItemsTypeWithOptionalAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems Optional=""true"">
        <Size>arraySize_0</Size>
        <ValuePointer>array_0</ValuePointer>
      </ArrayItems>
      <Item Name=""ChildItem"">childValue</Item>
      <ArrayItems Optional=""true"">
        <Size>arraySize_1</Size>
        <ValuePointer>array_1</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("childValue", 7));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("ChildItem"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("7"));
        }

        [Test]
        public async Task ArrayItemsTypeWithChildExpressionFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems>
        <Size>arraySize</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
      <Item Name=""ChildItem"">childValue</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("childValue", 7));

            var arraySize = RemoteValueFakeUtil.CreateSimpleInt("arraySize", 3);
            remoteValue.AddChild(arraySize);

            remoteValue.AddValueFromExpression("(array)[0]",
                RemoteValueFakeUtil.CreateSimpleChar("", 'a'));
            remoteValue.AddValueFromExpression("(array)[2]",
                RemoteValueFakeUtil.CreateSimpleChar("", 'c'));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ParentType"));
            Assert.That(children.Length, Is.EqualTo(4));

            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));

            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Does.Contain("Error"));

            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("c"));

            Assert.That(children[3].DisplayName, Is.EqualTo("ChildItem"));
            Assert.That(await children[3].ValueAsync(), Is.EqualTo("7"));
        }

        [Test]
        public void ArrayItemsTypeWithDirection()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand>
      <ArrayItems>
        <Direction>Forward</Direction>
        <Size>arraySize</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<Direction>"));
        }

        [Test]
        public void ArrayItemsTypeWithRank()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand>
      <ArrayItems>
        <Rank>1</Rank>
        <Size>arraySize</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<Rank>"));
        }

        [Test]
        public void ArrayItemsTypeWithLowerBound()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand>
      <ArrayItems>
        <LowerBound>0</LowerBound>
        <Size>arraySize</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<LowerBound>"));
        }

        [Test]
        public void ArrayItemsTypeWithIncludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems IncludeView=""MyIncludeView"">
        <Size>arraySize</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("arraySize", 1));
            remoteValue.AddValueFromExpression("(array)[0]",
                RemoteValueFakeUtil.CreateSimpleString("[0]", "arrayElement"));

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.Not.HaveChildren());

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(varInfo, Does.HaveChildWithValue("arrayElement"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void ArrayItemsTypeWithExcludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems ExcludeView=""MyExcludeView"">
        <Size>arraySize</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("arraySize", 1));
            remoteValue.AddValueFromExpression("(array)[0]",
                RemoteValueFakeUtil.CreateSimpleString("[0]", "arrayElement"));

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildWithValue("arrayElement"));

            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(varInfo, Does.Not.HaveChildren());

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public async Task ArrayItemsSizeWithModuleNameAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems>
        <Size ModuleName=""DummyModule"">arraySize</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleName"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var arraySize = RemoteValueFakeUtil.CreateSimpleInt("arraySize", 2);
            remoteValue.AddChild(arraySize);

            remoteValue.AddValueFromExpression("(array)[0]",
                RemoteValueFakeUtil.CreateSimpleInt("$11", 17));
            remoteValue.AddValueFromExpression("(array)[1]",
                RemoteValueFakeUtil.CreateSimpleInt("$12", 23));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("23"));
        }

        [Test]
        public async Task ArrayItemsSizeWithModuleVersionMinAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems>
        <Size ModuleVersionMin=""1.2.3"">arraySize</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleVersionMin"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var arraySize = RemoteValueFakeUtil.CreateSimpleInt("arraySize", 2);
            remoteValue.AddChild(arraySize);

            remoteValue.AddValueFromExpression("(array)[0]",
                RemoteValueFakeUtil.CreateSimpleInt("$11", 17));
            remoteValue.AddValueFromExpression("(array)[1]",
                RemoteValueFakeUtil.CreateSimpleInt("$12", 23));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("23"));
        }

        [Test]
        public async Task ArrayItemsSizeWithModuleVersionMaxAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems>
        <Size ModuleVersionMax=""3.2.1"">arraySize</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleVersionMax"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var arraySize = RemoteValueFakeUtil.CreateSimpleInt("arraySize", 2);
            remoteValue.AddChild(arraySize);

            remoteValue.AddValueFromExpression("(array)[0]",
                RemoteValueFakeUtil.CreateSimpleInt("$11", 17));
            remoteValue.AddValueFromExpression("(array)[1]",
                RemoteValueFakeUtil.CreateSimpleInt("$12", 23));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("23"));
        }

        [Test]
        public async Task ArrayItemsSizeWithTrueOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems>
        <Size Optional=""true"">invalidArraySize</Size>
        <Size Optional=""true"" Condition=""invalidCondition"">validArraySize_0</Size>
        <Size>validArraySize_1</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var validArraySize_0 = RemoteValueFakeUtil.CreateSimpleInt("validArraySize_0", 19);
            remoteValue.AddChild(validArraySize_0);

            var validArraySize_1 = RemoteValueFakeUtil.CreateSimpleInt("validArraySize_1", 2);
            remoteValue.AddChild(validArraySize_1);

            remoteValue.AddValueFromExpression("(array)[0]",
                RemoteValueFakeUtil.CreateSimpleInt("$11", 17));
            remoteValue.AddValueFromExpression("(array)[1]",
                RemoteValueFakeUtil.CreateSimpleInt("$12", 23));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("23"));
        }

        [Test]
        public async Task ArrayItemsSizeWithInvalidSizeValueAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems>
        <Size Optional=""false"">invalidArraySize</Size>
        <Size>validArraySize</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var arraySize = RemoteValueFakeUtil.CreateSimpleInt("arraySize", 2);
            remoteValue.AddChild(arraySize);

            remoteValue.AddValueFromExpression("(array)[0]",
                RemoteValueFakeUtil.CreateSimpleInt("$11", 17));
            remoteValue.AddValueFromExpression("(array)[1]",
                RemoteValueFakeUtil.CreateSimpleInt("$12", 23));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("invalidArraySize"));

            var raw = children[1];
            Assert.That(raw.DisplayName, Is.EqualTo("[Raw View]"));
            Assert.That(await raw.ValueAsync(), Is.EqualTo("parentValue"));

            var rawChildren = await raw.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(1));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("arraySize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("2"));
        }

        [Test]
        public void ArrayItemsSizeWithIncludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems>
        <Size IncludeView=""MyIncludeView"">arraySize1</Size>
        <Size>arraySize2</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("arraySize1", 1));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("arraySize2", 2));

            remoteValue.AddValueFromExpression("(array)[0]",
                RemoteValueFakeUtil.CreateSimpleInt("$11", 17));
            remoteValue.AddValueFromExpression("(array)[1]",
                RemoteValueFakeUtil.CreateSimpleInt("$12", 23));

            // arraySize1=1 does not get included here, so it falls back to arraySize2=2.
            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildrenWithValues("17", "23"));

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(varInfo, Does.HaveChildWithValue("17"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void ArrayItemsSizeWithExcludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems>
        <Size ExcludeView=""MyExcludeView"">arraySize1</Size>
        <Size>arraySize2</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("arraySize1", 1));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("arraySize2", 2));

            remoteValue.AddValueFromExpression("(array)[0]",
                RemoteValueFakeUtil.CreateSimpleInt("$11", 17));
            remoteValue.AddValueFromExpression("(array)[1]",
                RemoteValueFakeUtil.CreateSimpleInt("$12", 23));

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildWithValue("17"));

            // arraySize1=1 gets excluded here, so it falls back to arraySize2=2.
            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(varInfo, Does.HaveChildrenWithValues("17", "23"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public async Task ArrayItemsSizeWithConditionsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ArrayItems>
        <Size Condition=""false"">invalidArraySize</Size>
        <Size Condition=""true"">validArraySize</Size>
        <ValuePointer>array</ValuePointer>
      </ArrayItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var validArraySize = RemoteValueFakeUtil.CreateSimpleInt("validArraySize", 2);
            remoteValue.AddChild(validArraySize);
            remoteValue.AddChild(TRUE_REMOTE_VALUE);

            remoteValue.AddValueFromExpression("(array)[0]",
                RemoteValueFakeUtil.CreateSimpleInt("$11", 17));
            remoteValue.AddValueFromExpression("(array)[1]",
                RemoteValueFakeUtil.CreateSimpleInt("$12", 23));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("23"));
        }

        #endregion

        #region ExpandedItem

        [Test]
        public async Task ExpandedItemTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ExpandedItem>autoExpandedChild</ExpandedItem>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var autoExpandedChild =
                RemoteValueFakeUtil.CreateClass("string", "autoExpandedChild", "Z");
            autoExpandedChild.AddChild(RemoteValueFakeUtil.CreateClass("string", "someVar_0", "a"));
            autoExpandedChild.AddChild(RemoteValueFakeUtil.CreateClass("string", "someVar_1", "b"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(autoExpandedChild);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("someVar_0"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("someVar_1"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
        }

        [Test]
        public async Task EmptyExpandedItemTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ExpandedItem></ExpandedItem>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var autoExpandedChild =
                RemoteValueFakeUtil.CreateClass("string", "autoExpandedChild", "Z");
            autoExpandedChild.AddChild(RemoteValueFakeUtil.CreateClass("string", "someVar_0", "a"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(autoExpandedChild);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(0));
        }

        [Test]
        public async Task ExpandedItemTypeWithEmptyChildAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ExpandedItem>autoExpandedChild</ExpandedItem>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var autoExpandedChild = RemoteValueFakeUtil.CreateClass(
                "string", "autoExpandedChild", "Z");
            autoExpandedChild.AddChild(RemoteValueFakeUtil.CreateSimpleInt("grandchild", 12358));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(autoExpandedChild);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("grandchild"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("12358"));
        }

        [Test]
        public async Task ExpandedItemTypeWithConditionsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ExpandedItem Condition=""false"">autoExpandedChild_0</ExpandedItem>
      <ExpandedItem Condition=""true"">autoExpandedChild_1</ExpandedItem>
      <ExpandedItem>autoExpandedChild_2</ExpandedItem>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var autoExpandedChild_0 =
                RemoteValueFakeUtil.CreateClass(
                    "string", "autoExpandedChild_0", "unexpectedValue_0");
            autoExpandedChild_0.AddChild(
                RemoteValueFakeUtil.CreateClass("string", "unexpectedVar_0_0", "T"));
            autoExpandedChild_0.AddChild(
                RemoteValueFakeUtil.CreateClass("string", "unexpectedVar_0_1", "U"));

            var autoExpandedChild_1 =
                RemoteValueFakeUtil.CreateClass("string", "autoExpandedChild_1", "V");
            autoExpandedChild_1.AddChild(RemoteValueFakeUtil.CreateClass(
                "string", "someVar_1_0", "a"));
            autoExpandedChild_1.AddChild(RemoteValueFakeUtil.CreateClass(
                "string", "someVar_1_1", "b"));

            var autoExpandedChild_2 =
                RemoteValueFakeUtil.CreateClass("string", "autoExpandedChild_2", "W");
            autoExpandedChild_2.AddChild(RemoteValueFakeUtil.CreateClass(
                "string", "someVar_2_0", "x"));
            autoExpandedChild_2.AddChild(RemoteValueFakeUtil.CreateClass(
                "string", "someVar_2_1", "y"));
            autoExpandedChild_2.AddChild(RemoteValueFakeUtil.CreateClass(
                "string", "someVar_2_1", "z"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);
            remoteValue.AddChild(autoExpandedChild_0);
            remoteValue.AddChild(autoExpandedChild_1);
            remoteValue.AddChild(autoExpandedChild_2);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(5));
            Assert.That(children[0].DisplayName, Is.EqualTo("someVar_1_0"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("someVar_1_1"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
            Assert.That(children[2].DisplayName, Is.EqualTo("someVar_2_0"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("x"));
            Assert.That(children[3].DisplayName, Is.EqualTo("someVar_2_1"));
            Assert.That(await children[3].ValueAsync(), Is.EqualTo("y"));
            Assert.That(children[4].DisplayName, Is.EqualTo("someVar_2_1"));
            Assert.That(await children[4].ValueAsync(), Is.EqualTo("z"));
        }

        [Test]
        public async Task ExpandedItemTypeWithOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ExpandedItem Optional=""true"">invalidExpression</ExpandedItem>
      <ExpandedItem>validExpression</ExpandedItem>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var validExpression = RemoteValueFakeUtil.CreateClass(
                "dummyType", "validExpression", "");
            validExpression.AddChild(RemoteValueFakeUtil.CreateClass(
                "string", "someVar", "a"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(validExpression);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("someVar"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
        }

        [Test]
        public void ExpandedItemTypeWithIncludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ExpandedItem IncludeView=""MyIncludeView"">autoExpandedChild</ExpandedItem>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var autoExpandedChild =
                RemoteValueFakeUtil.CreateClass("string", "autoExpandedChild", "Z");
            autoExpandedChild.AddChild(RemoteValueFakeUtil.CreateSimpleInt("someVar", 42));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(autoExpandedChild);

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.Not.HaveChildren());

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(varInfo, Does.HaveChildWithValue("42"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void ExpandedItemTypeWithExcludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <ExpandedItem ExcludeView=""MyExcludeView"">autoExpandedChild</ExpandedItem>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var autoExpandedChild =
                RemoteValueFakeUtil.CreateClass("string", "autoExpandedChild", "Z");
            autoExpandedChild.AddChild(RemoteValueFakeUtil.CreateSimpleInt("someVar", 42));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(autoExpandedChild);

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildWithValue("42"));

            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(varInfo, Does.Not.HaveChildren());

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        #endregion

        #region IndexListItems

        [Test]
        public async Task IndexListItemsTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems>
        <Size>listSize</Size>
        <ValueNode>list[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize", 2));
            remoteValue.AddValueFromExpression(
                "list[0U]", RemoteValueFakeUtil.CreateClass("string", "$10", "a"));
            remoteValue.AddValueFromExpression(
                "list[1U]", RemoteValueFakeUtil.CreateClass("string", "$11", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
        }

        [Test]
        public async Task IndexListItemsType_WithFormatSpecifierAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems>
        <Size>listSize</Size>
        <ValueNode>list[$i],x</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize", 2));
            remoteValue.AddValueFromExpression(
                "list[0U]", RemoteValueFakeUtil.CreateClass("string", "$10", "12"));
            remoteValue.AddValueFromExpression(
                "list[1U]", RemoteValueFakeUtil.CreateClass("string", "$11", "13"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0xc"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("0xd"));
        }

        [Test]
        public async Task IndexListItemsWithConditionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems Condition=""false"">
        <Size>listSize_0</Size>
        <ValueNode>list_0[$i]</ValueNode>
      </IndexListItems>
      <IndexListItems Condition=""true"">
        <Size>listSize_1</Size>
        <ValueNode>list_1[$i]</ValueNode>
      </IndexListItems>
      <IndexListItems>
        <Size>listSize_2</Size>
        <ValueNode>list_2[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize_1", 2));
            remoteValue.AddValueFromExpression(
                "list_1[0U]", RemoteValueFakeUtil.CreateClass("string", "$10", "a"));
            remoteValue.AddValueFromExpression(
                "list_1[1U]", RemoteValueFakeUtil.CreateClass("string", "$11", "b"));

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize_2", 3));
            remoteValue.AddValueFromExpression(
                "list_2[0U]", RemoteValueFakeUtil.CreateClass("string", "$20", "x"));
            remoteValue.AddValueFromExpression(
                "list_2[1U]", RemoteValueFakeUtil.CreateClass("string", "$21", "y"));
            remoteValue.AddValueFromExpression(
                "list_2[2U]", RemoteValueFakeUtil.CreateClass("string", "$21", "z"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(5));

            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));

            Assert.That(children[2].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("x"));
            Assert.That(children[3].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[3].ValueAsync(), Is.EqualTo("y"));
            Assert.That(children[4].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[4].ValueAsync(), Is.EqualTo("z"));
        }

        [Test]
        public async Task IndexListItemsSizeWithModuleNameAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems>
        <Size ModuleName=""DummyModule"">listSize</Size>
        <ValueNode>list[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleName"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var listSize = RemoteValueFakeUtil.CreateSimpleInt("listSize", 2);
            remoteValue.AddChild(listSize);

            remoteValue.AddValueFromExpression(
                "list[0U]", RemoteValueFakeUtil.CreateClass("string", "$11", "a"));
            remoteValue.AddValueFromExpression(
                "list[1U]", RemoteValueFakeUtil.CreateClass("string", "$12", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
        }

        [Test]
        public async Task IndexListItemsSizeWithModuleVersionMinAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems>
        <Size ModuleVersionMin=""1.2.3"">listSize</Size>
        <ValueNode>list[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleVersionMin"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var listSize = RemoteValueFakeUtil.CreateSimpleInt("listSize", 2);
            remoteValue.AddChild(listSize);

            remoteValue.AddValueFromExpression(
                "list[0U]", RemoteValueFakeUtil.CreateClass("string", "$11", "a"));
            remoteValue.AddValueFromExpression(
                "list[1U]", RemoteValueFakeUtil.CreateClass("string", "$12", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
        }

        [Test]
        public async Task IndexListItemsSizeWithModuleVersionMaxAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems>
        <Size ModuleVersionMax=""3.2.1"">listSize</Size>
        <ValueNode>list[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleVersionMax"));

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var listSize = RemoteValueFakeUtil.CreateSimpleInt("listSize", 2);
            remoteValue.AddChild(listSize);

            remoteValue.AddValueFromExpression(
                "list[0U]", RemoteValueFakeUtil.CreateClass("string", "$11", "a"));
            remoteValue.AddValueFromExpression(
                "list[1U]", RemoteValueFakeUtil.CreateClass("string", "$12", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
        }

        [Test]
        public async Task IndexListItemsSizeWithTrueOptionalAttributeAsync()
        {
            var xml = @"
        <AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
          <Type Name=""ParentType"">
            <Expand HideRawView=""true"">
              <IndexListItems>
                <Size Optional=""true"">invalidListSize</Size>
                <Size Optional=""true"" Condition=""invalidCondition"">validListSize_0</Size>
                <Size>validListSize_1</Size>
                <ValueNode>list[$i]</ValueNode>
              </IndexListItems>
            </Expand>
          </Type>
        </AutoVisualizer>
        ";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var validArraySize_0 = RemoteValueFakeUtil.CreateSimpleInt("validListSize_9", 19);
            remoteValue.AddChild(validArraySize_0);

            var validArraySize_1 = RemoteValueFakeUtil.CreateSimpleInt("validListSize_1", 2);
            remoteValue.AddChild(validArraySize_1);

            remoteValue.AddValueFromExpression(
                "list[0U]", RemoteValueFakeUtil.CreateClass("string", "$11", "a"));
            remoteValue.AddValueFromExpression(
                "list[1U]", RemoteValueFakeUtil.CreateClass("string", "$12", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
        }

        [Test]
        public async Task IndexListItemsSizeWithInvalidSizeValueAsync()
        {
            var xml = @"
        <AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
          <Type Name=""ParentType"">
            <Expand HideRawView=""true"">
              <IndexListItems>
                <Size Optional=""false"">invalidListSize</Size>
                <Size>validArraySize</Size>
                <ValueNode>list[%i]</ValueNode>
              </IndexListItems>
            </Expand>
          </Type>
        </AutoVisualizer>
        ";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            var arraySize = RemoteValueFakeUtil.CreateSimpleInt("arraySize", 2);
            remoteValue.AddChild(arraySize);

            remoteValue.AddValueFromExpression(
                "list[0U]", RemoteValueFakeUtil.CreateClass("string", "$11", "a"));
            remoteValue.AddValueFromExpression(
                "list[1U]", RemoteValueFakeUtil.CreateClass("string", "$12", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("invalidListSize"));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("invalidListSize"));

            var raw = children[1];
            Assert.That(raw.DisplayName, Is.EqualTo("[Raw View]"));
            Assert.That(await raw.ValueAsync(), Is.EqualTo("parentValue"));

            var rawChildren = await raw.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(1));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("arraySize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("2"));
        }

        [Test]
        public void IndexListItemsSizeWithIncludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems>
        <Size IncludeView=""MyIncludeView"">listSize1</Size>
        <Size>listSize2</Size>
        <ValueNode>list[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize1", 1));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize2", 2));

            remoteValue.AddValueFromExpression("list[0U]",
                                               RemoteValueFakeUtil.CreateSimpleString("$11", "a"));
            remoteValue.AddValueFromExpression("list[1U]",
                                               RemoteValueFakeUtil.CreateSimpleString("$12", "b"));

            // listSize1=1 does not get included here, so it falls back to listSize2=2.
            IVariableInformation varInfo = CreateVarInfo(remoteValue, "");
            Assert.That(varInfo, Does.HaveChildrenWithValues("a", "b"));

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(varInfo, Does.HaveChildWithValue("a"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void IndexListItemsSizeWithExcludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems>
        <Size ExcludeView=""MyExcludeView"">listSize1</Size>
        <Size>listSize2</Size>
        <ValueNode>list[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize1", 1));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize2", 2));

            remoteValue.AddValueFromExpression("list[0U]",
                                               RemoteValueFakeUtil.CreateSimpleString("$11", "a"));
            remoteValue.AddValueFromExpression("list[1U]",
                                               RemoteValueFakeUtil.CreateSimpleString("$12", "b"));

            IVariableInformation varInfo = CreateVarInfo(remoteValue, "");
            Assert.That(varInfo, Does.HaveChildWithValue("a"));

            // listSize1=1 gets excluded here, so it falls back to listSize2=2.
            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(varInfo, Does.HaveChildrenWithValues("a", "b"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public async Task IndexListItemsWithSizeConditionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems>
        <Size Condition=""false"">listSize_0</Size>
        <Size Condition=""true"">listSize_1</Size>
        <Size>invalidExpression</Size>
        <ValueNode>list[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize_1", 2));
            remoteValue.AddValueFromExpression(
                "list[0U]", RemoteValueFakeUtil.CreateClass("string", "$10", "a"));
            remoteValue.AddValueFromExpression(
                "list[1U]", RemoteValueFakeUtil.CreateClass("string", "$11", "b"));

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
        }

        [Test]
        public async Task IndexListItemsWithValueNodeConditionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems>
        <Size>listSize</Size>
        <ValueNode Condition=""false"">list_0[$i]</ValueNode>
        <ValueNode Condition=""true"">list_1[$i]</ValueNode>
        <ValueNode>invalidList[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize", 2));
            remoteValue.AddValueFromExpression(
                "list_1[0U]", RemoteValueFakeUtil.CreateClass("string", "$10", "a"));
            remoteValue.AddValueFromExpression(
                "list_1[1U]", RemoteValueFakeUtil.CreateClass("string", "$11", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
        }

        [Test]
        public async Task IndexListItemsWithDynamicValueNodeConditionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems>
        <Size>listSize</Size>
        <ValueNode Condition=""$i % 2 == 0"">list_0[$i]</ValueNode>
        <ValueNode Condition=""$i % 2 == 1"">list_1[$i]</ValueNode>
        <ValueNode>invalidList[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue =
                RemoteValueFakeUtil.CreateClass("ParentType", "parentName", "parentValue");
            remoteValue.AddValueFromExpression("0U % 2 == 0", TRUE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("0U % 2 == 1", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("1U % 2 == 0", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("1U % 2 == 1", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize", 2));
            remoteValue.AddValueFromExpression(
                "list_0[0U]", RemoteValueFakeUtil.CreateClass("string", "$10", "a"));
            remoteValue.AddValueFromExpression(
                "list_1[1U]", RemoteValueFakeUtil.CreateClass("string", "$11", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
        }

        [Test]
        public async Task IndexListItemsWithoutValidValueNodeAsync()
        {

            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems>
        <Size>listSize</Size>
        <ValueNode Condition=""$i > 10"">list_0[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue =
                RemoteValueFakeUtil.CreateClass("ParentType", "parentName", "parentValue");
            remoteValue.AddValueFromExpression("0U > 10", FALSE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize", 1));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(children[0].Error, Is.EqualTo(true));
            Assert.That(await children[0].ValueAsync(), Does.Contain("No valid <ValueNode> found"));
        }

        [Test]
        public async Task IndexListItemsTypeMissingSizeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand>
      <IndexListItems>
        <ValueNode>list[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateClass("string", "siblingVar", "a"));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize", 2));
            remoteValue.AddValueFromExpression("list[0]", RemoteValueFakeUtil.CreateClass(
                "string", "$10", "a"));
            remoteValue.AddValueFromExpression("list[1]", RemoteValueFakeUtil.CreateClass(
                "string", "$11", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("<Size>"));

            var raw = children[1];
            Assert.That(raw.DisplayName, Is.EqualTo("[Raw View]"));
            Assert.That(await raw.ValueAsync(), Is.EqualTo("parentValue"));

            var rawChildren = await raw.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("siblingVar"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("listSize"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo("2"));
        }

        [Test]
        public async Task IndexListItemsTypeEmptySizeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand>
      <IndexListItems>
        <Size></Size>
        <ValueNode>list[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateClass("string", "siblingVar", "a"));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize", 2));
            remoteValue.AddValueFromExpression("list[0]", RemoteValueFakeUtil.CreateClass(
                "string", "$10", "a"));
            remoteValue.AddValueFromExpression("list[1]", RemoteValueFakeUtil.CreateClass(
                "string", "$11", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));

            var raw = children[1];
            Assert.That(raw.DisplayName, Is.EqualTo("[Raw View]"));
            Assert.That(await raw.ValueAsync(), Is.EqualTo("parentValue"));

            var rawChildren = await raw.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("siblingVar"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("listSize"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo("2"));
        }

        [Test]
        public async Task IndexListItemsTypeNullValueNodeTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand>
      <IndexListItems>
        <Size>listSize</Size>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateClass("string", "siblingVar", "a"));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize", 2));
            remoteValue.AddValueFromExpression("list[0]", RemoteValueFakeUtil.CreateClass(
                "string", "$10", "a"));
            remoteValue.AddValueFromExpression("list[1]", RemoteValueFakeUtil.CreateClass(
                "string", "$11", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));

            var raw = children[1];
            Assert.That(raw.DisplayName, Is.EqualTo("[Raw View]"));
            Assert.That(await raw.ValueAsync(), Is.EqualTo("parentValue"));

            var rawChildren = await raw.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("siblingVar"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("listSize"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo("2"));
        }

        [Test]
        public async Task IndexListItemsTypeEmptyValueNodeTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand>
      <IndexListItems>
        <Size>listSize</Size>
        <ValueNode></ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateClass(
                "string", "siblingVar", "a"));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize", 2));
            remoteValue.AddValueFromExpression("list[0]", RemoteValueFakeUtil.CreateClass(
                "string", "$10", "a"));
            remoteValue.AddValueFromExpression("list[1]", RemoteValueFakeUtil.CreateClass(
                "string", "$11", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("<ValueNode>"));

            var raw = children[1];
            Assert.That(raw.DisplayName, Is.EqualTo("[Raw View]"));
            Assert.That(await raw.ValueAsync(), Is.EqualTo("parentValue"));

            var rawChildren = await raw.GetAllChildrenAsync();
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("siblingVar"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("listSize"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo("2"));
        }

        [Test]
        public async Task IndexListItemsTypeWithTrueOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems Optional=""true"">
        <Size>invalidListSize</Size>
        <ValueNode>invalidList[$i]</ValueNode>
      </IndexListItems>
      <IndexListItems>
        <Size>validListSize</Size>
        <ValueNode>validList[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("validListSize", 2));
            remoteValue.AddValueFromExpression(
                "validList[0U]", RemoteValueFakeUtil.CreateClass("string", "$10", "a"));
            remoteValue.AddValueFromExpression(
                "validList[1U]", RemoteValueFakeUtil.CreateClass("string", "$11", "b"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("b"));
        }

        [Test]
        public async Task IndexListItemsTypeWithChildExpressionFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems>
        <Size>validListSize</Size>
        <ValueNode>validList[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("validListSize", 3));
            remoteValue.AddValueFromExpression("validList[0U]",
                                               RemoteValueFakeUtil.CreateSimpleChar("$10", 'a'));
            remoteValue.AddValueFromExpression("validList[2U]",
                                               RemoteValueFakeUtil.CreateSimpleChar("$12", 'c'));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ParentType"));
            Assert.That(children.Length, Is.EqualTo(3));

            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("a"));

            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Does.Contain("Error"));

            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("c"));
        }

        [Test]
        public void IndexListItemsWithIncludeView()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems IncludeView=""MyIncludeView"">
        <Size>listSize</Size>
        <ValueNode>list[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize", 1));
            remoteValue.AddValueFromExpression(
                "list[0U]", RemoteValueFakeUtil.CreateSimpleString("$0", "listItem"));

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.Not.HaveChildren());

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(varInfo, Does.HaveChildWithValue("listItem"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void IndexListItemsWithExcludeView()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <IndexListItems ExcludeView=""MyExcludeView"">
        <Size>listSize</Size>
        <ValueNode>list[$i]</ValueNode>
      </IndexListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("listSize", 1));
            remoteValue.AddValueFromExpression(
                "list[0U]", RemoteValueFakeUtil.CreateSimpleString("$0", "listItem"));

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildWithValue("listItem"));

            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(varInfo, Does.Not.HaveChildren());

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        #endregion

        #region Item

        [Test]
        public async Task SimpleItemTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <Item Name=""Child_0"">expression_0</Item>
      <Item Name=""Child_1"">expression_1</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateClass(
                "hiddenType", "hiddenVar", "hiddenValue"));
            remoteValue.AddValueFromExpression("expression_0",
                RemoteValueFakeUtil.CreateClass("dummyType", "dummyName", "value_0"));
            remoteValue.AddValueFromExpression("expression_1",
                RemoteValueFakeUtil.CreateClass("dummyType", "dummyName", "value_1"));

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("Child_0"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("value_0"));
            Assert.That(children[1].DisplayName, Is.EqualTo("Child_1"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("value_1"));
        }

        [Test]
        public async Task ItemTypeWithFalseOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand>
      <Item Name=""Child_0"" Optional=""false"">invalidExpression</Item>
      <Item Name=""Child_1"">validExpression</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateClass("string", "childVar", "a"));
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateClass("dummyType", "dummyName", "value_1"));

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("Child_0"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ParentType"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("invalidExpression"));

            Assert.That(children.Length, Is.EqualTo(3));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("invalidExpression"));

            Assert.That(children[1].DisplayName, Is.EqualTo("Child_1"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("value_1"));

            var rawInfo = children[2];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));
            Assert.That(await rawInfo.ValueAsync(), Is.EqualTo("parentValue"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(1));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("childVar"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("a"));
        }

        [Test]
        public async Task ItemTypeWithTrueOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <Item Name=""Child_0"" Optional=""true"">invalidExpression</Item>
      <Item Name=""Child_1"">validExpression</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateClass("string", "childVar", "a"));
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateSimpleLong("dummyName", 11358));

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("DEBUG"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("Child_0"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ParentType"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("invalidExpression"));

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("Child_1"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("11358"));
        }

        [Test]
        public void ItemTypeWithIncludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <Item IncludeView=""MyIncludeView"">""MyItemValue""</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddStringLiteral("MyItemValue");

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.Not.HaveChildren());

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(varInfo, Does.HaveChildWithValue("\"MyItemValue\""));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void ItemTypeWithExcludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ParentType"">
    <Expand HideRawView=""true"">
      <Item ExcludeView=""MyExcludeView"">""MyItemValue""</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ParentType", "parentName", "parentValue");
            remoteValue.AddStringLiteral("MyItemValue");

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildWithValue("\"MyItemValue\""));

            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(varInfo, Does.Not.HaveChildren());

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        #endregion

        #region LinkedListItems

        [Test]
        public async Task ExpandLinkedListItemsTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item2 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item2.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 12));
            item2.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS2);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(item2);

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS3);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 3));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("LinkedListType"));

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("11"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("12"));
        }

        [Test]
        public async Task ExpandLinkedListItemsTypeWithNoSizeDefinedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item2 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item2.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 12));
            item2.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS2);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(item2);

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS3);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("LinkedListType"));

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("11"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("12"));
        }

        [Test]
        public async Task LinkedListItemsWithValueNodeNameAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode Name=""DummyName"">_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("Name"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<LinkedListItems>"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<ValueNode>"));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS1);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 1));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("LinkedListType"));

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
        }

        [Test]
        public async Task LinkedListItemsWithMissingChildItemAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item2 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item2.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 12));
            item2.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS2);
            // No _value child added to |item1|.
            item1.AddChild(item2);

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS3);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 3));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("Node"));

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Does.Contain("<Error>"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("12"));
        }

        [Test]
        public async Task LinkedListItemsTypeSizeMismatchAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 4));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("4"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("2"));

            Assert.That(children.Length, Is.EqualTo(4));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("11"));
            Assert.That(children[2].DisplayName, Is.EqualTo("<Error>"));
            Assert.That(await children[2].ValueAsync(), Does.Contain("2"));
            Assert.That(await children[2].ValueAsync(), Does.Contain("4"));
            Assert.That(children[3].DisplayName, Is.EqualTo("<Error>"));
            Assert.That(await children[3].ValueAsync(), Does.Contain("2"));
            Assert.That(await children[3].ValueAsync(), Does.Contain("4"));
        }

        [Test]
        public async Task LinkedListItemsTypeWithEmptySizeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size></Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<Node>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<Size>"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("<Size>"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_listSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("2"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_head"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS2));
        }

        [Test]
        public async Task LinkedListItemsWithConditionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems Condition=""false"">
        <Size>_listSize_0</Size>
        <HeadPointer>_head_0</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
      <LinkedListItems Condition=""true"">
        <Size>_listSize_1</Size>
        <HeadPointer>_head_1</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head_1", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<Node>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize_1", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(2));

            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("11"));
        }

        [Test]
        public async Task LinkedListItemsSizeWithModuleNameAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size ModuleName=""DummyModule"">_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleName"));

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<Node>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("11"));
        }

        [Test]
        public async Task LinkedListItemsSizeWithModuleVersionMinAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size ModuleVersionMin=""1.2.3"">_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleVersionMin"));

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<Node>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("11"));
        }

        [Test]
        public async Task LinkedListItemsSizeWithModuleVersionMaxAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size ModuleVersionMax=""3.2.1"">_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleVersionMax"));

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<Node>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("11"));
        }

        [Test]
        public async Task LinkedListItemsSizeWithTrueOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size Optional=""true"">_invalidListSize</Size>
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<Node>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("DEBUG"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("_invalidListSize"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("11"));
        }

        [Test]
        public async Task LinkedListItemsSizeWithFalseOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size Optional=""false"">_invalidListSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<Node>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("LinkedList<Node>"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("_invalidListSize"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("_invalidListSize"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_listSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("2"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_head"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS2));
        }

        [Test]
        public void LinkedListItemsSizeWithIncludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size IncludeView=""MyIncludeView"">_listSize1</Size>
        <Size>_listSize2</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake item1 =
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            RemoteValueFake item0 =
                RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("LinkedList", "myList", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize1", 1));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize2", 2));
            remoteValue.AddChild(item0);

            // _listSize1=1 does not get included here, so it falls back to _listSize2=2.
            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildrenWithValues("10", "11"));

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(varInfo, Does.HaveChildWithValue("10"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void LinkedListItemsSizeWithExcludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size ExcludeView=""MyExcludeView"">_listSize1</Size>
        <Size>_listSize2</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake item1 =
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            RemoteValueFake item0 =
                RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("LinkedList", "myList", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize1", 1));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize2", 2));
            remoteValue.AddChild(item0);

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildWithValue("10"));

            // _listSize1=1 gets excluded here, so it falls back to _listSize2=2.
            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(varInfo, Does.HaveChildrenWithValues("10", "11"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task LinkedListItemsWithSizeConditionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size Condition=""false"">_invalidListSize</Size>
        <Size Condition=""true"">_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(traceLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(traceLogSpy.GetOutput(), Does.Not.Contain("DEBUG"));
            Assert.That(traceLogSpy.GetOutput(), Does.Not.Contain("_invalidListSize"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("11"));
        }

        [Test]
        public async Task LinkedListItemsWithMissingHeadPointerAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size>_listSize</Size>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<HeadPointer>"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("<HeadPointer>"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_listSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("2"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_head"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS2));
        }

        [Test]
        public async Task LinkedListItemsWithThisHeadPointerAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedListNode"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size>_listSize</Size>
        <HeadPointer>this</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 =
                RemoteValueFakeUtil.CreateClassPointer("LinkedListNode", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 =
                RemoteValueFakeUtil.CreateClassPointer("LinkedListNode", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            item0.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            item0.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(item0);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("11"));
        }

        [Test]
        public async Task LinkedListItemsWithMissingNextPointerAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<NextPointer>"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("<NextPointer>"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_listSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("2"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_head"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS2));
        }

        [Test]
        public async Task LinkedListItemsWithThisForValueNodeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>this</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo(MEM_ADDRESS2));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS1));
        }

        [Test]
        public async Task LinkedListItemsWithEmptyValueNodeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode></ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<ValueNode>"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("<ValueNode>"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_listSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("2"));
        }

        [Test]
        public async Task LinkedListItemsWithMissingValueNodeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<ValueNode>"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("<ValueNode>"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_listSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("2"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_head"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS2));
        }

        [Test]
        public async Task LinkedListItemsTypeNullValueNodeTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems>
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("<ValueNode>"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_listSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("2"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_head"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS2));
        }

        [Test]
        public async Task LinkedListItemsTypeOptionalWithChildExpressionFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems Optional=""true"">
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS2);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            // No next pointer.

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS3);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 3));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("11"));
            Assert.That(children[2].DisplayName, Is.EqualTo("<Warning>"));
            Assert.That(await children[2].ValueAsync(), Does.Contain("_next"));
        }

        [Test]
        public async Task LinkedListItemsTypeOptionalWithFirstChildExpressionFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems Optional=""true"">
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
      <Item Name=""Size"">_listSize</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item2 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item2.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 12));
            item2.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS2);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(item2);

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS3);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 3));
            // Missing link to head.

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("Size"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("3"));
        }

        [Test]
        public async Task LinkedListItemsWithChildValueExpressionFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems Optional=""true"">
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item2 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item2.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 12));
            item2.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS2);
            // Missing value node.
            item1.AddChild(item2);

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS3);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 3));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Does.Contain("Error"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("12"));
        }

        [Test]
        public async Task LinkedListItemsTypeWithTrueOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems Optional=""true"">
        <Size>_invalidListSize</Size>
        <HeadPointer>_invalidHead</HeadPointer>
        <NextPointer>_invalidNext</NextPointer>
        <ValueNode>_invalidValue</ValueNode>
      </LinkedListItems>
      <LinkedListItems>
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("DEBUG"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("LinkedList<int>"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("_invalidHead"));

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("11"));
        }

        [Test]
        public async Task LinkedListItemsTypeWithFalseOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList&lt;*&gt;"">
    <Expand HideRawView=""true"">
      <LinkedListItems Optional=""false"">
        <Size>_invalidListSize</Size>
        <HeadPointer>_invalidHead</HeadPointer>
        <NextPointer>_invalidNext</NextPointer>
        <ValueNode>_invalidValue</ValueNode>
      </LinkedListItems>
      <LinkedListItems>
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var item1 = RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS1);
            item1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 11));
            item1.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            var item0 = RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS2);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_value", 10));
            item0.AddChild(item1);

            var remoteValue = RemoteValueFakeUtil.CreateClass("LinkedList<int>", "myList", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 2));
            remoteValue.AddChild(item0);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(4));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("_invalidHead"));

            Assert.That(children[1].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("10"));

            Assert.That(children[2].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("11"));

            var rawInfo = children[3];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_listSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("2"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_head"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS2));
        }

        [Test]
        public void LinkedListItemsTypeWithIncludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList"">
    <Expand HideRawView=""true"">
      <LinkedListItems IncludeView=""MyIncludeView"">
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake item0 =
                RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS1);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleString("_value", "listValue"));
            item0.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("LinkedList", "myList", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 1));
            remoteValue.AddChild(item0);

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.Not.HaveChildren());

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(varInfo, Does.HaveChildWithValue("listValue"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void LinkedListItemsTypeWithExcludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""LinkedList"">
    <Expand HideRawView=""true"">
      <LinkedListItems ExcludeView=""MyExcludeView"">
        <Size>_listSize</Size>
        <HeadPointer>_head</HeadPointer>
        <NextPointer>_next</NextPointer>
        <ValueNode>_value</ValueNode>
      </LinkedListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake item0 =
                RemoteValueFakeUtil.CreateClassPointer("Node", "_head", MEM_ADDRESS1);
            item0.AddChild(RemoteValueFakeUtil.CreateSimpleString("_value", "listValue"));
            item0.AddChild(
                RemoteValueFakeUtil.CreateClassPointer("Node", "_next", MEM_ADDRESS_NULL));

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("LinkedList", "myList", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_listSize", 1));
            remoteValue.AddChild(item0);

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildWithValue("listValue"));

            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(varInfo, Does.Not.HaveChildren());

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        #endregion

        #region Synthetic

        [Test]
        public async Task ExpandSyntheticItemTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Size]"">
        <DisplayString Condition=""false"">invalidExpression</DisplayString>
        <DisplayString Condition=""true"">Mixed {validExpression}</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateSimpleInt("$17", 12358));

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[Size]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("Mixed 12358"));
        }

        [Test]
        public async Task SyntheticItemWithConditionAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Unexpected]"" Condition=""false"">
        <DisplayString>Unexpected</DisplayString>
      </Synthetic>
      <Synthetic Name=""[Expected]"" Condition=""true"">
        <DisplayString>Expected</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateSimpleInt("$17", 12358));

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[Expected]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("Expected"));
        }

        [Test]
        public async Task SyntheticItemWithCustomVisualizerAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Size]"">
      <CustomVisualizer VisualizerId=""AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE""/>
        <DisplayString>12358</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<CustomVisualizer>"));

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[Size]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("12358"));
        }

        [Test]
        public void SyntheticItemWithIncludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Size]"" IncludeView=""MyIncludeView"">
        <DisplayString>12358</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.Not.HaveChildren());

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(varInfo, Does.HaveChildWithValue("12358"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void SyntheticItemWithExcludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Size]"" ExcludeView=""MyExcludeView"">
        <DisplayString>12358</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildWithValue("12358"));

            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(varInfo, Does.Not.HaveChildren());

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public async Task SyntheticItemWithExpandAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Size]"" Expression=""SyntheticExpression"">
        <DisplayString>17</DisplayString>
        <Expand>
          <Item Name=""[SyntheticChild]"">childExpression</Item>
        </Expand>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("childExpression",
                RemoteValueFakeUtil.CreateSimpleInt("$10", 12358));

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[Size]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));

            var syntheticChildren = await children[0].GetAllChildrenAsync();
            Assert.That(syntheticChildren.Length, Is.EqualTo(1));
            Assert.That(syntheticChildren[0].DisplayName, Is.EqualTo("[SyntheticChild]"));
            Assert.That(await syntheticChildren[0].ValueAsync(), Is.EqualTo("12358"));
        }

        [Test]
        public async Task SyntheticItemWithoutExpandAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Size]"" Expression=""SyntheticExpression"">
        <DisplayString>17</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[Size]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));

            var syntheticChildren = await children[0].GetAllChildrenAsync();
            Assert.That(syntheticChildren.Length, Is.EqualTo(0));
        }

        [Test]
        public async Task SyntheticItemWithExpressionAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Size]"" Expression=""SyntheticExpression"">
        <DisplayString>12358</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("Expression"));

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[Size]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("12358"));
        }

        [Test]
        public async Task SyntheticItemWithTrueOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Unexpected]"" Optional=""true"">
        <DisplayString>{invalidExpression}</DisplayString>
      </Synthetic>
      <Item Name=""[Size]"">validExpression</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("actualChild", 23));

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateSimpleInt("$17", 12358));

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("DEBUG"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<Synthetic>"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("invalidExpression"));

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[Size]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("12358"));
        }

        [Test]
        public async Task SyntheticItemWithFalseOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Unexpected]"" Optional=""false"">
        <DisplayString>{invalidExpression}</DisplayString>
      </Synthetic>
      <Item Name=""[Size]"">validExpression</Item>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("actualChild", 23));

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("validExpression",
                RemoteValueFakeUtil.CreateSimpleInt("$17", 12358));

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("invalidExpression"));

            Assert.That(children.Length, Is.EqualTo(3));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("invalidExpression"));

            Assert.That(children[1].DisplayName, Is.EqualTo("[Size]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("12358"));

            var rawInfo = children[2];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(1));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("actualChild"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("23"));
        }

        [Test]
        public async Task SyntheticItemWithStringViewAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""int"">
    <StringView>""raw integer""</StringView>
  </Type>
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Size]"">
        <DisplayString>17</DisplayString>
        <StringView>validExpression</StringView>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var intRemoteValue = RemoteValueFakeUtil.CreateSimpleInt("$17", 12358);
            intRemoteValue.AddStringLiteral("raw integer");

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("validExpression", intRemoteValue);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[Size]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
            Assert.That(children[0].StringView, Is.EqualTo("raw integer"));
        }

        [Test]
        public async Task SyntheticItemWithoutDisplayStringAndStringViewAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""MySyntheticItem"">
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            IVariableInformation[] children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(1));

            // Make sure these don't explode.
            Assert.That(await children[0].ValueAsync(), Is.EqualTo(""));
            Assert.That(children[0].StringView, Is.EqualTo(""));
        }

        [Test]
        public async Task SyntheticItemWithModuleNameAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Size]"" ModuleName=""DummyModuleName"">
        <DisplayString>17</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleName"));

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[Size]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
        }

        [Test]
        public async Task SyntheticItemWithModuleVersionMinAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Size]"" ModuleVersionMin=""1.2.3"">
        <DisplayString>17</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleVersionMin"));

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[Size]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
        }

        [Test]
        public async Task SyntheticItemWitModuleVersionMaxAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <Expand HideRawView=""true"">
      <Synthetic Name=""[Size]"" ModuleVersionMax=""3.2.1"">
        <DisplayString>17</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleVersionMax"));

            var remoteValue = RemoteValueFakeUtil.CreateClass("MyCustomType", "myVar", "");
            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[Size]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("17"));
        }

        private void AddNullTreePointers(string typeName, string leftPointer,
            string rightPointer, params RemoteValueFake[] treeNodes)
        {
            foreach (RemoteValueFake f in treeNodes)
            {
                f.AddChild(RemoteValueFakeUtil.CreatePointer(typeName,
                    leftPointer, MEM_ADDRESS_NULL));
                f.AddChild(RemoteValueFakeUtil.CreatePointer(typeName,
                    rightPointer, MEM_ADDRESS_NULL));
            }
        }

        #endregion

        #region TreeItems

        [Test]
        public async Task ExpandTreeItemsTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21
            // 30  31  32  33

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS4);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            var node_30 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS2);
            node_30.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 30));

            var node_31 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_31.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 31));

            var node_32 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS3);
            node_32.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 32));

            var node_33 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS4);
            node_33.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 33));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            node_20.AddChild(node_30);
            node_20.AddChild(node_31);

            node_21.AddChild(node_32);
            node_21.AddChild(node_33);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight",
                node_30, node_31, node_32, node_33);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 7));
            remoteValue.AddChild(node_10);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(7));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("30"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("20"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("31"));
            Assert.That(children[3].DisplayName, Is.EqualTo("[3]"));
            Assert.That(await children[3].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[4].DisplayName, Is.EqualTo("[4]"));
            Assert.That(await children[4].ValueAsync(), Is.EqualTo("32"));
            Assert.That(children[5].DisplayName, Is.EqualTo("[5]"));
            Assert.That(await children[5].ValueAsync(), Is.EqualTo("21"));
            Assert.That(children[6].DisplayName, Is.EqualTo("[6]"));
            Assert.That(await children[6].ValueAsync(), Is.EqualTo("33"));
        }

        [Test]
        public async Task ExpandEmptyTreeItemsTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 0));
            remoteValue.AddChild(RemoteValueFakeUtil.CreatePointer(
                "TreeNode", "_pHead", MEM_ADDRESS_NULL));

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(0));
        }

        [Test]
        public async Task TreeItemsWithFalseOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems Optional=""false"">
        <Size>_invalidTreeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS3);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);
            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomTreeType"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("_invalidTreeSize"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("CustomTreeType"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("_invalidTreeSize"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_treeSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("3"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_pHead"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS3));
        }

        [Test]
        public async Task TreeItemsWithTrueOptionalAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems Optional=""true"">
        <Size>_invalidTreeSize</Size>
        <HeadPointer>_invalidHead</HeadPointer>
        <LeftPointer>_invalidLeft</LeftPointer>
        <RightPointer>_invalidRight</RightPointer>
        <ValueNode>_invalidValue</ValueNode>
      </TreeItems>
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS4);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("DEBUG"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomTreeType"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<TreeItems>"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("_invalidHead"));

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("20"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("21"));
        }

        [Test]
        public async Task TreeItemsWithFalseConditionalAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems Condition=""false"">
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS3);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(0));
        }

        [Test]
        public async Task TreeItemsWithInvalidSizeExpressionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_invalidTreeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS3);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomTreeType"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("_invalidTreeSize"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("CustomTreeType"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("_invalidTreeSize"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_treeSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("3"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_pHead"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS3));
        }

        [Test]
        public async Task TreeItemsWithNonIntSizeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS3);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateClass("string", "_treeSize", "three"));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("_treeSize"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("DEBUG"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("_treeSize"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("three"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_treeSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("three"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_pHead"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS3));
        }

        [Test]
        public async Task TreeItemsWithMissingSizeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS3);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("DEBUG"));

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("20"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("21"));
        }

        [Test]
        public async Task TreeItemsWithSizeMismatchAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS3);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 6));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("DEBUG"));

            Assert.That(children.Length, Is.EqualTo(6));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("20"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("21"));
            Assert.That(children[3].DisplayName, Is.EqualTo("<Error>"));
            Assert.That(await children[3].ValueAsync(), Does.Contain("6"));
            Assert.That(await children[3].ValueAsync(), Does.Contain("3"));
            Assert.That(children[4].DisplayName, Is.EqualTo("<Error>"));
            Assert.That(await children[4].ValueAsync(), Does.Contain("6"));
            Assert.That(await children[4].ValueAsync(), Does.Contain("3"));
            Assert.That(children[5].DisplayName, Is.EqualTo("<Error>"));
            Assert.That(await children[5].ValueAsync(), Does.Contain("6"));
            Assert.That(await children[5].ValueAsync(), Does.Contain("3"));
        }

        [Test]
        public async Task TreeItemsWithCustomSizeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS4);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            node_10.AddChild(node_20);

            AddNullTreePointers("TreeNode", "", "_pRight", node_10);
            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 1));
            remoteValue.AddChild(node_10);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("20"));
        }

        [Test]
        public async Task TreeItemsWithEmptyHeadPointerAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer></HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS3);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<HeadPointer>"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("<HeadPointer>"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_treeSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("3"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_pHead"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS3));
        }

        [Test]
        public async Task TreeItemsWithInvalidHeadPointerAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>_invalidHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS3);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("_invalidHead"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("_invalidHead"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_treeSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("3"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_pHead"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS3));
        }

        [Test]
        public async Task TreeItemsWithThisAsHeadPointerAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>this</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));
            remoteValue.AddChild(node_20);
            remoteValue.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("20"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("21"));
        }

        [Test]
        public async Task TreeItemsWithEmptyLeftPointerAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer></LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS3);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS1);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS2);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<LeftPointer>"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("<LeftPointer>"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_treeSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("3"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_pHead"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS3));
        }

        [Test]
        public async Task TreeItemsWithEmptyRightPointerAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer></RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS1);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS2);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS3);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<RightPointer>"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("<RightPointer>"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_treeSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("3"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_pHead"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS1));
        }

        [Test]
        public async Task TreeItemsWithEmptyValueNodeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode></ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS1);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS2);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS3);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<ValueNode>"));

            Assert.That(children.Length, Is.EqualTo(2));

            var errInfo = children[0];
            Assert.That(errInfo.DisplayName, Does.Contain("Error"));
            Assert.That(await errInfo.ValueAsync(), Does.Contain("<ValueNode>"));

            var rawInfo = children[1];
            Assert.That(rawInfo.DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await rawInfo.GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_treeSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("3"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_pHead"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS1));
        }

        [Test]
        public async Task TreeItemsWithInvalidValueNodeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       ??
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS1);
            // |node_10| does not have a _pValue child.

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS2);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS3);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomTreeType"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("[1]"));

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("20"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Does.Contain("<Error>"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("21"));
        }

        [Test]
        public async Task TreeItemsWithThisAsValueNodeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems>
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>this</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS1);
            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS2);
            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS3);

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo(MEM_ADDRESS2));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Does.Contain(MEM_ADDRESS1));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo(MEM_ADDRESS3));
        }

        [Test]
        public void TreeItemsWithIncludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems IncludeView=""MyIncludeView"">
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake head =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS1);
            head.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 99));
            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", head);

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 1));
            remoteValue.AddChild(head);

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.Not.HaveChildren());

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(varInfo, Does.HaveChildWithValue("99"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void TreeItemsWithExcludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems ExcludeView=""MyExcludeView"">
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake head =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS1);
            head.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 99));
            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", head);

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 1));
            remoteValue.AddChild(head);

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo, Does.HaveChildWithValue("99"));

            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(varInfo, Does.Not.HaveChildren());

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public async Task TreeItemsWithModuleNameAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems ModuleName=""DummyModule"">
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleName"));

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS1);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS2);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS3);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("20"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Does.Contain("10"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("21"));
        }

        [Test]
        public async Task TreeItemsWithModuleVersionMinAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems ModuleVersionMin=""1.2.3"">
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleVersionMin"));

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS1);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS2);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS3);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("20"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Does.Contain("10"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("21"));
        }

        [Test]
        public async Task TreeItemsWithModuleVersionMaxAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems ModuleVersionMax=""3.2.1"">
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ModuleVersionMax"));

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS1);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS2);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS3);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);

            nLogSpy.Clear();
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("20"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Does.Contain("10"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("21"));
        }

        [Test]
        public async Task TreeItemsOptionalWithChildExpressionFailedOnFirstValueAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand>
      <TreeItems Optional=""true"">
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS1);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS2);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS3);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            // link broken from 10 to 20.
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();
            Assert.That(children.Length, Is.EqualTo(1));

            Assert.That(children[0].DisplayName, Is.EqualTo("[Raw View]"));

            var rawChildren = await children[0].GetAllChildrenAsync();
            Assert.That(rawChildren.Length, Is.EqualTo(2));
            Assert.That(rawChildren[0].DisplayName, Is.EqualTo("_treeSize"));
            Assert.That(await rawChildren[0].ValueAsync(), Is.EqualTo("3"));
            Assert.That(rawChildren[1].DisplayName, Is.EqualTo("_pHead"));
            Assert.That(await rawChildren[1].ValueAsync(), Is.EqualTo(MEM_ADDRESS1));

        }

        [Test]
        public async Task TreeItemsOptionalWithChildExpressionFailedOnNotFirstItemAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems Optional=""true"">
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS1);
            node_10.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 10));

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS2);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS3);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            // Link broken from 10 to 21

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();
            Assert.That(children.Length, Is.EqualTo(3));

            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("20"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[2].DisplayName, Is.EqualTo("<Warning>"));
            Assert.That(await children[2].ValueAsync(), Does.Contain("_pRight"));
        }

        [Test]
        public async Task TreeItemsOptionalWithChildValueExpressionFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomTreeType"">
    <Expand HideRawView=""true"">
      <TreeItems Optional=""true"">
        <Size>_treeSize</Size>
        <HeadPointer>_pHead</HeadPointer>
        <LeftPointer>_pLeft</LeftPointer>
        <RightPointer>_pRight</RightPointer>
        <ValueNode>_pValue</ValueNode>
      </TreeItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            // Build the following tree:
            //       10
            //   20      21

            var node_10 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pHead", MEM_ADDRESS1);
            // Missing value node.

            var node_20 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pLeft", MEM_ADDRESS2);
            node_20.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 20));

            var node_21 =
                RemoteValueFakeUtil.CreateClassPointer("TreeNode", "_pRight", MEM_ADDRESS3);
            node_21.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_pValue", 21));

            node_10.AddChild(node_20);
            node_10.AddChild(node_21);

            AddNullTreePointers("TreeNode", "_pLeft", "_pRight", node_20, node_21);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomTreeType", "myTree", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_treeSize", 3));
            remoteValue.AddChild(node_10);

            remoteValue.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            remoteValue.AddValueFromExpression("true", TRUE_REMOTE_VALUE);

            var varInfo = CreateVarInfo(remoteValue);
            var children = await varInfo.GetAllChildrenAsync();
            Assert.That(children.Length, Is.EqualTo(3));

            Assert.That(children[0].DisplayName, Is.EqualTo("[0]"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("20"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[1]"));
            Assert.That(await children[1].ValueAsync(), Does.Contain("Error"));
            Assert.That(children[2].DisplayName, Is.EqualTo("[2]"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("21"));
        }

        #endregion

        #region CustomListItems

        [Test]
        public async Task CustomListNotNotSupportedWhenExperimentOffAsync()
        {
            // This controls the result of the function the custom list expander factory uses to
            // check if the experiment is on.
            compRoot.GetVsiService().DebuggerOptions[DebuggerOption.NATVIS_EXPERIMENTAL] =
                DebuggerOptionState.DISABLED;

            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClassPointer("List", "l", MEM_ADDRESS1);

            var children = await CreateVarInfo(list).GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(2));

            Assert.That(children[0].DisplayName, Does.Contain("Error"));
            Assert.That(await children[0].ValueAsync(), Does.Contain("List*"));
            Assert.That(await children[0].ValueAsync(), Does.Contain("CustomListItems"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[Raw View]"));

            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("List*"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomListItems"));
        }

        [Test]
        public async Task CustomListItemsTypeWithOptionalAndVariableDeclarationFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
        <Variable Name=""cur"" InitialValue=""this->head"" />
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClassPointer("List", "l", MEM_ADDRESS1);

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(0));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomListItems"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("cur"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("this->head"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
        }

        [Test]
        public async Task CustomListItemsTypeWithWrongIfElseOrderAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""false"">
        <Else />
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClassPointer("List", "l", MEM_ADDRESS1);

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Does.Contain("Error"));
            Assert.That(await children[0].ValueAsync(), Does.Contain("List*"));
            Assert.That(await children[0].ValueAsync(), Does.Contain("CustomListItems"));
            Assert.That(await children[0].ValueAsync(), Does.Contain("<If>"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[Raw View]"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("List*"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomListItems"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("<If>"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
        }

        [Test]
        public async Task CustomListItemsTypeWithOptionalAndLoopConditionFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
        <Loop Condition=""cur != nullptr"">
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("cur != nullptr",
                RemoteValueFakeUtil.CreateError("<invalid expression>"));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(0));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomListItems"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("cur != nullptr"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
        }

        [Test]
        public async Task CustomListItemsTypeWithOptionalAndExecFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
        <Exec>cur = cur->next</Exec>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("cur = cur->next",
                RemoteValueFakeUtil.CreateError("<invalid expression>"));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(0));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomListItems"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("cur = cur->next"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
        }

        [Test]
        public async Task CustomListItemsTypeWithOptionalAndExecConditionFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
        <Exec Condition=""exec_condition"">cur = cur->next</Exec>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("exec_condition",
                RemoteValueFakeUtil.CreateError("<invalid expression>"));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(0));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomListItems"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("exec_condition"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
        }

        [Test]
        public async Task CustomListItemsTypeWithOptionalAndIfConditionFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
        <If Condition=""if_cond""></If>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClassPointer("List", "l", MEM_ADDRESS1);
            list.AddValueFromExpression("if_cond",
                RemoteValueFakeUtil.CreateError("<invalid expression>"));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(0));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomListItems"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("if_cond"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
        }

        [Test]
        public async Task CustomListItemsTypeWithItemWithoutNameAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems>
        <Item>foo</Item>
        <Item Name=""named"">foo</Item>
        <Item>foo</Item>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("foo", RemoteValueFakeUtil.CreateSimpleInt("foo", -1));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            // Note that the index is inc'ed even if there's a named item in between.
            Assert.That(children.Length, Is.EqualTo(3));
            Assert.AreEqual(children[0].DisplayName, "[0]");
            Assert.AreEqual(children[1].DisplayName, "named");
            Assert.AreEqual(children[2].DisplayName, "[2]");
        }

        [Test]
        public async Task CustomListItemsTypeWithOptionalAndElseIfConditionFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
        <If Condition=""if_cond"">
            <Item Name=""{index}"">if_expr</Item>
        </If>
        <Elseif Condition=""elseif_cond""></Elseif>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("if_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("elseif_cond",
                RemoteValueFakeUtil.CreateError("<invalid expression>"));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(0));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomListItems"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("elseif_cond"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
        }

        [Test]
        public async Task CustomListItemsTypeWithOptionalAndItemConditionFailedAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
        <Item Name=""{index}"" Condition=""item_cond"">if_expr</Item>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("item_cond",
                RemoteValueFakeUtil.CreateError("<invalid expression>"));

            var varInfo = CreateVarInfo(list);

            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(0));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomListItems"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("item_cond"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
        }

        [Test]
        public async Task CustomListItemsWithOptionalAndErrorOnSecondItemAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
        <Item Name=""{index}"">item_expr</Item>
        <Item Name=""{index}"">item_expr2</Item>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("index",
                                        RemoteValueFakeUtil.CreateSimpleInt("tmp", 10));
            list.AddValueFromExpression("item_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 255));
            list.AddValueFromExpression("index",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 11));
            list.AddValueFromExpression("item_expr2",
                RemoteValueFakeUtil.CreateError("<invalid expression>"));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("10"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("255"));
            Assert.That(children[1].DisplayName, Does.Contain("<Error>"));
            Assert.That(await children[1].ValueAsync(), Does.Contain("item_expr2"));

            Assert.That(nLogSpy.GetOutput(), Does.Contain("CustomListItems"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("item_expr2"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
        }

        [Test]
        public async Task CustomListItemsWithRawViewAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""false"">
      <CustomListItems Optional=""true"">
        <Item Name=""{index}"">item_expr</Item>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("index",
                                        RemoteValueFakeUtil.CreateSimpleInt("tmp", 10));
            list.AddValueFromExpression("item_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 255));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("10"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("255"));
            Assert.That(children[1].DisplayName, Is.EqualTo("[Raw View]"));
        }

        [Test]
        public async Task CustomListItemsTypeWithItemAndExecConditionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
        <Variable Name=""cur"" InitialValue=""this->head"" />
        <Variable Name=""index"" InitialValue=""0"" />
        <Item Name=""{index}"">item_expr</Item>
        <Exec Condition=""exec_cond"">cur = cur->next</Exec>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            var cur_var = RemoteValueFakeUtil.CreatePointer("Node*", "head", MEM_ADDRESS1);
            list.AddValueFromExpression("auto cur=this->head; cur", cur_var);
            list.AddValueFromExpression("cur", cur_var);
            RemoteValue index_var = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", index_var);
            list.AddValueFromExpression("index", index_var);
            list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            list.AddValueFromExpression("item_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 15));
            list.AddValueFromExpression("exec_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();
            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("1"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("15"));
        }

        [Test]
        public async Task CustomListItemsTypeWithItemConditionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
        <Item Condition=""item_cond"" Name=""{index}"">item_expr</Item>
        <Item Name=""index: {item_index_expr}"">item_expr2</Item>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("item_cond",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("item_index_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 10));
            list.AddValueFromExpression("item_expr2",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 255));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();
            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("index: 10"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("255"));
        }

        [Test]
        public async Task CustomListItemsTypeWithBreakOutsideOfLoopAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
        <Item Name=""{index}"">item_expr</Item>
        <Break Condition=""break_cond""/>
        <Item Name=""{index}"">item_expr2</Item>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            list.AddValueFromExpression("item_expr",
                                        RemoteValueFakeUtil.CreateSimpleInt("tmp", 42));
            list.AddValueFromExpression("break_cond",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", true));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();
            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("1"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("42"));
        }

        [Test]
        public async Task CustomListItemsTypeWithNestedLoopsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems>
        <Variable Name=""cur"" InitialValue=""this->head"" />
        <Variable Name=""index"" InitialValue=""0"" />
        <Loop Condition=""outerloop_cond"">
            <Loop Condition=""innerloop_cond"">
                <Item Name=""{item_name}"">item_expr</Item>
            </Loop>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            // Variable declarations
            var cur_var = RemoteValueFakeUtil.CreatePointer("Node*", "head", MEM_ADDRESS1);
            list.AddValueFromExpression("auto cur=this->head; cur", cur_var);
            list.AddValueFromExpression("cur", cur_var);
            var index_var = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", index_var);
            list.AddValueFromExpression("index", index_var);
            // First outer loop iteration
            list.AddValueFromExpression("outerloop_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            // First inner loop iteration
            list.AddValueFromExpression("innerloop_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("item_name",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 0));
            list.AddValueFromExpression("item_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 0));
            // Second inner loop iteration
            list.AddValueFromExpression("innerloop_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("item_name",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            list.AddValueFromExpression("item_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            // End inner loop iterations
            list.AddValueFromExpression("innerloop_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            // Second outer loop iteration
            list.AddValueFromExpression("outerloop_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            // First inner loop iteration
            list.AddValueFromExpression("innerloop_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("item_name",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 10));
            list.AddValueFromExpression("item_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 10));
            // Second inner loop iteration
            list.AddValueFromExpression("innerloop_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("item_name",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 11));
            list.AddValueFromExpression("item_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 11));
            // End inner loop iterations
            list.AddValueFromExpression("innerloop_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            // End out loop iterations
            list.AddValueFromExpression("outerloop_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(4));
            Assert.That(children[0].DisplayName, Is.EqualTo("0"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(children[1].DisplayName, Is.EqualTo("1"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
            Assert.That(children[2].DisplayName, Is.EqualTo("10"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("10"));
            Assert.That(children[3].DisplayName, Is.EqualTo("11"));
            Assert.That(await children[3].ValueAsync(), Is.EqualTo("11"));
        }

        [Test]
        public async Task CustomListItemsTypeWithBreakFromInnerLoopAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems>
        <Loop Condition=""outerloop_cond"">
            <Loop>
                <Break Condition=""innerloop_break""/>
                <Item>item_expr</Item>
            </Loop>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            // First outer loop iteration
            list.AddValueFromExpression("outerloop_cond",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            // First inner loop iteration
            list.AddValueFromExpression("innerloop_break",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("item_expr", RemoteValueFakeUtil.CreateSimpleInt("tmp", 0));
            // Second inner loop iteration
            list.AddValueFromExpression("innerloop_break",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("item_expr", RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            // End inner loop iterations
            list.AddValueFromExpression("innerloop_break",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            // Second outer loop iteration
            list.AddValueFromExpression("outerloop_cond",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            // First inner loop iteration
            list.AddValueFromExpression("innerloop_break",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("item_expr",
                                        RemoteValueFakeUtil.CreateSimpleInt("tmp", 10));
            // Second inner loop iteration
            list.AddValueFromExpression("innerloop_break",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("item_expr",
                                        RemoteValueFakeUtil.CreateSimpleInt("tmp", 11));
            // End inner loop iteration
            list.AddValueFromExpression("innerloop_break",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            // End outer loop iteration
            list.AddValueFromExpression("outerloop_cond",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", false));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(4));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("10"));
            Assert.That(await children[3].ValueAsync(), Is.EqualTo("11"));
        }

        [Test]
        public async Task CustomListItemsWithBreakInsideOfConditionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems>
        <Loop>
            <If Condition=""condition"">
                <Break/>
            </If>
            <Item>item_expr</Item>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            // First loop iteration
            list.AddValueFromExpression("condition",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("item_expr", RemoteValueFakeUtil.CreateSimpleInt("tmp", 2));
            // Second loop iteration
            list.AddValueFromExpression("condition",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("item_expr", RemoteValueFakeUtil.CreateSimpleInt("tmp", 4));
            // End loop
            list.AddValueFromExpression("condition",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", true));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("2"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("4"));
        }

        [Test]
        public async Task CustomListItemsTypeWithLoopAndExecAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems>
        <Variable Name=""cur"" InitialValue=""this->head"" />
        <Variable Name=""index"" InitialValue=""0"" />
        <Loop Condition=""cur != nullptr"">
            <Item Name=""{index}"">cur->value</Item>
            <Exec>cur = cur->next</Exec>
            <Exec>index++</Exec>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            var cur_var = RemoteValueFakeUtil.CreatePointer("Node*", "head", MEM_ADDRESS1);
            list.AddValueFromExpression("auto cur=this->head; cur", cur_var);
            list.AddValueFromExpression("cur", cur_var);
            RemoteValue index_var = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", index_var);
            list.AddValueFromExpression("index", index_var);
            // First loop iteration
            list.AddValueFromExpression("cur != nullptr",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("index",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 0));
            list.AddValueFromExpression("cur->value",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 2));
            list.AddValueFromExpression("cur = cur->next",
                RemoteValueFakeUtil.CreatePointer("Node*", "next", MEM_ADDRESS2));
            list.AddValueFromExpression("index++",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            // Second loop iteration
            list.AddValueFromExpression("cur != nullptr",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("index",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            list.AddValueFromExpression("cur->value",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 4));
            list.AddValueFromExpression("cur = cur->next",
                RemoteValueFakeUtil.CreatePointer("Node*", "next", MEM_ADDRESS3));
            list.AddValueFromExpression("index++",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 2));
            // End loop
            list.AddValueFromExpression("cur != nullptr",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("0"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("2"));
            Assert.That(children[1].DisplayName, Is.EqualTo("1"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("4"));
        }

        [Test]
        public async Task CustomListItemsTypeWithLoopExecAndBreakAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems>
        <Variable Name=""index"" InitialValue=""0"" />
        <Loop>
            <Break Condition=""break_condition"" />
            <Item Name=""{index}"">item_expr</Item>
            <Exec>index++</Exec>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            // Variable declarations
            RemoteValue index_var = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", index_var);
            list.AddValueFromExpression("index", index_var);

            // First loop iteration
            list.AddValueFromExpression("break_condition",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("tmp", 0));
            list.AddValueFromExpression("item_expr", RemoteValueFakeUtil.CreateSimpleInt("tmp", 2));
            list.AddValueFromExpression("index++", RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            // Second loop iteration
            list.AddValueFromExpression("break_condition",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            list.AddValueFromExpression("item_expr", RemoteValueFakeUtil.CreateSimpleInt("tmp", 4));
            list.AddValueFromExpression("index++", RemoteValueFakeUtil.CreateSimpleInt("tmp", 2));
            // End loop
            list.AddValueFromExpression("break_condition",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", true));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].DisplayName, Is.EqualTo("0"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("2"));
            Assert.That(children[1].DisplayName, Is.EqualTo("1"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("4"));
        }

        [Test]
        public async Task CustomListItemsTypeWithLoopExecIfElseifElseAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems>
        <Variable Name=""cur"" InitialValue=""this->head"" />
        <Variable Name=""index"" InitialValue=""0"" />
        <Loop Condition=""cur != nullptr"">
            <If Condition=""if_cond"">
                <Item Name=""{index}"">if_expr</Item>
            </If>
            <Elseif Condition=""elseif_cond"">
                <Item Name=""{index}"">elseif_expr</Item>
            </Elseif>
            <Else>
                <Item Name=""{index}"">else_expr</Item>
            </Else>
            <Exec>cur = cur->next</Exec>
            <Exec>index++</Exec>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            var cur_var = RemoteValueFakeUtil.CreatePointer("Node*", "head", MEM_ADDRESS1);
            list.AddValueFromExpression("auto cur=this->head; cur", cur_var);
            list.AddValueFromExpression("cur", cur_var);
            var index_var = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", index_var);
            list.AddValueFromExpression("index", index_var);
            // First loop iteration
            list.AddValueFromExpression("cur != nullptr",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("if_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("index",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 0));
            list.AddValueFromExpression("if_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 100));
            list.AddValueFromExpression("cur = cur->next",
                RemoteValueFakeUtil.CreatePointer("Node*", "next", MEM_ADDRESS2));
            list.AddValueFromExpression("index++",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            // Second loop iteration
            list.AddValueFromExpression("cur != nullptr",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("if_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("elseif_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("index",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            list.AddValueFromExpression("elseif_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 200));
            list.AddValueFromExpression("cur = cur->next",
                RemoteValueFakeUtil.CreatePointer("Node*", "next", MEM_ADDRESS2));
            list.AddValueFromExpression("index++",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            // Third loop iteration
            list.AddValueFromExpression("cur != nullptr",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("if_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("elseif_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("index",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 2));
            list.AddValueFromExpression("else_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 300));
            list.AddValueFromExpression("cur = cur->next",
                RemoteValueFakeUtil.CreatePointer("Node*", "next", MEM_ADDRESS2));
            list.AddValueFromExpression("index++",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            // End loop
            list.AddValueFromExpression("cur != nullptr",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("0"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("100"));
            Assert.That(children[1].DisplayName, Is.EqualTo("1"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("200"));
            Assert.That(children[2].DisplayName, Is.EqualTo("2"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("300"));
        }

        [Test]
        public async Task CustomListItemsTypeWithLoopExecConditionAndBreakAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems>
        <Variable Name=""cur"" InitialValue=""this->head"" />
        <Variable Name=""index"" InitialValue=""0"" />
        <Loop>
            <Break Condition=""cur == nullptr"" />
            <If Condition=""if_cond"">
                <Item Name=""{index}"">if_expr</Item>
            </If>
            <Elseif Condition=""elseif_cond"">
                <Item Name=""{index}"">elseif_expr</Item>
            </Elseif>
            <Else>
                <Item Name=""{index}"">else_expr</Item>
            </Else>
            <Exec>cur = cur->next</Exec>
            <Exec>index++</Exec>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            // Variable declarations
            var cur_var = RemoteValueFakeUtil.CreatePointer("Node*", "head", MEM_ADDRESS1);
            list.AddValueFromExpression("auto cur=this->head; cur", cur_var);
            list.AddValueFromExpression("cur", cur_var);
            var index_var = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", index_var);
            list.AddValueFromExpression("index", index_var);
            // First loop iteration
            list.AddValueFromExpression("cur == nullptr",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("if_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("index",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 0));
            list.AddValueFromExpression("if_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 100));
            list.AddValueFromExpression("cur = cur->next",
                RemoteValueFakeUtil.CreatePointer("Node*", "next", MEM_ADDRESS2));
            list.AddValueFromExpression("index++",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            // Second loop iteration
            list.AddValueFromExpression("cur == nullptr",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("if_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("elseif_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
            list.AddValueFromExpression("index",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            list.AddValueFromExpression("elseif_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 200));
            list.AddValueFromExpression("cur = cur->next",
                RemoteValueFakeUtil.CreatePointer("Node*", "next", MEM_ADDRESS2));
            list.AddValueFromExpression("index++",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            // Third loop iteration
            list.AddValueFromExpression("cur == nullptr",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("if_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("elseif_cond",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));
            list.AddValueFromExpression("index",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 2));
            list.AddValueFromExpression("else_expr",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 300));
            list.AddValueFromExpression("cur = cur->next",
                RemoteValueFakeUtil.CreatePointer("Node*", "next", MEM_ADDRESS2));
            list.AddValueFromExpression("index++",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 1));
            // End loop
            list.AddValueFromExpression("cur == nullptr",
                    RemoteValueFakeUtil.CreateSimpleBool("tmp", true));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("0"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("100"));
            Assert.That(children[1].DisplayName, Is.EqualTo("1"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("200"));
            Assert.That(children[2].DisplayName, Is.EqualTo("2"));
            Assert.That(await children[2].ValueAsync(), Is.EqualTo("300"));
        }

        [Test]
        public async Task CustomListItemsNonOptionalFailureFirstElementAsync()
        {

            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""false"">
        <If Condition=""if_cond""></If>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("if_cond",
                                        RemoteValueFakeUtil.CreateError("<invalid expression>"));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(2));

            Assert.That(children[0].DisplayName, Is.EqualTo("<Error>"));
            Assert.That(await children[0].ValueAsync(), Does.Contain("List"));
            Assert.That(await children[0].ValueAsync(), Does.Contain("if_cond"));

            Assert.That(children[1].DisplayName, Is.EqualTo("[Raw View]"));

            Assert.That(nLogSpy.GetOutput(), Does.Contain("List"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("if_cond"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
        }

        [Test]
        public async Task CustomListItemsFirstElementFailureWhenOptionalAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""true"">
        <If Condition=""if_cond""></If>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClassPointer("List", "l", MEM_ADDRESS1);

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(0));

            Assert.That(nLogSpy.GetOutput(), Does.Contain("List"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("if_cond"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
        }

        [Test]
        public async Task CustomListItemsOptionalFailureAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""false"">
        <Item Name=""Test"">item_expr</Item>
        <If Condition=""if_cond""></If>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("item_expr",
                                        RemoteValueFakeUtil.CreateSimpleInt("tmp", 100));
            list.AddValueFromExpression("if_cond",
                RemoteValueFakeUtil.CreateError("<invalid expression>"));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(3));

            Assert.That(children[0].DisplayName, Is.EqualTo("Test"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("100"));

            Assert.That(children[1].DisplayName, Is.EqualTo("<Error>"));
            Assert.That(await children[1].ValueAsync(), Does.Contain("List"));
            Assert.That(await children[1].ValueAsync(), Does.Contain("if_cond"));

            Assert.That(children[2].DisplayName, Is.EqualTo("[Raw View]"));

            Assert.That(nLogSpy.GetOutput(), Does.Contain("List"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("if_cond"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("ERROR"));
        }

        [Test]
        public async Task CustomListItemsItemNameHexadecimalDisplayAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Optional=""false"">
        <Variable Name=""var"" InitialValue=""0"" />
        <Item Name=""{var}"">foo</Item>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            // Check that hex display for <Item Name="{var}"> works. 175 is hex AF!
            list.AddValueFromExpression("auto var=0; var",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 175));
            list.AddValueFromExpression("var",
                RemoteValueFakeUtil.CreateSimpleInt("tmp", 175));
            list.AddValueFromExpression("foo",
                RemoteValueFakeUtil.CreateSimpleInt("foo", 42));

            var varInfo = CreateVarInfo(list);
            varInfo.FallbackValueFormat = ValueFormat.Hex;
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("0xaf"));
        }

        [Test]
        public async Task CustomListItemsTypeWithConditionAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems Condition=""false"">
        <Item>item1</Item>
      </CustomListItems>
      <CustomListItems Condition=""true"">
        <Item>item2</Item>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake list =
                RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddStringLiteral("MyItem");
            list.AddValueFromExpression("false", FALSE_REMOTE_VALUE);
            list.AddValueFromExpression("true", TRUE_REMOTE_VALUE);
            list.AddValueFromExpression("item2",
                RemoteValueFakeUtil.CreateSimpleInt("item", 42));

            IVariableInformation varInfo = CreateVarInfo(list);
            IVariableInformation[] children = await varInfo.GetAllChildrenAsync();
            Assert.That(children.Length, Is.EqualTo(1));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("42"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void CustomListItemsTypeWithIncludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems IncludeView=""MyIncludeView"">
        <Item>""MyItem""</Item>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake list =
                RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("\"MyItem\"",
                                        RemoteValueFakeUtil.CreateSimpleString("MyItem", "MyItem"));
            list.AddStringLiteral("MyItem");

            IVariableInformation varInfo = CreateVarInfo(list);
            Assert.That(varInfo, Does.Not.HaveChildren());

            varInfo = CreateVarInfo(list, "view(MyIncludeView)");
            Assert.That(varInfo, Does.HaveChildWithValue("MyItem"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void CustomListItemsTypeWithExcludeViewAttribute()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems ExcludeView=""MyExcludeView"">
        <Item>""MyItem""</Item>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake list =
                RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            list.AddValueFromExpression("\"MyItem\"",
                    RemoteValueFakeUtil.CreateSimpleString("MyItem", "MyItem"));
            list.AddStringLiteral("MyItem");

            IVariableInformation varInfo = CreateVarInfo(list);
            Assert.That(varInfo, Does.HaveChildWithValue("MyItem"));

            varInfo = CreateVarInfo(list, "view(MyExcludeView)");
            Assert.That(varInfo, Does.Not.HaveChildren());

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task CustomListItemsNameEscapeCurlyBracesAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
    <Type Name=""List"">
      <Expand HideRawView=""true"">
      <CustomListItems>
        <Item Name=""{{Test: {index}}}"">index</Item>
        <Item Name=""{{Test: {index}}}}"">index</Item>
        <Item Name=""{{Test: {index}}} {{}"">index</Item>
      </CustomListItems>
    </Expand>
    </Type>
</AutoVisualizer>";

            LoadFromString(xml);

            RemoteValueFake list =
                RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");
            list.AddStringLiteral("MyItem");

            list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("index", 0));
            list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("index", 0));
            list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("index", 1));
            list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("index", 1));
            list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("index", 2));
            list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("index", 2));

            IVariableInformation varInfo = CreateVarInfo(list);
            IVariableInformation[] children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(3));
            Assert.That(children[0].DisplayName, Is.EqualTo("{Test: 0}"));
            Assert.That(children[1].DisplayName, Is.EqualTo("{Test: 1}}"));
            Assert.That(children[2].DisplayName, Is.EqualTo("{Test: 2} {}"));
        }

        [Test]
        public async Task CustomListItemsMaxItemsPerViewDefaultAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""Range"">
    <Expand HideRawView=""true"">
      <CustomListItems>
        <Variable Name=""index"" InitialValue=""0"" />
        <Loop Condition=""index &lt; 100"">
          <Item>index</Item>
          <Exec>index += 1</Exec>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("Range", "myRange", "myValue");

            RemoteValue index_var = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", index_var);
            list.AddValueFromExpression("index", index_var);

            for (int i = 0; i < 100; i++)
            {
                list.AddValueFromExpression(
                    "index < 100",
                    RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
                list.AddValueFromExpression(
                    "index",
                    RemoteValueFakeUtil.CreateSimpleInt("tmp", i));
                list.AddValueFromExpression("index += 1",
                                            RemoteValueFakeUtil.CreateSimpleInt("tmp", i + 1));
            }
            list.AddValueFromExpression(
                "index < 100",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(21));

            for (int i = 0; i < children.Length - 1; i++)
            {
                Assert.That(await children[i].ValueAsync(), Is.EqualTo(i.ToString()));
            }
            Assert.That(children[children.Length - 1].DisplayName, Is.EqualTo("[More]"));

            // Check that only the requested number of elements was evaluated.
            Assert.That(list.ExpressionValues["index"].Count, Is.EqualTo(79));
            Assert.That(list.ExpressionValues["index < 100"].Count, Is.EqualTo(80));
            Assert.That(list.ExpressionValues["index += 1"].Count, Is.EqualTo(80));
        }

        [Test]
        public async Task CustomListItemsMaxItemsPerViewAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""Range"">
    <Expand HideRawView=""true"">
      <CustomListItems MaxItemsPerView=""5"">
        <Variable Name=""index"" InitialValue=""0"" />
        <Loop Condition=""index &lt; 100"">
          <Item>index</Item>
          <Exec>index += 1</Exec>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("Range", "myRange", "myValue");

            RemoteValue index_var = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", index_var);
            list.AddValueFromExpression("index", index_var);

            for (int i = 0; i < 100; i++)
            {
                list.AddValueFromExpression(
                    "index < 100",
                    RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
                list.AddValueFromExpression(
                    "index",
                    RemoteValueFakeUtil.CreateSimpleInt("tmp", i));
                list.AddValueFromExpression(
                    "index += 1",
                    RemoteValueFakeUtil.CreateSimpleInt("tmp", i + 1));
            }
            list.AddValueFromExpression(
                "index < 100",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(6));

            for (int i = 0; i < children.Length - 1; i++)
            {
                Assert.That(await children[i].ValueAsync(), Is.EqualTo(i.ToString()));
            }
            Assert.That(children[children.Length - 1].DisplayName, Is.EqualTo("[More]"));

            // Check that only the requested number of elements was evaluated.
            Assert.That(list.ExpressionValues["index"].Count, Is.EqualTo(94));
            Assert.That(list.ExpressionValues["index < 100"].Count, Is.EqualTo(95));
            Assert.That(list.ExpressionValues["index += 1"].Count, Is.EqualTo(95));
        }

        [Test]
        public async Task CustomListItemsMaxItemsPerViewNotEnoughItemsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""Range"">
    <Expand HideRawView=""true"">
      <CustomListItems MaxItemsPerView=""5"">
        <Variable Name=""index"" InitialValue=""0"" />
        <Loop Condition=""index &lt; 2"">
          <Item>index</Item>
          <Exec>index += 1</Exec>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("Range", "myRange", "myValue");

            RemoteValue index_var = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", index_var);
            list.AddValueFromExpression("index", index_var);

            for (int i = 0; i < 2; i++)
            {
                list.AddValueFromExpression(
                    "index < 2",
                    RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
                list.AddValueFromExpression(
                    "index",
                    RemoteValueFakeUtil.CreateSimpleInt("tmp", i));
                list.AddValueFromExpression(
                    "index += 1",
                    RemoteValueFakeUtil.CreateSimpleInt("tmp", i + 1));
            }
            list.AddValueFromExpression(
                "index < 2",
                RemoteValueFakeUtil.CreateSimpleBool("tmp", false));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(2));

            for (int i = 0; i < children.Length; i++)
            {
                Assert.That(await children[i].ValueAsync(), Is.EqualTo(i.ToString()));
            }
        }

        [Test]
        public async Task CustomListItemsSizeAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems>
        <Variable Name=""index"" InitialValue=""0"" />
        <Size>5</Size>
        <Loop>
          <Item>index</Item>
          <Exec>index += 1</Exec>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            RemoteValue indexVar = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", indexVar);
            list.AddValueFromExpression("index", indexVar);

            list.AddValueFromExpression("5", RemoteValueFakeUtil.CreateSimpleInt("size", 5));

            for (int i = 0; i < 5; i++)
            {
                list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("tmp", i));
                list.AddValueFromExpression("index += 1",
                                            RemoteValueFakeUtil.CreateSimpleInt("tmp", i + 1));
            }

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(5));
            for (int i = 0; i < children.Length; i++)
            {
                Assert.That(await children[i].ValueAsync(), Is.EqualTo(i.ToString()));
            }
        }

        [Test]
        public async Task CustomListItemsSizeWithConditionsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems>
        <Variable Name=""index"" InitialValue=""0"" />
        <Size Condition=""false"">5</Size>
        <Size Condition=""true"">6</Size>
        <Loop>
          <Item>index</Item>
          <Exec>index += 1</Exec>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            RemoteValue indexVar = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", indexVar);
            list.AddValueFromExpression("index", indexVar);

            // Conditions and size
            list.AddValueFromExpression("false",
                                        RemoteValueFakeUtil.CreateSimpleBool("false", false));
            list.AddValueFromExpression("true", RemoteValueFakeUtil.CreateSimpleBool("true", true));
            list.AddValueFromExpression("6", RemoteValueFakeUtil.CreateSimpleInt("size", 6));

            for (int i = 0; i < 6; i++)
            {
                list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("tmp", i));
                list.AddValueFromExpression("index += 1",
                                            RemoteValueFakeUtil.CreateSimpleInt("tmp", i + 1));
            }

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(6));
            for (int i = 0; i < children.Length; i++)
            {
                Assert.That(await children[i].ValueAsync(), Is.EqualTo(i.ToString()));
            }
        }

        [Test]
        public async Task CustomListItemsUnsatisfiableSizeConditionsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems MaxItemsPerView=""20"">
        <Variable Name=""index"" InitialValue=""0"" />
        <Size Condition=""cond1"">5</Size>
        <Size Condition=""cond2"">6</Size>
        <Loop>
          <Item>index</Item>
          <Exec>index += 1</Exec>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            RemoteValue indexVar = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", indexVar);
            list.AddValueFromExpression("index", indexVar);

            list.AddValueFromExpression("cond1",
                                        RemoteValueFakeUtil.CreateSimpleBool("false", false));
            list.AddValueFromExpression("cond2",
                                        RemoteValueFakeUtil.CreateSimpleBool("false", false));

            for (int i = 0; i < 20; i++)
            {
                list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("tmp", i));
                list.AddValueFromExpression("index += 1",
                                            RemoteValueFakeUtil.CreateSimpleInt("tmp", i + 1));
            }

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(21));
            for (int i = 0; i < 20; i++)
            {
                Assert.That(await children[i].ValueAsync(), Is.EqualTo(i.ToString()));
            }
            Assert.That(children[20].DisplayName, Is.EqualTo("[More]"));
        }

        [Test]
        public async Task CustomListItemsInvalidSizeExpressionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems>
        <Variable Name=""index"" InitialValue=""0"" />
        <Size>invalidSizeExpr</Size>
        <Loop>
          <Item>index</Item>
          <Exec>index += 1</Exec>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            RemoteValue indexVar = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", indexVar);
            list.AddValueFromExpression("index", indexVar);

            list.AddValueFromExpression("invalidSizeExpr",
                                        RemoteValueFakeUtil.CreateError("error message"));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(2));
            Assert.That(children[0].Error, Is.EqualTo(true));
            Assert.That(await children[0].ValueAsync(), Does.Contain("error message"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("myValue")); // raw view
        }

        [Test]
        public async Task CustomListItemsBreakBeforeReachingSizeLimitAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""List"">
    <Expand HideRawView=""true"">
      <CustomListItems MaxItemsPerView=""3"">
        <Variable Name=""index"" InitialValue=""0"" />
        <Size>4</Size>
        <Loop Condition=""index &lt; 2"">
          <Item>index</Item>
          <Exec>index += 1</Exec>
        </Loop>
      </CustomListItems>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var list = RemoteValueFakeUtil.CreateClass("List", "myList", "myValue");

            RemoteValue indexVar = RemoteValueFakeUtil.CreateSimpleInt("index", 0);
            list.AddValueFromExpression("auto index=0; index", indexVar);
            list.AddValueFromExpression("index", indexVar);

            list.AddValueFromExpression("4", RemoteValueFakeUtil.CreateSimpleInt("size", 4));

            for (int i = 0; i < 2; i++)
            {
                list.AddValueFromExpression("index < 2",
                                            RemoteValueFakeUtil.CreateSimpleBool("tmp", true));
                list.AddValueFromExpression("index", RemoteValueFakeUtil.CreateSimpleInt("tmp", i));
                list.AddValueFromExpression("index += 1",
                                            RemoteValueFakeUtil.CreateSimpleInt("tmp", i + 1));
            }

            list.AddValueFromExpression("index < 2",
                                        RemoteValueFakeUtil.CreateSimpleBool("tmp", false));

            var varInfo = CreateVarInfo(list);
            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children.Length, Is.EqualTo(4));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("0"));
            Assert.That(await children[1].ValueAsync(), Is.EqualTo("1"));
            Assert.That(children[2].Error, Is.True);
            Assert.That(await children[2].ValueAsync(), Does.Contain("only 2 item(s) found"));
            Assert.That(children[3].DisplayName, Is.EqualTo("[More]"));

            var moreChildren = await children[3].GetAllChildrenAsync();
            Assert.That(moreChildren.Length, Is.EqualTo(1));
            Assert.That(moreChildren[0].Error, Is.True);
            Assert.That(await moreChildren[0].ValueAsync(), Does.Contain("only 2 item(s) found"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
        }

#endregion

#region VisualizerType

        [Test]
        public async Task VisualizerTypeWithIncludeViewAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"" IncludeView=""MyIncludeView"">
    <DisplayString>MyFormattedDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue =
                RemoteValueFakeUtil.CreateClass("MyCustomType", "dummyVarName", "actualValue");

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("actualValue"));

            varInfo = CreateVarInfo(remoteValue, "view(MyIncludeView)");
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("MyFormattedDisplayString"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task VisualizerTypeWithExcludeViewAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"" ExcludeView=""MyExcludeView"">
    <DisplayString>MyFormattedDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue =
                RemoteValueFakeUtil.CreateClass("MyCustomType", "dummyVarName", "actualValue");

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("MyFormattedDisplayString"));

            varInfo = CreateVarInfo(remoteValue, "view(MyExcludeView)");
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("actualValue"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task VisualizerTypeWithPriorityAttributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"" Priority=""Low"">
    <DisplayString>Low Priority Value</DisplayString>
  </Type>
  <Type Name=""MyCustomType"" Priority=""High"">
    <DisplayString>HighPriority Value</DisplayString>
  </Type>
  <Type Name=""MyCustomType"" Priority=""MediumLow"">
    <DisplayString>Medium Priority Value</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("Priority"));

            var remoteValue =
                RemoteValueFakeUtil.CreateClass("MyCustomType", "dummyVarName", "actualValue");

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("Low Priority Value"));
        }

        [Test]
        public async Task VisualizerTypeWithInheritablettributeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"" Inheritable=""true"">
    <DisplayString>MyFormattedDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("Inheritable"));

            var remoteValue =
                RemoteValueFakeUtil.CreateClass("MyCustomType", "dummyVarName", "actualValue");

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("MyFormattedDisplayString"));
        }

        [Test]
        public async Task TemplateTypeWildcardLookupAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ContainerType&lt;*&gt;"">
    <DisplayString>MyContainer</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ContainerType<int>", "container", "");

            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("MyContainer"));
        }

        [Test]
        public async Task TemplateTypeExpressionResolutionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ContainerType&lt;*,*&gt;"">
    <DisplayString>{($T1)($T2)data}</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var data = RemoteValueFakeUtil.CreateSimpleInt("$1", 7);
            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ContainerType<int, char>", "container", "");
            remoteValue.AddChild(RemoteValueFakeUtil.Create(
                "void*", TypeFlags.IS_POINTER, "data", ""));
            remoteValue.SetAddressOf(
                RemoteValueFakeUtil.CreateAddressOf(remoteValue, 0xDEADBEEF));
            remoteValue.AddValueFromExpression(
                "(int)(char)data", data);

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("7"));
        }

        [Test]
        public async Task RecursiveTemplateTypeWildcardLookupAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ContainerType&lt;*,ContainerType&lt;*,*&gt;&gt;"">
    <DisplayString>{($T1)($T2)($T3)data}</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var data = RemoteValueFakeUtil.CreateSimpleInt("$1", 7);
            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "ContainerType<int, ContainerType<char, float>>", "container", "");
            remoteValue.AddValueFromExpression("(int)(char)(float)data", data);

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("7"));
        }

        [Test]
        public async Task TemplateTypeSkipKnownTypenameAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ContainerType&lt;*,char,*&gt;"">
    <DisplayString>{($T1)($T2)data}</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var data = RemoteValueFakeUtil.CreateSimpleInt("$1", 7);
            var remoteValue =
                RemoteValueFakeUtil.CreateClass("ContainerType<int, char, float>", "container", "");
            remoteValue.AddValueFromExpression("(int)(float)data", data);

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("7"));
        }

        [Test]
        public async Task TemplateWildcardReplacesMultipleTypesAsync()
        {
            // TODO: The following visualizer shouldn't accept type e.g.
            // "TmplA<TmplB<int>, int>". This should be fixed by pre-compiling all Natvis
            // expressions.
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""TmplA&lt;TmplB&lt;*&gt;,*&gt;"">
    <DisplayString>{($T4)($T5)($T1 + $T2 + $T3)}</DisplayString>
  </Type>
</AutoVisualizer>
";

            LoadFromString(xml);

            var data = RemoteValueFakeUtil.CreateSimpleInt("0", 0);
            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "TmplA<TmplB<2, 3, 4>, TmplC<int>, float>", "var", "");
            remoteValue.AddValueFromExpression("(TmplC<int>)(float)(2 + 3 + 4)", data);

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("0"));
        }

        [Test]
        public async Task TemplateTypeAcceptNegativeConstantAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""ContainerType&lt;*&gt;"">
    <DisplayString>{$T1}</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var data = RemoteValueFakeUtil.CreateSimpleInt("$1", -15);
            var remoteValue =
                RemoteValueFakeUtil.CreateClass("ContainerType<-15>", "container", "");
            remoteValue.AddValueFromExpression("-15", data);

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("-15"));
        }

        [Test]
        public async Task DerivedTypeUsesBaseTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""BaseType"">
    <DisplayString>Base Type Display String</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var leafType = new SbTypeStub("LeafType", TypeFlags.IS_CLASS);
            var middleType = new SbTypeStub("MiddleType", TypeFlags.IS_CLASS);
            var baseType = new SbTypeStub("BaseType", TypeFlags.IS_CLASS);

            leafType.AddDirectBaseClass(middleType);
            middleType.AddDirectBaseClass(baseType);

            var leafValue = RemoteValueFakeUtil.CreateClass("DummyType", "myVar", "");
            leafValue.SetTypeInfo(leafType);

            var varInfo = CreateVarInfo(leafValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("myVar"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("Base Type Display String"));
        }

        [Test]
        public async Task DerivedTypeUsesClosestInheritedTypeAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""BaseType"">
    <DisplayString>Base Type Display String</DisplayString>
  </Type>
  <Type Name=""MiddleType"">
    <DisplayString>Middle Type Display String</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var leafType = new SbTypeStub("LeafType", TypeFlags.IS_CLASS);
            var middleType = new SbTypeStub("MiddleType", TypeFlags.IS_CLASS);
            var baseType = new SbTypeStub("BaseType", TypeFlags.IS_CLASS);

            leafType.AddDirectBaseClass(middleType);
            middleType.AddDirectBaseClass(baseType);

            var leafValue = RemoteValueFakeUtil.CreateClass("DummyType", "myVar", "");
            leafValue.SetTypeInfo(leafType);

            var varInfo = CreateVarInfo(leafValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("myVar"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("Middle Type Display String"));
        }

        [Test]
        public async Task DeepClassHierarchyIsNotExhaustedWhenResolvingVisualizerAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""type_200"">
    <DisplayString>Unexpected Display String</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var leafType = new SbTypeStub("type_0", TypeFlags.IS_CLASS);
            var curType = leafType;
            const int TOTAL_NUM_TYPES = 200;
            for (int i = 1; i <= TOTAL_NUM_TYPES; i++)
            {
                var parentType = new SbTypeStub($"type_{i}", TypeFlags.IS_CLASS);
                curType.AddDirectBaseClass(parentType);
                curType = parentType;
            }
            var leafValue = new RemoteValueFake("leafVar", "leafValue");
            leafValue.SetTypeInfo(leafType);

            var varInfo = CreateVarInfo(leafValue);

            Assert.That(varInfo.DisplayName, Is.EqualTo("leafVar"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("leafValue"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("WARNING"));
            Assert.That(nLogSpy.GetOutput(), Does.Contain("type_0"));

            Assert.That(varInfo.GetAllInheritedTypes().Select(t => t.GetName()),
                        Does.Contain("type_200"));
        }

        [Test]
        public async Task ReferenceTypeResolutionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString>Custom Display String</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue =
                RemoteValueFakeUtil.CreateClassReference("MyCustomType", "myVar", MEM_ADDRESS1);
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("myVar"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("Custom Display String"));
        }

        [Test]
        public async Task PointerTypeResolutionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""MyCustomType"">
    <DisplayString>Custom Display String</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue =
                RemoteValueFakeUtil.CreateClassPointer("MyCustomType", "myVar", MEM_ADDRESS1);
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.DisplayName, Is.EqualTo("myVar"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("Custom Display String"));
        }

        [Test]
        public async Task FallbackToCanonicalTypeVisualizerAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CanonicalType"">
    <DisplayString>Display Canonical Type</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue =
                RemoteValueFakeUtil.CreateClassAlias("TypeAlias", "CanonicalType", "myVar", "");
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(varInfo.TypeName, Is.EqualTo("TypeAlias"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("Display Canonical Type"));

            string logs = nLogSpy.GetOutput();
            Assert.That(logs, Does.Not.Contain("ERROR"));
            Assert.That(logs, Does.Contain("Natvis Visualizer for type 'TypeAlias'"));
            Assert.That(logs, Does.Contain("Natvis Visualizer for canonical type 'CanonicalType'"));
        }

        [Test]
        public async Task PreferAliasToCanonicalTypeNameAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CanonicalType"">
    <DisplayString>Display Canonical Type</DisplayString>
  </Type>
  <Type Name=""TypeAlias"">
    <DisplayString>Display Type Alias</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue =
                RemoteValueFakeUtil.CreateClassAlias("TypeAlias", "CanonicalType", "myVar", "");
            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(varInfo.TypeName, Is.EqualTo("TypeAlias"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("Display Type Alias"));

            string logs = nLogSpy.GetOutput();
            Assert.That(logs, Does.Not.Contain("ERROR"));
            Assert.That(logs, Does.Contain("Natvis Visualizer for type 'TypeAlias'"));
            Assert.That(logs,
                        Does.Not.Contain("Natvis Visualizer for canonical type 'CanonicalType'"));
        }

#endregion

#region MultiLevelExpansion

        [Test]
        public async Task MultipleLevelExpansionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""Point"">
    <DisplayString>({x}, {y})</DisplayString>
      <Expand>
        <Item Name=""x"">x</Item>
        <Item Name=""y"">y</Item>
      </Expand>
  </Type>

  <Type Name=""Box"">
    <DisplayString>[{p1}:{p2}]</DisplayString>
      <Expand>
        <Item Name=""Top Left"">p1</Item>
        <Item Name=""Bottom Right"">p2</Item>
      </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var p1 = RemoteValueFakeUtil.CreateClass("Point", "p1", "p1ActualValue");
            p1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("x", 10));
            p1.AddChild(RemoteValueFakeUtil.CreateSimpleInt("y", 11));

            var p2 = RemoteValueFakeUtil.CreateClass("Point", "p2", "p2ActualValue");
            p2.AddChild(RemoteValueFakeUtil.CreateSimpleInt("x", 20));
            p2.AddChild(RemoteValueFakeUtil.CreateSimpleInt("y", 21));

            var box = RemoteValueFakeUtil.CreateClass("Box", "myBox", "actualBoxValue");
            box.AddChild(p1);
            box.AddChild(p2);

            var varInfo = CreateVarInfo(box);
            var boxChildren = await varInfo.GetAllChildrenAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));

            // box

            Assert.That(boxChildren.Length, Is.EqualTo(3));
            Assert.That(varInfo.DisplayName, Is.EqualTo("myBox"));
            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("[(10, 11):(20, 21)]"));

            Assert.That(boxChildren[0].DisplayName, Is.EqualTo("Top Left"));
            Assert.That(await boxChildren[0].ValueAsync(), Is.EqualTo("(10, 11)"));
            Assert.That(boxChildren[1].DisplayName, Is.EqualTo("Bottom Right"));
            Assert.That(await boxChildren[1].ValueAsync(), Is.EqualTo("(20, 21)"));
            Assert.That(boxChildren[2].DisplayName, Is.EqualTo("[Raw View]"));
            Assert.That(await boxChildren[2].ValueAsync(), Is.EqualTo("actualBoxValue"));

            // p1
            {
                var p1Children = await boxChildren[0].GetAllChildrenAsync();

                Assert.That(p1Children.Length, Is.EqualTo(3));
                Assert.That(p1Children[0].DisplayName, Is.EqualTo("x"));
                Assert.That(await p1Children[0].ValueAsync(), Is.EqualTo("10"));
                Assert.That(p1Children[1].DisplayName, Is.EqualTo("y"));
                Assert.That(await p1Children[1].ValueAsync(), Is.EqualTo("11"));
                Assert.That(p1Children[2].DisplayName, Is.EqualTo("[Raw View]"));
                Assert.That(await p1Children[2].ValueAsync(), Is.EqualTo("p1ActualValue"));
            }

            // p2
            {
                var p2Children = await boxChildren[1].GetAllChildrenAsync();

                Assert.That(p2Children.Length, Is.EqualTo(3));
                Assert.That(p2Children[0].DisplayName, Is.EqualTo("x"));
                Assert.That(await p2Children[0].ValueAsync(), Is.EqualTo("20"));
                Assert.That(p2Children[1].DisplayName, Is.EqualTo("y"));
                Assert.That(await p2Children[1].ValueAsync(), Is.EqualTo("21"));
                Assert.That(p2Children[2].DisplayName, Is.EqualTo("[Raw View]"));
                Assert.That(await p2Children[2].ValueAsync(), Is.EqualTo("p2ActualValue"));
            }
        }

        #endregion

        #region Format Specifiers

        [Test]
        public async Task FormatSpecifiersWorkAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomType"">
    <DisplayString>{_count,x}</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomType", "myVar", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_count", 16));

            var varInfo = CreateVarInfo(remoteValue);
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));

            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("0x10"));
        }

        #endregion

        #region MightHaveChildren

        [Test]
        public void MightHaveChildrenChecksRemoteValueWhenNoVisualizerDefined()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValueWithoutChildren =
                RemoteValueFakeUtil.CreateClass("CustomType", "mySimplVar", "");

            var remoteValueFakeWithChildren = RemoteValueFakeUtil.CreateClass(
                "CustomType", "myComplexVar", "");
            remoteValueFakeWithChildren.AddChild(
                RemoteValueFakeUtil.CreateSimpleInt("_childVar", 16));

            var remoteValueWithChildren =
                testDoubleHelper.DoNotCall<RemoteValue>(remoteValueFakeWithChildren,
                nameof(RemoteValue.GetChildren));

            var varInfoWithoutChildren = CreateVarInfo(remoteValueWithoutChildren);
            var varInfoWithChildren = CreateVarInfo(remoteValueWithChildren);

            Assert.That(varInfoWithoutChildren.MightHaveChildren(), Is.False);
            Assert.That(varInfoWithChildren.MightHaveChildren(), Is.True);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void MightHaveChildrenChecksRemoteValueWhenNoExpandTypeDefined()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomType"">
    <DisplayString>DummyDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValueWithoutChildren =
                RemoteValueFakeUtil.CreateClass("CustomType", "varWithoutChildren", "value");
            RemoteValueFake remoteValueFakeWithChildren =
                RemoteValueFakeUtil.CreateClass("CustomType", "varWithChildren", "value");
            remoteValueFakeWithChildren.AddChild(
                RemoteValueFakeUtil.CreateSimpleInt("_childVar", 16));

            var remoteValueWithChildren =
                testDoubleHelper.DoNotCall<RemoteValue>(remoteValueFakeWithChildren,
                nameof(RemoteValue.GetChildren));

            IVariableInformation varInfoWithoutChildren =
                CreateVarInfo(remoteValueWithoutChildren);
            IVariableInformation varInfoWithChildren = CreateVarInfo(remoteValueWithChildren);

            Assert.That(varInfoWithoutChildren.MightHaveChildren(), Is.False);
            Assert.That(varInfoWithChildren.MightHaveChildren(), Is.True);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void MightHaveChildrenIsFalseWhenExpandIsEmptyAndRawViewIsHidden()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomType"">
    <DisplayString>DummyDisplayString</DisplayString>
    <Expand HideRawView=""true""/>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("CustomType", "myComplexVar", "");
            remoteValue.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_childVar", 16));

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo.MightHaveChildren(), Is.False);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void MightHaveChildrenChecksRemoteValueWhenExpandIsEmptyAndRawViewIsVisible()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomType"">
    <DisplayString>DummyDisplayString</DisplayString>
    <Expand/>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValueWithoutChildren =
                RemoteValueFakeUtil.CreateClass("CustomType", "varWithoutChildren", "value");
            RemoteValueFake remoteValueWithChildren =
                RemoteValueFakeUtil.CreateClass("CustomType", "varWithChildren", "value");
            remoteValueWithChildren.AddChild(RemoteValueFakeUtil.CreateSimpleInt("_childVar", 16));

            IVariableInformation varInfoWithoutChildren =
                CreateVarInfo(remoteValueWithoutChildren);
            IVariableInformation varInfoWithChildren = CreateVarInfo(remoteValueWithChildren);

            Assert.That(varInfoWithoutChildren.MightHaveChildren(), Is.False);
            Assert.That(varInfoWithChildren.MightHaveChildren(), Is.True);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void MightHaveChildrenIsTrueWhenExpandIsNotEmpty()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomType"">
    <DisplayString>DummyDisplayString</DisplayString>
    <Expand HideRawView=""true"">
      <Synthetic Name=""[DummyVarName]"" Condition=""invalidExpression"">
        <DisplayString>DummySyntheticValue</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("CustomType", "myVar", "");

            IVariableInformation varInfo = CreateVarInfo(remoteValue);

            // In this case, MightHaveChildren() gives a false positive, but this is WAI as it
            // saves the evaluation of the synthetic condition.
            Assert.That(varInfo.MightHaveChildren(), Is.True);

            // Ad-hoc check to confirm <Expand> children were NOT evaluated.
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("invalidExpression"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        [Test]
        public void MightHaveChildrenIsTrueWhenSmartPointerIsDefined()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomType"">
    <SmartPointer>invalidPointer</SmartPointer>
    <DisplayString>DummyDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("CustomType", "myVar", "");

            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            Assert.That(varInfo.MightHaveChildren(), Is.True);

            // Ad-hoc check to confirm that <SmartPointer> was NOT evaluated.
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("invalidPointer"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("WARNING"));
        }

        #endregion

        #region LogStats

        [Test]
        public async Task LogStatsAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""CustomType"">
    <DisplayString>DummyDisplayString</DisplayString>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);
            var writer = new StringWriter();

            // Ensure a Visualizer is cached.
            RemoteValueFake remoteValue =
                RemoteValueFakeUtil.CreateClass("CustomType", "mySimpleVar", "");
            IVariableInformation varInfo = CreateVarInfo(remoteValue);
            string value = await varInfo.ValueAsync();

            // Don't count CustomVisualizer.None
            int customVisualizersInEnum = Enum.GetNames(typeof(CustomVisualizer)).Length - 1;

            // Other visualizers that are not part of the CustomVisualizer enum:
            // - std::__1::string
            int miscCustomVisualizers = 1;

            // Total count = customVisualizersCount + miscCustomVisualizers + <CustomType>
            int totalCustomVisualizers = customVisualizersInEnum + miscCustomVisualizers + 1;

            _natvisExpander.VisualizerScanner.LogStats(writer, 10);
            string log = writer.ToString();
            Assert.That(log, Does.Contain($"Total Visualizer Count = {totalCustomVisualizers}"));
            Assert.That(log, Does.Contain("vizCache.Count = 1"));
        }

        #endregion

        #region Raw Format Specifier

        [Test]
        public async Task RawFormatSpecifierValueAndExpansionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""Node"">
    <DisplayString>Natvis Display String</DisplayString>
    <Expand HideRawView=""true"">
      <Synthetic Name=""SyntheticChild"">
        <DisplayString>SyntheticValue</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("Node", "head", "RawHeadValue");
            var next = RemoteValueFakeUtil.CreateClass("Node", "next", "RawNextValue");
            remoteValue.AddChild(next);

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue, FormatSpecifierUtil.RawFormatSpecifier);

            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("RawHeadValue"));

            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children, Has.Length.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("next"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("Natvis Display String"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task InheritableRawFormatSpecifierValueAndExpansionAsync()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""Node"">
    <DisplayString>Natvis Display String</DisplayString>
    <Expand HideRawView=""true"">
      <Synthetic Name=""SyntheticChild"">
        <DisplayString>SyntheticValue</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("Node", "head", "RawHeadValue");
            var next = RemoteValueFakeUtil.CreateClass("Node", "next", "RawNextValue");
            remoteValue.AddChild(next);

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue,
                                        FormatSpecifierUtil.InheritableRawFormatSpecifier);

            Assert.That(await varInfo.ValueAsync(), Is.EqualTo("RawHeadValue"));

            var children = await varInfo.GetAllChildrenAsync();

            Assert.That(children, Has.Length.EqualTo(1));
            Assert.That(children[0].DisplayName, Is.EqualTo("next"));
            Assert.That(await children[0].ValueAsync(), Is.EqualTo("RawNextValue"));

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void RawFormatSpecifierMightHaveChildren()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""Node"">
    <DisplayString>DummyValue</DisplayString>
    <Expand HideRawView=""true"">
      <Synthetic Name=""SyntheticChild"">
        <DisplayString>SyntheticValue</DisplayString>
      </Synthetic>
    </Expand>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass("Node", "head", "RawHeadValue");

            nLogSpy.Clear();
            var varInfo = CreateVarInfo(remoteValue, FormatSpecifierUtil.RawFormatSpecifier);

            Assert.That(varInfo.MightHaveChildren(), Is.False);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        #endregion

        #region Custom Visualizers

        [Test]
        public async Task CustomVisualizersGetCachedAsync()
        {
            var remoteValue = RemoteValueFakeUtil.CreateClass("SSE", "mySimpleVar", "");

            var varInfo1 = compRoot.GetVariableInformationFactory().Create(
                remoteValue, "SSE", customVisualizer: CustomVisualizer.SSE);
            string varCached1 = await varInfo1.ValueAsync();

            // This should select a cached custom Natvis Visualizer
            var varInfo2 = compRoot.GetVariableInformationFactory().Create(
                remoteValue, "SSE", customVisualizer: CustomVisualizer.SSE);
            string varCached2 = await varInfo2.ValueAsync();

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(nLogSpy.GetOutput(),
                Does.Contain("Selected cached custom Natvis Visualizer"));
        }

        static void AddLongFromExpression(RemoteValueFake remoteValue, string expr, long val) =>
            remoteValue.AddValueFromExpression(expr, RemoteValueFakeUtil.CreateSimpleLong("", val));

        static async Task CheckRegisterFormatAsync(string expectedName, string expectedValue,
            string expectedFormatSpecifier, IVariableInformation register)
        {
            Assert.AreEqual(expectedName, register.DisplayName);
            Assert.AreEqual(expectedValue, await register.ValueAsync());
            Assert.AreEqual(expectedFormatSpecifier, register.FormatSpecifier);
        }

        [Test]
        public async Task CustomVisualizerSSEAsync()
        {
            var remoteValue = RemoteValueFakeUtil.CreateClass("SSE", "", "");
            AddLongFromExpression(remoteValue, "xmm0", 0);
            AddLongFromExpression(remoteValue, "xmm1", 1);
            AddLongFromExpression(remoteValue, "xmm2", 2);
            AddLongFromExpression(remoteValue, "xmm3", 3);
            AddLongFromExpression(remoteValue, "xmm4", 4);
            AddLongFromExpression(remoteValue, "xmm5", 5);
            AddLongFromExpression(remoteValue, "xmm6", 6);
            AddLongFromExpression(remoteValue, "xmm7", 7);

            var varInfo = compRoot.GetVariableInformationFactory().Create(
                remoteValue, "SSE", customVisualizer: CustomVisualizer.SSE);

            IVariableInformation[] children = await varInfo.GetAllChildrenAsync();

            Assert.AreEqual("SSE", varInfo.DisplayName);
            Assert.AreEqual(8, await varInfo.GetChildAdapter().CountChildrenAsync());
            Assert.AreEqual(8, children.Length);
            await CheckRegisterFormatAsync("xmm0f", "{0.00000E0}", "vf32", children[0]);
            await CheckRegisterFormatAsync("xmm1f", "{1.00000E0}", "vf32", children[1]);
            await CheckRegisterFormatAsync("xmm2f", "{2.00000E0}", "vf32", children[2]);
            await CheckRegisterFormatAsync("xmm3f", "{3.00000E0}", "vf32", children[3]);
            await CheckRegisterFormatAsync("xmm4f", "{4.00000E0}", "vf32", children[4]);
            await CheckRegisterFormatAsync("xmm5f", "{5.00000E0}", "vf32", children[5]);
            await CheckRegisterFormatAsync("xmm6f", "{6.00000E0}", "vf32", children[6]);
            await CheckRegisterFormatAsync("xmm7f", "{7.00000E0}", "vf32", children[7]);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task CustomVisualizerSSE2Async()
        {
            var remoteValue = RemoteValueFakeUtil.CreateClass("SSE2", "", "");
            AddLongFromExpression(remoteValue, "xmm0", 0);
            AddLongFromExpression(remoteValue, "xmm1", 1);
            AddLongFromExpression(remoteValue, "xmm2", 2);
            AddLongFromExpression(remoteValue, "xmm3", 3);
            AddLongFromExpression(remoteValue, "xmm4", 4);
            AddLongFromExpression(remoteValue, "xmm5", 5);
            AddLongFromExpression(remoteValue, "xmm6", 6);
            AddLongFromExpression(remoteValue, "xmm7", 7);
            AddLongFromExpression(remoteValue, "xmm8", 8);
            AddLongFromExpression(remoteValue, "xmm9", 9);
            AddLongFromExpression(remoteValue, "xmm10", 10);
            AddLongFromExpression(remoteValue, "xmm11", 11);
            AddLongFromExpression(remoteValue, "xmm12", 12);
            AddLongFromExpression(remoteValue, "xmm13", 13);
            AddLongFromExpression(remoteValue, "xmm14", 14);
            AddLongFromExpression(remoteValue, "xmm15", 15);

            var varInfo = compRoot.GetVariableInformationFactory().Create(
                remoteValue, "SSE2", customVisualizer: CustomVisualizer.SSE2);

            IVariableInformation[] children = await varInfo.GetAllChildrenAsync();

            Assert.AreEqual("SSE2", varInfo.DisplayName);
            Assert.AreEqual(24, await varInfo.GetChildAdapter().CountChildrenAsync());
            Assert.AreEqual(24, children.Length);
            await CheckRegisterFormatAsync("xmm0d", "{0.00000000000000E0}", "vf64",children[0]);
            await CheckRegisterFormatAsync("xmm1d", "{1.00000000000000E0}", "vf64", children[1]);
            await CheckRegisterFormatAsync("xmm2d", "{2.00000000000000E0}", "vf64", children[2]);
            await CheckRegisterFormatAsync("xmm3d", "{3.00000000000000E0}", "vf64", children[3]);
            await CheckRegisterFormatAsync("xmm4d", "{4.00000000000000E0}", "vf64", children[4]);
            await CheckRegisterFormatAsync("xmm5d", "{5.00000000000000E0}", "vf64", children[5]);
            await CheckRegisterFormatAsync("xmm6d", "{6.00000000000000E0}", "vf64", children[6]);
            await CheckRegisterFormatAsync("xmm7d", "{7.00000000000000E0}", "vf64", children[7]);
            await CheckRegisterFormatAsync("xmm8d", "{8.00000000000000E0}", "vf64", children[8]);
            await CheckRegisterFormatAsync("xmm9d", "{9.00000000000000E0}", "vf64", children[9]);
            await CheckRegisterFormatAsync("xmm10d", "{1.00000000000000E1}", "vf64", children[10]);
            await CheckRegisterFormatAsync("xmm11d", "{1.10000000000000E1}", "vf64", children[11]);
            await CheckRegisterFormatAsync("xmm12d", "{1.20000000000000E1}", "vf64", children[12]);
            await CheckRegisterFormatAsync("xmm13d", "{1.30000000000000E1}", "vf64", children[13]);
            await CheckRegisterFormatAsync("xmm14d", "{1.40000000000000E1}", "vf64", children[14]);
            await CheckRegisterFormatAsync("xmm15d", "{1.50000000000000E1}", "vf64", children[15]);
            await CheckRegisterFormatAsync("xmm8f", "{8.00000E0}", "vf32", children[16]);
            await CheckRegisterFormatAsync("xmm9f", "{9.00000E0}", "vf32", children[17]);
            await CheckRegisterFormatAsync("xmm10f", "{1.00000E1}", "vf32", children[18]);
            await CheckRegisterFormatAsync("xmm11f", "{1.10000E1}", "vf32", children[19]);
            await CheckRegisterFormatAsync("xmm12f", "{1.20000E1}", "vf32", children[20]);
            await CheckRegisterFormatAsync("xmm13f", "{1.30000E1}", "vf32", children[21]);
            await CheckRegisterFormatAsync("xmm14f", "{1.40000E1}", "vf32", children[22]);
            await CheckRegisterFormatAsync("xmm15f", "{1.50000E1}", "vf32", children[23]);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public async Task CustomVisualizerAreReloadedAsync()
        {
            var remoteValue = RemoteValueFakeUtil.CreateClass("SSE", "", "");
            AddLongFromExpression(remoteValue, "xmm0", 0);
            AddLongFromExpression(remoteValue, "xmm1", 1);
            AddLongFromExpression(remoteValue, "xmm2", 2);
            AddLongFromExpression(remoteValue, "xmm3", 3);
            AddLongFromExpression(remoteValue, "xmm4", 4);
            AddLongFromExpression(remoteValue, "xmm5", 5);
            AddLongFromExpression(remoteValue, "xmm6", 6);
            AddLongFromExpression(remoteValue, "xmm7", 7);

            _natvisExpander.VisualizerScanner.Reload();

            var varInfo = compRoot.GetVariableInformationFactory().Create(
                remoteValue, "SSE", customVisualizer: CustomVisualizer.SSE);

            IVariableInformation[] children = await varInfo.GetAllChildrenAsync();

            Assert.AreEqual("SSE", varInfo.DisplayName);
            Assert.AreEqual(8, children.Length);
            await CheckRegisterFormatAsync("xmm0f", "{0.00000E0}", "vf32", children[0]);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
        }

        [Test]
        public void CustomVisualizersDefinesStdString()
        {
            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "std::__1::string", "dummyVarName", "actualValue");
            remoteValue.AddValueFromExpression("this->c_str()",
                RemoteValueFakeUtil.CreateSimpleString("strVal", "\"StringViewValue\""));

            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.StringView, Is.EqualTo("StringViewValue"));
        }

        [Test]
        public void CustomVisualizersStdStringCanBeOverridden()
        {
            var xml = @"
<AutoVisualizer xmlns=""http://schemas.microsoft.com/vstudio/debugger/natvis/2010"">
  <Type Name=""std::__1::string"">
    <StringView>""StringViewOverride""</StringView>
  </Type>
</AutoVisualizer>
";
            LoadFromString(xml);

            var remoteValue = RemoteValueFakeUtil.CreateClass(
                "std::__1::string", "dummyVarName", "actualValue");
            remoteValue.AddStringLiteral("StringViewOverride");

            var varInfo = CreateVarInfo(remoteValue);

            Assert.That(nLogSpy.GetOutput(), Does.Not.Contain("ERROR"));
            Assert.That(varInfo.StringView, Is.EqualTo("StringViewOverride"));
        }

        #endregion

        #region Helpers

        IVariableInformation CreateVarInfo(RemoteValue remoteValue, string formatSpecifier = null)
        {
            return compRoot.GetVariableInformationFactory().Create(
                remoteValue, formatSpecifier: new FormatSpecifier(formatSpecifier));
        }

        #endregion
    }
}
