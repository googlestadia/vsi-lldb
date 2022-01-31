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
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio;

namespace YetiVSI.PortSupplier
{
    // DebugProperty provides a single named string property.
    public class DebugProperty : IDebugProperty2
    {
        public class Factory
        {
            public virtual DebugProperty Create(string name, string value)
            {
                return new DebugProperty(name, value);
            }
        }

        private string name;
        private string value;

        private DebugProperty(string name, string value)
        {
            this.name = name;
            this.value = value;
        }

        public int EnumChildren(
            enum_DEBUGPROP_INFO_FLAGS fields, uint radix, ref Guid guidFilter,
            enum_DBG_ATTRIB_FLAGS attribFilter, string nameFilter, uint timeout,
            out IEnumDebugPropertyInfo2 infoEnum)
        {
            infoEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetDerivedMostProperty(out IDebugProperty2 derivedMost)
        {
            derivedMost = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetExtendedInfo(ref Guid extendedInfoGuid, out object extendedInfo)
        {
            extendedInfo = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetMemoryBytes(out IDebugMemoryBytes2 memoryBytes)
        {
            memoryBytes = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetMemoryContext(out IDebugMemoryContext2 memory)
        {
            memory = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetParent(out IDebugProperty2 parent)
        {
            parent = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetPropertyInfo(
            enum_DEBUGPROP_INFO_FLAGS fields, uint radix, uint timeout,
            IDebugReference2[] args, uint argCount, DEBUG_PROPERTY_INFO[] propertyInfo)
        {
            propertyInfo[0] = new DEBUG_PROPERTY_INFO();
            propertyInfo[0].bstrName = name;
            propertyInfo[0].bstrValue = value;
            propertyInfo[0].dwFields =
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME |
                enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE;
            return VSConstants.S_OK;
        }

        public int GetReference(out IDebugReference2 reference)
        {
            reference = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetSize(out uint size)
        {
            size = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int SetValueAsReference(
            IDebugReference2[] args, uint argCount, IDebugReference2 value, uint timeout)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int SetValueAsString(string value, uint radix, uint timeout)
        {
            return VSConstants.E_NOTIMPL;
        }
    }

}