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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YetiVSI.Test.TestSupport
{
    // Helper class to make it easy for subclasses to decorate a RemoteValue.
    public class RemoteValueDecorator : RemoteValue
    {
        private readonly RemoteValue value;

        public RemoteValueDecorator(RemoteValue value)
        {
            this.value = value;
        }

        public virtual RemoteValue AddressOf()
        {
            return value.AddressOf();
        }

        public virtual RemoteValue CreateValueFromExpression(string name, string expression)
        {
            return value.CreateValueFromExpression(name, expression);
        }

        public async Task<RemoteValue> CreateValueFromExpressionAsync(string name,
            string expression) => await value.CreateValueFromExpressionAsync(name, expression);

        public virtual RemoteValue CreateValueFromAddress(
            string name, ulong address, SbType type) => value.CreateValueFromAddress(name, address,
                                                                                     type);

        public virtual RemoteValue EvaluateExpression(string expression)
        {
            return value.EvaluateExpression(expression);
        }

        public async Task<RemoteValue> EvaluateExpressionAsync(string expression) =>
            await value.EvaluateExpressionAsync(expression);

        public async Task<RemoteValue> EvaluateExpressionLldbEvalAsync(string expression) =>
            await value.EvaluateExpressionLldbEvalAsync(expression);

        public virtual RemoteValue Dereference()
        {
            return value.Dereference();
        }

        public virtual List<RemoteValue> GetChildren(uint offset, uint count)
        {
            return value.GetChildren(offset, count);
        }

        public virtual RemoteValue GetChildMemberWithName(string name)
        {
            return value.GetChildMemberWithName(name);
        }

        public virtual SbError GetError()
        {
            return value.GetError();
        }

        public virtual string GetName()
        {
            return value.GetName();
        }

        public virtual uint GetNumChildren()
        {
            return value.GetNumChildren();
        }

        public virtual string GetSummary(ValueFormat format)
        {
            return value.GetSummary(format);
        }

        public virtual SbType GetTypeInfo()
        {
            return value.GetTypeInfo();
        }

        public virtual string GetTypeName()
        {
            return value.GetTypeName();
        }

        public virtual string GetValue(ValueFormat format)
        {
            return value.GetValue(format);
        }

        public virtual DebuggerApi.ValueType GetValueType()
        {
            return value.GetValueType();
        }

        public virtual bool TypeIsPointerType()
        {
            return value.TypeIsPointerType();
        }

        public virtual RemoteValue GetValueForExpressionPath(string expressionPath)
        {
            return value.GetValueForExpressionPath(expressionPath);
        }

        public virtual bool GetExpressionPath(out string path)
        {
            return value.GetExpressionPath(out path);
        }

        public RemoteValue GetCachedView(ValueFormat format)
        {
            return value.GetCachedView(format);
        }

        public ulong GetByteSize() => value.GetByteSize();

        public ulong GetValueAsUnsigned() => value.GetValueAsUnsigned();

        public byte[] GetPointeeAsByteString(uint charSize, uint maxStringSize, out string error)
        {
            return value.GetPointeeAsByteString(charSize, maxStringSize, out error);
        }
    }
}
