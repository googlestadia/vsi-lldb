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

using System.Collections.Concurrent;
using System.Threading.Tasks;
using Debugger.Common;
using Debugger.SbProcessRpc;
using Google.Protobuf;
using Grpc.Core;
using LldbApi;

namespace DebuggerGrpcServer
{
    /// <summary>
    /// Server implementation of SBProcess RPC.
    /// </summary>
    class SbProcessRpcServiceImpl : SbProcessRpcService.SbProcessRpcServiceBase
    {
        readonly ConcurrentDictionary<int, SbProcess> _processStore;
        readonly ObjectStore<SbUnixSignals> _signalsStore;
        readonly ObjectStore<RemoteThread> _threadStore;
        readonly RemoteThreadImpl.Factory _remoteThreadFactory;

        public SbProcessRpcServiceImpl(ConcurrentDictionary<int, SbProcess> processStore,
            ObjectStore<RemoteThread> threadStore,
            RemoteThreadImpl.Factory remoteThreadFactory,
            ObjectStore<SbUnixSignals> signalsStore)
        {
            _processStore = processStore;
            _threadStore = threadStore;
            _remoteThreadFactory = remoteThreadFactory;
            _signalsStore = signalsStore;
        }

        #region SbProcessRpcService.SbProcessRRGpcServiceBase
        
        /// <summary>
        /// Returns a read only property that represents the target
        /// (lldb.SBTarget) that owns this process.
        /// </summary>
        public override Task<GetTargetResponse> GetTarget(GetTargetRequest request,
            ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            SbTarget target = sbProcess.GetTarget();
            var grpcSbThread = new GrpcSbTarget
            {
                Id = target.GetId()
            };
            return Task.FromResult(new GetTargetResponse { Target = grpcSbThread });
        }

        /// <summary>
        /// Returns the number of threads in this process as an integer.
        /// </summary>
        public override Task<GetNumThreadsResponse> GetNumThreads(GetNumThreadsRequest request,
            ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            int numberThreads = sbProcess.GetNumThreads();
            return Task.FromResult(new GetNumThreadsResponse { NumberThreads = numberThreads });
        }

        /// <summary>
        /// Returns the index'th thread from the list of current threads. The index
        /// of a thread is only valid for the current stop. For a persistent thread
        /// identifier use either the thread ID or the IndexID.
        /// </summary>
        public override Task<GetThreadAtIndexResponse> GetThreadAtIndex(
            GetThreadAtIndexRequest request, ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            SbThread sbThread = sbProcess.GetThreadAtIndex(request.Index);
            var grpcSbThread = new GrpcSbThread();
            if (sbThread != null)
            {
                RemoteThread remoteThread = _remoteThreadFactory.Create(sbThread);
                grpcSbThread.Id = _threadStore.AddObject(remoteThread);
            }
            return Task.FromResult(new GetThreadAtIndexResponse { Thread = grpcSbThread });

        }

        /// <summary>
        /// Returns the currently selected thread.
        /// </summary>
        public override Task<GetSelectedThreadResponse> GetSelectedThread(
            GetSelectedThreadRequest request, ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            SbThread sbThread = sbProcess.GetSelectedThread();
            var grpcSbThread = new GrpcSbThread();
            if (sbThread != null)
            {
                RemoteThread remoteThread = _remoteThreadFactory.Create(sbThread);
                grpcSbThread.Id = _threadStore.AddObject(remoteThread);
            }
            return Task.FromResult(new GetSelectedThreadResponse { Thread = grpcSbThread });
        }

        /// <summary>
        /// Continues the process.
        /// </summary>
        public override Task<ContinueResponse> Continue(ContinueRequest request,
            ServerCallContext contest)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            bool result = sbProcess.Continue();
            return Task.FromResult(new ContinueResponse { Result = result });
        }

        /// <summary>
        /// Pauses the process.
        /// </summary>
        public override Task<StopResponse> Stop(StopRequest request, ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            bool result = sbProcess.Stop();
            return Task.FromResult(new StopResponse { Result = result });
        }

        /// <summary>
        /// Kills the process and shuts down all threads that were spawned to
        /// track and monitor process.
        /// </summary>
        public override Task<KillResponse> Kill(KillRequest request, ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            bool result = sbProcess.Kill();
            return Task.FromResult(new KillResponse { Result = result });
        }

        /// <summary>
        /// Detaches from the process and, optionally, keeps it stopped.
        /// </summary>
        public override Task<DetachResponse> Detach(DetachRequest request,
            ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            bool result = sbProcess.Detach(request.KeepStopped);
            return Task.FromResult(new DetachResponse { Result = result });
        }

        public override Task<SetSelectedThreadByIdResponse> SetSelectedThreadById(
            SetSelectedThreadByIdRequest request, ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            bool success = sbProcess.SetSelectedThreadById(request.ThreadId);
            return Task.FromResult(new SetSelectedThreadByIdResponse
            {
                Success = success
            });
        }

        public override Task<GetUnixSignalsResponse> GetUnixSignals(
            GetUnixSignalsRequest request, ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            SbUnixSignals signals = sbProcess.GetUnixSignals();
            var response = new GetUnixSignalsResponse();
            if (signals != null)
            {
                response.Signals = new GrpcSbUnixSignals
                {
                    Id = _signalsStore.AddObject(signals)
                };
            }
            return Task.FromResult(response);
        }

        public override Task<ReadMemoryResponse> ReadMemory(
            ReadMemoryRequest request, ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            byte[] buffer = new byte[request.Size];
            ulong sizeRead = sbProcess.ReadMemory(
                request.Address, buffer, request.Size, out SbError error);

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
            WriteMemoryRequest request, ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            ulong sizeWrote = sbProcess.WriteMemory(request.Address, request.Buffer.ToByteArray(),
                                                    request.Size, out SbError error);
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


        public override Task<SaveCoreResponse> SaveCore(
           SaveCoreRequest request, ServerCallContext context)
        {
            SbProcess sbProcess = GrpcLookupUtils.GetProcess(request.Process, _processStore);
            SbError error = sbProcess.SaveCore(request.DumpPath);
            return Task.FromResult(new SaveCoreResponse
            {
                Error = new GrpcSbError
                {
                    Success = error.Success(),
                    Error = error.GetCString()
                },
            });
        }
        #endregion
    }
}
