// Copyright 2021 Google LLC
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

using Google.VisualStudioFake.API;
using Google.VisualStudioFake.API.UI;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Google.VisualStudioFake.Internal.UI
{
    public class StackFrame : IStackFrame
    {
        readonly FRAMEINFO _frame;
        readonly IDebugSessionContext _debugSessionContext;

        public StackFrame(FRAMEINFO frame, IDebugSessionContext debugSessionContext)
        {
            _frame = frame;
            _debugSessionContext = debugSessionContext;
        }

        #region IStackFrame

        public string FuncName => _frame.m_bstrFuncName;
        public string ReturnType => _frame.m_bstrReturnType;
        public string Args => _frame.m_bstrArgs;
        public string Language => _frame.m_bstrLanguage;
        public string Module => _frame.m_bstrModule;
        public ulong AddrMin => _frame.m_addrMin;
        public ulong AddrMax => _frame.m_addrMax;
        public int HasDebugInfo => _frame.m_fHasDebugInfo;
        public int StaleCode => _frame.m_fStaleCode;
        public uint Flags => _frame.m_dwFlags;
        public void Select() => _debugSessionContext.SelectedStackFrame = _frame.m_pFrame;

        #endregion

        public override string ToString()
        {
            return string.IsNullOrEmpty(FuncName)
                ? "<invalid>"
                : FuncName;
        }
    }
}