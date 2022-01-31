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

namespace YetiVSI.DebugEngine.Variables
{
    /// <summary>
    /// Evaluate an expression to produce a remote value.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="remoteValue">
    /// The remote value produced by evaluating the expression ignoring its format specifier.
    /// </param>
    /// <param name="displayName">The display name to use.</param>
    public delegate bool ExpressionEvaluator(VsExpression expression,
                                             out RemoteValue remoteValue, out string displayName);

    /// <summary>
    /// This class creates IVariableInformation from expressions and remote values. If using an
    /// expression and the expression includes a format specifier, this will construct an
    /// IVariableInformation instance with the application strategy of the format specifier
    /// built in.
    /// </summary>
    public class VarInfoBuilder
    {
        readonly IVariableInformationFactory varInfoFactory;

        public VarInfoBuilder(IVariableInformationFactory varInfoFactory)
        {
            this.varInfoFactory = varInfoFactory;
        }

        /// <summary>
        /// Create an IVariableInformation instance from an expression. The expression may or
        /// may not contain a format specifier.
        /// </summary>
        /// <param name="remoteValue">The remote value associated with the expression.</param>
        /// <param name="displayName">
        /// The display name of the expression. Uses the value's name if not specified.
        /// </param>
        /// <param name="formatSpecifier">Value format specifier.</param>
        /// <returns></returns>
        public IVariableInformation Create(RemoteValue remoteValue, string displayName = null,
                                           FormatSpecifier formatSpecifier = null)
        {
            return varInfoFactory.Create(remoteValue, displayName, formatSpecifier);
        }
    }
}
