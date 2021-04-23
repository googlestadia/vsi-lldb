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

namespace DebuggerGrpcClient.Implementations
{
    // Creates CachedValue objects.
    public class CachedValueFactory
    {
        public virtual RemoteValue Create(RemoteValue remoteProxy, RemoteValue addressOf,
            SbType typeInfo, string expressionPath, bool hasExpressionPath, uint numChildren,
            string summary, string typeName, string value, ValueType valueType, bool isPointerType,
            ValueFormat valueFormat, ulong byteSize)
            => new CachedValue(remoteProxy, addressOf, typeInfo, expressionPath, hasExpressionPath,
                numChildren, summary, typeName, value, valueType, isPointerType, valueFormat,
                byteSize);
    }

    class CachedValue : RemoteValue
    {
        private readonly RemoteValue remoteProxy;
        private readonly RemoteValue addressOf;

        // Prefetched fields
        private readonly SbError error;
        private readonly SbType typeInfo;
        private readonly string expressionPath;
        private readonly bool hasExpressionPath;
        private readonly string name;
        private readonly uint numChildren;
        private readonly string typeName;
        private readonly ValueType valueType;
        private readonly bool isPointerType;
        private readonly ulong byteSize;

        // These can change if another format is passed into getters.
        private string summary;
        private string value;
        private ValueFormat valueFormat;

        internal CachedValue(RemoteValue remoteProxy, RemoteValue addressOf, SbType typeInfo,
            string expressionPath, bool hasExpressionPath, uint numChildren, string summary,
            string typeName, string value, ValueType valueType, bool isPointerType,
            ValueFormat valueFormat, ulong byteSize)
        {
            this.remoteProxy = remoteProxy;
            this.addressOf = addressOf;
            this.typeInfo = typeInfo;
            this.expressionPath = expressionPath;
            this.hasExpressionPath = hasExpressionPath;
            this.numChildren = numChildren;
            this.summary = summary;
            this.typeName = typeName;
            this.value = value;
            this.valueType = valueType;
            this.isPointerType = isPointerType;
            this.valueFormat = valueFormat;
            this.byteSize = byteSize;

            // These values are prefeteched by remoteProxy.
            error = remoteProxy.GetError();
            name = remoteProxy.GetName();
        }

        #region Prefetched members getters

        public Debugger.Common.GrpcSbValue GrpcValue => remoteProxy.GrpcValue;

        public RemoteValue AddressOf() => addressOf;

        public SbError GetError() => error;

        public SbType GetTypeInfo() => typeInfo;

        public string GetName() => name;

        public uint GetNumChildren() => numChildren;

        public string GetTypeName() => typeName;

        public ValueType GetValueType() => valueType;

        public bool TypeIsPointerType() => isPointerType;

        public ulong GetByteSize() => byteSize;

        public ulong GetValueAsUnsigned() => remoteProxy.GetValueAsUnsigned();

        public bool GetExpressionPath(out string path)
        {
            path = expressionPath;
            return hasExpressionPath;
        }

        #endregion

        #region (Possibly) Remote calls

        public virtual List<RemoteValue> GetChildren(uint offset, uint count) =>
            remoteProxy.GetChildren(offset, count);

        public RemoteValue GetChildMemberWithName(string name) =>
            remoteProxy.GetChildMemberWithName(name);

        public RemoteValue CreateValueFromExpression(string name, string expression) =>
            remoteProxy.CreateValueFromExpression(name, expression);

        public RemoteValue CreateValueFromAddress(string name, ulong address, SbType type) =>
            remoteProxy.CreateValueFromAddress(name, address, type);

        public async Task<RemoteValue>
            CreateValueFromExpressionAsync(string name, string expression) =>
            await remoteProxy.CreateValueFromExpressionAsync(name, expression);

        public RemoteValue EvaluateExpression(string expression) =>
            remoteProxy.EvaluateExpression(expression);

        public async Task<RemoteValue> EvaluateExpressionAsync(string expression) =>
            await remoteProxy.EvaluateExpressionAsync(expression);

        public async Task<RemoteValue> EvaluateExpressionLldbEvalAsync(
            string expression, IDictionary<string, RemoteValue> scratchVariables = null) =>
            await remoteProxy.EvaluateExpressionLldbEvalAsync(expression, scratchVariables);

        public RemoteValue Clone() => remoteProxy.Clone();

        public RemoteValue Dereference() => remoteProxy.Dereference();

        public RemoteValue GetValueForExpressionPath(string expressionPath) =>
            remoteProxy.GetValueForExpressionPath(expressionPath);

        public string GetSummary(ValueFormat format)
        {
            UpdateFormat(format);
            return summary;
        }

        public string GetValue(ValueFormat format)
        {
            UpdateFormat(format);
            return value;
        }

        void UpdateFormat(ValueFormat format)
        {
            // Changing the format may cause the value/summary to change, so update their content.
            if (valueFormat != format)
            {
                valueFormat = format;
                value = remoteProxy.GetValue(format);
                summary = remoteProxy.GetSummary(format);
            }
        }

        public byte[] GetPointeeAsByteString(uint charSize, uint maxStringSize, out string error)
            => remoteProxy.GetPointeeAsByteString(charSize, maxStringSize, out error);

        #endregion

        #region Value conversions

        // Note: Doesn't matter if |format| != |this.valueFormat| here. The format would be updated
        // by GetValue() / GetSummary() calls.
        public RemoteValue GetCachedView(ValueFormat format) => this;

        #endregion
    }
}
