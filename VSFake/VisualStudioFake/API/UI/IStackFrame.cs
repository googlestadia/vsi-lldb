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

using System.Runtime.InteropServices;

namespace Google.VisualStudioFake.API.UI
{
    public interface IStackFrame
    {
        string FuncName { get; }
        string ReturnType { get; }
        string Args { get; }
        string Language { get; }
        string Module { get; }
        ulong AddrMin { get; }
        ulong AddrMax { get; }
        int HasDebugInfo { get; }
        int StaleCode { get; }
        uint Flags { get; }

        /// <summary>
        /// Sets this stack frame as selected frame, so that variables are evaluated in this frame.
        /// </summary>
        void Select();
    }
}
