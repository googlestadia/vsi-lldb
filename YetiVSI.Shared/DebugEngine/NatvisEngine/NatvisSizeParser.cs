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
using System.Globalization;
using System.Threading.Tasks;
using DebuggerApi;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class NatvisSizeParser
    {
        readonly NatvisDiagnosticLogger _logger;
        readonly NatvisExpressionEvaluator _evaluator;

        public NatvisSizeParser(NatvisDiagnosticLogger logger, NatvisExpressionEvaluator evaluator)
        {
            _logger = logger;
            _evaluator = evaluator;
        }

        /// <summary>
        /// Processes a SizeType array and returns the first valid size value.
        ///
        /// Throws ExpressionEvaluationFailed in case of an evaluation error and
        /// InvalidOperationException if valid <Size> node is not found.
        /// </summary>
        /// <returns></returns>
        internal async Task<uint> ParseSizeAsync(SizeType[] sizes, IVariableInformation varInfo,
                                                 NatvisScope natvisScope)
        {
            if (sizes == null)
            {
                throw new InvalidOperationException("Valid <Size> node not found.");
            }

            foreach (SizeType curSize in sizes)
            {
                string errorMsg = null;
                string sizeText = null;
                try
                {
                    if (!NatvisViewsUtil.IsViewVisible(varInfo.FormatSpecifier, curSize.IncludeView,
                                                       curSize.ExcludeView) ||
                        !await _evaluator.EvaluateConditionAsync(curSize.Condition, varInfo,
                                                                 natvisScope))
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(curSize.Value))
                    {
                        errorMsg = "The expression cannot be empty.";
                    }
                    else
                    {
                        IVariableInformation sizeVarInfo = await _evaluator.EvaluateExpressionAsync(
                            curSize.Value, varInfo, natvisScope, null);

                        sizeVarInfo.FallbackValueFormat = ValueFormat.Default;
                        sizeText = await sizeVarInfo.ValueAsync();
                    }
                }
                catch (ExpressionEvaluationFailed ex) when (curSize.Optional)
                {
                    errorMsg = ex.Message;
                }

                uint size = 0;
                if (errorMsg == null)
                {
                    if (!ParseUint(sizeText, out size))
                    {
                        errorMsg = "The expression's value was not a number. " +
                            $"Expression='{curSize.Value}' Value='{sizeText}'";
                    }
                }

                if (errorMsg != null)
                {
                    if (!curSize.Optional)
                    {
                        throw new ExpressionEvaluationFailed("Failed to evaluate <Size> node. " +
                                                             errorMsg);
                    }

                    _logger.Verbose(() => $"Failed to evaluate <Size> node for type" +
                                        $" '{varInfo.TypeName}'. Reason: {errorMsg}");
                }
                else
                {
                    return size;
                }
            }

            throw new InvalidOperationException("Valid <Size> node not found.");
        }

        /// <summary>
        /// Helper function to parse a string as a uint.
        /// </summary>
        /// <param name="str">The string with uint in decimal or hexadecimal format.</param>
        /// <param name="value">The resolved uint.</param>
        /// <returns>true if str was parsed successfully.</returns>
        public bool ParseUint(string str, out uint value)
        {
            if (string.IsNullOrEmpty(str))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return uint.TryParse(str.Substring(2), NumberStyles.HexNumber,
                                     CultureInfo.InvariantCulture, out value);
            }

            return uint.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                 out value);
        }
    }
}