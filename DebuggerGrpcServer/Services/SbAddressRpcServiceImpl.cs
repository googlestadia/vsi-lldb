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

using Debugger.SbAddressRpc;
using Debugger.Common;
using Grpc.Core;
using LldbApi;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    // Server implementation of SbAddress RPC.
    class SbAddressRpcServiceImpl : SbAddressRpcService.SbAddressRpcServiceBase
    {
        readonly ObjectStore<SbAddress> addressStore;
        readonly ConcurrentDictionary<long, RemoteTarget> targetStore;
        readonly ObjectStore<SbFunction> functionStore;
        readonly ObjectStore<SbSymbol> symbolStore;

        public SbAddressRpcServiceImpl(ObjectStore<SbAddress> addressStore,
            ConcurrentDictionary<long, RemoteTarget> targetStore,
            ObjectStore<SbFunction> functionStore,
            ObjectStore<SbSymbol> symbolStore)
        {
            this.addressStore = addressStore;
            this.targetStore = targetStore;
            this.functionStore = functionStore;
            this.symbolStore = symbolStore;
        }

        #region SbAddressRpcService.SbAddressRpcServiceBase
        public override Task<DeleteResponse> Delete(DeleteRequest request,
            ServerCallContext context)
        {
            addressStore.RemoveObject(request.Address.Id);
            return Task.FromResult(new DeleteResponse());
        }

        public override Task<GetLineEntryResponse> GetLineEntry(GetLineEntryRequest request,
            ServerCallContext context)
        {
            var address = addressStore.GetObject(request.Address.Id);
            var response = new GetLineEntryResponse();
            response.LineEntry = GrpcFactoryUtils.CreateGrpcLineEntryInfo(address.GetLineEntry());
            return Task.FromResult(response);
        }

        public override Task<GetLoadAddressResponse> GetLoadAddress(GetLoadAddressRequest request,
            ServerCallContext context)
        {
            var address = addressStore.GetObject(request.Address.Id);
            var target = GrpcLookupUtils.GetTarget(request.Target, targetStore);
            var loadAddress = address.GetLoadAddress(target.GetSbTarget());
            return Task.FromResult(new GetLoadAddressResponse { LoadAddress = loadAddress });
        }

        public override Task<GetFunctionResponse> GetFunction(GetFunctionRequest request,
            ServerCallContext context)
        {
            var address = addressStore.GetObject(request.Address.Id);
            var function = address.GetFunction();
            return Task.FromResult(new GetFunctionResponse
            {
                Function = new GrpcSbFunction
                {
                    Id = functionStore.AddObject(function),
                }
            });
        }

        public override Task<GetSymbolResponse> GetSymbol(GetSymbolRequest request,
            ServerCallContext context)
        {
            var address = addressStore.GetObject(request.Address.Id);
            var symbol = address.GetSymbol();
            var response = new GetSymbolResponse();
            if (symbol != null)
            {
                response.Symbol = new GrpcSbSymbol { Id = symbolStore.AddObject(symbol) };
            }
            return Task.FromResult(response);
        }
        #endregion
    }
}
