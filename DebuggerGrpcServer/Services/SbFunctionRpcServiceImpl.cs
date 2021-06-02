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
using Debugger.SbFunctionRpc;
using Grpc.Core;
using LldbApi;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    // Server implementation of SbFunction RPC.
    class SbFunctionRpcServiceImpl : SbFunctionRpcService.SbFunctionRpcServiceBase
    {
        readonly ObjectStore<SbAddress> addressStore;
        readonly ObjectStore<SbFunction> functionStore;
        readonly ObjectStore<SbInstruction> instructionStore;
        readonly ConcurrentDictionary<long, RemoteTarget> targetStore;

        public SbFunctionRpcServiceImpl(ObjectStore<SbAddress> addressStore,
            ObjectStore<SbFunction> functionStore,
            ObjectStore<SbInstruction> instructionStore,
            ConcurrentDictionary<long, RemoteTarget> targetStore)
        {
            this.addressStore = addressStore;
            this.functionStore = functionStore;
            this.instructionStore = instructionStore;
            this.targetStore = targetStore;
        }

        #region SbFunctionRpcService.SbFunctionRpcServiceBase
        public override Task<DeleteResponse> Delete(DeleteRequest request,
            ServerCallContext context)
        {
            functionStore.RemoveObject(request.Function.Id);
            return Task.FromResult(new DeleteResponse());
        }

        public override Task<GetStartAddressResponse> GetStartAddress(
            GetStartAddressRequest request, ServerCallContext context)
        {
            var function = functionStore.GetObject(request.Function.Id);
            var address = function.GetStartAddress();
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
            var function = functionStore.GetObject(request.Function.Id);
            var address = function.GetEndAddress();
            var response = new GetEndAddressResponse();
            if (address != null)
            {
                response.Address = new GrpcSbAddress { Id = addressStore.AddObject(address) };
            }
            return Task.FromResult(response);
        }

        public override Task<GetLanguageResponse> GetLanguage(GetLanguageRequest request,
            ServerCallContext context)
        {
            var function = functionStore.GetObject(request.Function.Id);
            GetLanguageResponse.Types.LanguageType languageType;
            switch (function.GetLanguage())
            {
                case LanguageType.C:
                    languageType = GetLanguageResponse.Types.LanguageType.C;
                    break;
                case LanguageType.C11:
                    languageType = GetLanguageResponse.Types.LanguageType.C11;
                    break;
                case LanguageType.C89:
                    languageType = GetLanguageResponse.Types.LanguageType.C89;
                    break;
                case LanguageType.C99:
                    languageType = GetLanguageResponse.Types.LanguageType.C99;
                    break;
                case LanguageType.C_PLUS_PLUS:
                    languageType = GetLanguageResponse.Types.LanguageType.CPlusPlus;
                    break;
                case LanguageType.C_PLUS_PLUS_03:
                    languageType = GetLanguageResponse.Types.LanguageType.CPlusPlus03;
                    break;
                case LanguageType.C_PLUS_PLUS_11:
                    languageType = GetLanguageResponse.Types.LanguageType.CPlusPlus11;
                    break;
                case LanguageType.C_PLUS_PLUS_14:
                    languageType = GetLanguageResponse.Types.LanguageType.CPlusPlus14;
                    break;
                case LanguageType.UNKNOWN:
                // fall through
                default:
                    languageType = GetLanguageResponse.Types.LanguageType.Unknown;
                    break;
            }
            return Task.FromResult(new GetLanguageResponse { LanguageType = languageType });
        }

        public override Task<GetInstructionsResponse> GetInstructions(
            GetInstructionsRequest request, ServerCallContext context)
        {
            var function = functionStore.GetObject(request.Function.Id);
            var target = GrpcLookupUtils.GetTarget(request.Target, targetStore);
            var instructions = function.GetInstructions(target.GetSbTarget());
            var response = new GetInstructionsResponse();
            if (instructions != null)
            {
                response.Instructions.Add(
                    instructions.Select(s => new GrpcSbInstruction
                        { Id = instructionStore.AddObject(s) }));
            }
            return Task.FromResult(response);
        }

        public override Task<GetNameResponse> GetName(GetNameRequest request,
            ServerCallContext context)
        {
            var function = functionStore.GetObject(request.Function.Id);
            return Task.FromResult(new GetNameResponse
            {
                Name = function?.GetName() ?? "",
            });
        }

        #endregion

    }
}
