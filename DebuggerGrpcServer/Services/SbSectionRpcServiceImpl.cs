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

﻿using Grpc.Core;
using Debugger.Common;
using Debugger.SbSectionRpc;
using LldbApi;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using YetiCommon;

namespace DebuggerGrpcServer
{
    // Server implementation of the SBSection RPC.
    public class SbSectionRpcServiceImpl : SbSectionRpcService.SbSectionRpcServiceBase
    {
        readonly ObjectStore<SbSection> sectionStore;
        readonly ConcurrentDictionary<long, RemoteTarget> targetStore;

        internal SbSectionRpcServiceImpl(ObjectStore<SbSection> sectionStore,
            ConcurrentDictionary<long, RemoteTarget> targetStore)
        {
            this.sectionStore = sectionStore;
            this.targetStore = targetStore;
        }

        #region SbSectionRpcService.SbSectionRpcServiceBase

        public override Task<DeleteResponse> Delete(DeleteRequest request,
            ServerCallContext context)
        {
            sectionStore.RemoveObject(request.Section.Id);
            return Task.FromResult(new DeleteResponse());
        }

        public override Task<GetFileAddressResponse> GetFileAddress(GetFileAddressRequest request,
            ServerCallContext context)
        {
            var section = sectionStore.GetObject(request.Section.Id);
            return Task.FromResult(
                new GetFileAddressResponse { FileAddress = section.GetFileAddress() });
        }

        public override Task<GetFileOffsetResponse> GetFileOffset(GetFileOffsetRequest request,
            ServerCallContext context)
        {
            var section = sectionStore.GetObject(request.Section.Id);
            return Task.FromResult(
                new GetFileOffsetResponse { FileOffset = section.GetFileOffset() });
        }

        public override Task<GetLoadAddressResponse> GetLoadAddress(GetLoadAddressRequest request,
            ServerCallContext context)
        {
            var section = sectionStore.GetObject(request.Section.Id);
            var target = GrpcLookupUtils.GetTarget(request.Target, targetStore);
            return Task.FromResult(
                new GetLoadAddressResponse {
                    LoadAddress = section.GetLoadAddress(target.GetSbTarget()) });
        }

        public override Task<GetSectionTypeResponse> GetSectionType(GetSectionTypeRequest request,
            ServerCallContext context)
        {
            var section = sectionStore.GetObject(request.Section.Id);
            return Task.FromResult(new GetSectionTypeResponse {
                SectionType = section.GetSectionType().ConvertTo<Debugger.Common.SectionType>() });
        }

        #endregion
    }
}
