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
using DebuggerCommonApi;
using LldbApi;

namespace DebuggerGrpcServer
{
    class GrpcFactoryUtils
    {
        internal static GrpcFrameInfo CreateFrameInfo(
            FrameInfo<SbModule> info, long? moduleId = null) =>
            new GrpcFrameInfo
            {
                AddrMax = info.AddrMax,
                AddrMin = info.AddrMin,
                Args = info.Args ?? "",
                FuncName = info.FuncName ?? "",
                Language = info.Language ?? "",
                ModuleName = info.ModuleName ?? "",
                ReturnType = info.ReturnType ?? "",
                Flags = info.Flags,
                ValidFields = (uint)info.ValidFields,
                HasDebugInfo = info.HasDebugInfo,
                StaleCode = info.StaleCode,
                Module = moduleId.HasValue ? new GrpcSbModule { Id = moduleId.Value } : null
            };

        internal static GrpcSbError CreateError(SbError error) =>
            error != null ? new GrpcSbError
            {
                Success = error.Success(),
                Code = error.GetError(),
                Error = error.GetCString() ?? ""
            } : null;

        internal static GrpcSbValue CreateValue(RemoteValue remoteValue, long id) =>
            new GrpcSbValue
            {
                Id = id,
                Name = remoteValue.GetName() ?? "",
                Error = CreateError(remoteValue.GetError())
            };

        internal static GrpcSbType CreateType(SbType sbType, long id) =>
            new GrpcSbType
            {
                Id = id,
                Flags = (uint)sbType.GetTypeFlags(),
                Name = sbType.GetName() ?? "",
                NumberOfDirectBaseClasses = sbType.GetNumberOfDirectBaseClasses(),
            };

        internal static GrpcLineEntryInfo CreateGrpcLineEntryInfo(SbLineEntry lineEntry)
        {
            return lineEntry == null ? null : new GrpcLineEntryInfo
            {
                FileName = lineEntry.GetFileName() ?? "",
                Directory = lineEntry.GetDirectory() ?? "",
                Line = lineEntry.GetLine(),
                Column = lineEntry.GetColumn()
            };
        }
    }
}
