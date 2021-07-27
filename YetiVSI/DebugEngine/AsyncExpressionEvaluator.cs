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
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using YetiCommon.CastleAspects;
using YetiCommon.PerformanceTracing;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Metrics;

namespace YetiVSI.DebugEngine
{
    public interface IAsyncExpressionEvaluator
    {
        Task<EvaluationResult> EvaluateExpressionAsync();
    }

    public class EvaluationResult
    {
        public IDebugProperty2 Result { get; }
        public int Status { get; }

        public static EvaluationResult Fail() => new EvaluationResult(null, VSConstants.E_FAIL);

        public static EvaluationResult FromResult(IDebugProperty2 result) =>
            new EvaluationResult(result, VSConstants.S_OK);

        EvaluationResult(IDebugProperty2 result, int status)
        {
            Result = result;
            Status = status;
        }
    }

    public class AsyncExpressionEvaluator : SimpleDecoratorSelf<IAsyncExpressionEvaluator>,
        IAsyncExpressionEvaluator
    {
        public class Factory
        {
            readonly IGgpDebugPropertyFactory _propertyFactory;
            readonly VarInfoBuilder _varInfoBuilder;
            readonly VsExpressionCreator _vsExpressionCreator;
            readonly ErrorDebugProperty.Factory _errorDebugPropertyFactory;
            readonly IDebugEngineCommands _debugEngineCommands;
            readonly IExtensionOptions _extensionOptions;
            readonly ExpressionEvaluationRecorder _expressionEvaluationRecorder;
            readonly ITimeSource _timeSource;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            public Factory(IGgpDebugPropertyFactory propertyFactory, VarInfoBuilder varInfoBuilder,
                           VsExpressionCreator vsExpressionCreator,
                           ErrorDebugProperty.Factory errorDebugPropertyFactory,
                           IDebugEngineCommands debugEngineCommands,
                           IExtensionOptions extensionOptions,
                           ExpressionEvaluationRecorder expressionEvaluationRecorder,
                           ITimeSource timeSource)
            {
                _propertyFactory = propertyFactory;
                _varInfoBuilder = varInfoBuilder;
                _vsExpressionCreator = vsExpressionCreator;
                _errorDebugPropertyFactory = errorDebugPropertyFactory;
                _debugEngineCommands = debugEngineCommands;
                _extensionOptions = extensionOptions;
                _expressionEvaluationRecorder = expressionEvaluationRecorder;
                _timeSource = timeSource;
            }

            public virtual IAsyncExpressionEvaluator Create(RemoteFrame frame, string text)
            {
                // Get preferred expression evaluation option. This is performed here to
                // pick up configuration changes in runtime (e.g. the user can enable and
                // disable lldb-eval during a single debug session).
                var expressionEvaluationStrategy = _extensionOptions.ExpressionEvaluationStrategy;

                return new AsyncExpressionEvaluator(frame, text, _vsExpressionCreator,
                                                    _varInfoBuilder, _propertyFactory,
                                                    _errorDebugPropertyFactory,
                                                    _debugEngineCommands,
                                                    expressionEvaluationStrategy,
                                                    _expressionEvaluationRecorder, _timeSource);
            }
        }

        const ExpressionEvaluationContext _expressionEvaluationContext =
            ExpressionEvaluationContext.FRAME;

        readonly VsExpressionCreator _vsExpressionCreator;
        readonly VarInfoBuilder _varInfoBuilder;
        readonly IGgpDebugPropertyFactory _propertyFactory;
        readonly ErrorDebugProperty.Factory _errorDebugPropertyFactory;
        readonly IDebugEngineCommands _debugEngineCommands;
        readonly RemoteFrame _frame;
        readonly string _text;
        readonly ExpressionEvaluationStrategy _expressionEvaluationStrategy;
        readonly ExpressionEvaluationRecorder _expressionEvaluationRecorder;
        readonly ITimeSource _timeSource;

        AsyncExpressionEvaluator(RemoteFrame frame, string text,
                                 VsExpressionCreator vsExpressionCreator,
                                 VarInfoBuilder varInfoBuilder,
                                 IGgpDebugPropertyFactory propertyFactory,
                                 ErrorDebugProperty.Factory errorDebugPropertyFactory,
                                 IDebugEngineCommands debugEngineCommands,
                                 ExpressionEvaluationStrategy expressionEvaluationStrategy,
                                 ExpressionEvaluationRecorder expressionEvaluationRecorder,
                                 ITimeSource timeSource)
        {
            _frame = frame;
            _text = text;
            _vsExpressionCreator = vsExpressionCreator;
            _varInfoBuilder = varInfoBuilder;
            _propertyFactory = propertyFactory;
            _errorDebugPropertyFactory = errorDebugPropertyFactory;
            _debugEngineCommands = debugEngineCommands;
            _expressionEvaluationStrategy = expressionEvaluationStrategy;
            _expressionEvaluationRecorder = expressionEvaluationRecorder;
            _timeSource = timeSource;
        }

        public async Task<EvaluationResult> EvaluateExpressionAsync()
        {
            VsExpression vsExpression =
                await _vsExpressionCreator.CreateAsync(_text, EvaluateSizeSpecifierExpressionAsync);
            IDebugProperty2 result;

            if (vsExpression.Value.StartsWith("."))
            {
                EvaluateCommand(vsExpression.Value, out result);
                return EvaluationResult.FromResult(result);
            }

            RemoteValue remoteValue = await CreateValueFromExpressionAsync(vsExpression.Value);

            if (remoteValue == null)
            {
                return EvaluationResult.Fail();
            }

            string displayName = vsExpression.ToString();
            IVariableInformation varInfo =
                _varInfoBuilder.Create(remoteValue, displayName, vsExpression.FormatSpecifier);
            result = _propertyFactory.Create(varInfo);

            return EvaluationResult.FromResult(result);
        }

        async Task<uint> EvaluateSizeSpecifierExpressionAsync(string expression)
        {
            RemoteValue value = await CreateValueFromExpressionAsync(expression);
            var err = value.GetError();
            if (err.Fail())
            {
                throw new ExpressionEvaluationFailed(err.GetCString());
            }
            if (!uint.TryParse(value.GetValue(ValueFormat.Default), out uint size))
            {
                throw new ExpressionEvaluationFailed("Expression isn't a uint");
            }
            return size;
        }

        void EvaluateCommand(string command, out IDebugProperty2 debugProperty)
        {
            debugProperty = _errorDebugPropertyFactory.Create(command, "", "Invalid Command");

            command = command.Trim();
            if (command == ".natvisreload")
            {
                debugProperty = new CommandDebugProperty(".natvisreload", "", () =>
                {
                    Trace.WriteLine("Reloading Natvis - triggered by .natvisreload command.");
                    var writer = new StringWriter();
                    Trace.WriteLine(
                        _debugEngineCommands.ReloadNatvis(writer, out string resultDescription)
                            ? $".natvisreload result: {writer}"
                            : $"Unable to reload Natvis.  {resultDescription}");
                    return resultDescription;
                });
            }
        }

        async Task<RemoteValue> CreateValueFromExpressionAsync(string expression)
        {
            var stepsRecorder = new ExpressionEvaluationRecorder.StepsRecorder(_timeSource);

            long startTimestampUs = _timeSource.GetTimestampUs();
            RemoteValue remoteValue =
                await CreateValueFromExpressionWithMetricsAsync(expression, stepsRecorder);
            long endTimestampUs = _timeSource.GetTimestampUs();

            _expressionEvaluationRecorder.Record(_expressionEvaluationStrategy,
                                                 _expressionEvaluationContext, stepsRecorder,
                                                 startTimestampUs, endTimestampUs);

            return remoteValue;
        }

        /// <summary>
        /// Asynchronously creates RemoteValue from the expression. Returns null in case of error.
        /// </summary>
        async Task<RemoteValue> CreateValueFromExpressionWithMetricsAsync(
            string expression, ExpressionEvaluationRecorder.StepsRecorder stepsRecorder)
        {
            if (_text.StartsWith(ExpressionConstants.RegisterPrefix))
            {
                // If text is prefixed by '$', check if it refers to a register by trying to find
                // a register named expression[1:]. If we can't, simply return false to prevent
                // LLDB scratch variables, which also start with '$', from being accessible.
                return _frame.FindValue(expression.Substring(1), DebuggerApi.ValueType.Register);
            }

            if (_expressionEvaluationStrategy == ExpressionEvaluationStrategy.LLDB_EVAL ||
                _expressionEvaluationStrategy ==
                ExpressionEvaluationStrategy.LLDB_EVAL_WITH_FALLBACK)
            {
                RemoteValue value;
                LldbEvalErrorCode errorCode;

                using (var step = stepsRecorder.NewStep(ExpressionEvaluationEngine.LLDB_EVAL))
                {
                    value = await _frame.EvaluateExpressionLldbEvalAsync(expression);

                    // Convert an error code to the enum value.
                    errorCode = (LldbEvalErrorCode)Enum.ToObject(
                        typeof(LldbEvalErrorCode), value.GetError().GetError());

                    step.Finalize(errorCode);
                }

                if (errorCode == LldbEvalErrorCode.Ok)
                {
                    return value;
                }

                if (errorCode == LldbEvalErrorCode.InvalidNumericLiteral ||
                    errorCode == LldbEvalErrorCode.InvalidOperandType ||
                    errorCode == LldbEvalErrorCode.UndeclaredIdentifier)
                {
                    // Evaluation failed with a well-known error. Don't fallback to LLDB native
                    // expression evaluator, since it will fail too.
                    return value;
                }

                if (_expressionEvaluationStrategy !=
                    ExpressionEvaluationStrategy.LLDB_EVAL_WITH_FALLBACK)
                {
                    // Don't fallback to LLDB native expression evaluator if that option is
                    // disabled.
                    return value;
                }
            }
            else
            {
                RemoteValue value;

                using (var step =
                    stepsRecorder.NewStep(ExpressionEvaluationEngine.LLDB_VARIABLE_PATH))
                {
                    value = EvaluateWithLldbVariablePath(expression);
                    step.Finalize(ToErrorCodeLLDB(value));
                }

                if (value != null)
                {
                    return value;
                }

            }

            // Evaluate the expression using LLDB.
            {
                RemoteValue value;

                using (var step = stepsRecorder.NewStep(ExpressionEvaluationEngine.LLDB))
                {
                    value = await _frame.EvaluateExpressionAsync(expression);
                    step.Finalize(ToErrorCodeLLDB(value));
                }

                return value;
            }

            LLDBErrorCode ToErrorCodeLLDB(RemoteValue v)
            {
                if (v == null)
                {
                    return LLDBErrorCode.ERROR;
                }
                return v.GetError().Success() ? LLDBErrorCode.OK : LLDBErrorCode.ERROR;
            }
        }

        RemoteValue EvaluateWithLldbVariablePath(string expression)
        {
            // Variables created via RemoteFrame::EvaluateExpression() don't return a valid,
            // non-contextual fullname so we attempt to use
            // RemoteFrame::GetValueForVariablePath() and RemoteFrame::FindValue() first. This
            // ensures some UI elements (ex variable tooltips) show a human readable expression
            // that can be re-evaluated across debug sessions.

            // RemoteFrame::GetValueForVariablePath() was not returning the proper address of
            // reference types. ex. "&myIntRef".
            if (!_text.Contains("&"))
            {
                RemoteValue remoteValue = _frame.GetValueForVariablePath(expression);

                if (remoteValue != null)
                {
                    return remoteValue;
                }
            }

            // Resolve static class variables because GetValueForVariablePath() doesn't.
            return _frame.FindValue(expression, DebuggerApi.ValueType.VariableGlobal);
        }
    }
}