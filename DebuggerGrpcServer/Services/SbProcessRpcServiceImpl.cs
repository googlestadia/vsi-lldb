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
using Debugger.SbProcessRpc;
using Google.Protobuf;
using Grpc.Core;
using LldbApi;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace DebuggerGrpcServer
{
    // Server implementation of SBProcess RPC.
    class SbProcessRpcServiceImpl : SbProcessRpcService.SbProcessRpcServiceBase
    {
        readonly ConcurrentDictionary<int, SbProcess> processStore;
        readonly ObjectStore<SbUnixSignals> signalsStore;
        readonly ObjectStore<RemoteThread> threadStore;
        readonly RemoteThreadImpl.Factory remoteThreadFactory;

        public SbProcessRpcServiceImpl(ConcurrentDictionary<int, SbProcess> processStore,
            ObjectStore<RemoteThread> threadStore,
            RemoteThreadImpl.Factory remoteThreadFactory,
            ObjectStore<SbUnixSignals> signalsStore)
        {
            this.processStore = processStore;
            this.threadStore = threadStore;
            this.remoteThreadFactory = remoteThreadFactory;
            this.signalsStore = signalsStore;
        }

        #region SbProcessRpcService.SbProcessRpcServiceBase

        public override Task<GetTargetResponse> GetTarget(GetTargetRequest request,
            ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, processStore);
            var target = sbProcess.GetTarget();
            var grpcSbThread = new GrpcSbTarget
            {
                Id = target.GetId()
            };
            return Task.FromResult(new GetTargetResponse { Target = grpcSbThread });
        }

        // Get the total number of threads in the thread list.
        public override Task<GetNumThreadsResponse> GetNumThreads(GetNumThreadsRequest request,
            ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, processStore);
            int numberThreads = sbProcess.GetNumThreads();
            return Task.FromResult(new GetNumThreadsResponse { NumberThreads = numberThreads });
        }

        // Get a thread at the specified index in the thread list.
        public override Task<GetThreadAtIndexResponse> GetThreadAtIndex(
            GetThreadAtIndexRequest request, ServerCallContext context)
        {
            var sbProcess = GrpcLookupUtils.GetProcess(request.Process, processStore);
            var sbThread = sbProcess.GetThreadAtIndex(request.Index);
            var grpcSbThread = new GrpcSbThread();
            if (sbThread != null)
            {
                var remoteThread = remoteThreadFactory.Create(sbThread);
                grpcSbThread.Id = threadStore.AddObject(remoteThread);
            }
            return Task.FromResult(new GetThreadAtIndexResponse { Thread = grpcSbThread });

        }

        // Returns the currently selected thread.
        public override Task<GetSelectedThreadResponse> GetSelectedThread(
            GetSelectedThreadRequest request, ServerCallContext context)
        {
            var sbProcess = GrpcLookupUtils.GetProcess(request.Process, processStore);
            var sbThread = sbProcess.GetSelectedThread();
            var grpcSbThread = new GrpcSbThread();
            if (sbThread != null)
            {
                var remoteThread = remoteThreadFactory.Create(sbThread);
                grpcSbThread.Id = threadStore.AddObject(remoteThread);
            }
            return Task.FromResult(new GetSelectedThreadResponse { Thread = grpcSbThread });
        }

        // Continues the process.
        // Returns true if successful, false otherwise.
        public override Task<ContinueResponse> Continue(ContinueRequest request,
            ServerCallContext contest)
        {
            var sbProcess = GrpcLookupUtils.GetProcess(request.Process, processStore);
            var result = sbProcess.Continue();
            return Task.FromResult(new ContinueResponse { Result = result });
        }

        // Pauses the process.
        // Retruns true is successful, false otherwise.
        public override Task<StopResponse> Stop(StopRequest request, ServerCallContext context)
        {
            var sbProcess = GrpcLookupUtils.GetProcess(request.Process, processStore);
            var result = sbProcess.Stop();
            return Task.FromResult(new StopResponse { Result = result });
        }

        // Kills the process.
        // Returns true if successful, false otherwise.
        public override Task<KillResponse> Kill(KillRequest request, ServerCallContext context)
        {
            var sbProcess = GrpcLookupUtils.GetProcess(request.Process, processStore);
            var result = sbProcess.Kill();
            return Task.FromResult(new KillResponse { Result = result });
        }

        // Detaches the process.
        // Retruns true if successful, false otherwise.
        public override Task<DetachResponse> Detach(DetachRequest request,
            ServerCallContext context)
        {
            var sbProcess = GrpcLookupUtils.GetProcess(request.Process, processStore);
            var result = sbProcess.Detach();
            return Task.FromResult(new DetachResponse { Result = result });
        }

        public override Task<SetSelectedThreadByIdResponse> SetSelectedThreadById(
            SetSelectedThreadByIdRequest request, ServerCallContext context)
        {
            var sbProcess = GrpcLookupUtils.GetProcess(request.Process, processStore);
            var success = sbProcess.SetSelectedThreadById(request.ThreadId);
            return Task.FromResult(new SetSelectedThreadByIdResponse
            {
                Success = success
            });
        }

        public override Task<GetUnixSignalsResponse> GetUnixSignals(
            GetUnixSignalsRequest request, ServerCallContext context)
        {
            var sbProcess = GrpcLookupUtils.GetProcess(request.Process, processStore);
            var signals = sbProcess.GetUnixSignals();
            var response = new GetUnixSignalsResponse();
            if (signals != null)
            {
                response.Signals = new GrpcSbUnixSignals
                {
                    Id = signalsStore.AddObject(signals)
                };
            }
            return Task.FromResult(response);
        }

        public override Task<ReadMemoryResponse> ReadMemory(
            ReadMemoryRequest request, ServerCallContext context)
        {
            var sbProcess = GrpcLookupUtils.GetProcess(request.Process, processStore);
            var buffer = new byte[request.Size];
            SbError error;
            var sizeRead = sbProcess.ReadMemory(
                request.Address, buffer, request.Size, out error);

            var response = new ReadMemoryResponse
            {
                Error = new GrpcSbError
                {
                    Success = error.Success(),
                    Error = error.GetCString(),
                },
                Memory = ByteString.CopyFrom(buffer),
                Size = sizeRead
            };
            return Task.FromResult(response);
        }

        public override Task<WriteMemoryResponse> WriteMemory(
            WriteMemoryRequest reqeust, ServerCallContext context)
        {
            var sbProcess = GrpcLookupUtils.GetProcess(reqeust.Process, processStore);
            SbError error;
            var sizeWrote = sbProcess.WriteMemory(reqeust.Address, reqeust.Buffer.ToByteArray(),
                reqeust.Size, out error);
            return Task.FromResult(new WriteMemoryResponse
            {
                Error = new GrpcSbError
                {
                    Success = error.Success(),
                    Error = error.GetCString()
                },
                Size = sizeWrote
            });
        }

        #endregion
    }
}
