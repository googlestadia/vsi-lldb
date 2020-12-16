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

using Debugger.SbCommandReturnObjectRpc;
using Debugger.Common;
using DebuggerApi;
using System;
using System.Runtime.InteropServices;
using SbCommandReturnObjectRpcServiceClient =
    Debugger.SbCommandReturnObjectRpc.SbCommandReturnObjectRpcService.SbCommandReturnObjectRpcServiceClient;

namespace DebuggerGrpcClient
{
    public class GrpcSbCommandReturnObjectFactory
    {
        public virtual SbCommandReturnObject Create(
            GrpcConnection connection, GrpcSbCommandReturnObject grpcSbReturnObject)
        {
            return new SbCommandReturnObjectImpl(connection, grpcSbReturnObject);
        }
    }

    class SbCommandReturnObjectImpl : SbCommandReturnObject
    {
        readonly GrpcConnection connection;
        readonly SbCommandReturnObjectRpcServiceClient client;
        readonly GrpcSbCommandReturnObject grpcSbReturnObject;
        readonly GrpcSbCommandReturnObjectFactory returnObjectFactory;
        readonly GCHandle gcHandle;

        internal SbCommandReturnObjectImpl(GrpcConnection connection,
            GrpcSbCommandReturnObject grpcSbReturnObject)
            : this(connection,
                  new SbCommandReturnObjectRpcServiceClient(connection.CallInvoker),
                  grpcSbReturnObject,
                  new GrpcSbCommandReturnObjectFactory())
        { }

        internal SbCommandReturnObjectImpl(
            GrpcConnection connection, SbCommandReturnObjectRpcServiceClient client,
            GrpcSbCommandReturnObject grpcSbReturnObject,
            GrpcSbCommandReturnObjectFactory returnObjectFactory)
        {
            this.connection = connection;
            this.client = client;
            this.grpcSbReturnObject = grpcSbReturnObject;
            this.returnObjectFactory = returnObjectFactory;

            // Keep a handle to objects we need in the destructor.
            gcHandle = GCHandle.Alloc(
                new Tuple<GrpcConnection,
                          SbCommandReturnObjectRpcServiceClient,
                          GrpcSbCommandReturnObject>(
                    connection, client, grpcSbReturnObject));
        }

        ~SbCommandReturnObjectImpl()
        {
            connection.InvokeRpc(() =>
            {
                client.Delete(new DeleteRequest { ReturnObject = grpcSbReturnObject });
            });
            gcHandle.Free();
        }

        #region SbCommandReturnObject

        public bool Succeeded()
        {
            SucceededResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.Succeeded(
                        new SucceededRequest { ReturnObject = grpcSbReturnObject });
                }))
            {
                return response.Succeeded;
            }
            return false;
        }

        public string GetOutput()
        {
            GetOutputResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetOutput(
                        new GetOutputRequest { ReturnObject = grpcSbReturnObject });
                }))
            {
                return response.Output;
            }
            return "";
        }

        public string GetError()
        {
            GetErrorResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetError(
                        new GetErrorRequest { ReturnObject = grpcSbReturnObject });
                }))
            {
                return response.Error;
            }
            return "";
        }

        public string GetDescription()
        {
            GetDescriptionResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetDescription(
                        new GetDescriptionRequest { ReturnObject = grpcSbReturnObject });
                }))
            {
                return response.Description;
            }
            return "";
        }

        #endregion
    }
}
