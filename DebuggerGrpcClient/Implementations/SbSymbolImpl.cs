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
using DebuggerApi;
using System;
using System.Runtime.InteropServices;
using SbSymbolRpcServiceClient = Debugger.SbSymbolRpc.SbSymbolRpcService.SbSymbolRpcServiceClient;

namespace DebuggerGrpcClient
{
    // Creates SbSymbol objects.
    public class GrpcSymbolFactory
    {
        public virtual SbSymbol Create(GrpcConnection connection, GrpcSbSymbol grpcSbSymbol)
        {
            return new SbSymbolImpl(connection, grpcSbSymbol);
        }
    }

    // Implementation of the SBSymbol interface that uses GRPC to make RPCs to a remote endpoint.
    class SbSymbolImpl : SbSymbol
    {
        readonly GrpcConnection connection;
        readonly SbSymbolRpcServiceClient client;
        readonly GrpcSbSymbol grpcSbSymbol;
        readonly GrpcAddressFactory addressFactory;
        readonly GCHandle gcHandle;

        internal SbSymbolImpl(GrpcConnection connection, GrpcSbSymbol grpcSbSymbol)
            : this(connection,
                  new SbSymbolRpcServiceClient(connection.CallInvoker),
                  grpcSbSymbol, new GrpcAddressFactory())
        { }

        internal SbSymbolImpl(
            GrpcConnection connection, SbSymbolRpcServiceClient client,
            GrpcSbSymbol grpcSbSymbol, GrpcAddressFactory addressFactory)
        {
            this.connection = connection;
            this.client = client;
            this.grpcSbSymbol = grpcSbSymbol;
            this.addressFactory = addressFactory;

            // Keep a handle to objects we need in the destructor.
            gcHandle = GCHandle.Alloc(
                new Tuple<GrpcConnection, SbSymbolRpcServiceClient, GrpcSbSymbol>(
                    connection, client, grpcSbSymbol));
        }

        ~SbSymbolImpl()
        {
            connection.InvokeRpc(() =>
                {
                    client.Delete(new DeleteRequest { Symbol = grpcSbSymbol });
                });
            gcHandle.Free();
        }

        #region SbSymbol
        public SbAddress GetStartAddress()
        {
            GetStartAddressResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetStartAddress(
                        new GetStartAddressRequest { Symbol = grpcSbSymbol });
                }))
            {
                if (response.Address != null && response.Address.Id != 0)
                {
                    return addressFactory.Create(connection, response.Address);
                }
            }
            return null;
        }

        public SbAddress GetEndAddress()
        {
            GetEndAddressResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetEndAddress(
                        new GetEndAddressRequest { Symbol = grpcSbSymbol });
                }))
            {
                if (response.Address != null && response.Address.Id != 0)
                {
                    return addressFactory.Create(connection, response.Address);
                }
            }
            return null;
        }

        public string GetName()
        {
            GetNameResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetName(
                        new GetNameRequest { Symbol = grpcSbSymbol });
                }))
            {
                return response.Name;
            }
            return "";
        }
        #endregion
    }
}