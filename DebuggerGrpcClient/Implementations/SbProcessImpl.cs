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
using DebuggerApi;
using Google.Protobuf;
using System;
using System.Diagnostics;

namespace DebuggerGrpcClient
{
    // Creates SBProcess objects.
    class GrpcProcessFactory
    {
        public SbProcess Create(GrpcConnection grpcConnection, GrpcSbProcess grpcSbProcess)
        {
            return new SbProcessImpl(grpcConnection, grpcSbProcess);
        }
    }

    // Implementation of the SBProcess interface that uses GRPC to make RPCs to a remote endpoint.
    class SbProcessImpl : SbProcess
    {
        readonly GrpcConnection connection;
        readonly GrpcSbProcess grpcSbProcess;
        readonly SbProcessRpcService.SbProcessRpcServiceClient client;
        readonly GrpcThreadFactory threadFactory;
        readonly GrpcTargetFactory targetFactory;
        readonly GrpcUnixSignalsFactory unixSignalsFactory;
        readonly GrpcErrorFactory errorFactory;

        internal SbProcessImpl(GrpcConnection connection, GrpcSbProcess grpcSbProcess)
            : this(connection, grpcSbProcess,
                  new GrpcThreadFactory(),
                  new GrpcTargetFactory(),
                  new GrpcUnixSignalsFactory(), new GrpcErrorFactory(),
                  new SbProcessRpcService.SbProcessRpcServiceClient(connection.CallInvoker))
        { }

        internal SbProcessImpl(GrpcConnection connection, GrpcSbProcess grpcSbProcess,
            GrpcThreadFactory threadFactory, GrpcTargetFactory targetFactory,
            GrpcUnixSignalsFactory unixSignalsFactory,
            GrpcErrorFactory errorFactory,
            SbProcessRpcService.SbProcessRpcServiceClient client)
        {
            this.connection = connection;
            this.grpcSbProcess = grpcSbProcess;
            this.client = client;
            this.threadFactory = threadFactory;
            this.targetFactory = targetFactory;
            this.unixSignalsFactory = unixSignalsFactory;
            this.errorFactory = errorFactory;
        }

        #region SbProcess

        public RemoteTarget GetTarget()
        {
            GetTargetResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.GetTarget(
                    new GetTargetRequest() { Process = grpcSbProcess });
            }))
            {
                return targetFactory.Create(connection, response.Target);
            }
            return null;
        }

        public int GetNumThreads()
        {
            var request = new GetNumThreadsRequest
            {
                Process = grpcSbProcess
            };
            GetNumThreadsResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetNumThreads(request);
                }))
            {
                return response.NumberThreads;
            }
            return 0;

        }

        public RemoteThread GetThreadAtIndex(int index)
        {
            var request = new GetThreadAtIndexRequest
            {
                Process = grpcSbProcess,
                Index = index
            };
            GetThreadAtIndexResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetThreadAtIndex(request);
                }))
            {
                return threadFactory.Create(connection, response.Thread);
            }
            return null;
        }

        public RemoteThread GetThreadById(ulong id)
        {
            throw new NotImplementedException();
        }

        public RemoteThread GetSelectedThread()
        {
            var request = new GetSelectedThreadRequest
            {
                Process = grpcSbProcess
            };
            GetSelectedThreadResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetSelectedThread(request);
                }))
            {
                return threadFactory.Create(connection, response.Thread);
            }
            return null;
        }

        public bool Continue()
        {
            var request = new ContinueRequest
            {
                Process = grpcSbProcess
            };
            ContinueResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.Continue(request);
                }))
            {
                return response.Result;
            }
            return false;
        }

        public bool Detach()
        {
            var request = new DetachRequest
            {
                Process = grpcSbProcess
            };
            DetachResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.Detach(request);
                }))
            {
                return response.Result;
            }
            return false;
        }

        public bool Kill()
        {
            var request = new KillRequest
            {
                Process = grpcSbProcess
            };
            KillResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.Kill(request);
                }))
            {
                return response.Result;
            }
            return false;
        }

        public bool Stop()
        {
            var request = new StopRequest
            {
                Process = grpcSbProcess
            };
            StopResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.Stop(request);
                }))
            {
                return response.Result;
            }
            return false;
        }

        public int GetUniqueId()
        {
            throw new NotImplementedException();
        }

        public bool SetSelectedThreadById(ulong threadId)
        {
            SetSelectedThreadByIdResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.SetSelectedThreadById(
                        new SetSelectedThreadByIdRequest
                        {
                            Process = grpcSbProcess,
                            ThreadId = threadId
                        });
                }))
            {
                return response.Success;
            }
            return false;
        }

        public SbUnixSignals GetUnixSignals()
        {
            GetUnixSignalsResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetUnixSignals(
                        new GetUnixSignalsRequest
                        {
                            Process = grpcSbProcess
                        });

                }))
            {
                if (response.Signals != null && response.Signals.Id != 0)
                {
                    return unixSignalsFactory.Create(connection, response.Signals);
                }
            }
            return null;
        }

        public ulong ReadMemory(ulong address, byte[] memory, ulong size, out SbError error)
        {
            ReadMemoryResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.ReadMemory(
                        new ReadMemoryRequest
                        {
                            Process = grpcSbProcess,
                            Address = address,
                            Size = size
                        });
                }))
            {
                error = errorFactory.Create(response.Error);
                var responseArray = response.Memory.ToByteArray();
                int startingIndex = 0;
                if (responseArray.Length > memory.Length)
                {
                    Trace.WriteLine("Error: buffer is not large enough for the output.");
                    startingIndex = responseArray.Length - memory.Length;
                }
                response.Memory.ToByteArray().CopyTo(memory, startingIndex);
                return response.Size;
            }
            var grpcError = new GrpcSbError
            {
                Success = false,
                Error = "Rpc error while calling ReadMemory."
            };
            error = errorFactory.Create(grpcError);
            return 0;
        }

        public ulong WriteMemory(ulong address, byte[] buffer, ulong size, out SbError error)
        {
            WriteMemoryResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.WriteMemory(
                        new WriteMemoryRequest
                        {
                            Process = grpcSbProcess,
                            Address = address,
                            Buffer = ByteString.CopyFrom(buffer, 0, (int)size),
                            Size = size
                        });
                }))
            {
                error = errorFactory.Create(response.Error);
                return response.Size;
            }
            var grpcError = new GrpcSbError
            {
                Success = false,
                Error = "Rpc error while calling WriteMemory."
            };
            error = errorFactory.Create(grpcError);
            return 0;
        }

        #endregion
    }
}
