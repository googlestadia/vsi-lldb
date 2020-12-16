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
using Grpc.Core;
using LldbApi;
using System.Threading.Tasks;
using YetiVSI.DebugEngine;

namespace DebuggerGrpcServer
{
    // Server implementation of the SBPlatform RPC.
    class SbPlatformRpcServiceImpl : SbPlatformRpcService.SbPlatformRpcServiceBase,
        SbPlatformManager
    {
        readonly LLDBPlatformFactory sbPlatformFactory;
        readonly LLDBPlatformConnectOptionsFactory sbPlatformConnectOptionsFactory;
        readonly LLDBPlatformShellCommandFactory sbPlatformShellCommandFactory;

        SbPlatform sbPlatform = null;

        public SbPlatformRpcServiceImpl()
            : this(new LLDBPlatformFactory(),
                   new LLDBPlatformConnectOptionsFactory(),
                   new LLDBPlatformShellCommandFactory())
        { }

        public SbPlatformRpcServiceImpl(LLDBPlatformFactory sbPlatformFactory,
            LLDBPlatformConnectOptionsFactory sbPlatformConnectOptionsFactory,
            LLDBPlatformShellCommandFactory sbPlatformShellCommandFactory)
        {
            this.sbPlatformFactory = sbPlatformFactory;
            this.sbPlatformConnectOptionsFactory = sbPlatformConnectOptionsFactory;
            this.sbPlatformShellCommandFactory = sbPlatformShellCommandFactory;
        }

        #region SbPlatformManager

        public SbPlatform GetPlatform()
        {
            return sbPlatform;
        }

        #endregion

        #region SbPlatformRpcService.SbPlatformRpcServiceBase

        public override Task<CreateResponse> Create(CreateRequest request,
            ServerCallContext context)
        {
            // We only support creating one SBPlatform object, fail if there is an attempt to
            // create more.
            if (sbPlatform != null)
            {
                ErrorUtils.ThrowError(StatusCode.FailedPrecondition,
                    "Creating multiple SBPlatform objects is not supported.");
            }

            sbPlatform = sbPlatformFactory.Create(request.PlatformName);
            if (sbPlatform == null)
            {
                ErrorUtils.ThrowError(StatusCode.Internal, "Could not create SBPlatform.");
            }
            return Task.FromResult(new CreateResponse());
        }

        // Connect to a LLDB server.
        public override Task<ConnectRemoteResponse> ConnectRemote(ConnectRemoteRequest request,
            ServerCallContext context)
        {
            PreconditionCheck();
            var connectOptions = sbPlatformConnectOptionsFactory.Create(request.ConnectOptions.Url);
            var error = sbPlatform.ConnectRemote(connectOptions);
            var grpcError = new GrpcSbError
            {
                Success = error.Success(),
                Error = error.GetCString()
            };
            return Task.FromResult(new ConnectRemoteResponse { Error = grpcError });
        }

        // Run the specified command.
        public override Task<RunResponse> Run(RunRequest request, ServerCallContext context)
        {
            PreconditionCheck();
            var shellCommand = sbPlatformShellCommandFactory.Create(request.ShellCommand.Command);
            var error = sbPlatform.Run(shellCommand);
            var grpcError = new GrpcSbError
            {
                Success = error.Success(),
                Error = error.GetCString()
            };
            var response = new RunResponse
            {
                Error = grpcError,
                Output = shellCommand.GetOutput(),
                Signal = shellCommand.GetSignal(),
                Status = shellCommand.GetStatus()
            };
            return Task.FromResult(response);
        }

        #endregion

        // Checks general preconditions for SBPlatform APIs.
        void PreconditionCheck()
        {
            if (sbPlatform == null)
            {
                ErrorUtils.ThrowError(StatusCode.FailedPrecondition,
                    "Call 'Create' before calling any other API.");
            }
        }
    }
}
