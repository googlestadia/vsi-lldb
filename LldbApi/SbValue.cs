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

using System.Collections.Generic;

namespace LldbApi
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

    // Interface mirrors the SBValue API as closely as possible.
    public interface SbValue
    {
        /// <summary>
        /// Returns the node's name.
        /// </summary>
        string GetName();

        /// <summary>
        /// Returns the node's value.
        /// Parent nodes that are solely for grouping may have a blank value.
        /// </summary>
        string GetValue();

        /// <summary>
        /// Defines the serialization format of GetValue().
        /// </summary>
        void SetFormat(ValueFormat format);

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
        string GetSummary();

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
        /// Returns the child value at the provided index or null if index is out of bounds.
        /// </summary>
        SbValue GetChildAtIndex(uint index);

        /// <summary>
        /// Returns the child values at the provided index range [offset, offset + count). Entries
        /// are null if getting the child at the corresponding index failed (e.g. out of bounds).
        /// </summary>
        List<SbValue> GetChildren(uint offset, uint count);

        /// <summary>
        /// Evaluates an expression and returns the resulting value.  The result will be given the
        /// specified name.
        /// Returns null if |expression| cannot be evaluated.
        /// </summary>
        SbValue CreateValueFromExpression(string name, string expression,
            SbExpressionOptions options);

        /// <summary>
        /// Creates a value from an address and a type and returns the resulting value.
        /// The resulting value's name will be the specified name.
        /// </summary>
        SbValue CreateValueFromAddress(string name, ulong address, SbType type);

        /// <summary>
        /// Evaluates an expression in the variable context and returns the resulting value.
        /// Returns null if |expression| cannot be evaluated.
        /// </summary>
        SbValue EvaluateExpression(string expression, SbExpressionOptions options);

        /// <summary>
        /// Dereferences a variable if it is a pointer.
        /// If the variable is not a pointer, it returns null.
        /// </summary>
        SbValue Dereference();

        /// <summary>
        /// Matches child members of this object and child members of any base classes.
        /// </summary>
        SbValue GetChildMemberWithName(string name);

        /// <summary>
        /// Returns the address of this value, or null if it doesn't exist.
        /// </summary>
        SbValue AddressOf();

        /// <summary>
        /// Returns true if the underlying type is a pointer type.
        /// </summary>
        bool TypeIsPointerType();

        /// <summary>
        /// Expands nested expressions like.a->b[0].c[1]->d.
        /// </summary>
        SbValue GetValueForExpressionPath(string expressionPath);

        /// <summary>
        /// Returns an expression path for this value.
        /// </summary>
        bool GetExpressionPath(out string path);

        /// <summary>
        /// Returns the current serialization format of this value.
        /// </summary>
        ValueFormat GetFormat();

        /// <summary>
        /// Returns the byte size of this value.
        /// </summary>
        ulong GetByteSize();

        /// <summary>
        /// Retrieves the unsigned representation of the value.
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
