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

﻿namespace DebuggerCommonApi
{
    // Enumeration mimics Microsoft.VisualStudio.Debugger.Interop.enum_FRAMEINFO_FLAGS
    public enum FrameInfoFlags
    {
        FIF_DESIGN_TIME_EXPR_EVAL = int.MinValue,
        FIF_FUNCNAME = 1,
        FIF_RETURNTYPE = 2,
        FIF_ARGS = 4,
        FIF_LANGUAGE = 8,
        FIF_MODULE = 16,
        FIF_STACKRANGE = 32,
        FIF_FRAME = 64,
        FIF_DEBUGINFO = 128,
        FIF_STALECODE = 256,
        FIF_FLAGS = 512,
        FIF_DEBUG_MODULEP = 1024,
        FIF_FUNCNAME_FORMAT = 4096,
        FIF_FUNCNAME_RETURNTYPE = 8192,
        FIF_FUNCNAME_ARGS = 16384,
        FIF_FUNCNAME_LANGUAGE = 32768,
        FIF_FUNCNAME_MODULE = 65536,
        FIF_FUNCNAME_LINES = 131072,
        FIF_FUNCNAME_OFFSET = 262144,
        FIF_FILTER_INCLUDE_ALL = 524288,
        FIF_FUNCNAME_ARGS_TYPES = 1048576,
        FIF_FUNCNAME_ARGS_NAMES = 2097152,
        FIF_FUNCNAME_ARGS_VALUES = 4194304,
        FIF_FUNCNAME_ARGS_ALL = 7340032,
        FIF_ARGS_TYPES = 16777216,
        FIF_ARGS_NAMES = 33554432,
        FIF_ARGS_VALUES = 67108864,
        FIF_ARGS_ALL = 117440512,
        FIF_ARGS_NOFORMAT = 134217728,
        FIF_ARGS_NO_FUNC_EVAL = 268435456,
        FIF_FILTER_NON_USER_CODE = 536870912,
        FIF_ARGS_NO_TOSTRING = 1073741824
    }

    // Struct based off Microsoft.VisualStudio.Debugger.Interop.FRAMEINFO
    public struct FrameInfo<TModule>
    {
        public ulong AddrMax;
        public ulong AddrMin;
        public string Args;
        public string FuncName;
        public string Language;
        public string ModuleName;
        public string ReturnType;
        public uint Flags;
        public FrameInfoFlags ValidFields;
        public int HasDebugInfo;
        public int StaleCode;
        public TModule Module;
    }
}
