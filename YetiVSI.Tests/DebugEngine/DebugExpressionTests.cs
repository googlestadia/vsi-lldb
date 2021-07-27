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

using System;
using DebuggerApi;
using NUnit.Framework;
using NSubstitute;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI.DebugEngine;
using YetiVSI.Test.TestSupport;
using YetiVSI.DebugEngine.Variables;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YetiCommon.PerformanceTracing;
using YetiCommon.Tests.PerformanceTracing.TestSupport;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.Metrics;
using YetiVSI.Shared.Metrics;
using YetiVSI.Test.Metrics.TestSupport;
using ValueType = DebuggerApi.ValueType;
using YetiVSITestsCommon;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugExpressionTests
    {
        RemoteFrame _mockDebuggerStackFrame;
        IDebugEngineHandler _mockDebugEngineHandler;
        IGgpDebugProgram _mockProgram;
        IDebugThread2 _mockThread;
        IGgpDebugPropertyFactory _propertyFactory;
        VarInfoBuilder _varInfoBuilder;
        IDebugEngineCommands _engineCommandsMock;
        VsExpressionCreator _vsExpressionCreator;
        IMetrics _metrics;
        ExpressionEvaluationRecorder _expressionEvaluationRecorder;
        ITimeSource _timeSource;

        ITaskExecutor _taskExecutor;

        [SetUp]
        public void SetUp()
        {
            _mockDebuggerStackFrame = Substitute.For<RemoteFrame>();
            var childAdapterFactory = new RemoteValueChildAdapter.Factory();
            var varInfoFactory = new LLDBVariableInformationFactory(childAdapterFactory);

            _vsExpressionCreator = new VsExpressionCreator();

            _metrics = Substitute.For<IMetrics>();
            var eventScheduler = new EventSchedulerFake();
            var eventSchedulerFactory = Substitute.For<IEventSchedulerFactory>();
            eventSchedulerFactory.Create(Arg.Do<System.Action>(a => eventScheduler.Callback = a),
                                         Arg.Any<int>())
                .Returns(eventScheduler);
            const int minimumBatchSeparationMilliseconds = 1;
            var exceptionRecorder = new ExceptionRecorder(_metrics);
            var batchEventAggregator =
                new BatchEventAggregator<ExpressionEvaluationBatch, ExpressionEvaluationBatchParams,
                    ExpressionEvaluationBatchSummary>(minimumBatchSeparationMilliseconds,
                                                      eventSchedulerFactory,
                                                      exceptionRecorder);

            _expressionEvaluationRecorder =
                new ExpressionEvaluationRecorder(batchEventAggregator, _metrics);
            _timeSource = new MonotonicTimeSource();

            _taskExecutor = Substitute.ForPartsOf<FakeTaskExecutor>();

            var enumFactory = new VariableInformationEnum.Factory(_taskExecutor);

            var childrenProviderFactory = new ChildrenProvider.Factory();
            _propertyFactory = new DebugAsyncProperty.Factory(
                enumFactory, childrenProviderFactory, null, _vsExpressionCreator, _taskExecutor);

            childrenProviderFactory.Initialize(_propertyFactory);

            _varInfoBuilder = new VarInfoBuilder(varInfoFactory);
            varInfoFactory.SetVarInfoBuilder(_varInfoBuilder);

            _engineCommandsMock = Substitute.For<IDebugEngineCommands>();

            _mockDebugEngineHandler = Substitute.For<IDebugEngineHandler>();
            _mockProgram = Substitute.For<IGgpDebugProgram>();
            _mockThread = Substitute.For<IDebugThread2>();
        }

        IDebugExpression CreateExpression(string expression,
                                          ExpressionEvaluationStrategy expressionEvaluationStrategy
                                              = ExpressionEvaluationStrategy.LLDB)
        {
            var extensionOptionsMock = Substitute.For<IExtensionOptions>();

            extensionOptionsMock.ExpressionEvaluationStrategy.Returns(expressionEvaluationStrategy);

            var asyncEvaluatorFactory = new AsyncExpressionEvaluator.Factory(
                _propertyFactory, _varInfoBuilder, _vsExpressionCreator,
                new ErrorDebugProperty.Factory(), _engineCommandsMock, extensionOptionsMock,
                _expressionEvaluationRecorder, _timeSource);

            return new DebugAsyncExpression.Factory(asyncEvaluatorFactory, _taskExecutor).Create(
                _mockDebuggerStackFrame, expression, _mockDebugEngineHandler, _mockProgram,
                _mockThread);
        }

        string GetName(IDebugProperty2 debugProperty) => GetPropertyInfo(debugProperty).bstrName;

        IDebugProperty2 GetResult(DebugExpressionEvaluationCompleteEvent e)
        {
            e.GetResult(out IDebugProperty2 result);
            return result;
        }

        string GetFullName(IDebugProperty2 debugProperty) =>
            GetPropertyInfo(debugProperty).bstrFullName;

        string GetType(IDebugProperty2 debugProperty) => GetPropertyInfo(debugProperty).bstrType;

        string GetValue(IDebugProperty2 debugProperty) => GetPropertyInfo(debugProperty).bstrValue;

        DEBUG_PROPERTY_INFO GetPropertyInfo(IDebugProperty2 debugProperty,
                                            enum_DEBUGPROP_INFO_FLAGS dwFields =
                                                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ALL)
        {
            var propertyInfos = new DEBUG_PROPERTY_INFO[1];
            debugProperty.GetPropertyInfo(dwFields, 0, 0, null, 0, propertyInfos);
            return propertyInfos[0];
        }

        [Test]
        public void Abort()
        {
            IDebugExpression expression = CreateExpression("myVar");
            Assert.AreEqual(VSConstants.E_NOTIMPL, expression.Abort());
        }

        [Test]
        public async Task Async()
        {
            const string expressionText = "myVar";
            IDebugExpression expression = CreateExpression(expressionText);
            RemoteValueFake expressionValueNode =
                RemoteValueFakeUtil.CreateClass("CustomType", "$17", "23");

            _mockDebuggerStackFrame.GetValueForVariablePath(expressionText)
                .Returns((RemoteValue) null);
            _mockDebuggerStackFrame.FindValue(expressionText, ValueType.VariableGlobal)
                .Returns((RemoteValue) null);
            _mockDebuggerStackFrame.EvaluateExpressionAsync(expressionText)
                .Returns(expressionValueNode);

            int status = expression.EvaluateAsync(0, null);

            var debugEventVerifier = Arg.Is<DebugExpressionEvaluationCompleteEvent>(
                e => expressionText.Equals(GetName(GetResult(e))) &&
                    "CustomType".Equals(GetType(GetResult(e))) &&
                    "23".Equals(GetValue(GetResult(e))));

            // This assumes that task executor stub works synchronously.
            _mockDebugEngineHandler.Received()
                .SendEvent(debugEventVerifier, Arg.Is(_mockProgram), Arg.Is(_mockThread));

            await _taskExecutor.ReceivedWithAnyArgs(1)
                .SubmitAsync(() => Task.CompletedTask, Arg.Any<CancellationToken>(),
                             Arg.Any<string>(), Arg.Any<Type>());

            Assert.AreEqual(VSConstants.S_OK, status);
        }

        [Test]
        public async Task LldbEvalAsync()
        {
            const string expressionText = "myVar";
            IDebugExpression expression =
                CreateExpression(expressionText, ExpressionEvaluationStrategy.LLDB_EVAL);
            RemoteValueFake expressionValueNode =
                RemoteValueFakeUtil.CreateClass("CustomType", "$17", "23");

            _mockDebuggerStackFrame.EvaluateExpressionLldbEvalAsync(expressionText)
                .Returns(expressionValueNode);

            int status = expression.EvaluateAsync(0, null);
            var debugEventVerifier = Arg.Is<DebugExpressionEvaluationCompleteEvent>(
                e => expressionText.Equals(GetName(GetResult(e))) &&
                    "CustomType".Equals(GetType(GetResult(e))) &&
                    "23".Equals(GetValue(GetResult(e))));

            // This assumes that task executor stub works synchronously.
            _mockDebugEngineHandler.Received()
                .SendEvent(debugEventVerifier, Arg.Is(_mockProgram), Arg.Is(_mockThread));

            await _taskExecutor.ReceivedWithAnyArgs(1)
                .SubmitAsync(() => Task.CompletedTask, Arg.Any<CancellationToken>(),
                             Arg.Any<string>(), Arg.Any<Type>());

            Assert.AreEqual(VSConstants.S_OK, status);
        }

        [Test]
        public void EvaluateSyncForLldbScratchVariable()
        {
            IDebugExpression expression = CreateExpression("$10");

            _mockDebuggerStackFrame.FindValue("10", ValueType.Register).Returns((RemoteValue) null);

            Assert.AreEqual(VSConstants.E_FAIL,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));
            Assert.AreEqual(null, property);
        }

        [Test]
        public void EvaluateSyncForNullResult()
        {
            string testText = "myVar";
            IDebugExpression expression = CreateExpression(testText);

            _mockDebuggerStackFrame.GetValueForVariablePath(testText).Returns((RemoteValue) null);
            _mockDebuggerStackFrame.FindValue(testText, ValueType.VariableGlobal)
                .Returns((RemoteValue) null);
            _mockDebuggerStackFrame.EvaluateExpressionAsync(testText).Returns((RemoteValue) null);

            Assert.AreEqual(VSConstants.E_FAIL,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));
            Assert.AreEqual(null, property);
        }

        [Test]
        public void EvaluateSyncForAddressOf()
        {
            const string expressionText = "&myVar";
            IDebugExpression expression = CreateExpression("&myVar");

            var expressionValueNode = new RemoteValueFake("$17", "23");
            expressionValueNode.SetTypeInfo(new SbTypeStub("CustomType", TypeFlags.IS_CLASS));

            _mockDebuggerStackFrame.FindValue(expressionText, ValueType.VariableGlobal)
                .Returns((RemoteValue) null);
            _mockDebuggerStackFrame.EvaluateExpressionAsync(expressionText)
                .Returns(expressionValueNode);

            Assert.AreEqual(VSConstants.S_OK,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));

            Assert.AreEqual(expressionText, GetName(property));
            Assert.AreEqual("CustomType", GetType(property));
            Assert.AreEqual("23", GetValue(property));
            _mockDebuggerStackFrame.DidNotReceiveWithAnyArgs().GetValueForVariablePath(null);
        }

        [Test]
        public void EvaluateSyncForExpression()
        {
            const string expressionText = "myVar";
            IDebugExpression expression = CreateExpression(expressionText);
            RemoteValueFake expressionValueNode =
                RemoteValueFakeUtil.CreateClass("CustomType", "$17", "23");

            _mockDebuggerStackFrame.GetValueForVariablePath(expressionText)
                .Returns((RemoteValue) null);
            _mockDebuggerStackFrame.FindValue(expressionText, ValueType.VariableGlobal)
                .Returns((RemoteValue) null);
            _mockDebuggerStackFrame.EvaluateExpressionAsync(expressionText)
                .Returns(expressionValueNode);

            Assert.AreEqual(VSConstants.S_OK,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));

            Assert.AreEqual(expressionText, GetName(property));
            Assert.AreEqual("CustomType", GetType(property));
            Assert.AreEqual("23", GetValue(property));
        }

        [TestCase(LldbEvalErrorCode.InvalidExpressionSyntax)]
        [TestCase(LldbEvalErrorCode.NotImplemented)]
        [TestCase(LldbEvalErrorCode.Unknown)]
        public async Task LldbEvalWithFallbackAsync(LldbEvalErrorCode lldbEvalErrorCode)
        {
            const string expressionText = "myVar";
            IDebugExpression expression = CreateExpression(
                expressionText, ExpressionEvaluationStrategy.LLDB_EVAL_WITH_FALLBACK);

            RemoteValueFake errorValue = RemoteValueFakeUtil.CreateLldbEvalError(lldbEvalErrorCode);
            RemoteValueFake expressionValue =
                RemoteValueFakeUtil.CreateClass("CustomType", "$17", "23");

            _mockDebuggerStackFrame.EvaluateExpressionLldbEvalAsync(expressionText)
                .Returns(errorValue);
            _mockDebuggerStackFrame.EvaluateExpressionAsync(expressionText)
                .Returns(expressionValue);

            Assert.AreEqual(VSConstants.S_OK,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));

            Assert.AreEqual(expressionText, GetName(property));
            Assert.AreEqual("CustomType", GetType(property));
            Assert.AreEqual("23", GetValue(property));

            // Make sure that a fallback to the LLDB happened.
            await _mockDebuggerStackFrame.Received(1)
                .EvaluateExpressionLldbEvalAsync(Arg.Is(expressionText));
            await _mockDebuggerStackFrame.Received(1)
                .EvaluateExpressionAsync(Arg.Is(expressionText));
        }

        [TestCase(LldbEvalErrorCode.InvalidNumericLiteral)]
        [TestCase(LldbEvalErrorCode.InvalidOperandType)]
        [TestCase(LldbEvalErrorCode.UndeclaredIdentifier)]
        public async Task LldbEvalWithFallbackCommonErrorsAsync(LldbEvalErrorCode lldbEvalErrorCode)
        {
            const string expressionText = "myVar";
            IDebugExpression expression = CreateExpression(
                expressionText, ExpressionEvaluationStrategy.LLDB_EVAL_WITH_FALLBACK);

            RemoteValueFake errorValue = RemoteValueFakeUtil.CreateLldbEvalError(lldbEvalErrorCode);

            _mockDebuggerStackFrame.EvaluateExpressionLldbEvalAsync(expressionText)
                .Returns(errorValue);

            Assert.AreEqual(VSConstants.S_OK,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));

            // Make sure that a fallback to the LLDB didn't happen.
            await _mockDebuggerStackFrame.Received(1)
                .EvaluateExpressionLldbEvalAsync(Arg.Is(expressionText));
            await _mockDebuggerStackFrame.DidNotReceive()
                .EvaluateExpressionAsync(Arg.Any<string>());
        }

        [TestCase(LldbEvalErrorCode.InvalidExpressionSyntax)]
        [TestCase(LldbEvalErrorCode.InvalidNumericLiteral)]
        [TestCase(LldbEvalErrorCode.InvalidOperandType)]
        [TestCase(LldbEvalErrorCode.UndeclaredIdentifier)]
        [TestCase(LldbEvalErrorCode.NotImplemented)]
        [TestCase(LldbEvalErrorCode.Unknown)]
        public async Task LldbEvalWithoutFallbackAsync(LldbEvalErrorCode lldbEvalErrorCode)
        {
            const string expressionText = "myVar";
            IDebugExpression expression =
                CreateExpression(expressionText, ExpressionEvaluationStrategy.LLDB_EVAL);

            RemoteValueFake errorValue = RemoteValueFakeUtil.CreateLldbEvalError(lldbEvalErrorCode);

            _mockDebuggerStackFrame.EvaluateExpressionLldbEvalAsync(expressionText)
                .Returns(errorValue);

            Assert.AreEqual(VSConstants.S_OK,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));

            // Make sure that a fallback to the LLDB didn't happen.
            await _mockDebuggerStackFrame.Received(1)
                .EvaluateExpressionLldbEvalAsync(Arg.Is(expressionText));
            await _mockDebuggerStackFrame.DidNotReceive()
                .EvaluateExpressionAsync(Arg.Any<string>());
        }

        [Test]
        public void EvaluateSyncForVariablePath()
        {
            const string expressionText = "myVar";
            IDebugExpression expression = CreateExpression(expressionText);
            RemoteValueFake variablePathValueNode =
                RemoteValueFakeUtil.CreateClass("CustomType", "$17", "23");

            _mockDebuggerStackFrame.GetValueForVariablePath(expressionText)
                .Returns(variablePathValueNode);

            Assert.AreEqual(VSConstants.S_OK,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));

            Assert.AreEqual(expressionText, GetName(property));
            Assert.AreEqual("CustomType", GetType(property));
            Assert.AreEqual("23", GetValue(property));
        }

        [Test]
        public void EvaluateSyncForValue()
        {
            string expressionText = "myVar";

            IDebugExpression expression = CreateExpression(expressionText);
            RemoteValueFake findValueValueNode =
                RemoteValueFakeUtil.CreateClass("CustomType", "$17", "23");

            _mockDebuggerStackFrame.FindValue(expressionText, ValueType.VariableGlobal)
                .Returns((RemoteValue) null);

            _mockDebuggerStackFrame.GetValueForVariablePath(expressionText)
                .Returns((RemoteValue) null);
            _mockDebuggerStackFrame.FindValue(expressionText, ValueType.VariableGlobal)
                .Returns(findValueValueNode);

            Assert.AreEqual(VSConstants.S_OK,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));

            Assert.AreEqual(expressionText, GetName(property));
            Assert.AreEqual("CustomType", GetType(property));
            Assert.AreEqual("23", GetValue(property));
        }

        [Test]
        public void EvaluateSyncForRegister()
        {
            string expressionText = "$rax";

            IDebugExpression expression = CreateExpression(expressionText);
            RemoteValueFake findValueValueNode =
                RemoteValueFakeUtil.CreateUnsignedLongRegister("rax", 123u);

            _mockDebuggerStackFrame.FindValue("rax", ValueType.Register)
                .Returns(findValueValueNode);

            Assert.AreEqual(VSConstants.S_OK,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));

            Assert.AreEqual(expressionText, GetName(property));
            Assert.AreEqual("unsigned long", GetType(property));
            Assert.AreEqual("123", GetValue(property));
        }

        [Test]
        public void EvaluateSyncWithFormatSpecifier()
        {
            string expressionText = "myVar";
            string formatSpecifier = ",x";
            string testText = expressionText + formatSpecifier;
            IDebugExpression expression = CreateExpression(testText);

            RemoteValueFake remoteValue = RemoteValueFakeUtil.CreateSimpleInt("myVar", 16);

            _mockDebuggerStackFrame.FindValue(testText, ValueType.VariableGlobal)
                .Returns((RemoteValue) null);
            _mockDebuggerStackFrame.GetValueForVariablePath(expressionText)
                .Returns((RemoteValue) null);
            _mockDebuggerStackFrame.FindValue(expressionText, ValueType.VariableGlobal)
                .Returns((RemoteValue) null);
            _mockDebuggerStackFrame.EvaluateExpressionAsync(expressionText).Returns(remoteValue);

            Assert.AreEqual(VSConstants.S_OK,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));

            Assert.AreEqual(testText, GetName(property));
            Assert.AreEqual(testText, GetFullName(property));
            Assert.AreEqual("int", GetType(property));
            Assert.AreEqual("0x10", GetValue(property));
        }

        [Test]
        public void InvalidCommand()
        {
            IDebugExpression expression = CreateExpression(".invalidcommand");

            Assert.AreEqual(VSConstants.S_OK,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));

            DEBUG_PROPERTY_INFO info =
                GetPropertyInfo(property, enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB);
            Assert.That(info.dwAttrib.HasFlag(enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_ERROR));
        }

        [Test]
        public void NatvisReloadCommand()
        {
            IDebugExpression expression = CreateExpression(".natvisreload");

            Assert.AreEqual(VSConstants.S_OK,
                            expression.EvaluateSync(0, 0, null, out IDebugProperty2 property));

            DEBUG_PROPERTY_INFO info =
                GetPropertyInfo(property, enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB);
            Assert.That(!info.dwAttrib.HasFlag(enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_ERROR));

            _engineCommandsMock.Received().ReloadNatvis(Arg.Any<TextWriter>(), out string _);
        }

        [Test]
        public void EvaluateAsyncOp()
        {
            IDebugExpression expression = CreateExpression("someExpression");
            var pCompletionHandler = Substitute.For<IAsyncDebugEvaluateCompletionHandler>();
            int status = expression.GetEvaluateAsyncOp(0, 0, 0, 0, pCompletionHandler,
                                                       out IAsyncDebugEngineOperation
                                                           asyncOperation);
            Assert.AreEqual(VSConstants.S_OK, status);
            Assert.NotNull(asyncOperation);
        }
    }
}