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
using System.Diagnostics;
using System.Threading.Tasks;

namespace YetiVSI.DebugEngine.Variables
{
    public class FormatSpecifier
    {
        public FormatSpecifier(string expression)
        {
            Expression = expression;
        }

        public FormatSpecifier(string expression, uint size)
        {
            Expression = expression;
            Size = size;
        }

        /// <summary>
        /// Format specifier for the VS expression, e.g. "!view(myView)" or "[mySize]s".
        /// </summary>
        public string Expression { get; }

        /// <summary>
        /// Evaluated size from the size specifier expression. It's null if there is no
        /// size specifier expression.
        /// </summary>
        public uint? Size { get; }

        public static readonly FormatSpecifier EMPTY = new FormatSpecifier("");
    }

    /// <summary>
    /// An expression that may or may not contain a format specifier.
    /// </summary>
    public class VsExpression
    {
        public VsExpression(string value, FormatSpecifier formatSpecifier)
        {
            Value = value;
            FormatSpecifier = formatSpecifier;
        }

        /// <summary>
        /// Gets the value of the expression.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets the format specifier.
        /// </summary>
        public FormatSpecifier FormatSpecifier { get; }

        /// <summary>
        /// Clone the expression and specify a new value portion.
        /// </summary>
        public VsExpression Clone(string value) => new VsExpression(value, FormatSpecifier);

        /// <summary>
        /// Maps the expression value to a new value and keeps the format specifier.
        /// </summary>
        public VsExpression MapValue(Func<string, string> f) => Clone(value: f(Value));

        public override string ToString() => string.IsNullOrEmpty(FormatSpecifier?.Expression)
                                                 ? Value
                                                 : $"{Value},{FormatSpecifier.Expression}";
    }

    /// <summary>
    /// This class is responsible for parsing an expression into the raw and format specifier
    /// components.
    /// </summary>
    public class VsExpressionCreator
    {
        public VsExpressionCreator()
        {
        }

        /// <summary>
        /// Create a VsExpresion from a string representation of an expression
        /// </summary>
        public async Task<VsExpression> CreateAsync(string expression,
                                                    Func<string, Task<uint>> evaluate)
        {
            expression = Preprocess(expression);

            if (!TrySplitSizeSpecifierPrefix(expression, out string expressionWithSize,
                                             out string validSuffix))
            {
                // If we have not found a right bracket that can be part of an expresssion
                // size specifier, just try to split around the last comma.
                return ParseWithSimpleFormatter(expression);
            }

            // We have a candidate for the expression-with-size and a valid suffix.
            // For example, for "var,[len]!x", the expression-with-size would be "var,[len]"
            // and the suffix is "!x".

            // Now try to split the expression and the size. We use our clever splitter that
            // can handle the situation when the size specifier contains brackets or commas.
            VsExpression parsedExpressionAndSize = ParseWithExpressionFormatter(expressionWithSize);
            if (string.IsNullOrEmpty(parsedExpressionAndSize.FormatSpecifier.Expression))
            {
                return new VsExpression(expression, FormatSpecifier.EMPTY);
            }

            var sizeExpression = parsedExpressionAndSize.FormatSpecifier.Expression;
            // Remove the starting '[' and ending ']'.
            sizeExpression = sizeExpression.Substring(1, sizeExpression.Length - 2);

            var formatSpecifier = new FormatSpecifier(
                parsedExpressionAndSize.FormatSpecifier.Expression + validSuffix);
            try
            {
                uint size = await evaluate(sizeExpression);
                formatSpecifier = new FormatSpecifier(
                    parsedExpressionAndSize.FormatSpecifier.Expression + validSuffix, size);
            }
            catch (ExpressionEvaluationFailed e)
            {
                Trace.WriteLine(
                    $"ERROR: Failed to resolve size format expression: {sizeExpression}." +
                    $" Reason: {e.Message}.");
            }

            return new VsExpression(parsedExpressionAndSize.Value, formatSpecifier);
        }

        /// <summary>
        /// Tries to split the |expression| input into valid formatter suffix and a candidate
        /// for an expression with an expression size specifier (i.e., size specifier of the form
        /// [&lt;expr&gt;].
        ///
        /// Note that the method does not ensure validity of |prefixWithSizeSpecifier|, it only
        /// splits based on a valid suffix (|baseFormatter|).
        ///
        /// Returns false if it cannot find a right bracket followed by a valid formatter suffix.
        /// </summary>
        bool TrySplitSizeSpecifierPrefix(string expression, out string prefixWithSizeSpecifier,
                                         out string baseFormatter)
        {
            prefixWithSizeSpecifier = "";
            baseFormatter = "";
            int rightSquareIndex = expression.LastIndexOf(']');
            // If there is no right square bracket, it cannot be an expression size specifier.
            if (rightSquareIndex == -1)
            {
                return false;
            }
            // If we have a right bracket, try to see if the rest of the potential formatter
            // is a valid suffix.
            string suffix = expression.Substring(rightSquareIndex + 1);
            if (RemoteValueFormatProvider.IsValidSizeSpecifierSuffix(suffix))
            {
                prefixWithSizeSpecifier = expression.Substring(0, rightSquareIndex + 1);
                baseFormatter = suffix;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Create a VsExpression from a raw value and a format specifier.
        /// </summary>
        public VsExpression Create(string value, string formatSpecifier) =>
            new VsExpression(value, new FormatSpecifier(formatSpecifier));

        string Preprocess(string expression)
        {
            string preprocessed = expression.Trim();
            if (preprocessed.EndsWith(","))
            {
                return preprocessed.Remove(preprocessed.Length - 1);
            }
            return preprocessed;
        }

        VsExpression ParseWithSimpleFormatter(string expression)
        {
            string formatSpecifier = "";
            string value = expression;

            int lastComma = expression.LastIndexOf(',');
            if (lastComma > 0)
            {
                string formatCandidate = expression.Substring(lastComma + 1).Trim();

                if (RemoteValueFormatProvider.IsValidFormat(formatCandidate))
                {
                    formatSpecifier = formatCandidate;
                    value = expression.Substring(0, lastComma);
                }
            }

            return new VsExpression(value, new FormatSpecifier(formatSpecifier));
        }

        /// <summary>
        /// Tries to split |expression| into the expression part and size format expression
        /// part. For example, "expr, [length]" is split into "expr" and "[length]".
        ///
        /// If the method cannot find valid split, the FormatSpecifier field of the return
        /// value is empty and Value contains the expression.
        ///
        /// Note that the method can only handle expression size specifiers in the formatter
        /// position. It cannot handle literal size specifiers (e.g., "expr, 2"), base formatters
        /// (e.g., "expr, x") or combined formatters (e.g., "expr,[2]x" or "expr,2x").
        /// </summary>
        VsExpression ParseWithExpressionFormatter(string expression)
        {
            ExpressionParser parser = new ExpressionParser(expression);
            List<Token> tokens;
            bool parsed = parser.Parse(out tokens);
            if (!parsed || tokens.Count == 0 ||
                tokens[tokens.Count - 1].Type != TokenType.CLOSE_BRACKET)
            {
                return new VsExpression(expression, FormatSpecifier.EMPTY);
            }

            string formatSpecifier = "";
            string value = expression;
            int bracketsCount = 1;
            int position = -1;
            for (int i = tokens.Count - 2; i >= 0; i--)
            {
                if (tokens[i].Type == TokenType.CLOSE_BRACKET)
                {
                    bracketsCount++;
                }
                else if (tokens[i].Type == TokenType.OPEN_BRACKET)
                {
                    bracketsCount--;
                }

                if (bracketsCount == 0)
                {
                    position = tokens[i].Position;
                    break;
                }
            }
            if (position != -1)
            {
                var exprCandidate = expression.Substring(0, position).Trim();
                if (exprCandidate.EndsWith(","))
                {
                    value =
                        exprCandidate.Remove(exprCandidate.Length - 1).Trim();
                    formatSpecifier = expression.Substring(position).Trim();
                }
            }
            return new VsExpression(value, new FormatSpecifier(formatSpecifier));
        }
    }
}
