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
using DebuggerApi;
using System;
using System.Runtime.InteropServices;
using YetiCommon;
using RemoteValueRpcServiceClient =
    Debugger.RemoteValueRpc.RemoteValueRpcService.RemoteValueRpcServiceClient;
using DebuggerGrpcClient.Implementations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DebuggerGrpcClient
{
    // Creates RemoteValue objects.
    public class GrpcValueFactory
    {
        public virtual RemoteValue Create(
            GrpcConnection connection, GrpcSbValue grpcSbValue)
        {
            return new RemoteValueProxy(connection, grpcSbValue);
        }
    }

    // Implementation of the RemoteValue interface that uses GRPC to make RPCs to a remote
    // endpoint.
    class RemoteValueProxy : RemoteValue
    {
        readonly GrpcConnection connection;
        readonly RemoteValueRpcServiceClient client;
        readonly GrpcSbValue grpcSbValue;
        readonly GrpcValueFactory valueFactory;
        readonly GrpcErrorFactory errorFactory;
        readonly GrpcTypeFactory typeFactory;
        readonly CachedValueFactory cachedValueFactory;
        readonly GCHandle gcHandle;

        internal RemoteValueProxy(GrpcConnection connection, GrpcSbValue grpcSbValue)
            : this(connection,
                  new RemoteValueRpcServiceClient(connection.CallInvoker), grpcSbValue,
                  new GrpcValueFactory(), new GrpcErrorFactory(), new GrpcTypeFactory(),
                  new CachedValueFactory())
        { }

        internal RemoteValueProxy(
            GrpcConnection connection, RemoteValueRpcServiceClient client,
            GrpcSbValue grpcSbValue, GrpcValueFactory valueFactory, GrpcErrorFactory errorFactory,
            GrpcTypeFactory typeFactory, CachedValueFactory cachedValueFactory)
        {
            this.connection = connection;
            this.client = client;
            this.grpcSbValue = grpcSbValue;
            this.valueFactory = valueFactory;
            this.errorFactory = errorFactory;
            this.typeFactory = typeFactory;
            this.cachedValueFactory = cachedValueFactory;

            // Keep a handle to objects we need in the destructor.
            gcHandle = GCHandle.Alloc(
                new Tuple<GrpcConnection, RemoteValueRpcServiceClient, GrpcSbValue>(
                    connection, client, grpcSbValue));
        }

        ~RemoteValueProxy()
        {
            connection
                .GetOrCreateBulkDeleter<GrpcSbValue>()
                .QueueForDeletion(grpcSbValue, (List<GrpcSbValue> values) =>
                {
                    var request = new BulkDeleteRequest();
                    request.Values.AddRange(values);
                    connection.InvokeRpc(() =>
                    {
                        client.BulkDelete(request);
                    });
                });
            gcHandle.Free();
        }

        #region Prefetched properties

        public string GetName()
        {
            return grpcSbValue.Name;
        }

        public SbError GetError()
        {
            return errorFactory.Create(grpcSbValue.Error);
        }

        #endregion

        public string GetValue(DebuggerApi.ValueFormat format)
        {
            GetValueResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetValue(
                        new GetValueRequest
                        {
                            Value = grpcSbValue,
                            Format = format.ConvertTo<Debugger.Common.ValueFormat>()
                        });
                }))
            {
                return response.Value;
            }
            return "";
        }

        public SbType GetTypeInfo()
        {
            GetTypeInfoResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetTypeInfo(
                        new GetTypeInfoRequest { Value = grpcSbValue });
                }))
            {
                if (response.Type != null && response.Type.Id != 0)
                    return typeFactory.Create(connection, response.Type);
            }
            return null;
        }

        public string GetTypeName()
        {
            GetTypeNameResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetTypeName(
                        new GetTypeNameRequest { Value = grpcSbValue });
                }))
            {
                return response.TypeName;
            }
            return "";
        }

        public string GetSummary(DebuggerApi.ValueFormat format)
        {
            GetSummaryResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetSummary(
                        new GetSummaryRequest
                        {
                            Value = grpcSbValue,
                            Format = format.ConvertTo<Debugger.Common.ValueFormat>()
                        });
                }))
            {
                return response.Summary;
            }
            return "";
        }

        public DebuggerApi.ValueType GetValueType()
        {
            GetValueTypeResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetValueType(
                        new GetValueTypeRequest { Value = grpcSbValue });
                }))
            {
                return response.ValueType.ConvertTo<DebuggerApi.ValueType>();
            }
            return DebuggerApi.ValueType.Invalid;
        }

        public uint GetNumChildren()
        {
            GetNumChildrenResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetNumChildren(
                        new GetNumChildrenRequest { Value = grpcSbValue });
                }))
            {
                return response.NumChildren;
            }
            return 0;
        }

        public List<RemoteValue> GetChildren(uint offset, uint count)
        {
            var values = Enumerable.Repeat<RemoteValue>(null, (int)count).ToList();
            GetChildrenResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetChildren(
                    new GetChildrenRequest
                    {
                        Value = grpcSbValue,
                        Offset = offset,
                        Count = count
                    });
            }))
            {
                for (uint n = 0; n < count; ++n)
                {
                    GrpcSbValue sbValue;
                    if (response.Children != null &&
                        response.Children.TryGetValue(n + offset, out sbValue) &&
                        sbValue.Id != 0)
                    {
                        values[(int)n] = valueFactory.Create(connection, sbValue);
                    }
                }
            }

            return values;
        }

        public RemoteValue CreateValueFromExpression(string name, string expression)
        {
            CreateValueFromExpressionResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.CreateValueFromExpression(
                    new CreateValueFromExpressionRequest
                    { Value = grpcSbValue, Name = name, Expression = expression });
            }))
            {
                if (response.ExpressionResult != null && response.ExpressionResult.Id != 0)
                {
                    return valueFactory.Create(connection, response.ExpressionResult);
                }
            }
            return null;
        }

        public RemoteValue CreateValueFromAddress(string name, ulong address, SbType type)
        {
            CreateValueFromAddressResponse response = null;
            if (connection.InvokeRpc(() => {
                response = client.CreateValueFromAddress(new CreateValueFromAddressRequest {
                    Value = grpcSbValue, Name = name, Address = address,
                    Type = new GrpcSbType { Id = type.GetId() }
                });
            }))
            {
                if (response.ExpressionResult != null && response.ExpressionResult.Id != 0)
                {
                    return valueFactory.Create(connection, response.ExpressionResult);
                }
            }
            return null;
        }

        public async Task<RemoteValue> CreateValueFromExpressionAsync(string name,
            string expression)
        {
            CreateValueFromExpressionResponse response = null;
            if (await connection.InvokeRpcAsync(async () =>
            {
                response = await client.CreateValueFromExpressionAsync(
                    new CreateValueFromExpressionRequest
                    { Value = grpcSbValue, Name = name, Expression = expression });
            }))
            {
                if (response.ExpressionResult != null && response.ExpressionResult.Id != 0)
                {
                    return valueFactory.Create(connection, response.ExpressionResult);
                }
            }

            return null;
        }

        public RemoteValue EvaluateExpression(string expression)
        {
            EvaluateExpressionResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.EvaluateExpression(
                    new EvaluateExpressionRequest
                    { Value = grpcSbValue, Expression = expression });
            }))
            {
                if (response.ExpressionResult != null && response.ExpressionResult.Id != 0)
                {
                    return valueFactory.Create(connection, response.ExpressionResult);
                }
            }
            return null;
        }

        public async Task<RemoteValue> EvaluateExpressionAsync(string expression)
        {
            EvaluateExpressionResponse response = null;
            if (await connection.InvokeRpcAsync(async () =>
            {
                response = await client.EvaluateExpressionAsync(
                    new EvaluateExpressionRequest
                    { Value = grpcSbValue, Expression = expression });
            }))
            {
                if (response.ExpressionResult != null && response.ExpressionResult.Id != 0)
                {
                    return valueFactory.Create(connection, response.ExpressionResult);
                }
            }
            return null;
        }

        public async Task<RemoteValue> EvaluateExpressionLldbEvalAsync(string expression)
        {
            EvaluateExpressionLldbEvalResponse response = null;
            if (await connection.InvokeRpcAsync(async () =>
            {
                response = await client.EvaluateExpressionLldbEvalAsync(
                    new EvaluateExpressionLldbEvalRequest
                    { Value = grpcSbValue, Expression = expression });
            }))
            {
                if (response.Value != null && response.Value.Id != 0)
                {
                    return valueFactory.Create(connection, response.Value);
                }
            }
            return null;
        }

        public RemoteValue Dereference()
        {
            DereferenceResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.Dereference(new DereferenceRequest{ Value = grpcSbValue });
            }))
            {
                if (response.DereferenceResult != null && response.DereferenceResult.Id != 0)
                {
                    return valueFactory.Create(connection, response.DereferenceResult);
                }
            }
            return null;
        }

        public RemoteValue GetChildMemberWithName(string name)
        {
            GetChildMemberWithNameResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetChildMemberWithName(
                        new GetChildMemberWithNameRequest { Value = grpcSbValue, Name = name });
                }))
            {
                if (response.Child != null && response.Child.Id != 0)
                {
                    return valueFactory.Create(connection, response.Child);
                }
            }
            return null;
        }

        public RemoteValue AddressOf()
        {
            AddressOfResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.AddressOf(
                        new AddressOfRequest { Value = grpcSbValue });
                }))
            {
                if (response.AddressValue != null && response.AddressValue.Id != 0)
                {
                    return valueFactory.Create(connection, response.AddressValue);
                }
            }
            return null;
        }

        public bool TypeIsPointerType()
        {
            TypeIsPointerTypeResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                    response = client.TypeIsPointerType(
                        new TypeIsPointerTypeRequest { Value = grpcSbValue });
                }))
            {
                return response.IsPointer;
            }
            return false;
        }

        public RemoteValue GetValueForExpressionPath(string expressionPath)
        {
            GetValueForExpressionPathResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetValueForExpressionPath(
                        new GetValueForExpressionPathRequest
                        {
                            Value = grpcSbValue,
                            ExpressionPath = expressionPath
                        });
                }))
            {
                if (response.Value != null && response.Value.Id != 0)
                {
                    return valueFactory.Create(connection, response.Value);
                }
            }
            return null;
        }

        public bool GetExpressionPath(out string path)
        {
            GetExpressionPathResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetExpressionPath(
                        new GetExpressionPathRequest { Value = grpcSbValue });
                }))
            {
                path = response.Path;
                return response.ReturnValue;
            }
            path = null;
            return false;
        }

        public RemoteValue GetCachedView(DebuggerApi.ValueFormat format)
        {
            GetCachedViewResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetCachedView(
                    new GetCachedViewRequest
                    {
                        Value = grpcSbValue,
                        Format = format.ConvertTo<Debugger.Common.ValueFormat>()
                    });
            }))
            {
                if (response.ValueInfo != null)
                {
                    RemoteValue addressOf = null;
                    if (response.AddressValue != null && response.AddressValue.Id != 0)
                    {
                        addressOf = valueFactory.Create(connection, response.AddressValue);
                        if (response.AddressInfo != null)
                        {
                            // gRpc server does not set |format| on |addressOf|, so use default.
                            addressOf = CreateCachedValue(addressOf, response.AddressInfo, null,
                                DebuggerApi.ValueFormat.Default);
                        }
                    }
                    return CreateCachedValue(this, response.ValueInfo, addressOf, format);
                }
            }
            return null;
        }

        public ulong GetByteSize()
        {
            GetByteSizeResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetByteSize(
                    new GetByteSizeRequest { Value = grpcSbValue });
            }))
            {
                return response.ByteSize;
            }
            return 0;
        }

        public ulong GetValueAsUnsigned()
        {
            GetValueAsUnsignedResponse response = null;
            if (connection.InvokeRpc(() => {
                    response = client.GetValueAsUnsigned(
                        new GetValueAsUnsignedRequest { Value = grpcSbValue });
                }))
            {
                return response.Value;
            }
            return 0;
        }

        public byte[] GetPointeeAsByteString(uint charSize, uint maxStringSize, out string error)
        {
            GetPointeeAsByteStringResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetPointeeAsByteString(
                    new GetPointeeAsByteStringRequest
                    {
                        Value = grpcSbValue,
                        CharSize = charSize,
                        MaxStringSize = maxStringSize
                    });
            }))
            {
                error = response.Error;
                return response.Data.ToByteArray();
            }

            error = null;
            return null;
        }

        private RemoteValue CreateCachedValue(
            RemoteValue remoteProxy, GrpcValueInfo info, RemoteValue addressOf,
            DebuggerApi.ValueFormat format) =>
            cachedValueFactory.Create(
                remoteProxy,
                addressOf,
                info.Type != null && info.Type.Id != 0 ?
                typeFactory.Create(connection, info.Type) : null,
                info.ExpressionPath,
                info.HasExpressionPath,
                info.NumChildren,
                info.Summary,
                info.TypeName,
                info.Value,
                EnumUtil.ConvertTo<DebuggerApi.ValueType>(info.ValueType),
                info.IsPointerType,
                format,
                info.ByteSize);
    }
}
