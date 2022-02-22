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
using Debugger.RemoteFrameRpc;
using DebuggerCommonApi;
using Grpc.Core;
using LldbApi;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YetiCommon;

namespace DebuggerGrpcServer
{
    // Server implementation of SbFrame RPC.
    public class RemoteFrameRpcServiceImpl : RemoteFrameRpcService.RemoteFrameRpcServiceBase
    {
        readonly ObjectStore<RemoteValue> valueStore;
        readonly ObjectStore<SbFunction> functionStore;
        readonly ObjectStore<SbSymbol> symbolStore;
        readonly UniqueObjectStore<SbModule> moduleStore;
        readonly ObjectStore<RemoteFrame> frameStore;
        readonly ObjectStore<RemoteThread> threadStore;

        public RemoteFrameRpcServiceImpl(ObjectStore<RemoteValue> valueStore,
                                         ObjectStore<SbFunction> functionStore,
                                         ObjectStore<SbSymbol> symbolStore,
                                         UniqueObjectStore<SbModule> moduleStore,
                                         ObjectStore<RemoteFrame> frameStore,
                                         ObjectStore<RemoteThread> threadStore)
        {
            this.valueStore = valueStore;
            this.functionStore = functionStore;
            this.symbolStore = symbolStore;
            this.moduleStore = moduleStore;
            this.frameStore = frameStore;
            this.threadStore = threadStore;
        }

        #region RemoteFrameRpcService.RemoteFrameRpcServiceBase
        public override Task<BulkDeleteResponse> BulkDelete(BulkDeleteRequest request,
            ServerCallContext context)
        {
            foreach (GrpcSbFrame frame in request.Frames)
            {
                frameStore.RemoveObject(frame.Id);
            }
            return Task.FromResult(new BulkDeleteResponse());
        }

        public override Task<GetFunctionResponse> GetFunction(GetFunctionRequest request,
            ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);
            SbFunction function = frame.GetFunction();

            var response = new GetFunctionResponse();
            if (function != null)
            {
                response.Function = new GrpcSbFunction { Id = functionStore.AddObject(function) };
            }
            return Task.FromResult(response);
        }

        public override Task<GetSymbolResponse> GetSymbol(GetSymbolRequest request,
            ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);
            SbSymbol symbol = frame.GetSymbol();

            var response = new GetSymbolResponse();
            if (symbol != null)
            {
                response.Symbol = new GrpcSbSymbol { Id = symbolStore.AddObject(symbol) };
            }
            return Task.FromResult(response);
        }

        public override Task<GetVariablesResponse> GetVariables(GetVariablesRequest request,
           ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);
            List<RemoteValue> variables =
                frame.GetVariables(
                    request.Arguments, request.Locals, request.Statics, request.OnlyInScope);

            var response = new GetVariablesResponse();
            response.Variables.Add(
                variables.Select(s => GrpcFactoryUtils.CreateValue(
                    s, valueStore.AddObject(s))).ToList());

            return Task.FromResult(response);
        }

        public override Task<GetValueForVariablePathResponse> GetValueForVariablePath(
            GetValueForVariablePathRequest request,
            ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);
            RemoteValue value = frame.GetValueForVariablePath(request.VariablePath);

            var response = new GetValueForVariablePathResponse();
            if (value != null)
            {
                response.Value = GrpcFactoryUtils.CreateValue(value, valueStore.AddObject(value));
            }
            return Task.FromResult(response);
        }

        public override Task<FindValueResponse> FindValue(FindValueRequest request,
            ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);
            RemoteValue variable = frame.FindValue(request.VariableName,
                request.ValueType.ConvertTo<LldbApi.ValueType>());

            var response = new FindValueResponse();
            if (variable != null)
            {
                response.Variable = GrpcFactoryUtils.CreateValue(
                    variable, valueStore.AddObject(variable));
            }

            return Task.FromResult(response);
        }

        public override Task<GetRegistersResponse> GetRegisters(GetRegistersRequest request,
            ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);
            List<RemoteValue> registers = frame.GetRegisters();

            var response = new GetRegistersResponse();
            response.Registers.Add(
                registers.Select(s => GrpcFactoryUtils.CreateValue(
                    s, valueStore.AddObject(s))).ToList());

            return Task.FromResult(response);
        }

        public override Task<GetModuleResponse> GetModule(GetModuleRequest request,
            ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);
            SbModule module = frame.GetModule();

            GetModuleResponse response = new GetModuleResponse();
            if (module != null)
            {
                response.Module = new GrpcSbModule { Id = moduleStore.AddObject(module) };
            }
            return Task.FromResult(response);
        }

        public override Task<GetThreadResponse> GetThread(GetThreadRequest request,
            ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);
            RemoteThread thread = frame.GetThread();

            var response = new GetThreadResponse();
            if (thread != null)
            {
                response.Thread = new GrpcSbThread() { Id = threadStore.AddObject(thread) };
            }
            return Task.FromResult(response);
        }

        public override Task<SetPCResponse> SetPC(SetPCRequest request, ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);

            var response = new SetPCResponse
            {
                Result = frame.SetPC(request.Addr)
            };
            return Task.FromResult(response);
        }

        public override Task<EvaluateExpressionResponse> EvaluateExpression(
            EvaluateExpressionRequest request, ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);
            RemoteValue value = frame.EvaluateExpression(request.Expression);

            var response = new EvaluateExpressionResponse();
            if (value != null)
            {
                response.Value = GrpcFactoryUtils.CreateValue(value, valueStore.AddObject(value));
            }
            return Task.FromResult(response);
        }

        public override Task<EvaluateExpressionLldbEvalResponse> EvaluateExpressionLldbEval(
            EvaluateExpressionLldbEvalRequest request, ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);
            RemoteValue value = frame.EvaluateExpressionLldbEval(request.Expression);

            var response = new EvaluateExpressionLldbEvalResponse();
            if (value != null)
            {
                response.Value = GrpcFactoryUtils.CreateValue(value, valueStore.AddObject(value));
            }
            return Task.FromResult(response);
        }

        public override Task<GetPhysicalStackRangeResponse> GetPhysicalStackRange(
            GetPhysicalStackRangeRequest request, ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);
            AddressRange addressRange = frame.GetPhysicalStackRange();

            var response = new GetPhysicalStackRangeResponse();
            if (addressRange != null)
            {
                response.AddressRange = new GrpcAddressRange()
                {
                    AddressMin = addressRange.addressMin,
                    AddressMax = addressRange.addressMax
                };
            }
            return Task.FromResult(response);
        }

        public override Task<GetInfoResponse> GetInfo(GetInfoRequest request,
            ServerCallContext context)
        {
            RemoteFrame frame = frameStore.GetObject(request.Frame.Id);
            FrameInfo<SbModule> info = frame.GetInfo((FrameInfoFlags)request.Fields);

            // Note: info can't be null since it's a struct.
            var moduleId = info.Module != null ? moduleStore.AddObject(info.Module) : (long?)null;
            return Task.FromResult(new GetInfoResponse
            {
                Info = GrpcFactoryUtils.CreateFrameInfo(info, moduleId)
            });
        }

        #endregion
    }
}
