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
using DebuggerApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using SbFunctionRpcServiceClient =
    Debugger.SbFunctionRpc.SbFunctionRpcService.SbFunctionRpcServiceClient;

namespace DebuggerGrpcClient
{
    // Creates SbFunction objects.
    public class GrpcFunctionFactory
    {
        public virtual SbFunction Create(GrpcConnection connection, GrpcSbFunction grpcSbFunction)
        {
            return new SbFunctionImpl(connection, grpcSbFunction);
        }
    }

    // Implementation of the SBFunction interface that uses GRPC to make RPCs to a remote endpoint.
    class SbFunctionImpl : SbFunction
    {
        readonly GrpcConnection connection;
        readonly SbFunctionRpcServiceClient client;
        readonly GrpcSbFunction grpcSbFunction;
        readonly GrpcAddressFactory addressFactory;
        readonly GCHandle gcHandle;

        internal SbFunctionImpl(GrpcConnection connection, GrpcSbFunction grpcSbFunction)
            : this(connection,
                  new SbFunctionRpcServiceClient(connection.CallInvoker),
                  grpcSbFunction, new GrpcAddressFactory())
        { }

        internal SbFunctionImpl(
            GrpcConnection connection, SbFunctionRpcServiceClient client,
            GrpcSbFunction grpcSbFunction, GrpcAddressFactory addressFactory)
        {
            this.connection = connection;
            this.client = client;
            this.grpcSbFunction = grpcSbFunction;
            this.addressFactory = addressFactory;

            // Keep a handle to objects we need in the destructor.
            gcHandle = GCHandle.Alloc(
                new Tuple<GrpcConnection, SbFunctionRpcServiceClient, GrpcSbFunction>(
                    connection, client, grpcSbFunction));
        }

        ~SbFunctionImpl()
        {
            connection.InvokeRpc(() =>
            {
                client.Delete(new DeleteRequest { Function = grpcSbFunction });
            });
            gcHandle.Free();
        }

        #region SbFunction
        public SbAddress GetStartAddress()
        {
            GetStartAddressResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetStartAddress(
                        new GetStartAddressRequest { Function = grpcSbFunction });
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
                    response =  client.GetEndAddress(
                        new GetEndAddressRequest { Function = grpcSbFunction });
                }))
            {
                if (response.Address != null && response.Address.Id != 0)
                {
                    return addressFactory.Create(connection, response.Address);
                }
            }
            return null;
        }

        public LanguageType GetLanguage()
        {
            GetLanguageResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response =  client.GetLanguage(
                        new GetLanguageRequest { Function = grpcSbFunction });
                }))
            {
                switch (response.LanguageType)
                {
                    case GetLanguageResponse.Types.LanguageType.C:
                        return LanguageType.C;
                    case GetLanguageResponse.Types.LanguageType.C11:
                        return LanguageType.C11;
                    case GetLanguageResponse.Types.LanguageType.C89:
                        return LanguageType.C89;
                    case GetLanguageResponse.Types.LanguageType.C99:
                        return LanguageType.C99;
                    case GetLanguageResponse.Types.LanguageType.CPlusPlus:
                        return LanguageType.C_PLUS_PLUS;
                    case GetLanguageResponse.Types.LanguageType.CPlusPlus03:
                        return LanguageType.C_PLUS_PLUS_03;
                    case GetLanguageResponse.Types.LanguageType.CPlusPlus11:
                        return LanguageType.C_PLUS_PLUS_11;
                    case GetLanguageResponse.Types.LanguageType.CPlusPlus14:
                        return LanguageType.C_PLUS_PLUS_14;
                    case GetLanguageResponse.Types.LanguageType.Unknown:
                    //fall through
                    default:
                        return LanguageType.UNKNOWN;
                }
            }
            return LanguageType.UNKNOWN;
        }

        public string GetName()
        {
            GetNameResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response =  client.GetName(new GetNameRequest
                    {
                        Function = grpcSbFunction,
                    });
                }))
            {
                return response.Name;
            }
            return "";
        }

        #endregion
    }
}
