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
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Threading.Tasks;
using DebuggerApi;
using YetiCommon.CastleAspects;
using YetiVSI.DebugEngine.Variables;

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
            readonly VsExpressionCreator vsExpressionCreator;
            readonly VarInfoBuilder varInfoBuilder;
            readonly CreateDebugPropertyDelegate createPropertyDelegate;
            readonly ErrorDebugProperty.Factory errorDebugPropertyFactory;
            readonly IDebugEngineCommands debugEngineCommands;
            readonly IExtensionOptions extensionOptions;

            [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
            protected Factory()
            {
            }

            public Factory(CreateDebugPropertyDelegate createPropertyDelegate,
                VarInfoBuilder varInfoBuilder, VsExpressionCreator vsExpressionCreator,
                ErrorDebugProperty.Factory errorDebugPropertyFactory,
                IDebugEngineCommands debugEngineCommands,
                IExtensionOptions extensionOptions)
            {
                this.createPropertyDelegate = createPropertyDelegate;
                this.varInfoBuilder = varInfoBuilder;
                this.vsExpressionCreator = vsExpressionCreator;
                this.errorDebugPropertyFactory = errorDebugPropertyFactory;
                this.debugEngineCommands = debugEngineCommands;
                this.extensionOptions = extensionOptions;
            }

            public virtual IAsyncExpressionEvaluator Create(RemoteFrame frame, string text)
            {
                // Get preferred expression evaluation option. This is performed here to
                // pick up configuration changes in runtime (e.g. the user can enable and
                // disable lldb-eval during a single debug session).
                var expressionEvaluationStrategy = extensionOptions.ExpressionEvaluationStrategy;

                return new AsyncExpressionEvaluator(
                    frame, text, vsExpressionCreator, varInfoBuilder, createPropertyDelegate,
                    errorDebugPropertyFactory, debugEngineCommands, expressionEvaluationStrategy);
            }
        }

        readonly VsExpressionCreator vsExpressionCreator;
        readonly VarInfoBuilder varInfoBuilder;
        readonly CreateDebugPropertyDelegate createPropertyDelegate;
        readonly ErrorDebugProperty.Factory errorDebugPropertyFactory;
        readonly IDebugEngineCommands debugEngineCommands;
        readonly RemoteFrame frame;
        readonly string text;
        readonly ExpressionEvaluationStrategy expressionEvaluationStrategy;

        AsyncExpressionEvaluator(RemoteFrame frame, string text,
                                 VsExpressionCreator vsExpressionCreator,
                                 VarInfoBuilder varInfoBuilder,
                                 CreateDebugPropertyDelegate createPropertyDelegate,
                                 ErrorDebugProperty.Factory errorDebugPropertyFactory,
                                 IDebugEngineCommands debugEngineCommands,
                                 ExpressionEvaluationStrategy expressionEvaluationStrategy)
        {
            this.frame = frame;
            this.text = text;
            this.vsExpressionCreator = vsExpressionCreator;
            this.varInfoBuilder = varInfoBuilder;
            this.createPropertyDelegate = createPropertyDelegate;
            this.errorDebugPropertyFactory = errorDebugPropertyFactory;
            this.debugEngineCommands = debugEngineCommands;
            this.expressionEvaluationStrategy = expressionEvaluationStrategy;
        }

        public async Task<EvaluationResult> EvaluateExpressionAsync()
        {
            VsExpression vsExpression =
                await vsExpressionCreator.CreateAsync(text, EvaluateSizeSpecifierExpressionAsync);
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
                varInfoBuilder.Create(remoteValue, displayName, vsExpression.FormatSpecifier);
            result = createPropertyDelegate.Invoke(varInfo);

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

            uint size;
            if (!uint.TryParse(value.GetValue(ValueFormat.Default), out size))
            {
                throw new ExpressionEvaluationFailed("Expression isn't a uint");
            }
            return size;
        }

        void EvaluateCommand(string command, out IDebugProperty2 debugProperty)
        {
            debugProperty = errorDebugPropertyFactory.Create(command, "", "Invalid Command");

            command = command.Trim();
            if (command == ".natvisreload")
            {
                debugProperty = new CommandDebugProperty(".natvisreload", "", () =>
                {
                    Trace.WriteLine("Reloading Natvis - triggered by .natvisreload command.");
                    var writer = new StringWriter();
                    Trace.WriteLine(
                        debugEngineCommands.ReloadNatvis(writer, out string resultDescription)
                            ? $".natvisreload result: {writer}"
                            : $"Unable to reload Natvis.  {resultDescription}");
                    return resultDescription;
                });
            }
        }

        /// <summary>
        /// Asynchronously creates RemoteValue from the expression. Returns null in case of error.
        /// </summary>
        async Task<RemoteValue> CreateValueFromExpressionAsync(string expression)
        {
            if (text.StartsWith(ExpressionConstants.RegisterPrefix))
            {
                // If text is prefixed by '$', check if it refers to a register by trying to find
                // a register named expression[1:]. If we can't, simply return false to prevent
                // LLDB scratch variables, which also start with '$', from being accessible.
                return frame.FindValue(expression.Substring(1), DebuggerApi.ValueType.Register);
            }

            if (expressionEvaluationStrategy == ExpressionEvaluationStrategy.LLDB_EVAL ||
                expressionEvaluationStrategy ==
                ExpressionEvaluationStrategy.LLDB_EVAL_WITH_FALLBACK)
            {
                var remoteValue = await frame.EvaluateExpressionLldbEvalAsync(expression);

                // Convert an error code to the enum value.
                var errorCode = (LldbEvalErrorCode)Enum.ToObject(
                    typeof(LldbEvalErrorCode), remoteValue.GetError().GetError());

                if (errorCode == LldbEvalErrorCode.Ok)
                {
                    return remoteValue;
                }

                if (errorCode == LldbEvalErrorCode.InvalidNumericLiteral ||
                    errorCode == LldbEvalErrorCode.InvalidOperandType ||
                    errorCode == LldbEvalErrorCode.UndeclaredIdentifier)
                {
                    // Evaluation failed with a well-known error. Don't fallback to LLDB native
                    // expression evaluator, since it will fail too.
                    return remoteValue;
                }

                if (expressionEvaluationStrategy !=
                    ExpressionEvaluationStrategy.LLDB_EVAL_WITH_FALLBACK)
                {
                    // Don't fallback to LLDB native expression evaluator if that option is
                    // disabled.
                    return remoteValue;
                }
            }
            else
            {
                // Variables created via RemoteFrame::EvaluateExpression() don't return a valid,
                // non-contextual fullname so we attempt to use
                // RemoteFrame::GetValueForVariablePath() and RemoteFrame::FindValue() first. This
                // ensures some UI elements (ex variable tooltips) show a human readable expression
                // that can be re-evaluated across debug sessions.

                // RemoteFrame::GetValueForVariablePath() was not returning the proper address of
                // reference types. ex. "&myIntRef".
                RemoteValue remoteValue;
                if (!text.Contains("&"))
                {
                    remoteValue = frame.GetValueForVariablePath(expression);
                    if (remoteValue != null)
                    {
                        return remoteValue;
                    }
                }

                // Resolve static class variables because GetValueForVariablePath() doesn't.
                remoteValue = frame.FindValue(expression, DebuggerApi.ValueType.VariableGlobal);
                if (remoteValue != null)
                {
                    return remoteValue;
                }
            }

            // Fall back on RemoteFrame::EvaluateExpressionAsync().
            return await frame.EvaluateExpressionAsync(expression);
        }
    }
}
