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

namespace YetiVSI.DebugEngine
{
    public class DebugCodeContext : DebugMemoryContext, IDebugCodeContext2
    {
        public new class Factory { readonly DebugMemoryContext.Factory _debugMemoryContextFactory;

        [Obsolete("This constructor only exists to support mocking libraries.", error: true)]
        protected Factory()
        {
        }

        public Factory(DebugMemoryContext.Factory debugMemoryContextFactory)
        {
            _debugMemoryContextFactory = debugMemoryContextFactory;
        }

        public virtual IDebugCodeContext2 Create(
            ulong address, string name, IDebugDocumentContext2 documentContext,
            Guid languageGuid) => new DebugCodeContext(_debugMemoryContextFactory, address, name,
                                                       documentContext, languageGuid);
    }

    readonly IDebugDocumentContext2 _documentContext;
    readonly Guid _languageGuid;

    // Creates a code context (ie PC)
    DebugCodeContext(DebugMemoryContext.Factory debugMemoryContextFactory, ulong address,
                     string name, IDebugDocumentContext2 documentContext, Guid languageGuid)
        : base(debugMemoryContextFactory, address, name)
    {
        _documentContext = documentContext;
        _languageGuid = languageGuid;
    }

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
}
}
