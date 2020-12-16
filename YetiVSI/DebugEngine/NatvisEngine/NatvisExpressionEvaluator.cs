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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.DebugEngine.Variables;

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

        public NatvisExpressionEvaluator(NatvisDiagnosticLogger logger,
                                         VsExpressionCreator vsExpressionCreator,
                                         IExtensionOptions extensionOptions)
        {
            _logger = logger;
            _vsExpressionCreator = vsExpressionCreator;
            // Instead of relying on the ExpressionEvaluationEngine flag directly, use
            // IExtensionOptions to get the flag. This will pick up configuration changes in
            // runtime.
            _extensionOptions = extensionOptions;
        }

        /// <summary>
        /// Invokes GetExpressionValue, but returns error variable in case of evaluation error.
        /// </summary>
        public async Task<IVariableInformation> GetExpressionValueOrErrorAsync(
            string expression, IVariableInformation variable,
            IDictionary<string, string> scopedNames, string displayName, string natvisType)
        {
            try
            {
                return await EvaluateExpressionAsync(expression, variable, scopedNames,
                                                     displayName);
            }
            catch (ExpressionEvaluationFailed e)
            {
                return NatvisErrorUtils.LogAndGetEvaluationError(
                    _logger, natvisType, variable?.TypeName, displayName, e.Message);
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
            string expression, IVariableInformation variable,
            IDictionary<string, string> scopedNames, string displayName)
        {
            var vsExpression =
                await _vsExpressionCreator.CreateAsync(expression, async (sizeExpression) => {
                    IVariableInformation value = await EvaluateLldbExpressionAsync(
                        _vsExpressionCreator.Create(sizeExpression, ""), variable, scopedNames,
                        displayName);
                    uint size;
                    if (!uint.TryParse(await value.ValueAsync(), out size))
                    {
                        throw new ExpressionEvaluationFailed("Expression isn't a uint");
                    }
                    return size;
                });
            return await EvaluateLldbExpressionAsync(vsExpression, variable, scopedNames,
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
        /// <param name="scopedNames">The Natvis tokens to be resolved before evaluation.</param>
        /// <param name="displayName">The display name given to the result. If null the underlying
        /// debugger's context specific name is used.</param>
        /// <returns>The expression result.</returns>
        async Task<IVariableInformation> EvaluateLldbExpressionAsync(
            VsExpression expression, IVariableInformation variable,
            IDictionary<string, string> scopedNames, string displayName)
        {
            bool variableReplaced = false;
            expression =
                expression.MapValue(v => ReplaceScopedNames(v, scopedNames, out variableReplaced));

            var lldbErrors = new List<string>();
            ExpressionEvaluationEngine engine = _extensionOptions.ExpressionEvaluationEngine;

            // A helper lambda function to construct an exception given the list of lldb errors.
            Func<IList<string>, ExpressionEvaluationFailed> createExpressionEvaluationException =
                errors =>
            {
                var exceptionMsg = $"Failed to evaluate expression, display name: {displayName}, " +
                                   $"expression: {expression}";

                errors = errors.Where(error => !string.IsNullOrEmpty(error)).ToList();
                if (errors.Any())
                {
                    exceptionMsg += $", info: {{{string.Join("; ", errors)}}}";
                }

                return new ExpressionEvaluationFailed(exceptionMsg);
            };

            // TODO: Come up with a solution to use scratch variables in the lldb-eval.
            // Don't use lldb-eval if there were scratch variables replaced in during scoped names
            // substitution.
            if (IsLldbEvalEnabled() && !variableReplaced)
            {
                // Try to evaluate expression using lldb-eval. This is much faster approach than
                // using a full featured expression evaluation by LLDB.
                var value =
                    await EvaluateExpressionLldbEvalAsync(variable, expression, displayName);
                if (value != null)
                {
                    var errorCode = (LldbEvalErrorCode)Enum.ToObject(typeof(LldbEvalErrorCode),
                                                                     value.ErrorCode);

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
            }

            if (engine == ExpressionEvaluationEngine.LLDB)
            {
                // If lldb-eval is not enabled, try to interpret the expression as member access
                // before using LLDB to evaluate the expression in the context of the variable.
                var value = GetValueForMemberAccessExpression(variable, expression, displayName);
                if (value != null && !value.Error)
                {
                    value.FallbackValueFormat = variable.FallbackValueFormat;
                    return value;
                }

                lldbErrors.Add(value?.ErrorMessage);
            }

            if (engine == ExpressionEvaluationEngine.LLDB ||
                engine == ExpressionEvaluationEngine.LLDB_EVAL_WITH_FALLBACK)
            {
                var value =
                    await EvaluateExpressionInVariableScopeAsync(variable, expression, displayName);
                if (value != null && !value.Error)
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
                                               string valueExpression,
                                               IDictionary<string, string> scopedNames)
        {
            string scratchVar = ReplaceScopedNames(variableName, scopedNames, out bool ignore);
            VsExpression vsExpression =
                _vsExpressionCreator.Create(valueExpression, "")
                    .MapValue(e => ReplaceScopedNames(e, scopedNames, out ignore));

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
            IVariableInformation result =
                await variable.EvaluateExpressionAsync(variableName, createExpression);
            if (result != null && !result.Error)
            {
                return;
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
                                                       IDictionary<string, string> scopedNames)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return true;
            }

            IVariableInformation exprValue = 
                await EvaluateExpressionAsync(condition, variable, scopedNames, null);
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
                vsExpression = vsExpression.Clone(
                    (variable.IsPointer ? "->" : ".") + vsExpression.Value);
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
        async Task<IVariableInformation> EvaluateExpressionInVariableScopeAsync(
            IVariableInformation variable, VsExpression vsExpression, string displayName)
        {
            if (variable.IsPointer || variable.IsReference)
            {
                variable = variable.Dereference();
            }
            return await variable?.EvaluateExpressionAsync(displayName, vsExpression);
        }

        /// <summary>
        /// Tries to resolve the given expression in the context of the variable using lldb-eval.
        /// Returns a variable representing the result of the evaluation or a variable with error
        /// if the expression can't be evaluated using lldb-eval.
        /// </summary>
        async Task<IVariableInformation> EvaluateExpressionLldbEvalAsync(
            IVariableInformation variable, VsExpression vsExpression, string displayName)
        {
            if (variable.IsPointer || variable.IsReference)
            {
                variable = variable.Dereference();
            }
            return await variable?.EvaluateExpressionLldbEvalAsync(displayName, vsExpression);
        }

        /// <summary>
        /// Replace occurrences of the scopedNames dictionary keys with the corresponding
        /// values. Output parameter |variableReplaced| indicates whether a scratch variable
        /// was replaced (e.g. var => $var_0).
        /// </summary>
        string ReplaceScopedNames(string expression, IDictionary<string, string> scopedNames,
                                  out bool variableReplaced)
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

            return SubstituteExpressionTokens(expression, out variableReplaced,
                                              replaceTemplateTokens);
        }

        /// <summary>
        /// Yields tokens (variable name, template token, etc) to the token substitutors. A
        /// token is replaced by the first non-null value returned by a token substitutor. The
        /// resulting expression is returned.
        /// </summary>
        string SubstituteExpressionTokens(string expression, out bool variableReplaced,
                                          params TokenSubstitutor[] tokenSubstitutors)
        {
            variableReplaced = false;
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
                        if (repl.StartsWith("$"))
                        {
                            // TODO: Improve check for scratch variable replacement.
                            // If the replacement string starts with a '$', then it is considered
                            // to be a scratch variable, e.g. var => $var_0.
                            variableReplaced = true;
                        }
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

        bool IsLldbEvalEnabled()
        {
            ExpressionEvaluationEngine engine = _extensionOptions.ExpressionEvaluationEngine;
            return engine == ExpressionEvaluationEngine.LLDB_EVAL ||
                   engine == ExpressionEvaluationEngine.LLDB_EVAL_WITH_FALLBACK;
        }
    }
}