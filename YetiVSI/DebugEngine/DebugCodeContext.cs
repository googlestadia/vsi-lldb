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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace YetiVSI.DebugEngine
{
    public interface IGgpDebugCodeContext : IDebugCodeContext2
    {
        ulong Address { get; }

        string FunctionName { get; }
    }

    public class DebugCodeContext : IGgpDebugCodeContext
    {
        public class Factory
        {
            public virtual IGgpDebugCodeContext Create(
                RemoteTarget target, ulong address, string functionName,
                IDebugDocumentContext2 documentContext)
            {
                return new DebugCodeContext(
                    this, target, address, functionName, documentContext, Guid.Empty);
            }

            public virtual IGgpDebugCodeContext Create(
                RemoteTarget target, ulong address, string functionName,
                IDebugDocumentContext2 documentContext, Guid languageGuid)
            {
                return new DebugCodeContext(
                    this, target, address, functionName, documentContext, languageGuid);
            }
        }

        readonly Factory _factory;
        readonly RemoteTarget _target;
        readonly IDebugDocumentContext2 _documentContext;
        readonly Guid _languageGuid;

        public ulong Address { get; private set; }

        string _functionName;

        public string FunctionName
        {
            get
            {
                if (_functionName == null)
                {
                    // Try resolving the address and extracting the function name from the symbol.
                    _functionName = _target.ResolveLoadAddress(Address)?.GetFunction()?.GetName();

                    // Pretty-print the address if the function name is not available.
                    if (string.IsNullOrWhiteSpace(_functionName))
                    {
                        _functionName = ToHexAddr(Address);
                    }
                }
                return _functionName;
            }
        }

        DebugCodeContext(Factory factory, RemoteTarget target,
                         ulong address, string functionName,
                         IDebugDocumentContext2 documentContext, Guid languageGuid)
        {
            _factory = factory;
            _target = target;
            _functionName = functionName;
            _documentContext = documentContext;
            _languageGuid = languageGuid;

            Address = address;
        }

        #region IDebugMemoryContext2 functions

        public int Add(ulong count, out IDebugMemoryContext2 newMemoryContext)
        {
            // This is not correct for IDebugCodeContext2 according to the docs
            // https://docs.microsoft.com/en-us/visualstudio/extensibility/debugger/reference/idebugcodecontext2#remarks
            // But it's not used in practice (instead: IDebugDisassemblyStream2.Seek)

            // Function name and the document context are no longer valid for the new address, so
            // pass null values. Function name will be resolved if needed, but the document
            // context is lost.
            // TODO: figure out if we need to re-resolve the document context.

            newMemoryContext = _factory.Create(
                _target, Address + count, null, null, _languageGuid);
            return VSConstants.S_OK;
        }

        public int Subtract(ulong count, out IDebugMemoryContext2 newMemoryContext)
        {
            // Function name and the document context are no longer valid for the new address, so
            // pass null values. Function name will be resolved if needed, but the document
            // context is lost.
            // TODO: figure out if we need to re-resolve the document context.

            newMemoryContext = _factory.Create(
                _target, Address - count, null, null, _languageGuid);
            return VSConstants.S_OK;
        }

        public int Compare(enum_CONTEXT_COMPARE comparisonType,
                           IDebugMemoryContext2[] memoryContexts, uint numMemoryContexts,
                           out uint matchIndex)
        {
            matchIndex = uint.MaxValue;
            for (uint i = 0; i < numMemoryContexts; i++)
            {
                ulong otherAddress = ((IGgpDebugCodeContext)memoryContexts[i]).Address;

                switch (comparisonType)
                {
                    case enum_CONTEXT_COMPARE.CONTEXT_EQUAL:
                        if (Address == otherAddress)
                        {
                            matchIndex = i;
                        }
                        break;
                    case enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN:
                        if (Address > otherAddress)
                        {
                            matchIndex = i;
                        }
                        break;
                    case enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN_OR_EQUAL:
                        if (Address >= otherAddress)
                        {
                            matchIndex = i;
                        }
                        break;
                    case enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN:
                        if (Address < otherAddress)
                        {
                            matchIndex = i;
                        }
                        break;
                    case enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN_OR_EQUAL:
                        if (Address <= otherAddress)
                        {
                            matchIndex = i;
                        }
                        break;
                    case enum_CONTEXT_COMPARE.CONTEXT_SAME_FUNCTION:
                        memoryContexts[i].GetName(out string otherName);
                        GetName(out string thisName);
                        if (Address == otherAddress || thisName == otherName)
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
                info.bstrAddress = ToHexAddr(Address);
                info.dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS;
            }
            if ((enum_CONTEXT_INFO_FIELDS.CIF_ADDRESSABSOLUTE & fields) != 0)
            {
                // `Name` in the breakpoint list for Disassembly breakpoints and also
                // `Address` for all breakpoints types.
                info.bstrAddressAbsolute = ToHexAddr(Address);
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
                info.bstrFunction = FunctionName;
                info.dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION;
            }
            // TODO: implement more info fields if we determine they are needed
            contextInfo[0] = info;
            return VSConstants.S_OK;
        }

        public int GetName(out string name)
        {
            name = FunctionName;
            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugCodeContext2 functions

        public int GetDocumentContext(out IDebugDocumentContext2 documentContext)
        {
            documentContext = _documentContext;
            return documentContext != null ? VSConstants.S_OK : VSConstants.S_FALSE;
        }

        public int GetLanguageInfo(ref string language, ref Guid languageGuid)
        {
            if (languageGuid == Guid.Empty)
            {
                languageGuid = _languageGuid;
                language = AD7Constants.GetLanguageByGuid(languageGuid);
            }

            return VSConstants.S_OK;
        }

        #endregion

        static string ToHexAddr(ulong addr)
        {
            // TODO: Use x8 for 32-bit processes.
            return $"0x{addr:x16}";
        }
    }
}