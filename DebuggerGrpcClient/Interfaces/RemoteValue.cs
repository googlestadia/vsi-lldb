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

using Debugger.Common;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DebuggerApi
{
    // Enumeration of types of values.
    // Naming is intentionally camel case to match proto generated code and support
    // EnumUtil::ConvertTo<T>().
    public enum ValueType
    {
        Invalid = 0,
        VariableGlobal = 1,
        VariableStatic = 2,
        VariableArgument = 3,
        VariableLocal = 4,
        Register = 5,
        RegisterSet = 6,
        ConstResult = 7,
        VariableThreadLocal = 8,
    };

    /// <summary>
    /// Interface based off of the SBValue API.
    /// </summary>
    public interface RemoteValue
    {
        /// <summary>
        /// Return the value representation that can be used in gRPC messages.
        /// </summary>
        GrpcSbValue GrpcValue { get; }

        /// <summary>
        /// Returns the node's name.
        /// </summary>
        string GetName();

        /// <summary>
        /// Returns the node's value.
        /// Parent nodes that are solely for grouping may have a blank value.
        /// </summary>
        string GetValue(ValueFormat format);

        /// <summary>
        /// Returns the node's type information.
        /// </summary>
        SbType GetTypeInfo();

        /// <summary>
        /// Returns the type of this node's value.
        /// </summary>
        string GetTypeName();

        /// <summary>
        /// Returns the contents of this node's data, if it is in string or array form.
        /// </summary>
        string GetSummary(ValueFormat format);

        /// <summary>
        /// Returns a description of the value's type.
        /// </summary>
        ValueType GetValueType();

        /// <summary>
        /// Returns the value's error.
        /// </summary>
        SbError GetError();

        /// <summary>
        /// Returns the number of child values.
        /// </summary>
        uint GetNumChildren();

        /// <summary>
        /// Returns the child values at the provided index range [offset, offset + count). Entries
        /// are null if getting the child at the corresponding index failed (e.g. out of bounds).
        /// </summary>
        List<RemoteValue> GetChildren(uint offset, uint count);

        /// <summary>
        /// Evaluates an expression and returns the resulting value.  The result will be given the
        /// specified name.
        /// Returns null if |expression| cannot be evaluated.
        /// </summary>
        RemoteValue CreateValueFromExpression(string name, string expression);

        /// <summary>
        /// Builds a value from an address.
        /// </summary>
        RemoteValue CreateValueFromAddress(string name, ulong address, SbType type);

        /// <summary>
        /// Evaluates an expression asynchronously and returns the resulting value. The result will
        /// be given the specified name.
        /// The task result is null if |expression| cannot be evaluated.
        /// </summary>
        Task<RemoteValue> CreateValueFromExpressionAsync(string name, string expression);

        /// <summary>
        /// Evaluates an expression in the variable context and returns the resulting value.
        /// Returns null if |expression| cannot be evaluated.
        /// </summary>
        RemoteValue EvaluateExpression(string expression);

        /// <summary>
        /// Evaluates an expression asynchronously in the variable context and returns the 
        /// resulting value.
        /// The task returns null if |expression| cannot be evaluated.
        /// </summary>
        Task<RemoteValue> EvaluateExpressionAsync(string expression);

        /// <summary>
        /// Evaluates an expression asynchronously in a variable context using lldb-eval.
        /// </summary>
        Task<RemoteValue> EvaluateExpressionLldbEvalAsync(
            string expression, IDictionary<string, RemoteValue> contextVariables = null);

        /// <summary>
        /// Creates a new value with the same data content (copies the value).
        /// </summary>
        RemoteValue Clone();

        /// <summary>
        /// Dereferences a variable if it is a pointer.
        /// If the variable is not a pointer, it returns null.
        /// </summary>
        RemoteValue Dereference();

        /// <summary>
        /// Matches child members of this object and child members of any base classes.
        /// </summary>
        RemoteValue GetChildMemberWithName(string name);

        /// <summary>
        /// Returns the address of this value, or null if it doesn't exist.
        /// </summary>
        RemoteValue AddressOf();

        /// <summary>
        /// Returns true if the type is a pointer type. A typedef backed by a pointer type also
        /// returns true.
        /// </summary>
        bool TypeIsPointerType();

        /// <summary>
        /// Expands nested expressions like.a->b[0].c[1]->d.
        /// </summary>
        RemoteValue GetValueForExpressionPath(string expressionPath);

        /// <summary>
        /// Returns an expression path for this value.
        /// </summary>
        bool GetExpressionPath(out string path);

        /// <summary>
        /// Returns a view of this value that prefetches all of its fields.
        /// </summary>
        RemoteValue GetCachedView(ValueFormat format);

        /// <summary>
        /// Returns the byte size of this value.
        /// </summary>
        ulong GetByteSize();

        /// <summary>
        /// Returns the unsigned representation of the value.
        /// </summary>
        ulong GetValueAsUnsigned();

        /// <summary>
        /// Reads memory from the pointee's memory address until a null terminator is hit or the
        /// memory cannot be read. The returned memory does not include the null terminator.
        /// Supports charSize of 1 (e.g. UTF-8), 2 (e.g. Unicode 16) and 4 (e.g. Unicode 32).
        /// Limits the size of the returned string to maxStringSize bytes.
        /// Returns null and sets error if the value is not a pointer or array or charSize is not
        /// 1, 2 or 4. Otherwise, sets error to null.
        /// </summary>
        byte[] GetPointeeAsByteString(uint charSize, uint maxStringSize, out string error);
    }
}
