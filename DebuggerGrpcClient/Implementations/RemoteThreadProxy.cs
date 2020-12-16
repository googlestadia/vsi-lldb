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
using DebuggerApi;
using DebuggerGrpcClient.Interfaces;
using System.Collections.Generic;
using System.Linq;
using DebuggerCommonApi;
using System.Runtime.InteropServices;
using System;
using System.Threading.Tasks;

namespace DebuggerGrpcClient
{
    // <summary>
    // Creates RemoteThread objects.
    // </summary>
    public class GrpcThreadFactory
    {
        public virtual RemoteThread Create(GrpcConnection connection, GrpcSbThread grpcSbThread)
        {
            if (grpcSbThread == null || grpcSbThread.Id == 0)
            {
                return null;
            }
            return new RemoteThreadProxy(connection, grpcSbThread);
        }
    }

    // <summary>
    // Implementation of the RemoteThread interface that uses GRPC to make RPCs
    // to a remote endpoint.
    // </summary>
    class RemoteThreadProxy : RemoteThread, IDisposable
    {
        readonly GrpcConnection connection;
        readonly GrpcSbThread grpcSbThread;
        readonly RemoteThreadRpcService.RemoteThreadRpcServiceClient client;
        readonly RemoteFrameProxyFactory frameFactory;
        readonly GrpcProcessFactory processFactory;
        readonly GrpcErrorFactory errorFactory;
        readonly GrpcModuleFactory moduleFactory;
        readonly GCHandle gcHandle;

        private bool disposed = false;

        internal RemoteThreadProxy(GrpcConnection connection, GrpcSbThread grpcSbThead)
            : this(connection, grpcSbThead,
                new RemoteThreadRpcService.RemoteThreadRpcServiceClient(connection.CallInvoker),
                new RemoteFrameProxyFactory(), new GrpcProcessFactory(),
                new GrpcErrorFactory(), new GrpcModuleFactory())
        { }

        // <summary>
        // Used by tests to pass in mock objects.
        // </summary>
        internal RemoteThreadProxy(GrpcConnection connection, GrpcSbThread grpcSbThread,
            RemoteThreadRpcService.RemoteThreadRpcServiceClient client,
            RemoteFrameProxyFactory frameFactory, GrpcProcessFactory processFactory,
            GrpcErrorFactory errorFactory, GrpcModuleFactory moduleFactory)
        {
            this.connection = connection;
            this.grpcSbThread = grpcSbThread;
            this.client = client;
            this.frameFactory = frameFactory;
            this.processFactory = processFactory;
            this.errorFactory = errorFactory;
            this.moduleFactory = moduleFactory;

            gcHandle = GCHandle.Alloc(
                new Tuple<GrpcConnection, RemoteThreadRpcService.RemoteThreadRpcServiceClient,
                    GrpcSbThread>(connection, client, grpcSbThread));
        }

        ~RemoteThreadProxy()
        {
            Dispose(false);
        }

        // <summary>
        // Disposing indicates whether this method has been called by user's code or by the
        // the destructor. In the latter case, we should not reference managed objects as we cannot
        // know if they have already been reclaimed by the garbage collector.
        // </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                // We only have unmanaged resources to dispose of in this class.
                connection
                    .GetOrCreateBulkDeleter<GrpcSbThread>()
                    .QueueForDeletion(grpcSbThread, (List<GrpcSbThread> values) =>
                    {
                        var request = new BulkDeleteRequest();
                        request.Threads.AddRange(values);
                        connection.InvokeRpc(() =>
                        {
                            client.BulkDelete(request);
                        });
                    });
                gcHandle.Free();
                disposed = true;
            }
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            // Prevent finalization code for this object from executing a second time.
            GC.SuppressFinalize(this);
        }

        #endregion

        #region RemoteThread

        public SbProcess GetProcess()
        {
            GetProcessResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetProcess(
                        new GetProcessRequest() { Thread = grpcSbThread });
                }))
            {
                return processFactory.Create(connection, response.Process);
            }
            return null;
        }

        public string GetName()
        {
            var request = new GetNameRequest()
            {
                Thread = grpcSbThread
            };
            GetNameResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetName(request);
                }))
            {
                return response.Name;
            }
            return "";
        }

        public ulong GetThreadId()
        {
            var request = new GetThreadIdRequest()
            {
                Thread = grpcSbThread
            };
            GetThreadIdResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetThreadId(request);
                }))
            {
                return response.Id;
            }
            return 0;
        }

        public string GetStatus()
        {
            var request = new GetStatusRequest()
            {
                Thread = grpcSbThread
            };
            GetStatusResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetStatus(request);
                }))
            {
                return response.Status;
            }
            return "";
        }

        public void StepInto()
        {
            var request = new StepIntoRequest()
            {
                Thread = grpcSbThread
            };
            connection.InvokeRpc(() => { client.StepInto(request); });
        }

        public void StepOver()
        {
            var request = new StepOverRequest()
            {
                Thread = grpcSbThread
            };
            connection.InvokeRpc(() => { client.StepOver(request); });
        }

        public void StepOut()
        {
            var request = new StepOutRequest()
            {
                Thread = grpcSbThread
            };
            connection.InvokeRpc(() => { client.StepOut(request); });
        }

        public void StepInstruction(bool stepOver)
        {
            var request = new StepInstructionRequest()
            {
                Thread = grpcSbThread,
                StepOver = stepOver,
            };
            connection.InvokeRpc(() => { client.StepInstruction(request); });
        }

        public StopReason GetStopReason()
        {
            var request = new GetStopReasonRequest()
            {
                Thread = grpcSbThread
            };
            GetStopReasonResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetStopReason(request);
                }))
            {
                return GetStopReason(response.StopReason);
            }
            return StopReason.INVALID;
        }

        public ulong GetStopReasonDataAtIndex(uint index)
        {
            var request = new GetStopReasonDataAtIndexRequest()
            {
                Thread = grpcSbThread,
                Index = index
            };
            GetStopReasonDataAtIndexResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetStopReasonDataAtIndex(request);
                }))
            {
                return response.StopReasonData;
            }
            return 0;
        }

        public uint GetStopReasonDataCount()
        {
            var request = new GetStopReasonDataCountRequest()
            {
                Thread = grpcSbThread
            };
            GetStopReasonDataCountResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetStopReasonDataCount(request);
                }))
            {
                return response.Count;
            }
            return 0;
        }

        public uint GetNumFrames()
        {
            var request = new GetNumFramesRequest()
            {
                Thread = grpcSbThread
            };
            GetNumFramesResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetNumFrames(request);
                }))
            {
                return response.NumberFrames;
            }
            return 0;
        }

        public RemoteFrame GetFrameAtIndex(uint index)
        {
            var request = new GetFrameAtIndexRequest()
            {
                Thread = grpcSbThread,
                Index = index
            };
            GetFrameAtIndexResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetFrameAtIndex(request);
                }))
            {
                return frameFactory.Create(connection, response.Frame);
            }
            return null;
        }

        public SbError JumpToLine(string filePath, uint line)
        {
            JumpToLineResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.JumpToLine(new JumpToLineRequest
                    {
                        Thread = grpcSbThread,
                        FilePath = filePath,
                        Line = line,
                    });
                }))
            {
                if (response.Error != null)
                {
                    return errorFactory.Create(response.Error);
                }
            }
            return errorFactory.Create(new GrpcSbError
            {
                Success = false,
                Error = "Rpc error while calling JumpToLine()."
            });
        }

        public List<FrameInfoPair> GetFramesWithInfo(
            FrameInfoFlags fields, uint startIndex, uint maxCount)
        {
            var request = new GetFramesWithInfoRequest()
            {
                Thread = grpcSbThread,
                Fields = (uint)fields,
                StartIndex = startIndex,
                MaxCount = maxCount
            };
            GetFramesWithInfoResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetFramesWithInfo(request);
            }))
            {
                return ToFrameInfoPairList(response);
            }
            return null;
        }

        public async Task<List<FrameInfoPair>> GetFramesWithInfoAsync(
            FrameInfoFlags fields, uint startIndex, uint maxCount)
        {
            var request = new GetFramesWithInfoRequest()
            {
                Thread = grpcSbThread,
                Fields = (uint)fields,
                StartIndex = startIndex,
                MaxCount = maxCount
            };
            GetFramesWithInfoResponse response = null;
            if (await connection.InvokeRpcAsync(async delegate
            {
                response = await client.GetFramesWithInfoAsync(request);
            }))
            {
                return ToFrameInfoPairList(response);
            }
            return null;
        }

        List<FrameInfoPair> ToFrameInfoPairList(GetFramesWithInfoResponse response)
        {
            var frames = RemoteObjectUtils.CreateRemoteObjects(
                    p => frameFactory.Create(connection, p),
                    p => frameFactory.Delete(connection, p),
                    response.FramesWithInfo.Select(p => p.Frame));
            return response.FramesWithInfo.Zip(frames,
                (p, f) => new FrameInfoPair(f, FrameInfoUtils.CreateFrameInfo(
                    p.Info, moduleFactory, connection))).ToList();
        }

        #endregion

        private StopReason GetStopReason(GetStopReasonResponse.Types.StopReason grpcStopReason)
        {
            switch (grpcStopReason)
            {
                case GetStopReasonResponse.Types.StopReason.Breakpoint:
                    return StopReason.BREAKPOINT;
                case GetStopReasonResponse.Types.StopReason.Exception:
                    return StopReason.EXCEPTION;
                case GetStopReasonResponse.Types.StopReason.Exec:
                    return StopReason.EXEC;
                case GetStopReasonResponse.Types.StopReason.Exiting:
                    return StopReason.EXITING;
                case GetStopReasonResponse.Types.StopReason.Instrumentation:
                    return StopReason.INSTRUMENTATION;
                case GetStopReasonResponse.Types.StopReason.Invalid:
                    return StopReason.INVALID;
                case GetStopReasonResponse.Types.StopReason.None:
                    return StopReason.NONE;
                case GetStopReasonResponse.Types.StopReason.PlanComplete:
                    return StopReason.PLAN_COMPLETE;
                case GetStopReasonResponse.Types.StopReason.Signal:
                    return StopReason.SIGNAL;
                case GetStopReasonResponse.Types.StopReason.Trace:
                    return StopReason.TRACE;
                case GetStopReasonResponse.Types.StopReason.Watchpoint:
                    return StopReason.WATCHPOINT;
            }
            return StopReason.INVALID;
        }
    }
}
