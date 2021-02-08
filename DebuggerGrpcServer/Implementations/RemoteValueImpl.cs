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

﻿using LldbApi;
using System.Collections.Generic;
using System.Linq;
using YetiCommon;
using YetiVSI.DebugEngine;

namespace DebuggerGrpcServer
{
    public class RemoteValueImpl : RemoteValue
    {
        public class Factory
        {
            readonly ILldbExpressionOptionsFactory _expressionOptionsFactory;

            public Factory(ILldbExpressionOptionsFactory expressionOptionsFactory)
            {
                _expressionOptionsFactory = expressionOptionsFactory;
            }

            public RemoteValue Create(SbValue sbValue) =>
                sbValue != null ?
                new RemoteValueImpl(sbValue, this, _expressionOptionsFactory) : null;
        }

        readonly SbValue _sbValue;
        readonly Factory _valueFactory;
        readonly ILldbExpressionOptionsFactory _expressionOptionsFactory;

        private RemoteValueImpl(SbValue sbValue, Factory valueFactory,
            ILldbExpressionOptionsFactory expressionOptionsFactory)
        {
            _sbValue = sbValue;
            _valueFactory = valueFactory;
            _expressionOptionsFactory = expressionOptionsFactory;
        }

        public string GetName() => _sbValue.GetName();

        public string GetValue() => _sbValue.GetValue();

        public void SetFormat(ValueFormat format) => _sbValue.SetFormat(format);

        public SbType GetTypeInfo() => _sbValue.GetTypeInfo();

        public string GetTypeName() => _sbValue.GetTypeName();

        public ValueType GetValueType() => _sbValue.GetValueType().ConvertTo<ValueType>();

        public SbError GetError() => _sbValue.GetError();

        public uint GetNumChildren() => _sbValue.GetNumChildren();

        public List<RemoteValue> GetChildren(uint offset, uint count) =>
            _sbValue.GetChildren(offset, count)
                .Select(child => _valueFactory.Create(child)).ToList();

        public RemoteValue CreateValueFromExpression(string name, string expression)
        {
            SbExpressionOptions options = _expressionOptionsFactory.Create();
            options.SetAutoApplyFixIts(false);
            return _valueFactory.Create(
                _sbValue.CreateValueFromExpression(name, expression, options));
        }

        public RemoteValue CreateValueFromAddress(string name, ulong address, SbType type)
        {
            return _valueFactory.Create(_sbValue.CreateValueFromAddress(name, address, type));
        }

        public RemoteValue EvaluateExpression(string expression)
        {
            SbExpressionOptions options = _expressionOptionsFactory.Create();
            options.SetAutoApplyFixIts(false);
            return _valueFactory.Create(_sbValue.EvaluateExpression(expression, options));
        }

        public RemoteValue EvaluateExpressionLldbEval(
            string expression, IDictionary<string, SbValue> contextVariables) =>
            _valueFactory.Create(LldbEval.EvaluateExpression(_sbValue, expression,
                                                             contextVariables));

        public RemoteValue Dereference() => _valueFactory.Create(_sbValue.Dereference());

        public RemoteValue GetChildMemberWithName(string name) =>
            _valueFactory.Create(_sbValue.GetChildMemberWithName(name));

        public RemoteValue AddressOf() => _valueFactory.Create(_sbValue.AddressOf());

        public bool TypeIsPointerType() => _sbValue.TypeIsPointerType();

        public RemoteValue GetValueForExpressionPath(string expressionPath) =>
            _valueFactory.Create(_sbValue.GetValueForExpressionPath(expressionPath));

        public bool GetExpressionPath(out string path) =>
            _sbValue.GetExpressionPath(out path);

        public SbValue GetSbValue() => _sbValue;

        public string GetSummary() => _sbValue.GetSummary();

        public ulong GetByteSize() => _sbValue.GetByteSize();

        public ulong GetValueAsUnsigned() => _sbValue.GetValueAsUnsigned();

        public byte[] GetPointeeAsByteString(uint charSize, uint maxStringSize, out string error)
            => _sbValue.GetPointeeAsByteString(charSize, maxStringSize, out error);
    }
}
