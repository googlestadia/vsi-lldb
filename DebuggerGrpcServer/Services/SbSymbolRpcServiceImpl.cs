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
using Debugger.SbSymbolRpc;
using Grpc.Core;
using LldbApi;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    // Server implementation of SbSymbol RPC.
    class SbSymbolRpcServiceImpl : SbSymbolRpcService.SbSymbolRpcServiceBase
    {
        readonly ObjectStore<SbAddress> addressStore;
        readonly ObjectStore<SbSymbol> symbolStore;

        public SbSymbolRpcServiceImpl(ObjectStore<SbAddress> addressStore,
            ObjectStore<SbSymbol> symbolStore)
        {
            this.addressStore = addressStore;
            this.symbolStore = symbolStore;
        }

        #region SbSymbolRpcService.SbSymbolRpcServiceBase
        public override Task<DeleteResponse> Delete(DeleteRequest request,
            ServerCallContext context)
        {
            symbolStore.RemoveObject(request.Symbol.Id);
            return Task.FromResult(new DeleteResponse());
        }

        public override Task<GetStartAddressResponse> GetStartAddress(
            GetStartAddressRequest request, ServerCallContext context)
        {
            var symbol = symbolStore.GetObject(request.Symbol.Id);
            var address = symbol.GetStartAddress();
            var response = new GetStartAddressResponse();
            if (address != null)
            {
                response.Address = new GrpcSbAddress { Id = addressStore.AddObject(address) };
            }
            return Task.FromResult(response);
        }

        public override Task<GetEndAddressResponse> GetEndAddress(GetEndAddressRequest request,
            ServerCallContext context)
        {
            var symbol = symbolStore.GetObject(request.Symbol.Id);
            var address = symbol.GetEndAddress();
            var response = new GetEndAddressResponse();
            if (address != null)
            {
                response.Address = new GrpcSbAddress { Id = addressStore.AddObject(address) };
            }
            return Task.FromResult(response);
        }

        public override Task<GetNameResponse> GetName(GetNameRequest request,
            ServerCallContext context)
        {
            var symbol = symbolStore.GetObject(request.Symbol.Id);
            var name = symbol.GetName();
            return Task.FromResult(new GetNameResponse { Name = name });
        }
        #endregion
    }
}