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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Diagnostics;
using YetiCommon.CastleAspects;

namespace YetiVSI.DebugEngine
{
    // DebugMemoryContext implements IDebugMemoryContext2. It represents a position in the address
    // space of the machine running the program being debugged.
    public class DebugMemoryContext : IDebugMemoryContext2
    {
        public class Factory : SimpleDecoratorSelf<Factory>
        {
            readonly Factory _debugMemoryContextFactory;

            public Factory()
            {
            }

            public Factory(Factory debugMemoryContextFactory)
            {
                _debugMemoryContextFactory = debugMemoryContextFactory;
            }

            public virtual IDebugMemoryContext2 Create(ulong address, Lazy<string> filename)
            {
                var factory = _debugMemoryContextFactory ?? Self;
                return new DebugMemoryContext(factory, address, filename);
            }
        }

        protected readonly Lazy<string> _filename;
        protected readonly ulong _address;
        readonly string _addressAsHexString;
        readonly Factory _factory;

        protected DebugMemoryContext(Factory factory, ulong address, Lazy<string> filename)
        {
            _addressAsHexString = $"0x{address:x16}";

            _filename = filename;
            _address = address;
            _factory = factory;
        }

        #region IDebugMemoryContext2 functions

        public int Add(ulong count, out IDebugMemoryContext2 newMemoryContext)
        {
            newMemoryContext = _factory.Create(_address + count, _filename);
            return VSConstants.S_OK;
        }

        public int Subtract(ulong count, out IDebugMemoryContext2 newMemoryContext)
        {
            newMemoryContext = _factory.Create(_address - count, _filename);
            return VSConstants.S_OK;
        }

        public int Compare(enum_CONTEXT_COMPARE comparisonType,
                           IDebugMemoryContext2[] memoryContexts, uint numMemoryContexts,
                           out uint matchIndex)
        {
            matchIndex = uint.MaxValue;
            for (uint i = 0; i < numMemoryContexts; i++)
            {
                ulong otherAddress = memoryContexts[i].GetAddress();

                switch (comparisonType)
                {
                case enum_CONTEXT_COMPARE.CONTEXT_EQUAL:
                    if (_address == otherAddress)
                    {
                        matchIndex = i;
                    }
                    break;
                case enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN:
                    if (_address > otherAddress)
                    {
                        matchIndex = i;
                    }
                    break;
                case enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN_OR_EQUAL:
                    if (_address >= otherAddress)
                    {
                        matchIndex = i;
                    }
                    break;
                case enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN:
                    if (_address < otherAddress)
                    {
                        matchIndex = i;
                    }
                    break;
                case enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN_OR_EQUAL:
                    if (_address <= otherAddress)
                    {
                        matchIndex = i;
                    }
                    break;
                case enum_CONTEXT_COMPARE.CONTEXT_SAME_FUNCTION:
                    memoryContexts[i].GetName(out string otherName);
                    GetName(out string thisName);
                    if (_address == otherAddress || thisName == otherName)
                    {
                        matchIndex = i;
                    }
                    break;
                // TODO: if needed, implement other comparison types
                default:
                    return VSConstants.E_NOTIMPL;
                }
                if (matchIndex != uint.MaxValue)
                {
                    return VSConstants.S_OK;
                }
            }
            return VSConstants.S_FALSE;
        }

        public int GetInfo(enum_CONTEXT_INFO_FIELDS fields, CONTEXT_INFO[] contextInfo)
        {
            var info = new CONTEXT_INFO();
            if ((enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS & fields) != 0)
            {
                // Field used for requesting data from Lldb.
                info.bstrAddress = _addressAsHexString;
                info.dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS;
            }
            if ((enum_CONTEXT_INFO_FIELDS.CIF_ADDRESSABSOLUTE & fields) != 0)
            {
                // `Name` in the breakpoint list for Disassembly breakpoints and also
                // `Address` for all breakpoints types.
                info.bstrAddressAbsolute = _addressAsHexString;
                info.dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESSABSOLUTE;
            }
            // Combination of cif_moduleUrl!cif_function is used in a `Function` column.
            // TODO: fix these values, currently they overwrite data from VS
            if ((enum_CONTEXT_INFO_FIELDS.CIF_MODULEURL & fields) != 0)
            {
                info.bstrModuleUrl = "";
                info.dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_MODULEURL;
            }

            if ((enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION & fields) != 0)
            {
                GetName(out string functionName);
                info.bstrFunction = functionName;
                info.dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION;
            }
            // TODO: implement more info fields if we determine they are needed
            contextInfo[0] = info;
            return VSConstants.S_OK;
        }

        public int GetName(out string name)
        {
            name = _filename?.Value;

            if (string.IsNullOrEmpty(name))
            {
                name = _addressAsHexString;
            }

            return VSConstants.S_OK;
        }
#endregion
    }

    public static class DebugMemoryContextExtension
    {
        public static ulong GetAddress(this IDebugMemoryContext2 context)
        {
            var contextInfo = new CONTEXT_INFO[1];
            int res = context.GetInfo(enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS, contextInfo);
            Debug.Assert(res == VSConstants.S_OK);
            return System.Convert.ToUInt64(contextInfo[0].bstrAddress, 16);
        }
    }
}
