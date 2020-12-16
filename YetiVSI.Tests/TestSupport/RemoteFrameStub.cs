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
using DebuggerCommonApi;
using System.Linq;
using TestsCommon.TestSupport;
using System.Threading.Tasks;

namespace YetiVSI.Test.TestSupport
{

    /// <summary>
    /// RemoteFrameStub currently allows you to configure behavior for evaluating expressions. All
    /// other RemoteFrame interface throw NotImplementedTestDoubleException. Implementations to
    /// these methods can be provided as use cases arise.
    /// </summary>
    public class RemoteFrameStub : RemoteFrame
    {
        public class Builder
        {
            readonly List<ExpressionResultPair> expressionResultPairs =
                new List<ExpressionResultPair>();

            public Builder AddExpressionResult(string expression, RemoteValue value)
            {
                expressionResultPairs.Add(
                    new ExpressionResultPair() { Expression = expression, Value = value });
                return this;
            }

            public RemoteFrameStub Build()
                => new RemoteFrameStub(expressionResultPairs.GetEnumerator());
        }

        readonly IEnumerator<ExpressionResultPair> expressionResultPairs;

        private class ExpressionResultPair
        {
            public string Expression { get; set; }
            public RemoteValue Value { get; set; }
        }

        public RemoteFrameStub()
            : this(Enumerable.Empty<ExpressionResultPair>().GetEnumerator())
        {
        }

        private RemoteFrameStub(IEnumerator<ExpressionResultPair> expressionResultPairs)
        {
            this.expressionResultPairs = expressionResultPairs;
        }

        /// <summary>
        /// Evaluate the given expression and return the configured RemoteValue.
        /// </summary>
        /// <exception cref="ConfigurationException">Thrown when a RemoteValue has not been
        /// configured for the expression.</exception>
        public Task<RemoteValue> EvaluateExpressionAsync(string expression)
        {
            var hasCurrent = expressionResultPairs.MoveNext();
            if (!hasCurrent)
            {
                throw new ConfigurationException(
                    $"Configuration invalid, no expression results left for: {expression}");
            }
            if (expressionResultPairs.Current.Expression != expression)
            {
                throw new ConfigurationException(
                    "Configuration invalid, next expression isn't a match. " +
                    $"Got: {expressionResultPairs.Current.Expression}, Looking for: {expression}");
            }
            return Task.FromResult(expressionResultPairs.Current.Value);
        }

        #region Not Implemented

        public RemoteValue FindValue(string varName, DebuggerApi.ValueType value_type)
        {
            throw new NotImplementedTestDoubleException();
        }

        public SbFunction GetFunction()
        {
            throw new NotImplementedTestDoubleException();
        }

        public string GetFunctionName()
        {
            throw new NotImplementedTestDoubleException();
        }

        public FrameInfo<SbModule>? GetInfo(FrameInfoFlags fields)
        {
            throw new NotImplementedTestDoubleException();
        }

        public LineEntryInfo GetLineEntry()
        {
            throw new NotImplementedTestDoubleException();
        }

        public SbModule GetModule()
        {
            throw new NotImplementedTestDoubleException();
        }

        public ulong GetPC()
        {
            throw new NotImplementedTestDoubleException();
        }

        public AddressRange GetPhysicalStackRange()
        {
            throw new NotImplementedTestDoubleException();
        }

        public List<RemoteValue> GetRegisters()
        {
            throw new NotImplementedTestDoubleException();
        }

        public SbSymbol GetSymbol()
        {
            throw new NotImplementedTestDoubleException();
        }

        public RemoteThread GetThread()
        {
            throw new NotImplementedTestDoubleException();
        }

        public RemoteValue GetValueForVariablePath(string varPath)
        {
            throw new NotImplementedTestDoubleException();
        }

        public List<RemoteValue> GetVariables(bool arguments, bool locals, bool statics,
            bool only_in_scope)
        {
            throw new NotImplementedTestDoubleException();
        }

        public Task<RemoteValue> EvaluateExpressionLldbEvalAsync(string text)
        {
            throw new NotImplementedException();
        }

        #endregion

        public class ConfigurationException : Exception
        {
            public ConfigurationException(string message) : base(message)
            {
            }
        }
    }
}