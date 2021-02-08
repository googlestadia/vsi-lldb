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

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Interfaces;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class NatvisStringFormatter
    {
        internal class FormatStringContext
        {
            internal IEnumerable<IStringElement> StringElements { get; set; }
                = Enumerable.Empty<IStringElement>();

            internal NatvisScope NatvisScope { get; set; } = new NatvisScope();
        }

        readonly NatvisExpressionEvaluator _evaluator;
        readonly NatvisDiagnosticLogger _logger;
        readonly NatvisVisualizerScanner _visualizerScanner;
        readonly ITaskExecutor _taskExecutor;

        const int _maxFormatDepth = 10;
        const int _maxCharactersForPreview = 80;
        uint _curFormatStringElementDepth;

        static readonly Regex _expressionRegex = new Regex(@"^\{[^\}]*\}");

        public NatvisStringFormatter(NatvisExpressionEvaluator evaluator,
                                     NatvisDiagnosticLogger logger,
                                     NatvisVisualizerScanner visualizerScanner,
                                     ITaskExecutor taskExecutor)
        {
            _curFormatStringElementDepth = 0;

            _evaluator = evaluator;
            _logger = logger;
            _visualizerScanner = visualizerScanner;
            _taskExecutor = taskExecutor;
        }

        /// <summary>
        /// Formats the value for display in the debugger, e.g. for the Value column in the Watch
        /// window. Mostly based on the Natvis <DisplayString> nodes.
        /// </summary>
        internal async Task<string> FormatDisplayStringAsync(IVariableInformation variable)
        {
            try
            {
                return await FormatDisplayStringAsync(
                    BuildFormatStringContext<DisplayStringType>(
                        variable, e => new DisplayStringElement(e)), variable);
            }
            catch (ExpressionEvaluationFailed ex)
            {
                _logger.Log(NatvisLoggingLevel.ERROR,
                            $"Failed to format natvis display string. Reason: {ex.Message}.");

                return await variable.ValueAsync();
            }
        }

        internal async Task<string> FormatDisplayStringAsync(
            FormatStringContext formatStringContext, IVariableInformation variable) =>
            await FormatStringAsync(formatStringContext, variable, FormatDisplayStringAsync,
                                    "display string",
                                    async () => await ValueStringBuilder.BuildAsync(variable));

        /// <summary>
        /// Builds the format string context and returns a formatted string view accordingly.
        /// </summary>
        /// <param name="variable">The variable that the format string context
        /// should be built for.</param>
        internal string FormatStringView(IVariableInformation variable)
        {
            try
            {
                return FormatStringView(
                    BuildFormatStringContext<StringViewType>(
                        variable, e => new StringViewElement(e)), variable);
            }
            catch (ExpressionEvaluationFailed ex)
            {
                _logger.Log(NatvisLoggingLevel.ERROR,
                            $"Failed to format natvis string view. Reason: {ex.Message}.");

                return variable.StringView;
            }
        }

        /// <summary>
        /// Formats the value for display in special string visualizers in the debugger (text, xml,
        /// html, json). The visualizers can be accessed e.g. from the magnifier symbol in the Value
        /// column in the Watch window. Mostly based on the Natvis <StringView> node.
        /// </summary>
        internal string FormatStringView(FormatStringContext formatStringContext,
                                         IVariableInformation variable) =>
            _taskExecutor.Run(async () =>
                                  await FormatStringAsync(formatStringContext, variable,
                                                          varInfo =>
                                                              Task.FromResult(
                                                                  FormatStringView(varInfo)),
                                                          "string view",
                                                          () => Task.FromResult(
                                                              variable.StringView)));

        /// <summary>
        /// Asynchronously returns a formatted string based on the format string context and
        /// variable provided.
        /// In case this method does not succeeded, it returns the fallback value specified.
        /// </summary>
        /// <param name="formatStringContext">The format string context that the formatted string
        /// should rely on</param>
        /// <param name="variable">The variable context used to evaluate expressions.</param>
        /// <param name="subexpressionFormatter">Delegate used to format subexpressions found
        /// within the string.</param>
        /// <param name="elementName">The Natvis element name that should be reported in logs.
        /// </param>
        /// <param name="fallbackValue">Fallback value used in case this method fails.</param>
        internal async Task<string> FormatStringAsync(
            FormatStringContext formatStringContext, IVariableInformation variable,
            Func<IVariableInformation, Task<string>> subexpressionFormatter,
            string elementName, Func<Task<string>> fallbackValue)
        {
            try
            {
                if (++_curFormatStringElementDepth > _maxFormatDepth)
                {
                    return "...";
                }

                foreach (var element in formatStringContext.StringElements)
                {
                    try
                    {
                        // e.g. <DisplayString>{{ size={_Mypair._Myval2._Mylast -
                        // _Mypair._Myval2._Myfirst} }}</DisplayString>
                        if (!NatvisViewsUtil.IsViewVisible(variable.FormatSpecifier,
                                                           element.IncludeView,
                                                           element.ExcludeView) ||
                            !await _evaluator.EvaluateConditionAsync(
                                element.Condition, variable, formatStringContext.NatvisScope))
                        {
                            continue;
                        }

                        return await FormatValueAsync(element.Value, variable,
                                                      formatStringContext.NatvisScope,
                                                      subexpressionFormatter);
                    }
                    catch (ExpressionEvaluationFailed ex)
                    {
                        if (!element.Optional)
                        {
                            throw;
                        }

                        string expression = variable == null ? null : await variable.ValueAsync();
                        _logger.Verbose(
                            () => $"Failed to evaluate natvis {elementName} expression" +
                                $"  '{expression}' for type " +
                                $"'{variable?.TypeName}'. Reason: {ex.Message}");
                    }
                    catch (Exception ex) when (ex is NotSupportedException ||
                        ex is InvalidOperationException)
                    {
                        _logger.Log(NatvisLoggingLevel.ERROR,
                                    $"Failed to format natvis {elementName}. " +
                                    $"Reason: {ex.Message}.");

                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(() =>
                                          $"Failed to format natvis {elementName} for type" +
                                          $" '{variable?.TypeName}'. " +
                                          $"Reason: {ex.Message}.{Environment.NewLine}" +
                                          $"Stacktrace:{Environment.NewLine}{ex.StackTrace}");

                        throw;
                    }
                }

                return await fallbackValue.Invoke();
            }
            finally
            {
                --_curFormatStringElementDepth;
            }
        }

        /// <summary>
        /// Asynchronously processes a mixed format string that contains literal text and
        /// expressions embedded inside curly braces. The expressions are evaluated within the
        /// context of the variable specified, and subsequently formatted using the subexpression
        /// formatter provided.
        /// </summary>
        /// <remarks>
        /// Examples:
        ///   FormatValueAsync("Some literal text and an {expression}", varInfo, natvisScope,
        ///   subexpressionFormatter);
        ///   FormatValueAsync("{{Escaped, literal text.}}", varInfo, natvisScope,
        ///   subexpressionFormatter);
        /// </remarks>
        async Task<string> FormatValueAsync(
            string format, IVariableInformation variable, NatvisScope natvisScope,
            Func<IVariableInformation, Task<string>> subexpressionFormatter)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Empty;
            }

            var value = new StringBuilder();
            for (int i = 0; i < format.Length; ++i)
            {
                if (format[i] == '{')
                {
                    if (i + 1 < format.Length && format[i + 1] == '{')
                    {
                        value.Append('{');
                        i++;
                        continue;
                    }

                    // start of expression
                    Match m = _expressionRegex.Match(format.Substring(i));
                    if (m.Success)
                    {
                        string expression = format.Substring(i + 1, m.Length - 2);
                        IVariableInformation exprValue = await _evaluator.EvaluateExpressionAsync(
                            expression, variable, natvisScope, null);
                        value.Append(await subexpressionFormatter(exprValue));
                        i += m.Length - 1;
                    }
                }
                else if (format[i] == '}')
                {
                    // Accept both } and }} as closing braces to match native behavior.
                    value.Append('}');
                    if (i + 1 < format.Length && format[i + 1] == '}')
                    {
                        i++;
                    }
                }
                else
                {
                    value.Append(format[i]);
                }
            }

            return value.ToString();
        }

        /// <summary>
        /// Returns a context value containing StringElements constructed from the TElement types
        /// that get found, plus a SmartPointerElement if the corresponding SmartPointerType is
        /// present, and the Natvis tokens to resolve in expressions involving those.
        /// </summary>
        FormatStringContext BuildFormatStringContext<TElement>(
            IVariableInformation variable, Func<TElement, IStringElement> stringElementConstructor)
            where TElement : class
        {
            VisualizerInfo visualizer = _visualizerScanner.FindType(variable);
            if (visualizer?.Visualizer.Items == null)
            {
                return new FormatStringContext();
            }

            object[] items = visualizer.Visualizer.Items;
            IEnumerable<IStringElement> stringElements =
                items.OfType<TElement>().Select(e => stringElementConstructor((TElement)e));

            // Fall back to the smart pointee.
            stringElements = stringElements.Concat(
                items.OfType<SmartPointerType>()
                    .Take(1)
                    .Where(e => !string.IsNullOrWhiteSpace(e.Value))
                    .Select(e => new SmartPointerStringElement(e)));

            return new FormatStringContext { StringElements = stringElements,
                                             NatvisScope = visualizer.NatvisScope };
        }
    }
}