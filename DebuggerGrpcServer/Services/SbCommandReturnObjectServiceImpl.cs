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

using Debugger.SbCommandReturnObjectRpc;
using Grpc.Core;
using LldbApi;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    class SbCommandReturnObjectServiceImpl
        : SbCommandReturnObjectRpcService.SbCommandReturnObjectRpcServiceBase
    {
        readonly ObjectStore<SbCommandReturnObject> returnObjectStore;

        public SbCommandReturnObjectServiceImpl(
            ObjectStore<SbCommandReturnObject> returnObjectStore)
        {
            this.returnObjectStore = returnObjectStore;
        }

        #region SbCommandInterpreterRpcService.SbCommandInterpreterRpcServiceBase

        public override Task<DeleteResponse> Delete(DeleteRequest request,
            ServerCallContext context)
        {
            returnObjectStore.RemoveObject(request.ReturnObject.Id);
            return Task.FromResult(new DeleteResponse());
        }

        public override Task<SucceededResponse> Succeeded(SucceededRequest request,
            ServerCallContext context)
        {
            var returnObject = returnObjectStore.GetObject(request.ReturnObject.Id);
            return Task.FromResult(new SucceededResponse { Succeeded = returnObject.Succeeded() });
        }

        public override Task<GetOutputResponse> GetOutput(GetOutputRequest request,
            ServerCallContext context)
        {
            var returnObject = returnObjectStore.GetObject(request.ReturnObject.Id);
            return Task.FromResult(new GetOutputResponse { Output = returnObject.GetOutput() });
        }

        public override Task<GetErrorResponse> GetError(GetErrorRequest request,
            ServerCallContext context)
        {
            var returnObject = returnObjectStore.GetObject(request.ReturnObject.Id);
            return Task.FromResult(new GetErrorResponse { Error = returnObject.GetError() });
        }

        public override Task<GetDescriptionResponse> GetDescription(GetDescriptionRequest request,
            ServerCallContext context)
        {
            var returnObject = returnObjectStore.GetObject(request.ReturnObject.Id);
            return Task.FromResult(new GetDescriptionResponse {
                Description = returnObject.GetDescription() });
        }

        #endregion
    }
}
