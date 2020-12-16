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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using YetiCommon.CastleAspects;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// Simple debug property that displays the white 'X' on red circle icon.
    /// </summary>
    public class ErrorDebugProperty : SimpleDecoratorSelf<IDebugProperty2>, IDebugProperty2
    {
        public class Factory
        {
            public IDebugProperty2 Create(string name, string type, string errorValue)
            {
                return new ErrorDebugProperty(name, type, errorValue);
            }
        }

        readonly string name;
        readonly string type;
        readonly string errorValue;

        public ErrorDebugProperty(string name, string type, string errorValue)
        {
            this.name = name;
            this.type = type;
            this.errorValue = errorValue;
        }

        public int EnumChildren(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix,
            ref Guid guidFilter, enum_DBG_ATTRIB_FLAGS dwAttribFilter, string pszNameFilter,
            uint dwTimeout, out IEnumDebugPropertyInfo2 ppEnum)
        {
            ppEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetDerivedMostProperty(out IDebugProperty2 ppDerivedMost)
        {
            ppDerivedMost = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetExtendedInfo(ref Guid guidExtendedInfo, out object pExtendedInfo)
        {
            pExtendedInfo = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes)
        {
            ppMemoryBytes = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetMemoryContext(out IDebugMemoryContext2 ppMemory)
        {
            ppMemory = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetParent(out IDebugProperty2 ppParent)
        {
            ppParent = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, uint dwTimeout,
            IDebugReference2[] rgpArgs, uint dwArgCount, DEBUG_PROPERTY_INFO[] pPropertyInfo)
        {
            DEBUG_PROPERTY_INFO info = new DEBUG_PROPERTY_INFO();

            if ((enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME & dwFields) != 0)
            {
                info.bstrFullName = name;
                info.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME;
            }
            if ((enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME & dwFields) != 0)
            {
                info.bstrName = name;
                info.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME;
            }
            if ((enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE & dwFields) != 0)
            {
                info.bstrType = type;
                info.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE;
            }
            if ((enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE & dwFields) != 0)
            {
                info.bstrValue = errorValue;
                info.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE;
            }
            if ((enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP & dwFields) != 0)
            {
                info.pProperty = Self;
                info.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP;
            }
            if ((enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB & dwFields) != 0)
            {
                info.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_ERROR;
                info.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB;
            }

            pPropertyInfo[0] = info;
            return VSConstants.S_OK;
        }

        public int GetReference(out IDebugReference2 ppReference)
        {
            ppReference = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetSize(out uint pdwSize)
        {
            pdwSize = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int SetValueAsReference(IDebugReference2[] rgpArgs, uint dwArgCount,
            IDebugReference2 pValue, uint dwTimeout)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int SetValueAsString(string pszValue, uint dwRadix, uint dwTimeout)
        {
            return VSConstants.E_NOTIMPL;
        }
    }
}
