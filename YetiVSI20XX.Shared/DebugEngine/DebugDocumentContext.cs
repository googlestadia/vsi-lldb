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

using DebuggerCommonApi;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.IO;

namespace YetiVSI.DebugEngine
{
    /// <summary>
    /// This class represents a document context to the debugger. A document context represents a
    /// location within a source file.
    /// </summary>
    public class DebugDocumentContext : IDebugDocumentContext2
    {
        public class Factory
        {
            public virtual IDebugDocumentContext2 Create(
                LineEntryInfo lldbLineEntry) => new DebugDocumentContext(lldbLineEntry);
        }

        readonly LineEntryInfo _lldbLineEntry;

        DebugDocumentContext(LineEntryInfo lldbLineEntry)
        {
            _lldbLineEntry = lldbLineEntry;
        }

#region IDebugDocumentContext2 functions

        public int EnumCodeContexts(out IEnumDebugCodeContexts2 codeContexts)
        {
            codeContexts = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetLanguageInfo(ref string language,
                                   ref Guid languageGuid) => VSConstants.E_NOTIMPL;

        public int GetName(enum_GETNAME_TYPE nameType, out string fileName)
        {
            fileName = _lldbLineEntry == null
                           ? string.Empty
                           : Path.Combine(_lldbLineEntry.Directory, _lldbLineEntry.FileName);
            return VSConstants.S_OK;
        }

        // Gets the text positions marking the start and end of this context's code.
        public int GetStatementRange(TEXT_POSITION[] beginPosition, TEXT_POSITION[] endPosition)
        {
            uint lineNumber = _lldbLineEntry?.Line ?? 0;
            uint columnNumber = _lldbLineEntry?.Column ?? 0;

            // Convert from the 1-based LLDB numbers to the 0-based Visual Studio numbers.
            // There are also edge cases where the LLDB numbers might be 0. In those cases make
            // sure we don't subtract one and wrap the uint.
            uint vsLineNumber = lineNumber;
            uint vsColumnNumber = columnNumber;
            if (vsLineNumber > 0)
            {
                vsLineNumber--;
            }
            if (vsColumnNumber > 0)
            {
                vsColumnNumber--;
            }

            beginPosition[0] = new TEXT_POSITION {
                dwLine = vsLineNumber,
                dwColumn = vsColumnNumber,
            };

            endPosition[0] = new TEXT_POSITION { dwLine = lineNumber, dwColumn = 0 };

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Gets the text positions marking the start and end of the area of source related to this
        /// context's code, including lines such as comments between the end of the previous
        /// statement and the start of the current.
        /// </summary>
        public int GetSourceRange(TEXT_POSITION[] beginPosition,
                                  TEXT_POSITION[] endPosition) => VSConstants.E_NOTIMPL;

        public int Compare(enum_DOCCONTEXT_COMPARE comparisonType,
                           IDebugDocumentContext2[] documentContexts, uint numDocumentContexts,
                           out uint matchIndex)
        {
            matchIndex = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int GetDocument(out IDebugDocument2 document)
        {
            document = null;
            return VSConstants.E_NOTIMPL;
        }

        public int Seek(int nCount, out IDebugDocumentContext2 ppDocContext)
        {
            ppDocContext = null;
            return VSConstants.E_NOTIMPL;
        }

#endregion
    }
}
