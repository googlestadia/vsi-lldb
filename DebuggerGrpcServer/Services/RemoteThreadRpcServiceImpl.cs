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
using Debugger.RemoteThreadRpc;
using Grpc.Core;
using LldbApi;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using DebuggerCommonApi;

namespace DebuggerGrpcServer
{
    // Server implementation of the RemoteThread RPC.
    public class RemoteThreadRpcServiceImpl : RemoteThreadRpcService.RemoteThreadRpcServiceBase
    {
        readonly ConcurrentDictionary<int, SbProcess> processStore;
        readonly ObjectStore<RemoteThread> threadStore;
        readonly ObjectStore<RemoteFrame> frameStore;
        readonly UniqueObjectStore<SbModule> moduleStore;

        public RemoteThreadRpcServiceImpl(ConcurrentDictionary<int, SbProcess> processStore,
            ObjectStore<RemoteThread> threadStore, ObjectStore<RemoteFrame> frameStore,
            UniqueObjectStore<SbModule> moduleStore)
        {
            this.processStore = processStore;
            this.threadStore = threadStore;
            this.frameStore = frameStore;
            this.moduleStore = moduleStore;
        }

        #region RemoteThreadRpcService.RemoteThreadRpcServiceBase

        public override Task<GetProcessResponse> GetProcess(GetProcessRequest request,
            ServerCallContext context)
        {
            var response = new GetProcessResponse();
            var thread = threadStore.GetObject(request.Thread.Id);
            var sbProcess = thread.GetProcess();
            if (sbProcess != null)
            {
                response.Process = new GrpcSbProcess()
                {
                    Id = processStore.GetOrAdd(sbProcess.GetUniqueId(), sbProcess)
                            .GetUniqueId()
                };
            }
            return Task.FromResult(response);
        }

        public override Task<GetNameResponse> GetName(GetNameRequest request,
            ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            return Task.FromResult(new GetNameResponse { Name = thread.GetName() });
        }

        public override Task<GetThreadIdResponse> GetThreadId(GetThreadIdRequest request,
            ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            return Task.FromResult(new GetThreadIdResponse { Id = thread.GetThreadId() });
        }

        public override Task<GetStatusResponse> GetStatus(GetStatusRequest request,
            ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            return Task.FromResult(new GetStatusResponse { Status = thread.GetStatus() });
        }

        public override Task<StepIntoResponse> StepInto(StepIntoRequest request,
            ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            thread.StepInto();
            return Task.FromResult(new StepIntoResponse());
        }

        public override Task<StepOverResponse> StepOver(StepOverRequest request,
            ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            thread.StepOver();
            return Task.FromResult(new StepOverResponse());
        }

        public override Task<StepOutResponse> StepOut(StepOutRequest request,
            ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            thread.StepOut();
            return Task.FromResult(new StepOutResponse());
        }

        public override Task<StepInstructionResponse> StepInstruction(
            StepInstructionRequest request, ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            thread.StepInstruction(request.StepOver);
            return Task.FromResult(new StepInstructionResponse());
        }

        public override Task<GetStopReasonResponse> GetStopReason(GetStopReasonRequest request,
            ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            var grpcStopReason = GetGrpcStopReason(thread.GetStopReason());
            return Task.FromResult(new GetStopReasonResponse { StopReason = grpcStopReason });
        }

        public override Task<GetStopReasonDataAtIndexResponse> GetStopReasonDataAtIndex(
            GetStopReasonDataAtIndexRequest request, ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            return Task.FromResult(new GetStopReasonDataAtIndexResponse
            {
                StopReasonData = thread.GetStopReasonDataAtIndex(request.Index)
            });
        }

        public override Task<GetStopReasonDataCountResponse> GetStopReasonDataCount(
            GetStopReasonDataCountRequest request, ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            return Task.FromResult(new GetStopReasonDataCountResponse
            {
                Count = thread.GetStopReasonDataCount()
            });
        }

        public override Task<GetNumFramesResponse> GetNumFrames(GetNumFramesRequest request,
            ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            return Task.FromResult(new GetNumFramesResponse
            {
                NumberFrames = thread.GetNumFrames()
            });
        }

        public override Task<GetFrameAtIndexResponse> GetFrameAtIndex(
            GetFrameAtIndexRequest request, ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            var frame = thread.GetFrameAtIndex(request.Index);
            var response = new GetFrameAtIndexResponse();
            response.Frame = CreateGrpcSbFrame(frame);
            return Task.FromResult(response);
        }

        public override Task<JumpToLineResponse> JumpToLine(JumpToLineRequest request, ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            var error = thread.JumpToLine(request.FilePath, request.Line);
            return Task.FromResult(new JumpToLineResponse
            {
                Error = new GrpcSbError
                {
                    Success = error.Success(),
                    Error = error.GetCString()
                }
            });
        }

        public override Task<GetFramesWithInfoResponse> GetFramesWithInfo(
            GetFramesWithInfoRequest request, ServerCallContext context)
        {
            var thread = threadStore.GetObject(request.Thread.Id);
            var framesWithInfo = thread.GetFramesWithInfo((FrameInfoFlags)request.Fields,
                request.StartIndex, request.MaxCount);
            var response = new GetFramesWithInfoResponse();
            foreach (var frameWithInfo in framesWithInfo)
            {
                var grpcFrameWithInfo = new GrpcFrameWithInfo();
                grpcFrameWithInfo.Frame = CreateGrpcSbFrame(frameWithInfo.Frame);
                FrameInfo<SbModule> info = frameWithInfo.Info;
                long? moduleId = info.Module != null ?
                    moduleStore.AddObject(info.Module) : (long?)null;
                grpcFrameWithInfo.Info = GrpcFactoryUtils.CreateFrameInfo(info, moduleId);
                response.FramesWithInfo.Add(grpcFrameWithInfo);
            }
            return Task.FromResult(response);
        }

        public override Task<BulkDeleteResponse> BulkDelete(BulkDeleteRequest request,
            ServerCallContext context)
        {
            foreach (GrpcSbThread thread in request.Threads)
            {
                threadStore.RemoveObject(thread.Id);
            }
            return Task.FromResult(new BulkDeleteResponse());
        }

        #endregion

        GetStopReasonResponse.Types.StopReason GetGrpcStopReason(StopReason stopReason)
        {
            switch (stopReason)
            {
                case StopReason.BREAKPOINT:
                    return GetStopReasonResponse.Types.StopReason.Breakpoint;
                case StopReason.EXCEPTION:
                    return GetStopReasonResponse.Types.StopReason.Exception;
                case StopReason.EXEC:
                    return GetStopReasonResponse.Types.StopReason.Exec;
                case StopReason.EXITING:
                    return GetStopReasonResponse.Types.StopReason.Exiting;
                case StopReason.INSTRUMENTATION:
                    return GetStopReasonResponse.Types.StopReason.Instrumentation;
                case StopReason.INVALID:
                    return GetStopReasonResponse.Types.StopReason.Invalid;
                case StopReason.NONE:
                    return GetStopReasonResponse.Types.StopReason.None;
                case StopReason.PLAN_COMPLETE:
                    return GetStopReasonResponse.Types.StopReason.PlanComplete;
                case StopReason.SIGNAL:
                    return GetStopReasonResponse.Types.StopReason.Signal;
                case StopReason.TRACE:
                    return GetStopReasonResponse.Types.StopReason.Trace;
                case StopReason.WATCHPOINT:
                    return GetStopReasonResponse.Types.StopReason.Watchpoint;
            }
            return GetStopReasonResponse.Types.StopReason.Invalid;
        }

        GrpcSbFrame CreateGrpcSbFrame(RemoteFrame frame)
            => frame == null ? null : new GrpcSbFrame
                {
                    Id = frameStore.AddObject(frame),
                    FunctionName = frame.GetFunctionName() ?? "",
                    ProgramCounter = frame.GetPC(),
                    LineEntry = GrpcFactoryUtils.CreateGrpcLineEntryInfo(frame.GetLineEntry())
                };
    }
}
