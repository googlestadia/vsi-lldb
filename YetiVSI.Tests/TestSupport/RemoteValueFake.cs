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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TestsCommon.TestSupport;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.TestSupport
{
    // Fake test double implementation of a RemoteValue.
    //
    // Limitations:
    //   - Has limited support for ValueFormat.
    //   - Does not support assigning new values.
    public class RemoteValueFake : RemoteValue
    {
        private static Regex s_childRegex;

        private string name;
        private string value;
        private DebuggerApi.ValueType valueType;
        private ValueFormat valueFormat;
        private SbType sbType;
        private SbError sbError;
        private List<RemoteValue> children;
        private string summary;
        private Dictionary<string, Queue<RemoteValue>> expressionValues;
        private Dictionary<ulong, RemoteValue> valuesForAddress;
        private RemoteValue addressOf;
        private RemoteValue dereference;

        private RemoteValueFake parent;

        static RemoteValueFake()
        {
            s_childRegex = new Regex(@"^(\[[0-9]+\]|[a-zA-Z_][a-zA-Z_0-9]*)");
        }

        public RemoteValueFake(string name, string value)
        {
            this.name = name;
            this.value = value;
            valueType = DebuggerApi.ValueType.Invalid;
            valueFormat = ValueFormat.Default;
            children = new List<RemoteValue>();
            expressionValues = new Dictionary<string, Queue<RemoteValue>>();
            valuesForAddress = new Dictionary<ulong, RemoteValue>();
            sbError = new SbErrorStub(true);
            addressOf = null;
            dereference = null;
        }

        public void SetAddressOf(RemoteValue addressOf)
        {
            this.addressOf = addressOf;
        }

        public RemoteValue AddressOf()
        {
            return addressOf;
        }

        public void AddValueFromExpression(string expression, RemoteValue value)
        {
            Queue<RemoteValue> values;
            if (!expressionValues.TryGetValue(expression, out values))
            {
                values = new Queue<RemoteValue>();
                expressionValues[expression] = values;
            }
            values.Enqueue(value);
        }

        public void SetCreateValueFromAddress(ulong address, RemoteValue value)
        {
            valuesForAddress[address] = value;
        }

        public void AddStringLiteral(string value, string prefix = "")
        {
            // Use a custom StringLiteralType. We should not rely on the standard types like
            // const char* etc.
            string escapedValue = prefix + CStringEscapeHelper.Escape(value);
            AddValueFromExpression(
                escapedValue,
                RemoteValueFakeUtil.CreateClass("StringLiteralType", "", escapedValue));
        }

        // Resolves expressions to values added via AddValueFromExpression().
        // If there are multiple values added to the same expression it iterates them until we 
        // get to the last value, which is never discarded.
        //
        // Example:
        //   var box = new RemoteValueFake("myBox", "");
        //   box.AddValueFromExpression("expression", new RemoteValueFake("int", "1"));
        //   box.AddValueFromExpression("expression", new RemoteValueFake("int", "2"));
        //
        //   var t = box.CreateValueFromExpression("temp1", "expression");
        //   var y = box.CreateValueFromExpression("temp2", "expression");
        //   var z = box.CreateValueFromExpression("temp3", "expression");
        //   Assert.That(t.GetDefaultValue(), Is.EqualTo("1"));
        //   Assert.That(y.GetDefaultValue(), Is.EqualTo("2"));
        //   Assert.That(z.GetDefaultValue(), Is.EqualTo("2"));
        public RemoteValue CreateValueFromExpression(string name, string expression)
        {
            Queue<RemoteValue> values;
            if (expressionValues.TryGetValue(expression, out values))
            {
                RemoteValue resultValue = values.Count > 1 ? values.Dequeue() : values.Peek();
                return new RemoteValueNameDecorator(resultValue, name);
            }

            var remoteValueError = new RemoteValueFake("", "");
            remoteValueError.SetError(new SbErrorStub(false, "error: error: No value"));
            return remoteValueError;
        }

        public RemoteValue CreateValueFromAddress(string name, ulong address, SbType type)
        {
            if (valuesForAddress.TryGetValue(address, out RemoteValue value))
            {
                return new RemoteValueNameDecorator(value, name);
            }

            var remoteValueError = new RemoteValueFake("", "");
            remoteValueError.SetError(new SbErrorStub(false, "invalid"));
            return remoteValueError;
        }

        public Task<RemoteValue> CreateValueFromExpressionAsync(string name, string expression) =>
            Task.FromResult(CreateValueFromExpression(name, expression));

        // Resolves expressions to values added via AddValueFromExpression().
        //
        // Example:
        //   var box = new RemoteValueFake("myBox", "Box", "");
        //   box.AddValueFromExpression("true", new RemoteValueFake("", "bool", "true"));
        //
        //   var t = box.EvaluateExpression("temp1", "true");
        //   Assert.That(t.GetValue(), Is.EqualTo("true"));
        public RemoteValue EvaluateExpression(string expression)
        {
            Queue<RemoteValue> values;
            if (expressionValues.TryGetValue(expression, out values))
            {
                return values.Count > 1 ? values.Dequeue() : values.Peek();
            }

            var remoteValueError = new RemoteValueFake("", "");
            remoteValueError.SetError(new SbErrorStub(false, "error: error: No value"));
            return remoteValueError;
        }

        public Task<RemoteValue> EvaluateExpressionAsync(string expression) =>
            Task.FromResult(EvaluateExpression(expression));

        public Task<RemoteValue> EvaluateExpressionLldbEvalAsync(string expression) =>
            EvaluateExpressionAsync(expression);

        public RemoteValue Dereference()
        {
            if (dereference != null)
            {
                return dereference;
            }
            var remoteValueError = new RemoteValueFake("", "");
            remoteValueError.SetError(new SbErrorStub(false, 
                "error: error: Not a pointer type or no child"));
            return remoteValueError;
        }

        public void SetDereference(RemoteValue pointee)
        {
            dereference = pointee;
        }

        public void AddChild(RemoteValueFake child)
        {
            if (child == null)
            {
                throw new ArgumentException("The 'child' argument cannot be null.");
            }

            if (child.parent != null)
            {
                throw new InvalidOperationException(
                    "A RemoteValueFake cannot be added as a child more than once.");
            }

            if (child == this)
            {
                throw new InvalidOperationException(
                    "A RemoteValueFake cannot be added as a child of itself.");
            }

            child.parent = this;
            children.Add(child);
        }

        public List<RemoteValue> GetChildren(uint offset, uint count)
        {
            var values = new List<RemoteValue>();
            for (uint index = offset; index < offset + count; ++index)
            {
                values.Add(index < children.Count ? children[(int)index] : null);
            }
            return values;
        }

        public RemoteValue GetChildMemberWithName(string name)
        {
            foreach (var child in children)
            {
                if (child.GetName() == name)
                {
                    return child;
                }
            }
            return null;
        }

        public void SetError(SbError sbError)
        {
            this.sbError = sbError;
        }

        public SbError GetError()
        {
            return sbError;
        }

        public string GetName()
        {
            return name;
        }

        public uint GetNumChildren()
        {
            return (uint)children.Count;
        }

        public void SetSummary(string summary)
        {
            this.summary = summary;
        }

        public string GetSummary(ValueFormat format)
        {
            valueFormat = format;
            return summary;
        }

        public void SetTypeInfo(SbType sbType)
        {
            this.sbType = sbType;
        }

        public SbType GetTypeInfo()
        {
            return sbType;
        }

        public ulong GetValueAsUnsigned()
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToUInt64(value.Substring(2), 16);
            }
            return ulong.Parse(value);
        }

        public string GetTypeName()
        {
            return sbType?.GetName();
        }

        public string GetValue(ValueFormat format)
        {
            valueFormat = format;
            switch (valueFormat)
            {
                case ValueFormat.Hex:
                    return string.Format("0x{0:x}", long.Parse(value));
                case ValueFormat.HexUppercase:
                    return "0x" + string.Format("{0:x}", long.Parse(value)).ToUpper();
                case ValueFormat.Binary:
                    return "0b" + Convert.ToString(long.Parse(value), 2);
                case ValueFormat.VectorOfFloat32:
                case ValueFormat.VectorOfFloat64:
                case ValueFormat.Default:
                    return value;
                default:
                    throw new NotImplementedTestDoubleException(
                        $"ValueFormat '{valueFormat}' is not supported.");
            }
        }

        public ValueFormat GetFormat()
        {
            return valueFormat;
        }

        public void SetValueType(DebuggerApi.ValueType valueType)
        {
            this.valueType = valueType;
        }

        public DebuggerApi.ValueType GetValueType()
        {
            return valueType;
        }

        public virtual bool TypeIsPointerType()
        {
            return sbType != null && (sbType.GetTypeFlags().HasFlag(TypeFlags.IS_POINTER) ||
                sbType.GetTypeFlags().HasFlag(TypeFlags.INSTANCE_IS_POINTER));
        }

        public virtual RemoteValue GetValueForExpressionPath(string expressionPath)
        {
            var noValueError = new RemoteValueFake(null, null);
            noValueError.SetError(new SbErrorStub(false, "error: No value"));
            noValueError.SetValueType(DebuggerApi.ValueType.ConstResult);

            if (!ProcessExpressionPath(ref expressionPath))
            {
                return noValueError;
            }

            int index = 0;
            Match match = s_childRegex.Match(expressionPath, index);
            if (!match.Success)
            {
                return noValueError;
            }

            var child = GetChildMemberWithName(match.Value);
            if (child == null)
            {
                return noValueError;
            }

            var childExpressionPath = expressionPath.Substring(match.Length);
            if (childExpressionPath.Length == 0)
            {
                return child;
            }

            return child.GetValueForExpressionPath(childExpressionPath);
        }

        private bool ProcessExpressionPath(ref string expressionPath)
        {
            if (expressionPath.StartsWith("."))
            {
                if (TypeIsPointerType())
                {
                    return false;
                }

                expressionPath = expressionPath.Substring(1);
                return true;
            }
            else if (expressionPath.StartsWith("->"))
            {
                if (!TypeIsPointerType())
                {
                    return false;
                }

                expressionPath = expressionPath.Substring(2);
                return true;
            }
            else if (expressionPath.StartsWith("["))
            {
                return true;
            }
            return false;
        }

        public virtual bool GetExpressionPath(out string path)
        {
            string parentPath = "";
            parent?.GetExpressionPath(out parentPath);

            var op = "";
            if (parent != null && !name.StartsWith("["))
            {
                op = parent.TypeIsPointerType() ? "->" : ".";
            }
            path = $"{parentPath}{op}{name}";
            return true;
        }

        // Useful for test log output.
        public override string ToString()
        {
            return $"RemoteValueFake(Name={name},TypeName={GetTypeName()},Value={value})";
        }

        public RemoteValue GetCachedView(ValueFormat format)
        {
            valueFormat = format;
            return this;
        }

        public ulong GetByteSize()
        {
            return GetTypeInfo().GetByteSize();
        }

        public byte[] GetPointeeAsByteString(uint charSize, uint maxStringSize, out string error)
        {
            throw new NotImplementedException();
        }
    }
}
