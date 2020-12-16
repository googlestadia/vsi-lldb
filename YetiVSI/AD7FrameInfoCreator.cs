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

﻿using DebuggerApi;
using DebuggerCommonApi;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI.DebugEngine;

namespace YetiVSI
{
    public class AD7FrameInfoCreator
    {
        readonly IDebugModuleCache _debugModuleCache;

        public AD7FrameInfoCreator(IDebugModuleCache debugModuleCache)
        {
            _debugModuleCache = debugModuleCache;
        }

        /// <summary>
        /// Creates an AD7 FRAMEINFO based on a FrameInfo value.
        /// </summary>
        public virtual FRAMEINFO Create(IDebugStackFrame frame, FrameInfo<SbModule> info,
                                        IGgpDebugProgram program) => new FRAMEINFO {
            m_addrMax = info.AddrMax,
            m_addrMin = info.AddrMin,
            m_bstrArgs = info.Args,
            m_bstrFuncName = info.FuncName,
            m_bstrLanguage = info.Language,
            m_bstrModule = info.ModuleName,
            m_bstrReturnType = info.ReturnType,
            m_dwFlags = info.Flags,
            m_dwValidFields = (enum_FRAMEINFO_FLAGS)info.ValidFields,
            m_fHasDebugInfo = info.HasDebugInfo,
            m_pModule = info.Module != null ? _debugModuleCache.GetOrCreate(info.Module, program)
                                            : null,
            m_pFrame = (FrameInfoFlags.FIF_FRAME & info.ValidFields) != 0 ? frame : null
        };
    }
}