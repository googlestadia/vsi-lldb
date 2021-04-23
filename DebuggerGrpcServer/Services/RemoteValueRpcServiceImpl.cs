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
using Debugger.RemoteValueRpc;
using Grpc.Core;
using LldbApi;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using YetiCommon;
using System.Collections.Generic;

namespace DebuggerGrpcServer
{
    // Server implementation of RemoteValue RPC.
    class RemoteValueRpcServiceImpl : RemoteValueRpcService.RemoteValueRpcServiceBase
    {
        readonly ObjectStore<RemoteValue> valueStore;
        readonly ObjectStore<SbType> typeStore;

        public RemoteValueRpcServiceImpl(ObjectStore<RemoteValue> valueStore,
            ObjectStore<SbType> typeStore)
        {
            this.valueStore = valueStore;
            this.typeStore = typeStore;
        }

        #region RemoteValueRpcService.RemoteValueRpcServiceBase

        public override Task<BulkDeleteResponse> BulkDelete(BulkDeleteRequest request,
            ServerCallContext context)
        {
            foreach (GrpcSbValue value in request.Values)
            {
                valueStore.RemoveObject(value.Id);
            }
            return Task.FromResult(new BulkDeleteResponse());
        }

        public override Task<GetValueResponse> GetValue(GetValueRequest request,
            ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            value.SetFormat(request.Format.ConvertTo<LldbApi.ValueFormat>());
            return Task.FromResult(new GetValueResponse { Value = value.GetValue() });
        }

        public override Task<GetTypeInfoResponse> GetTypeInfo(
               GetTypeInfoRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            var sbType = value.GetTypeInfo();
            var response = new GetTypeInfoResponse();

            if (sbType != null)
            {
                response.Type = GrpcFactoryUtils.CreateType(sbType, typeStore.AddObject(sbType));
            }
            return Task.FromResult(response);
        }

        public override Task<GetTypeNameResponse> GetTypeName(GetTypeNameRequest request,
            ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            return Task.FromResult(new GetTypeNameResponse { TypeName = value.GetTypeName() });
        }

        public override Task<GetSummaryResponse> GetSummary(GetSummaryRequest request,
            ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            value.SetFormat(request.Format.ConvertTo<LldbApi.ValueFormat>());
            return Task.FromResult(new GetSummaryResponse { Summary = value.GetSummary() });
        }

        public override Task<GetValueTypeResponse> GetValueType(GetValueTypeRequest request,
            ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            var valueType = value.GetValueType()
                                 .ConvertTo<Debugger.Common.ValueType>();
            return Task.FromResult(new GetValueTypeResponse { ValueType = valueType });
        }

        public override Task<GetNumChildrenResponse> GetNumChildren(GetNumChildrenRequest request,
            ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            return Task.FromResult(
                new GetNumChildrenResponse { NumChildren = value.GetNumChildren() });
        }

        public override Task<GetChildrenResponse> GetChildren(
            GetChildrenRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            var children = value.GetChildren(request.Offset, request.Count);
            var response = new GetChildrenResponse();
            for (uint n = 0; n < children.Count; ++n)
            {
                RemoteValue child = children[(int)n];
                if (child != null)
                {
                    response.Children[n + request.Offset] =
                        GrpcFactoryUtils.CreateValue(child, valueStore.AddObject(child));
                }
            }

            // (internal): Special case for pointers. LLDB names them $"*{value.GetName()}", but
            // Visual Studio just shows an empty name.
            if (value.TypeIsPointerType() && response.Children.ContainsKey(0) &&
                response.Children[0].Name == $"*{value.GetName()}")
            {
                response.Children[0].Name = string.Empty;
            }

            return Task.FromResult(response);
        }

        public override Task<CreateValueFromExpressionResponse> CreateValueFromExpression(
            CreateValueFromExpressionRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            var expressionResult =
                value.CreateValueFromExpression(request.Name, request.Expression);
            var response = new CreateValueFromExpressionResponse();
            if (expressionResult != null)
            {
                response.ExpressionResult = GrpcFactoryUtils.CreateValue(
                    expressionResult, valueStore.AddObject(expressionResult));
            }
            return Task.FromResult(response);
        }

        public override Task<CreateValueFromAddressResponse> CreateValueFromAddress(
            CreateValueFromAddressRequest request, ServerCallContext context)
        {
            RemoteValue value = valueStore.GetObject(request.Value.Id);
            RemoteValue expressionResult = value.CreateValueFromAddress(
                request.Name, request.Address, typeStore.GetObject(request.Type.Id));
            var response = new CreateValueFromAddressResponse();
            if (expressionResult != null)
            {
                response.ExpressionResult = GrpcFactoryUtils.CreateValue(
                    expressionResult, valueStore.AddObject(expressionResult));
            }
            return Task.FromResult(response);
        }

        public override Task<EvaluateExpressionResponse> EvaluateExpression(
            EvaluateExpressionRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            var expressionResult =
                value.EvaluateExpression(request.Expression);
            var response = new EvaluateExpressionResponse();
            if (expressionResult != null)
            {
                response.ExpressionResult = GrpcFactoryUtils.CreateValue(
                    expressionResult, valueStore.AddObject(expressionResult));
            }
            return Task.FromResult(response);
        }

        public override Task<EvaluateExpressionLldbEvalResponse> EvaluateExpressionLldbEval(
            EvaluateExpressionLldbEvalRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            IDictionary<string, SbValue> contextValues = new Dictionary<string, SbValue>();
            if (request.ContextVariables != null)
            {
                foreach (var contextVariable in request.ContextVariables)
                {
                    var val = valueStore.GetObject(contextVariable.Value.Id);

                    if (val != null)
                    {
                        contextValues.Add(contextVariable.Name, val.GetSbValue());
                    }
                }
            }
            var result = value.EvaluateExpressionLldbEval(request.Expression, contextValues);
            var response = new EvaluateExpressionLldbEvalResponse();
            if (result != null)
            {
                response.Value = GrpcFactoryUtils.CreateValue(result, valueStore.AddObject(result));
            }
            return Task.FromResult(response);
        }

        public override Task<CloneResponse> Clone(CloneRequest request, ServerCallContext context)
        {
            RemoteValue value = valueStore.GetObject(request.Value.Id);
            RemoteValue cloneResult = value.Clone();
            var response = new CloneResponse();
            if (cloneResult != null)
            {
                response.CloneResult =
                    GrpcFactoryUtils.CreateValue(cloneResult, valueStore.AddObject(cloneResult));
            }
            return Task.FromResult(response);
        }

        public override Task<DereferenceResponse> Dereference(
            DereferenceRequest request, ServerCallContext context)
        {
            RemoteValue value = valueStore.GetObject(request.Value.Id);
            RemoteValue dereferenceResult = value.Dereference();
            var response = new DereferenceResponse();
            if (dereferenceResult != null)
            {
                response.DereferenceResult = GrpcFactoryUtils.CreateValue(
                    dereferenceResult, valueStore.AddObject(dereferenceResult));
            }
            return Task.FromResult(response);
        }

        public override Task<GetChildMemberWithNameResponse> GetChildMemberWithName(
            GetChildMemberWithNameRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            var child = value.GetChildMemberWithName(request.Name);
            var response = new GetChildMemberWithNameResponse();
            if (child != null)
            {
                response.Child = GrpcFactoryUtils.CreateValue(child, valueStore.AddObject(child));
            }
            return Task.FromResult(response);
        }

        public override Task<AddressOfResponse> AddressOf(
            AddressOfRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            var address = value.AddressOf();
            var response = new AddressOfResponse();
            if (address != null)
            {
                response.AddressValue = GrpcFactoryUtils.CreateValue(
                    address, valueStore.AddObject(address));
            }
            return Task.FromResult(response);
        }

        public override Task<TypeIsPointerTypeResponse> TypeIsPointerType(
            TypeIsPointerTypeRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            return Task.FromResult(
                new TypeIsPointerTypeResponse { IsPointer = value.TypeIsPointerType() });
        }

        public override Task<GetValueForExpressionPathResponse> GetValueForExpressionPath(
            GetValueForExpressionPathRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            var childValue = value.GetValueForExpressionPath(request.ExpressionPath);
            GetValueForExpressionPathResponse response = new GetValueForExpressionPathResponse();
            if (childValue != null)
            {
                response.Value = GrpcFactoryUtils.CreateValue(
                    childValue, valueStore.AddObject(childValue));
            }
            return Task.FromResult(response);
        }

        public override Task<GetExpressionPathResponse> GetExpressionPath(
            GetExpressionPathRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            string path;
            bool returnValue = value.GetExpressionPath(out path);
            return Task.FromResult(
                new GetExpressionPathResponse { ReturnValue = returnValue, Path = path});
        }

        public override Task<GetCachedViewResponse> GetCachedView(
            GetCachedViewRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            value.SetFormat(request.Format.ConvertTo<LldbApi.ValueFormat>());
            var response = new GetCachedViewResponse
            {
                ValueInfo = CreateValueInfoAndUpdateStores(value)
            };
            var addressOf = value.AddressOf();
            if (addressOf != null)
            {
                response.AddressValue = GrpcFactoryUtils.CreateValue(
                    addressOf, valueStore.AddObject(addressOf));
                response.AddressInfo = CreateValueInfoAndUpdateStores(addressOf);
            }
            return Task.FromResult(response);
        }

        public override Task<GetByteSizeResponse> GetByteSize(GetByteSizeRequest request,
            ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            return Task.FromResult(
                new GetByteSizeResponse { ByteSize = value.GetByteSize() });
        }

        public override Task<GetValueAsUnsignedResponse> GetValueAsUnsigned(
            GetValueAsUnsignedRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            return Task.FromResult(
                new GetValueAsUnsignedResponse { Value = value.GetValueAsUnsigned() });
        }

        public override Task<GetPointeeAsByteStringResponse> GetPointeeAsByteString(
            GetPointeeAsByteStringRequest request, ServerCallContext context)
        {
            var value = valueStore.GetObject(request.Value.Id);
            string error;
            byte[] data =
                value.GetPointeeAsByteString(request.CharSize, request.MaxStringSize, out error);
            return Task.FromResult(new GetPointeeAsByteStringResponse
            {
                Data = ByteString.CopyFrom(data ?? new byte[0]),
                Error = error ?? ""
            });
        }

        #endregion

        private GrpcValueInfo CreateValueInfoAndUpdateStores(RemoteValue remoteValue)
        {
            if (remoteValue == null)
            {
                return null;
            }

            string expressionPath;
            var hasExpressionPath = remoteValue.GetExpressionPath(out expressionPath);
            var valueInfo = new GrpcValueInfo
            {
                ExpressionPath = expressionPath ?? "",
                HasExpressionPath = hasExpressionPath,
                NumChildren = remoteValue.GetNumChildren(),
                Summary = remoteValue.GetSummary() ?? "",
                TypeName = remoteValue.GetTypeName() ?? "",
                Value = remoteValue.GetValue() ?? "",
                ValueType = EnumUtil.ConvertTo<Debugger.Common.ValueType>(
                    remoteValue.GetValueType()),
                IsPointerType = remoteValue.TypeIsPointerType(),
                ByteSize = remoteValue.GetByteSize(),
            };
            var typeInfo = remoteValue.GetTypeInfo();
            if (typeInfo != null)
            {
                valueInfo.Type = GrpcFactoryUtils.CreateType(
                    typeInfo, typeStore.AddObject(typeInfo));
            }
            return valueInfo;
        }
    }
}
