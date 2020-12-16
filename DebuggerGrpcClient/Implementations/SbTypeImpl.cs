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
using DebuggerApi;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SbTypeRpcServiceClient = Debugger.SbTypeRpc.SbTypeRpcService.SbTypeRpcServiceClient;

namespace DebuggerGrpcClient
{
    // Creates SbType objects.
    public class GrpcTypeFactory
    {
        public virtual SbType Create(GrpcConnection connection, GrpcSbType grpcSbType)
        {
            return new SbTypeImpl(connection, grpcSbType);
        }
    }
    
    // Implementation of the SBType interface that uses GRPC to make RPCs to a remote endpoint.
    class SbTypeImpl : SbType
    {
        readonly GrpcConnection connection;
        readonly SbTypeRpcServiceClient client;
        readonly GrpcSbType grpcSbType;
        readonly GrpcTypeFactory typeFactory;
        readonly GrpcTypeMemberFactory typeMemberFactory;
        readonly GCHandle gcHandle;

        internal SbTypeImpl(GrpcConnection connection, GrpcSbType grpcSbType)
            : this(connection,
                new SbTypeRpcServiceClient(connection.CallInvoker), grpcSbType,
                new GrpcTypeFactory(), new GrpcTypeMemberFactory())
        { }

        internal SbTypeImpl(
            GrpcConnection connection, SbTypeRpcServiceClient client,
            GrpcSbType grpcSbType, GrpcTypeFactory typeFactory,
            GrpcTypeMemberFactory typeMemberFactory)
        {
            this.connection = connection;
            this.client = client;
            this.grpcSbType = grpcSbType;
            this.typeFactory = typeFactory;
            this.typeMemberFactory = typeMemberFactory;

            // Keep a handle to objects we need in the destructor.
            gcHandle = GCHandle.Alloc(
                new Tuple<GrpcConnection, SbTypeRpcServiceClient, GrpcSbType>(
                    connection, client, grpcSbType));
        }

        ~SbTypeImpl()
        {
            connection
                .GetOrCreateBulkDeleter<GrpcSbType>()
                .QueueForDeletion(grpcSbType, (List<GrpcSbType> types) =>
                {
                    var request = new BulkDeleteRequest();
                    request.Types_.AddRange(types);
                    connection.InvokeRpc(() =>
                    {
                        client.BulkDelete(request);
                    });
                });
            gcHandle.Free();
        }

        #region Prefetched properties

        public TypeFlags GetTypeFlags()
        {
            return (TypeFlags)grpcSbType.Flags;
        }

        public string GetName()
        {
            return grpcSbType.Name;
        }

        public uint GetNumberOfDirectBaseClasses()
        {
            return grpcSbType.NumberOfDirectBaseClasses;
        }

        public long GetId()
        {
            return grpcSbType.Id;
        }

#endregion

        public SbTypeMember GetDirectBaseClassAtIndex(uint index)
        {
            GetDirectBaseClassAtIndexResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetDirectBaseClassAtIndex(
                        new GetDirectBaseClassAtIndexRequest { Type = grpcSbType, Index = index });
                }))
            {
                if (response.TypeMember != null && response.TypeMember.Id != 0)
                {
                    return typeMemberFactory.Create(connection, response.TypeMember);
                }
            }
            return null;
        }

        public SbType GetCanonicalType()
        {
            GetCanonicalTypeResponse response = null;
            if (connection.InvokeRpc(() => {
                    response =
                        client.GetCanonicalType(new GetCanonicalTypeRequest { Type = grpcSbType });
                }))
            {
                if (response.Type != null && response.Type.Id != 0)
                {
                    return typeFactory.Create(connection, response.Type);
                }
            }
            return null;
        }

        public SbType GetPointeeType()
        {
            GetPointeeTypeResponse response = null;
            if (connection.InvokeRpc(() => {
                    response =
                        client.GetPointeeType(new GetPointeeTypeRequest { Type = grpcSbType });
                }))
            {
                if (response.Type != null && response.Type.Id != 0)
                {
                    return typeFactory.Create(connection, response.Type);
                }
            }
            return null;
        }

        public ulong GetByteSize()
        {
            GetByteSizeResponse response = null;
            if (connection.InvokeRpc(() => {
                    response = client.GetByteSize(new GetByteSizeRequest { Type = grpcSbType });
                }))
            {
                return response.ByteSize;
            }
            return 0;
        }
    }
}
