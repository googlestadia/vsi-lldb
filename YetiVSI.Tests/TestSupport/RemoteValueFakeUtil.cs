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
using System.Globalization;

namespace YetiVSI.Test.TestSupport
{
    // Helper class to create common flavours of RemoteValueFakes.
    public class RemoteValueFakeUtil
    {
        // Fixing it to 8 so it matches 64-bit Stadia game processes.
        const ulong PtrSize = 8;

        public static RemoteValueFake Create(string typename, TypeFlags typeFlags, string name,
                                             string val)
        {
            var remoteValue = new RemoteValueFake(name, val.ToString());
            remoteValue.SetTypeInfo(new SbTypeStub(typename, typeFlags));
            return remoteValue;
        }

        public static RemoteValueFake CreateSimpleInt(string name, int val)
        {
            var remoteValue = new RemoteValueFake(name, val.ToString());
            var type = new SbTypeStub("int", TypeFlags.IS_INTEGER);
            type.SetByteSize(sizeof(int));
            remoteValue.SetTypeInfo(type);
            return remoteValue;
        }

        public static RemoteValueFake CreateSimpleIntArray(string name, params int[] values)
             => CreateSimpleArray(name, "int", CreateSimpleInt, values);

        public static RemoteValueFake CreateSimpleLong(string name, long val)
        {
            var remoteValue = new RemoteValueFake(name, val.ToString());
            var type = new SbTypeStub("long", TypeFlags.IS_INTEGER);
            type.SetByteSize(sizeof(long));
            remoteValue.SetTypeInfo(type);
            return remoteValue;
        }

        public static RemoteValueFake CreateSimpleLongArray(string name, params long[] values)
            => CreateSimpleArray(name, "long", CreateSimpleLong, values);

        public static RemoteValueFake CreateSimpleChar(string name, char val)
        {
            var remoteValue = new RemoteValueFake(name, val.ToString());
            var type = new SbTypeStub("char", TypeFlags.IS_SCALAR);
            type.SetByteSize(sizeof(char));
            remoteValue.SetTypeInfo(type);
            return remoteValue;
        }

        public static RemoteValueFake CreateSimpleCharArray(string name, params char[] values)
            => CreateSimpleArray(name, "char", CreateSimpleChar, values);

        public static RemoteValueFake CreateSimpleBool(string name, bool val)
        {
            var remoteValue = new RemoteValueFake(name, val ? "true" : "false");
            var type = new SbTypeStub("bool", TypeFlags.IS_SCALAR);
            type.SetByteSize(sizeof(bool));
            remoteValue.SetTypeInfo(type);
            return remoteValue;
        }

        public static RemoteValueFake CreateSimpleBoolArray(string name, params bool[] values)
            => CreateSimpleArray(name, "bool", CreateSimpleBool, values);

        public static RemoteValueFake CreateSimpleFloat(string name, float val)
        {
            var remoteValue = new RemoteValueFake(name, val.ToString());
            var type = new SbTypeStub("float", TypeFlags.IS_FLOAT);
            type.SetByteSize(sizeof(float));
            remoteValue.SetTypeInfo(type);
            return remoteValue;
        }

        public static RemoteValueFake CreateSimpleDouble(string name, double val)
        {
            var remoteValue = new RemoteValueFake(name, val.ToString());
            var type = new SbTypeStub("double", TypeFlags.IS_FLOAT);
            type.SetByteSize(sizeof(double));
            remoteValue.SetTypeInfo(type);
            return remoteValue;
        }

        public static RemoteValueFake CreateSimpleString(string name, string val)
        {
            var remoteValue = new RemoteValueFake(name, val);
            remoteValue.SetTypeInfo(new SbTypeStub("std::string", TypeFlags.IS_SCALAR));
            return remoteValue;
        }

        public static RemoteValueFake CreateAddressOf(RemoteValueFake remoteValue, long address)
        {
            var sbAddress = new RemoteValueFake(null, $"0x{address.ToString("X")}");
            var type =
                new SbTypeStub($"{remoteValue.GetTypeName()}*",
                               TypeFlags.HAS_CHILDREN | TypeFlags.IS_POINTER | TypeFlags.HAS_VALUE);
            type.SetByteSize(PtrSize);
            sbAddress.SetTypeInfo(type);
            sbAddress.AddChild(remoteValue);
            return sbAddress;
        }

        // Heuristically sets TypeIsPointerType based on whether |typeName| contains a '*'.
        public static RemoteValueFake CreateClass(string typeName, string name, string value)
        {
            var remoteValue = new RemoteValueFake(name, value);
            var typeFlags = TypeFlags.IS_CLASS;
            if (typeName.Contains("*"))
            {
                typeFlags |= TypeFlags.IS_POINTER;
            }
            if (typeName.Contains("&"))
            {
                typeFlags |= TypeFlags.IS_REFERENCE;
            }
            remoteValue.SetTypeInfo(new SbTypeStub(typeName, typeFlags));
            return remoteValue;
        }

        public static RemoteValueFake CreateClassAlias(string aliasTypeName,
                                                       string canonicalTypeName, string name,
                                                       string value)
        {
            var remoteValue = new RemoteValueFake(name, value);
            var typeFlags = TypeFlags.IS_CLASS;
            if (aliasTypeName.Contains("*"))
            {
                typeFlags |= TypeFlags.IS_POINTER;
            }
            if (aliasTypeName.Contains("&"))
            {
                typeFlags |= TypeFlags.IS_REFERENCE;
            }
            var type = new SbTypeStub(aliasTypeName, typeFlags);
            var canonicalType = new SbTypeStub(canonicalTypeName, typeFlags);
            type.SetCanonicalType(canonicalType);
            remoteValue.SetTypeInfo(type);
            return remoteValue;
        }

        public static RemoteValueFake CreatePointer(string typeName, string name, string value)
        {
            if (!IsHex(value))
            {
                throw new Exception("Pointer RemoteValueFake requires value to be a hex value");
            }

            return CreateUnsafeAddressPointer(typeName, name, value);
        }

        public static RemoteValueFake CreateReference(string typeName, string name, string value)
        {
            if (!IsHex(value))
            {
                throw new Exception("Reference RemoteValueFake requires value to be a hex value");
            }

            var remoteValue = new RemoteValueFake(name, value);
            remoteValue.SetTypeInfo(new SbTypeStub(typeName, TypeFlags.IS_REFERENCE));
            return remoteValue;
        }

        public static RemoteValueFake CreateUnsafeAddressPointer(
            string typeName, string name, string value)
        {
            var remoteValue = new RemoteValueFake(name, value);
            var typeFlags = TypeFlags.IS_POINTER;
            remoteValue.SetTypeInfo(new SbTypeStub(typeName, typeFlags));
            return remoteValue;
        }

        public static RemoteValueFake CreateClassPointer(string className, string name,
                                                         string value)
        {
            if (!IsHex(value))
            {
                throw new Exception("Pointer RemoteValueFake requires value to be a hex value");
            }
            var remoteValue = new RemoteValueFake(name, value);
            var classType = new SbTypeStub(className, TypeFlags.IS_CLASS);
            var pointerType = new SbTypeStub(className + "*", TypeFlags.IS_POINTER, classType);
            remoteValue.SetTypeInfo(pointerType);
            return remoteValue;
        }

        public static RemoteValueFake CreateClassReference(string className, string name,
                                                           string value)
        {
            if (!IsHex(value))
            {
                throw new Exception("Pointer RemoteValueFake requires value to be a hex value");
            }
            var remoteValue = new RemoteValueFake(name, value);
            var classType = new SbTypeStub(className, TypeFlags.IS_CLASS);
            var pointerType = new SbTypeStub(className + "&", TypeFlags.IS_REFERENCE, classType);
            remoteValue.SetTypeInfo(pointerType);
            return remoteValue;
        }

        public static RemoteValueFake CreateSimpleArray<T>(string name, string typeName,
            Func<string, T, RemoteValueFake> itemFactory, params T[] values)
        {
            var remoteValue = new RemoteValueFake(name, "");
            remoteValue.SetTypeInfo(new SbTypeStub($"{typeName}[]", TypeFlags.IS_ARRAY));
            for (int i = 0; i < values.Length; i++)
            {
                remoteValue.AddChild(itemFactory($"[{i}]", values[i]));
            }
            return remoteValue;
        }

        public static RemoteValueFake CreateError(string errorMessage, string name = "",
            string value = "")
        {
            var errorValue = new RemoteValueFake(name, value);
            errorValue.SetError(new SbErrorStub(false, errorMessage));
            return errorValue;
        }

        public static RemoteValueFake CreateLldbEvalError(LldbEvalErrorCode errCode,
                                                          string errorMessage = "",
                                                          string name = "", string value = "")
        {
            var errorValue = new RemoteValueFake(name, value);
            errorValue.SetError(new SbErrorStub(false, errorMessage, Convert.ToUInt32(errCode)));
            return errorValue;
        }

        public static RemoteValueFake CreateUnsignedLongRegister(string name, ulong val)
        {
            var remoteValue = new RemoteValueFake(name, val.ToString());
            remoteValue.SetTypeInfo(new SbTypeStub("unsigned long", TypeFlags.IS_INTEGER));
            remoteValue.SetValueType(DebuggerApi.ValueType.Register);
            return remoteValue;
        }

        private static bool IsHex(string value)
        {
            var hexValue = value;
            if (hexValue.StartsWith("0X") || hexValue.StartsWith("0x"))
            {
                hexValue = hexValue.Substring(2);
            }

            int intVal;
            if (!int.TryParse(hexValue, NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out intVal))
            {
                return false;
            }
            return true;
        }
    }
}
