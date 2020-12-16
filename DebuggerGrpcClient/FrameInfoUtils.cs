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

﻿using Debugger.Common;
using DebuggerApi;
using DebuggerCommonApi;

namespace DebuggerGrpcClient
{
    class FrameInfoUtils
    {
        internal static FrameInfo<SbModule> CreateFrameInfo(GrpcFrameInfo info,
            GrpcModuleFactory moduleFactory, GrpcConnection connection) =>
            new FrameInfo<SbModule>
            {
                AddrMax = info.AddrMax,
                AddrMin = info.AddrMin,
                Args = info.Args,
                FuncName = info.FuncName,
                Language = info.Language,
                ModuleName = info.ModuleName,
                ReturnType = info.ReturnType,
                Flags = info.Flags,
                ValidFields = (FrameInfoFlags)info.ValidFields,
                HasDebugInfo = info.HasDebugInfo,
                StaleCode = info.StaleCode,
                Module = info.Module != null ? moduleFactory.Create(connection, info.Module) : null
            };

        internal static LineEntryInfo CreateLineEntryInfo(GrpcLineEntryInfo info) =>
            info == null ? null : new LineEntryInfo
            {
                FileName = info.FileName,
                Directory = info.Directory,
                Line = info.Line,
                Column = info.Column
            };
    }
}
