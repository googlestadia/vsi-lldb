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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YetiCommon.PerformanceTracing;
using YetiVSI.DebugEngine.Variables;
using YetiVSI.Metrics;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class NatvisExpressionEvaluator
    {
        static readonly Regex _varNameRegex = new Regex("[a-zA-Z$_][a-zA-Z$_0-9]*");

        static readonly Regex _subfieldNameHereRegex =
            new Regex(@"\G((\.|->)[a-zA-Z$_][a-zA-Z$_0-9]*)+");

        /// <summary>
        /// A TokenSubstitutor takes the given token and returns a replacement token. If there
        /// is no substitution required, returns null.
        /// </summary>
        delegate string TokenSubstitutor(string token);

        readonly VsExpressionCreator _vsExpressionCreator;
        readonly NatvisDiagnosticLogger _logger;
        readonly IExtensionOptions _extensionOptions;
        readonly ExpressionEvaluationRecorder _expressionEvaluationRecorder;
        readonly ITimeSource _timeSource;

        public NatvisExpressionEvaluator(NatvisDiagnosticLogger logger,
                                         VsExpressionCreator vsExpressionCreator,
                                         IExtensionOptions extensionOptions,
                                         ExpressionEvaluationRecorder expressionEvaluationRecorder,
                                         ITimeSource timeSource)
        {
            _logger = logger;
            _vsExpressionCreator = vsExpressionCreator;
            // Instead of relying on the ExpressionEvaluationEngine flag directly, use
            // IExtensionOptions to get the flag. This will pick up configuration changes in
            // runtime.
            _extensionOptions = extensionOptions;
            _expressionEvaluationRecorder = expressionEvaluationRecorder;
            _timeSource = timeSource;
        }

        /// <summary>
        /// Invokes GetExpressionValue, but returns error variable in case of evaluation error.
        /// </summary>
        public async Task<IVariableInformation> GetExpressionValueOrErrorAsync(
            string expression, IVariableInformation variable, NatvisScope natvisScope,
            string displayName, string natvisType)
        {
            try
            {
                return await EvaluateExpressionAsync(expression, variable, natvisScope,
                                                     displayName);
            }
            catch (ExpressionEvaluationFailed e)
            {
                _logger.Error($"Failed to evaluate {natvisType} node" +
                              $" for {displayName}, type: {variable?.TypeName}.");
                return new ErrorVariableInformation(displayName, $"<Error> Reason: {e.Message}");
            }
        }

        /// <summary>
        /// Evaluates a Natvis expression in the context of a variable asynchronously.
        ///
        /// Examples:
        ///   "myData[0] == true"
        ///   "myData[$i]"
        ///   "MyContainer<$T1, $T2>",
        ///   "(char*)myData,[myLength]s"
        /// </summary>
        /// <param name="expression">The expression to evaluate. Natvis tokens are resolved prior to
        /// evaluation, ex. $i, $Tx. </param>
        /// <param name="variable"></param>
        /// <param name="scopedNames">The Natvis tokens to resolve in expression.</param>
        /// <param name="displayName">The display name given to the result. If null the underlying
        /// debugger's context specific name is used.</param>
        /// <returns>The expression result.</returns>
        public async Task<IVariableInformation> EvaluateExpressionAsync(
            string expression, IVariableInformation variable, NatvisScope natvisScope,
            string displayName)
        {
            var vsExpression = await _vsExpressionCreator.CreateAsync(
                expression, async (sizeExpression) =>
                {
                    IVariableInformation value = await EvaluateLldbExpressionAsync(
                        _vsExpressionCreator.Create(sizeExpression, ""), variable, natvisScope,
                        displayName);
                    uint size;
                    if (!uint.TryParse(await value.ValueAsync(), out size))
                    {
                        throw new ExpressionEvaluationFailed("Expression isn't a uint");
                    }

                    return size;
                });
            return await EvaluateLldbExpressionAsync(vsExpression, variable, natvisScope,
                                                     displayName);
        }

        /// <summary>
        /// Evaluates an LLDB expression. It decides which expression evaluation method to use
        /// (e.g. LLDB, lldb-eval, path expression, etc.) depending on the Stadia SDK settings and
        /// the input |expression|. It doesn't support format specifiers, only expressions that
        /// can be directly evaluated in the LLDB environment.
        /// </summary>
        /// <param name="expression">The expression to be evaluated.</param>
        /// <param name="variable">The evaluation context.</param>
        /// <param name="natvisScope">The Natvis tokens to be resolved before evaluation.</param>
        /// <param name="displayName">The display name given to the result. If null the underlying
        /// debugger's context specific name is used.</param>
        /// <returns>The expression result.</returns>
        async Task<IVariableInformation> EvaluateLldbExpressionAsync(VsExpression expression,
                                                                     IVariableInformation variable,
                                                                     NatvisScope natvisScope,
                                                                     string displayName)
        {
            ExpressionEvaluationStrategy strategy = _extensionOptions.ExpressionEvaluationStrategy;
            var stepsRecorder = new ExpressionEvaluationRecorder.StepsRecorder(_timeSource);

            long startTimestampUs = _timeSource.GetTimestampUs();

            IVariableInformation varInfo = null;
            try
            {
                // TODO: Don't throw exceptions, return ErrorVariableInformation instead.
                varInfo = await EvaluateLldbExpressionWithMetricsAsync(
                    expression, variable, natvisScope, displayName, strategy, stepsRecorder);
            }
            finally
            {
                long endTimestampUs = _timeSource.GetTimestampUs();

                _expressionEvaluationRecorder.Record(strategy, ExpressionEvaluationContext.VALUE,
                                                     stepsRecorder, startTimestampUs,
                                                     endTimestampUs, variable.Id);
                _logger.Verbose(() =>
                {
                    var dt = new TimeSpan(endTimestampUs - startTimestampUs);
                    if (varInfo == null)
                    {
                        return $"Failed to evaluated expression '{expression}' " +
                               $"(took {dt.TotalMilliseconds}ms)";
                    }
                    else if (varInfo.Error)
                    {
                        return $"Failed to evaluated expression '{expression}': " +
                               $"{varInfo.ErrorMessage} (took {dt.TotalMilliseconds}ms)";
                    }
                    else
                    {
                        return $"Evaluated expression '{expression}' (took {dt.TotalMilliseconds}ms)";
                    }
                });
            }

            // Evaluating a context variable will just return the reference to it. Because of
            // deferred evaluation of display values, some values could be incorrectly displayed
            // (in the case a context variable was changed in between two expression evaluations).
            // In order to prevent this, we create a copy of result if the expression was simply
            // a context variable.
            if (natvisScope.IsContextVariable(expression.Value))
            {
                varInfo = varInfo.Clone(expression.FormatSpecifier);
            }

            return varInfo;
        }

        async Task<IVariableInformation> EvaluateLldbExpressionWithMetricsAsync(
            VsExpression expression, IVariableInformation variable, NatvisScope natvisScope,
            string displayName, ExpressionEvaluationStrategy strategy,
            ExpressionEvaluationRecorder.StepsRecorder stepsRecorder)
        {
            expression = expression.MapValue(v => ReplaceScopedNames(v, natvisScope?.ScopedNames));

            var lldbErrors = new List<string>();

            // A helper lambda function to construct an exception given the list of lldb errors.
            Func<IList<string>, ExpressionEvaluationFailed> createExpressionEvaluationException =
                errors =>
                {
                    var exceptionMsg =
                        $"Failed to evaluate expression, display name: {displayName}, " +
                        $"expression: {expression}";

                    errors = errors.Where(error => !string.IsNullOrEmpty(error)).ToList();
                    if (errors.Any())
                    {
                        exceptionMsg += $", info: {{{string.Join("; ", errors)}}}";
                    }

                    return new ExpressionEvaluationFailed(exceptionMsg);
                };

            if (strategy == ExpressionEvaluationStrategy.LLDB_EVAL ||
                strategy == ExpressionEvaluationStrategy.LLDB_EVAL_WITH_FALLBACK)
            {
                IVariableInformation value;
                LldbEvalErrorCode errorCode;

                using (var step = stepsRecorder.NewStep(ExpressionEvaluationEngine.LLDB_EVAL))
                {
                    value = await EvaluateExpressionLldbEvalAsync(variable, expression, displayName,
                                                                  natvisScope);
                    errorCode = (LldbEvalErrorCode)Enum.ToObject(typeof(LldbEvalErrorCode),
                                                                 value.ErrorCode);

                    step.Finalize(errorCode);
                }

                if (errorCode == LldbEvalErrorCode.Ok)
                {
                    value.FallbackValueFormat = variable.FallbackValueFormat;
                    return value;
                }

                lldbErrors.Add(value?.ErrorMessage);

                if (errorCode == LldbEvalErrorCode.InvalidNumericLiteral ||
                    errorCode == LldbEvalErrorCode.InvalidOperandType ||
                    errorCode == LldbEvalErrorCode.UndeclaredIdentifier)
                {
                    // In the case of a well-known error, there's no need to fallback to
                    // LLDB, as it will fail with the same error.
                    throw createExpressionEvaluationException(lldbErrors);
                }
            }

            if (strategy == ExpressionEvaluationStrategy.LLDB)
            {
                // If lldb-eval is not enabled, try to interpret the expression as member access
                // before using LLDB to evaluate the expression in the context of the variable.
                IVariableInformation value;
                LLDBErrorCode errorCode;

                using (var step =
                    stepsRecorder.NewStep(ExpressionEvaluationEngine.LLDB_VARIABLE_PATH))
                {
                    value = GetValueForMemberAccessExpression(variable, expression, displayName);
                    errorCode = value != null && !value.Error
                        ? LLDBErrorCode.OK
                        : LLDBErrorCode.ERROR;

                    step.Finalize(errorCode);
                }

                if (errorCode == LLDBErrorCode.OK)
                {
                    value.FallbackValueFormat = variable.FallbackValueFormat;
                    return value;
                }

                lldbErrors.Add(value?.ErrorMessage);
            }

            if (strategy == ExpressionEvaluationStrategy.LLDB ||
                strategy == ExpressionEvaluationStrategy.LLDB_EVAL_WITH_FALLBACK)
            {
                IVariableInformation value;
                LLDBErrorCode errorCode;

                using (var step = stepsRecorder.NewStep(ExpressionEvaluationEngine.LLDB))
                {
                    value = await EvaluateExpressionInVariableScopeAsync(
                        variable, expression, displayName);
                    errorCode = value != null && !value.Error
                        ? LLDBErrorCode.OK
                        : LLDBErrorCode.ERROR;

                    step.Finalize(errorCode);
                }

                if (errorCode == LLDBErrorCode.OK)
                {
                    value.FallbackValueFormat = variable.FallbackValueFormat;
                    return value;
                }

                lldbErrors.Add(value?.ErrorMessage);
            }

            throw createExpressionEvaluationException(lldbErrors);
        }

        /// <summary>
        /// Declare a variable in using the given variable scope to execute the value expression.
        /// Token replacement using scopedNames is done against both the variable name and the
        /// value expression.
        /// </summary>
        /// <exception cref="ExpressionEvaluationFailed">
        /// Expression to declare the variable failed to evaluate.
        /// </exception>
        public async Task DeclareVariableAsync(IVariableInformation variable, string variableName,
                                               string valueExpression, NatvisScope natvisScope)
        {
            string scratchVar = ReplaceScopedNames(variableName, natvisScope?.ScopedNames);
            VsExpression vsExpression =
                _vsExpressionCreator.Create(valueExpression, "")
                    .MapValue(e => ReplaceScopedNames(e, natvisScope?.ScopedNames));

            // Declare variable and return it. Pure declaration expressions will always return
            // error because these expressions don't return a valid value.
            VsExpression createExpression =
                vsExpression.MapValue(e => $"auto {scratchVar}={e}; {scratchVar}");
            if (variable.IsPointer || variable.IsReference)
            {
                variable = variable.Dereference();
                if (variable == null)
                {
                    string failMsg = $"Failed to dereference pointer: Name: {variableName}";
                    _logger.Error(failMsg);
                    throw new ExpressionEvaluationFailed(failMsg);
                }
            }

            // TODO: Split the logic for LLDB and lldb-eval. Currently, LLDB is always
            // used to create a scratch variable (even if lldb-eval is the selected engine).
            IVariableInformation result =
                await variable.EvaluateExpressionAsync(variableName, createExpression);
            if (result != null && !result.Error && natvisScope != null)
            {
                // Result of 'auto {scratchVar}={e}; {scratchVar}' creates a copy of the scratch
                // variable. Evaluating '{scratchVar}' returns the reference to the original
                // variable. By using the original variable we make sure that the we always use its
                // up-to-date value.
                // TODO: Use RemoteFrame.FindValue to get the scratch variable.
                // EvaluateExpression method already is optimised for the case of fetching scratch
                // variables, but it isn't a convenient one.
                result = await variable.EvaluateExpressionAsync(
                    variableName, _vsExpressionCreator.Create($"{scratchVar}", ""));

                if (result != null && !result.Error)
                {
                    natvisScope.AddContextVariable(scratchVar, result.GetRemoteValue());
                    return;
                }
            }

            string msg = $"Failed to declare variable: Name: {variableName}, " +
                $"Expression: {valueExpression}";

            string resultMessage = result?.ErrorMessage;
            if (!string.IsNullOrEmpty(resultMessage))
            {
                msg += $", Info: {{{resultMessage}}}";
            }

            _logger.Error(msg);
            throw new ExpressionEvaluationFailed(msg);
        }

        /// <summary>
        /// Evaluates a condition in the context of a variable.
        /// </summary>
        /// <returns>The result of the condition evaluation.</returns>
        public async Task<bool> EvaluateConditionAsync(string condition,
                                                       IVariableInformation variable,
                                                       NatvisScope natvisScope)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return true;
            }

            IVariableInformation exprValue =
                await EvaluateExpressionAsync(condition, variable, natvisScope, null);
            return exprValue.IsTruthy;
        }

        /// <summary>
        /// Tries to resolve the given expression assuming it is a member of the given variable. If
        /// the expression is not a member of the given variable, null is returned. Otherwise, a
        /// variable resulting from evaluating the expression is returned.
        /// </summary>
        IVariableInformation GetValueForMemberAccessExpression(IVariableInformation variable,
                                                               VsExpression vsExpression,
                                                               string displayName)
        {
            if (!vsExpression.Value.StartsWith("["))
            {
                vsExpression = vsExpression.Clone((variable.IsPointer
                                                      ? "->"
                                                      : ".") + vsExpression.Value);
            }

            if (!NatvisTextMatcher.IsExpressionPath(vsExpression.Value))
            {
                return null;
            }

            var value = variable.GetValueForExpressionPath(vsExpression);
            if (value == null || value.Error)
            {
                return null;
            }

            if (displayName != null)
            {
                return new NamedVariableInformation(value, displayName);
            }

            return value;
        }

        /// <summary>
        /// Try to evaluate the given expression in the context of the variable.
        /// Imagine the variable is a class and the expression is in a method of that class.
        /// Member variables can be accessed and take precedence over global variables of the same name.
        /// Globals can be accessed as well. Returns a variable representing the result of the evaluation
        /// or null if the expression can't be evaluated.
        /// </summary>
        Task<IVariableInformation> EvaluateExpressionInVariableScopeAsync(
            IVariableInformation variable, VsExpression vsExpression, string displayName)
        {
            if (variable.IsPointer || variable.IsReference)
            {
                variable = variable.Dereference();
            }

            return variable.EvaluateExpressionAsync(displayName, vsExpression);
        }

        /// <summary>
        /// Tries to resolve the given expression in the context of the variable using lldb-eval.
        /// Returns a variable representing the result of the evaluation or a variable with error
        /// if the expression can't be evaluated using lldb-eval.
        /// </summary>
        Task<IVariableInformation> EvaluateExpressionLldbEvalAsync(IVariableInformation variable,
                                                                   VsExpression vsExpression,
                                                                   string displayName,
                                                                   NatvisScope natvisScope)
        {
            if (variable.IsPointer)
            {
                variable = variable.Dereference();
            }

            return variable.EvaluateExpressionLldbEvalAsync(displayName, vsExpression,
                                                            natvisScope?.ContextVariables);
        }

        /// <summary>
        /// Replace occurrences of the scopedNames dictionary keys with the corresponding
        /// values.
        /// </summary>
        static internal string ReplaceScopedNames(string expression,
                                                  IDictionary<string, string> scopedNames)
        {
            TokenSubstitutor replaceTemplateTokens = token =>
            {
                string res;
                if (scopedNames != null && scopedNames.TryGetValue(token, out res))
                {
                    return res;
                }

                return null;
            };

            return SubstituteExpressionTokens(expression, replaceTemplateTokens);
        }

        /// <summary>
        /// Yields tokens (variable name, template token, etc) to the token substitutors. A
        /// token is replaced by the first non-null value returned by a token substitutor. The
        /// resulting expression is returned.
        /// </summary>
        static string SubstituteExpressionTokens(string expression,
                                                 params TokenSubstitutor[] tokenSubstitutors)
        {
            StringBuilder result = new StringBuilder();
            int pos = 0;
            do
            {
                Match m = _varNameRegex.Match(expression, pos);
                if (!m.Success)
                {
                    break; // failed to match a name
                }

                result.Append(expression.Substring(pos, m.Index - pos));
                pos = m.Index;
                bool found = false;

                foreach (var p in tokenSubstitutors)
                {
                    string repl = p(m.Value);
                    if (repl != null)
                    {
                        result.Append(repl);
                        found = true;
                        break; // found a substitute
                    }
                }

                if (!found)
                {
                    result.Append(m.Value); // no name replacement to perform
                }

                pos = m.Index + m.Length;
                Match sub = _subfieldNameHereRegex.Match(expression, pos); // span the subfields
                if (sub.Success)
                {
                    result.Append(expression.Substring(pos, sub.Length));
                    pos = pos + sub.Length;
                }
            } while (pos < expression.Length);

            if (pos < expression.Length)
            {
                result.Append(expression.Substring(pos, expression.Length - pos));
            }

            return result.ToString();
        }
    }
}