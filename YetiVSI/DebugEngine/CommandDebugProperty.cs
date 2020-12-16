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
    /// Debug property that executes a Func<string> during side-effect allowed conditions.
    /// </summary>
    public class CommandDebugProperty : SimpleDecoratorSelf<IDebugProperty2>, IDebugProperty2
    {
        public class Factory
        {
            public IDebugProperty2 Create(string name, string type, Func<String> command)
            {
                return new CommandDebugProperty(name, type, command);
            }
        }

        readonly string name;
        readonly string type;
        readonly Func<string> command;

        /// <param name="command">
        /// The command to execute.  The return value is present in as the value field.
        /// </param>
        public CommandDebugProperty(string name, string type, Func<string> command)
        {
            this.name = name;
            this.type = type;
            this.command = command;
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

            // Shows purple wire-frame box icon.
            info.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_SIDE_EFFECT;

            var value = "Internal Error";

            if (((int)dwFields &
                    (int)enum_DEBUGPROP_INFO_FLAGS100.DEBUGPROP100_INFO_NOSIDEEFFECTS) != 0)
            {
                // Shows refresh button.
                info.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_ERROR;
                value = "This expression has side effects and will not be evaluated.";
            }
            else
            {
                // TODO: Provide a mechanism for the command to fail.
                value = command();
            }

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
                info.bstrValue = value;
                info.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE;
            }
            if ((enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP & dwFields) != 0)
            {
                info.pProperty = Self;
                info.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP;
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
