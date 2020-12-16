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
using DebuggerApi;
using System;
using System.Runtime.InteropServices;
using DebuggerCommonApi;
using SbAddressRpcServiceClient = Debugger.SbAddressRpc.SbAddressRpcService.SbAddressRpcServiceClient;

namespace DebuggerGrpcClient
{
    // Creates SbAddress objects.
    public class GrpcAddressFactory
    {
        public virtual SbAddress Create(
            GrpcConnection connection, GrpcSbAddress grpcSbAddress)
        {
            return new SbAddressImpl(connection, grpcSbAddress);
        }
    }

    // Implementation of the SBAddress interface that uses GRPC to make RPCs to a remote endpoint.
    class SbAddressImpl : SbAddress
    {
        readonly GrpcConnection connection;
        readonly SbAddressRpcServiceClient client;
        readonly GrpcSbAddress grpcSbAddress;
        readonly GrpcFunctionFactory functionFactory;
        readonly GrpcSymbolFactory symbolFactory;
        readonly GCHandle gcHandle;

        internal SbAddressImpl(GrpcConnection connection, GrpcSbAddress grpcSbAddress)
            : this(connection,
                  new SbAddressRpcServiceClient(connection.CallInvoker),
                  grpcSbAddress, new GrpcFunctionFactory(), new GrpcSymbolFactory())
        { }

        internal SbAddressImpl(
            GrpcConnection connection, SbAddressRpcServiceClient client,
            GrpcSbAddress grpcSbAddress,
            GrpcFunctionFactory functionFactory,
            GrpcSymbolFactory symbolFactory)
        {
            this.connection = connection;
            this.client = client;
            this.grpcSbAddress = grpcSbAddress;
            this.functionFactory = functionFactory;
            this.symbolFactory = symbolFactory;

            // Keep a handle to objects we need in the destructor.
            gcHandle = GCHandle.Alloc(
                new Tuple<GrpcConnection, SbAddressRpcServiceClient, GrpcSbAddress>(
                    connection, client, grpcSbAddress));
        }

        ~SbAddressImpl()
        {
            connection.InvokeRpc(() =>
                {
                    client.Delete(new DeleteRequest { Address = grpcSbAddress });
                });
            gcHandle.Free();
        }

        #region SbAddress
        public long GetId()
        {
            return grpcSbAddress.Id;
        }

        public LineEntryInfo GetLineEntry()
        {
            GetLineEntryResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetLineEntry(
                    new GetLineEntryRequest {Address = grpcSbAddress});
            }))
            {
                if (response.LineEntry != null)
                {
                    return new LineEntryInfo
                    {
                        FileName = response.LineEntry.FileName,
                        Directory = response.LineEntry.Directory,
                        Line = response.LineEntry.Line,
                        Column = response.LineEntry.Column,
                    };
                }
            }
            return null;
        }

        public ulong GetLoadAddress(RemoteTarget target)
        {
            GetLoadAddressResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetLoadAddress(
                    new GetLoadAddressRequest
                    {
                        Address = grpcSbAddress,
                        Target = new GrpcSbTarget { Id = target.GetId() }
                    });
            }))
            {
                return response.LoadAddress;
            }
            return 0;
        }

        public SbFunction GetFunction()
        {
            GetFunctionResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetFunction(
                        new GetFunctionRequest
                        {
                            Address = grpcSbAddress,
                        });
                }))
            {
                if (response.Function != null && response.Function.Id != 0)
                {
                    return functionFactory.Create(connection, response.Function);
                }
            }
            return null;
        }

        public SbSymbol GetSymbol()
        {
            GetSymbolResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetSymbol(
                        new GetSymbolRequest
                        {
                            Address = grpcSbAddress,
                        });
                }))
            {
                if (response.Symbol != null && response.Symbol.Id != 0)
                {
                    return symbolFactory.Create(connection, response.Symbol);
                }
            }
            return null;
        }
        #endregion
    }
}
