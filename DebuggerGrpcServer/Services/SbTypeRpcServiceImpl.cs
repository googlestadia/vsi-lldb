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

using Debugger.Common;
using Debugger.SbTypeRpc;
using Grpc.Core;
using LldbApi;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    // Server implementation of SbType RPC.
    class SbTypeRpcServiceImpl : SbTypeRpcService.SbTypeRpcServiceBase
    {
        readonly ObjectStore<SbType> typeStore;
        readonly ObjectStore<SbTypeMember> typeMemberStore;

        public SbTypeRpcServiceImpl(ObjectStore<SbType> typeStore,
            ObjectStore<SbTypeMember> typeMemberStore)
        {
            this.typeStore = typeStore;
            this.typeMemberStore = typeMemberStore;
        }

        #region SbTypeRpcService.SbTypeRpcServiceBase

        public override Task<BulkDeleteResponse> BulkDelete(BulkDeleteRequest request,
            ServerCallContext context)
        {
            foreach (GrpcSbType type in request.Types_)
            {
                typeStore.RemoveObject(type.Id);
            }
            return Task.FromResult(new BulkDeleteResponse());
        }

        public override Task<GetTypeFlagsResponse> GetTypeFlags(GetTypeFlagsRequest request,
            ServerCallContext context)
        {
            var type = typeStore.GetObject(request.Type.Id);
            return Task.FromResult(new GetTypeFlagsResponse { Flags = (uint)type.GetTypeFlags() });
        }

        public override Task<GetNameResponse> GetName(GetNameRequest request,
            ServerCallContext context)
        {
            var type = typeStore.GetObject(request.Type.Id);
            return Task.FromResult(new GetNameResponse { Name = type.GetName() });
        }

        public override Task<GetNumberOfDirectBaseClassesResponse> GetNumberOfDirectBaseClasses(
            GetNumberOfDirectBaseClassesRequest request,
            ServerCallContext context)
        {
            var type = typeStore.GetObject(request.Type.Id);
            return Task.FromResult(new GetNumberOfDirectBaseClassesResponse {
                Count = (uint)type.GetNumberOfDirectBaseClasses()
            });
        }

        public override Task<GetDirectBaseClassAtIndexResponse> GetDirectBaseClassAtIndex(
            GetDirectBaseClassAtIndexRequest request,
            ServerCallContext context)
        {
            var type = typeStore.GetObject(request.Type.Id);
            var child = type.GetDirectBaseClassAtIndex(request.Index);
            var response = new GetDirectBaseClassAtIndexResponse();
            if (child != null)
            {
                long id = typeMemberStore.AddObject(child);
                response.TypeMember = new GrpcSbTypeMember{ Id = id };
            }
            return Task.FromResult(response);
        }

        public override Task<GetCanonicalTypeResponse> GetCanonicalType(
            GetCanonicalTypeRequest request, ServerCallContext context)
        {
            var type = typeStore.GetObject(request.Type.Id);
            var canonicalType = type.GetCanonicalType();
            var response = new GetCanonicalTypeResponse();
            if (canonicalType != null)
            {
                response.Type = GrpcFactoryUtils.CreateType(
                    canonicalType, typeStore.AddObject(canonicalType));
            }
            return Task.FromResult(response);
        }

        public override Task<GetPointeeTypeResponse> GetPointeeType(GetPointeeTypeRequest request,
                                                                    ServerCallContext context)
        {
            SbType type = typeStore.GetObject(request.Type.Id);
            SbType pointeeType = type.GetPointeeType();
            var response = new GetPointeeTypeResponse();
            if (pointeeType != null)
            {
                response.Type =
                    GrpcFactoryUtils.CreateType(pointeeType, typeStore.AddObject(pointeeType));
            }
            return Task.FromResult(response);
        }

        public override Task<GetByteSizeResponse> GetByteSize(GetByteSizeRequest request,
                                                              ServerCallContext context)
        {
            SbType type = typeStore.GetObject(request.Type.Id);
            return Task.FromResult(new GetByteSizeResponse { ByteSize = type.GetByteSize() });
        }

        #endregion
    }
}
