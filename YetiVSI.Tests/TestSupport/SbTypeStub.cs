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
using DebuggerApi;
using System.Collections.Generic;

namespace YetiVSI.Test.TestSupport
{
    // Stub test double for an SbType.
    public class SbTypeStub : SbType
    {
        TypeFlags typeFlags;
        string name;
        List<SbTypeMember> directBaseClasses;
        SbType canonicalType;
        SbType pointeeType;
        ulong? byteSize = null;

        public SbTypeStub(string name, TypeFlags typeFlags, SbType pointeeType = null)
        {
            this.name = name;
            this.typeFlags = typeFlags;
            directBaseClasses = new List<SbTypeMember>();
            this.canonicalType = this;
            this.pointeeType = pointeeType;
        }

        public void SetTypeFlags(TypeFlags typeFlags)
        {
            this.typeFlags = typeFlags;
        }

        public TypeFlags GetTypeFlags()
        {
            return typeFlags;
        }

        public void SetName(string name)
        {
            this.name = name;
        }

        public string GetName()
        {
            return name;
        }

        public uint GetNumberOfDirectBaseClasses()
        {
            return (uint)directBaseClasses.Count;
        }

        public SbTypeMember AddDirectBaseClass(SbType typeInfo)
        {
            var typeMember = new SbTypeMemberStub();
            typeMember.SetTypeInfo(typeInfo);
            directBaseClasses.Add(typeMember);
            return typeMember;
        }

        public SbTypeMember GetDirectBaseClassAtIndex(uint index)
        {
            if (index >= directBaseClasses.Count)
            {
                // The LLDBWorker layer massages invalid indices in to null values.
                return null;
            }
            return directBaseClasses[(int)index];
        }

        public SbType GetCanonicalType() => canonicalType;

        public SbType GetPointeeType() => pointeeType;

        public ulong GetByteSize()
        {
            if (byteSize is ulong byteSizeValue)
            {
                return byteSizeValue;
            }
            throw new InvalidOperationException("byteSize not set");
        }

        public void SetCanonicalType(SbType t) => canonicalType = t;

        public long GetId() => 0;

        public void SetByteSize(ulong byteSize)
        {
            this.byteSize = byteSize;
        }
    }
}
