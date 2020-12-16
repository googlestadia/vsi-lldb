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

using System;

namespace DebuggerApi
{
    //----------------------------------------------------------------------
    // Bitmask that describes details about a type.
    //
    // Copied from lldb-enumerations.h.
    //----------------------------------------------------------------------
    [Flags]
    public enum TypeFlags
    {
        NONE = 0,
        HAS_CHILDREN = (1 << 0),
        HAS_VALUE = (1 << 1),
        IS_ARRAY = (1 << 2),
        IS_BLOCK = (1 << 3),
        IS_BUILT_IN = (1 << 4),
        IS_CLASS = (1 << 5),
        IS_C_PLUS_PLUS = (1 << 6),
        IS_ENUMERATION = (1 << 7),
        IS_FUNC_PROTOTYPE = (1 << 8),
        IS_MEMBER = (1 << 9),
        IS_OBJ_C = (1 << 10),
        IS_POINTER = (1 << 11),
        IS_REFERENCE = (1 << 12),
        IS_STRUCT_UNION = (1 << 13),
        IS_TEMPLATE = (1 << 14),
        IS_TYPEDEF = (1 << 15),
        IS_VECTOR = (1 << 16),
        IS_SCALAR = (1 << 17),
        IS_INTEGER = (1 << 18),
        IS_FLOAT = (1 << 19),
        IS_COMPLEX = (1 << 20),
        IS_SIGNED = (1 << 21),
        INSTANCE_IS_POINTER = (1 << 22)
    };

    public interface SbType
    {
        // Returns the type flags as a bitmask. Flag values are defined by the TypeFlags enum.
        TypeFlags GetTypeFlags();

        // Returns the type name that this represents.
        string GetName();

        // Returns number of direct base classes in this type.
        uint GetNumberOfDirectBaseClasses();

        // Returns the direct base class at a given index.
        SbTypeMember GetDirectBaseClassAtIndex(uint index);

        // Returns the canonical type. A canonical type is a class of type that isn't produced by
        // applying syntactic sugar (such as typedef).
        SbType GetCanonicalType();

        // Returns the pointed-to type for reference and pointer types.
        SbType GetPointeeType();

        // Returns byte size.
        ulong GetByteSize();

        // Returns a unique identifier for the type. Not part of the LLDB API.
        long GetId();
    }
}