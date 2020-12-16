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
using DebuggerApi;
using System;
using System.Runtime.InteropServices;
using SbTypeMemberRpcServiceClient =
    Debugger.SbTypeMemberRpc.SbTypeMemberRpcService.SbTypeMemberRpcServiceClient;

namespace DebuggerGrpcClient
{
    // Creates SbTypeMember objects.
    public class GrpcTypeMemberFactory
    {
        public virtual SbTypeMember Create(
            GrpcConnection connection, GrpcSbTypeMember grpcSbTypeMember)
        {
            return new SbTypeMemberImpl(connection, grpcSbTypeMember);
        }
    }

    class SbTypeMemberImpl : SbTypeMember
    {
        readonly GrpcConnection connection;
        readonly SbTypeMemberRpcServiceClient client;
        readonly GrpcSbTypeMember grpcSbTypeMember;
        readonly GrpcTypeFactory typeFactory;
        readonly GCHandle gcHandle;

        internal SbTypeMemberImpl(GrpcConnection connection, GrpcSbTypeMember grpcSbTypeMember)
            : this(connection,
                  new SbTypeMemberRpcServiceClient(connection.CallInvoker), grpcSbTypeMember,
                  new GrpcTypeFactory())
        { }

        internal SbTypeMemberImpl(
            GrpcConnection connection, SbTypeMemberRpcServiceClient client,
            GrpcSbTypeMember grpcSbTypeMember, GrpcTypeFactory typeFactory)
        {
            this.connection = connection;
            this.client = client;
            this.grpcSbTypeMember = grpcSbTypeMember;
            this.typeFactory = typeFactory;

            // Keep a handle to objects we need in the destructor.
            gcHandle = GCHandle.Alloc(
                new Tuple<GrpcConnection, SbTypeMemberRpcServiceClient, GrpcSbTypeMember>(
                    connection, client, grpcSbTypeMember));
        }

        ~SbTypeMemberImpl()
        {
            connection.InvokeRpc(() =>
            {
                client.Delete(new DeleteRequest { TypeMember = grpcSbTypeMember });
            });
            gcHandle.Free();
        }

        #region SbTypeMember

        public SbType GetTypeInfo()
        {
            GetTypeInfoResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetTypeInfo(
                        new GetTypeInfoRequest { TypeMember = grpcSbTypeMember });
                }))
            {
                if (response.Type != null && response.Type.Id != 0)
                {
                    return typeFactory.Create(connection, response.Type);
                }
            }
            return null;
        }

        #endregion
    }
}
