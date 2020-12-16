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
using Debugger.SbDebuggerRpc;
using Grpc.Core;
using LldbApi;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using YetiVSI.DebugEngine;

namespace DebuggerGrpcServer
{
    /// <summary>
    /// Server implementation of the SBDebugger RPC.
    /// </summary>
    class SbDebuggerRpcServiceImpl : SbDebuggerRpcService.SbDebuggerRpcServiceBase,
        SbDebuggerService, SbDebuggerManager
    {
        readonly LLDBDebuggerFactory sbDebuggerFactory;
        readonly RemoteTargetFactory remoteTargetFactory;
        readonly object thisLock = new object();

        SbDebugger sbDebugger = null;
        ConcurrentDictionary<long, RemoteTarget> targetStore;
        readonly ObjectStore<SbCommandInterpreter> interpreterStore;
        SbPlatformManager sbPlatformManager;

        public SbDebuggerRpcServiceImpl(ConcurrentDictionary<long, RemoteTarget> targetStore,
                ObjectStore<SbCommandInterpreter> interpreterStore)
            : this(targetStore, interpreterStore, new LLDBDebuggerFactory(),
                  new RemoteTargetFactory(new RemoteBreakpointFactory())) {}

        /// <summary>
        /// Constructor that can be used by tests to pass in mock objects.
        /// </summary>
        public SbDebuggerRpcServiceImpl(ConcurrentDictionary<long, RemoteTarget> targetStore,
            ObjectStore<SbCommandInterpreter> interpreterStore,
            LLDBDebuggerFactory sbDebuggerFactory, RemoteTargetFactory remoteTargetFactory)
        {
            this.targetStore = targetStore;
            this.interpreterStore = interpreterStore;
            this.sbDebuggerFactory = sbDebuggerFactory;
            this.remoteTargetFactory = remoteTargetFactory;
        }

        #region SbDebuggerService

        public void Initialize(SbPlatformManager sbPlatformManager)
        {
            this.sbPlatformManager = sbPlatformManager;
        }

        #endregion

        #region SbDebuggerManager

        public SbDebugger GetDebugger()
        {
            lock(thisLock)
            {
                return sbDebugger;
            }
        }

        #endregion

        #region SbDebuggerRpcService.SbDebuggerRpcServiceBase

        /// <summary>
        /// Create a new LLDB SBDebugger object locally.  This SBDebugger object is then used for
        /// all subsecent requests.
        /// </summary>
        public override Task<CreateResponse> Create(CreateRequest request,
            ServerCallContext context)
        {
            // Lock around sbDebugger.
            lock (thisLock)
            {
                // We only support creating one SBDebugger object, fail if there is an attempt to
                // create more.
                if (sbDebugger != null)
                {
                    ErrorUtils.ThrowError(StatusCode.FailedPrecondition,
                        "Creating multiple SBDebugger objects is not supported.");
                }

                sbDebugger = sbDebuggerFactory.Create(request.SourceInitFiles);
                if (sbDebugger == null)
                {
                    ErrorUtils.ThrowError(StatusCode.Internal, "Could not create SBDebugger.");
                }
                return Task.FromResult(new CreateResponse());
            }
        }

        /// <summary>
        /// Set async execution for the command interpreter.
        /// </summary>
        public override Task<SkipLLDBInitFilesResponse> SkipLLDBInitFiles(
            SkipLLDBInitFilesRequest request,
            ServerCallContext context)
        {
            SbDebuggerPreconditionCheck();
            sbDebugger.SkipLLDBInitFiles(request.Skip);
            return Task.FromResult(new SkipLLDBInitFilesResponse());
        }

        public override Task<SetAsyncResponse> SetAsync(SetAsyncRequest request,
            ServerCallContext context)
        {
            SbDebuggerPreconditionCheck();
            sbDebugger.SetAsync(request.Async);
            return Task.FromResult(new SetAsyncResponse());
        }

        public override Task<GetCommandInterpreterResponse> GetCommandInterpreter(
            GetCommandInterpreterRequest request,
            ServerCallContext context)
        {
            SbDebuggerPreconditionCheck();
            SbCommandInterpreter interpreter = sbDebugger.GetCommandInterpreter();
            if (interpreter == null) {
                return Task.FromResult(new GetCommandInterpreterResponse());
            }

            var response = new GetCommandInterpreterResponse()
            {
                Interpreter = new GrpcSbCommandInterpreter {
                    Id = interpreterStore.AddObject(interpreter)
                }
            };

            return Task.FromResult(response);
        }

        /// <summary>
        /// Create a new LLDB SBTarget locally, and return a GrpcSbTarget object to the client.
        /// Locally we then map GrpcSbTarget objects to RemoteTarget objects.
        /// </summary>
        public override Task<CreateTargetResponse> CreateTarget(CreateTargetRequest request,
            ServerCallContext context)
        {
            SbDebuggerPreconditionCheck();
            SbTarget sbTarget = sbDebugger.CreateTarget(request.Filename);
            if (sbTarget == null)
            {
                ErrorUtils.ThrowError(StatusCode.Internal, "Could not create SBTarget.");
            }
            if (!targetStore.TryAdd(sbTarget.GetId(), remoteTargetFactory.Create(sbTarget)))
            {
                ErrorUtils.ThrowError(
                    StatusCode.Internal, "Could not add target to store: " + sbTarget.GetId());
            }
            var grpcSbTarget = new GrpcSbTarget { Id = sbTarget.GetId() };
            var response = new CreateTargetResponse { GrpcSbTarget = grpcSbTarget };
            return Task.FromResult(response);
        }

        /// <summary>
        /// Set the selected platform.
        /// </summary>
        public override Task<SetSelectedPlatformResponse> SetSelectedPlatform(
            SetSelectedPlatformRequest request, ServerCallContext context)
        {
            SbDebuggerPreconditionCheck();

            // We currently only support one platform, so get it from the manager instead of the
            // request.
            SbPlatform sbPlatform = sbPlatformManager.GetPlatform();
            if (sbPlatform == null)
            {
                ErrorUtils.ThrowError(StatusCode.NotFound,
                    "Could not find SBPlatform, make sure one has been created.");
            }
            sbDebugger.SetSelectedPlatform(sbPlatform);
            return Task.FromResult(new SetSelectedPlatformResponse());
        }

        /// <summary>
        /// Gets the currently selected platform.
        /// </summary>
        public override Task<GetSelectedPlatformResponse> GetSelectedPlatform(
            GetSelectedPlatformRequest request, ServerCallContext context)
        {
            SbDebuggerPreconditionCheck();

            // We currently only support one platform, so just return an empty response.  Since
            // for all platform APIs we assume it's the single instance we have created.
            var response = new GetSelectedPlatformResponse();
            return Task.FromResult(response);
        }

        /// <summary>
        /// Enable LLDB internal logging.  Takes a log channel and a list of log types.
        /// </summary>
        public override Task<EnableLogResponse> EnableLog(EnableLogRequest request,
            ServerCallContext context)
        {
            SbDebuggerPreconditionCheck();
            var result = sbDebugger.EnableLog(request.Channel, new List<string>(request.Types_));
            var response = new EnableLogResponse { Result = result };
            return Task.FromResult(response);
        }

        /// <summary>
        /// Checks whether specified platform is available.
        /// </summary>
        public override Task<IsPlatformAvailableResponse> IsPlatformAvailable(
            IsPlatformAvailableRequest request, ServerCallContext context)
        {
            SbDebuggerPreconditionCheck();
            bool result = sbDebugger.IsPlatformAvailable(request.PlatformName);
            var response = new IsPlatformAvailableResponse { Result = result };
            return Task.FromResult(response);
        }

        #endregion

        /// <summary>
        /// Checks general preconditions for SBDebugger APIs.
        /// </summary>
        void SbDebuggerPreconditionCheck()
        {
            if (sbDebugger == null)
            {
                ErrorUtils.ThrowError(StatusCode.FailedPrecondition,
                    "Call 'Create' before calling any other API.");
            }
        }
    }
}
