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
using Debugger.SbTypeMemberRpc;
using Grpc.Core;
using LldbApi;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    class SbTypeMemberRpcServiceImpl : SbTypeMemberRpcService.SbTypeMemberRpcServiceBase
    {
        readonly ObjectStore<SbTypeMember> typeMemberStore;
        readonly ObjectStore<SbType> typeStore;

        public SbTypeMemberRpcServiceImpl(ObjectStore<SbTypeMember> typeMemberStore,
            ObjectStore<SbType> typeStore)
        {
            this.typeMemberStore = typeMemberStore;
            this.typeStore = typeStore;
        }

        #region SbTypeMemberRpcService.SbTypeMemberRpcServiceBase

        public override Task<DeleteResponse> Delete(DeleteRequest request,
            ServerCallContext context)
        {
            typeMemberStore.RemoveObject(request.TypeMember.Id);
            return Task.FromResult(new DeleteResponse());
        }

        public override Task<GetTypeInfoResponse> GetTypeInfo(
            GetTypeInfoRequest request, ServerCallContext context)
        {
            var typeMember = typeMemberStore.GetObject(request.TypeMember.Id);
            var sbType = typeMember.GetTypeInfo();
            var response = new GetTypeInfoResponse();

            if (sbType != null)
            {
                response.Type = GrpcFactoryUtils.CreateType(sbType, typeStore.AddObject(sbType));
            }
            return Task.FromResult(response);
        }

        #endregion
    }
}
