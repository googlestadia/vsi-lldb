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
using Debugger.SbPlatformRpc;
using DebuggerApi;

namespace DebuggerGrpcClient
{
    // Creates SBPlatform objects.
    public class GrpcPlatformFactory
    {
        public virtual SbPlatform Create(string platformName, GrpcConnection grpcConnection)
        {
            var instance = new SbPlatformImpl(grpcConnection);
            if (instance.Create(platformName))
            {
                return instance;
            }
            return null;
        }

        // Only to be used internally.  After a platform has already been created, you may want to
        // create another instance to call APIs on (ie. the return value for GetSelectedPlatform).
        // Since this new instance won't be creating a new SbPlatform object on the server there is
        // no need to pass in a platform name.
        //
        // This approach only works because we only support a single platform on the server.  If
        // this changes we will need to have the server return a unique ID for the instance of
        // platform that APIs should be called on.  So this Create method will most likely change
        // to accept ID.
        internal SbPlatform Create(GrpcConnection grpcConnection)
        {
            return new SbPlatformImpl(grpcConnection);
        }
    }

    // Implementation of the SBPlatform interface that uses GRPC to make RPCs to a remote endpoint.
    class SbPlatformImpl : SbPlatform
    {
        readonly SbPlatformRpcService.SbPlatformRpcServiceClient client;
        readonly GrpcErrorFactory sbErrorFactory;
        readonly GrpcConnection connection;

        internal SbPlatformImpl(GrpcConnection connection)
            : this(connection, new GrpcErrorFactory(),
            new SbPlatformRpcService.SbPlatformRpcServiceClient(connection.CallInvoker))
        { }

        // Constructor that can be used by tests to pass in mock objects.
        internal SbPlatformImpl(GrpcConnection connection, GrpcErrorFactory sbErrorFactory,
            SbPlatformRpcService.SbPlatformRpcServiceClient client)
        {
            this.sbErrorFactory = sbErrorFactory;
            this.client = client;
            this.connection = connection;
        }

        internal bool Create(string platformName)
        {
            return connection.InvokeRpc(() =>
                {
                    client.Create(new CreateRequest { PlatformName = platformName });
                });
        }

        #region SbPlatform

        public SbError ConnectRemote(SbPlatformConnectOptions connectOptions)
        {
            var grpcSbPlatformConnectOptions = new GrpcSbPlatformConnectOptions
            {
                Url = connectOptions.GetUrl()
            };
            var request = new ConnectRemoteRequest
            {
                ConnectOptions = grpcSbPlatformConnectOptions
            };
            ConnectRemoteResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.ConnectRemote(request);
                }))
            {
                return sbErrorFactory.Create(response.Error);
            }
            var grpcSbError = new GrpcSbError
            {
                Success = false,
                Error = "Rpc error while calling ConnectRemote.  Inspect the logs for more " +
                    "information."
            };
            return sbErrorFactory.Create(grpcSbError);
        }

        public SbError Run(SbPlatformShellCommand command)
        {
            var grpcSbPlatformShellCommand = new GrpcSbPlatformShellCommand
            {
                Command = command.GetCommand()
            };
            var request = new RunRequest { ShellCommand = grpcSbPlatformShellCommand };
            RunResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.Run(request);
                }))
            {
                command.SetOutput(response.Output);
                command.SetSignal(response.Signal);
                command.SetStatus(response.Status);
                return sbErrorFactory.Create(response.Error);
            }
            var grpcSbError = new GrpcSbError
            {
                Success = false,
                Error = "Rpc error while calling Run.  Inspect the logs for more information."
            };
            return sbErrorFactory.Create(grpcSbError);
        }

        #endregion SbPlatform
    }
}
