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
using Debugger.RemoteTargetRpc;
using DebuggerApi;
using DebuggerCommonApi;
using DebuggerGrpcClient.Interfaces;
using System;
using System.Collections.Generic;
using YetiCommon;

namespace DebuggerGrpcClient
{
    // <summary>
    // Creates RemoteTarget objects.
    // </summary>
    public class GrpcTargetFactory
    {
        public virtual RemoteTarget Create(GrpcConnection connection, GrpcSbTarget grpcSbTarget)
        {
            return new RemoteTargetProxy(connection, grpcSbTarget);
        }
    }

    // <summary>
    // Implementation of the RemoteTarget interface that uses GRPC to make RPCs to a remote
    // endpoint.
    // </summary>
    class RemoteTargetProxy : RemoteTarget
    {
        readonly GrpcConnection connection;
        readonly GrpcSbTarget grpcSbTarget;
        readonly RemoteTargetRpcService.RemoteTargetRpcServiceClient client;
        readonly GrpcBreakpointFactory breakpointFactory;
        readonly GrpcErrorFactory errorFactory;
        readonly GrpcProcessFactory processFactory;
        readonly GrpcModuleFactory moduleFactory;
        readonly GrpcWatchpointFactory watchpointFactory;
        readonly GrpcAddressFactory addressFactory;

        public RemoteTargetProxy(GrpcConnection connection, GrpcSbTarget grpcSbTarget) : this(
            connection, grpcSbTarget,
            new RemoteTargetRpcService.RemoteTargetRpcServiceClient(connection.CallInvoker),
            new GrpcBreakpointFactory(), new GrpcErrorFactory(), new GrpcProcessFactory(),
            new GrpcModuleFactory(), new GrpcWatchpointFactory(), new GrpcAddressFactory())
        { }

        public RemoteTargetProxy(GrpcConnection connection, GrpcSbTarget grpcSbTarget,
            RemoteTargetRpcService.RemoteTargetRpcServiceClient client,
            GrpcBreakpointFactory breakpointFactory, GrpcErrorFactory errorFactory,
            GrpcProcessFactory processFactory, GrpcModuleFactory moduleFactory,
            GrpcWatchpointFactory watchpointFactory, GrpcAddressFactory addressFactory)
        {
            this.connection = connection;
            this.grpcSbTarget = grpcSbTarget;
            this.client = client;
            this.breakpointFactory = breakpointFactory;
            this.errorFactory = errorFactory;
            this.processFactory = processFactory;
            this.moduleFactory = moduleFactory;
            this.watchpointFactory = watchpointFactory;
            this.addressFactory = addressFactory;
        }

        public SbProcess AttachToProcessWithID(SbListener listener, ulong pid, out SbError error)
        {
            var request = new AttachToProcessWithIDRequest()
            {
                Target = grpcSbTarget,
                Listener = new GrpcSbListener() { Id = listener.GetId() },
                Pid = pid,
            };
            AttachToProcessWithIDResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.AttachToProcessWithID(request);
                }))
            {
                error = errorFactory.Create(response.Error);
                if (response.Process == null)
                {
                    return null;
                }
                return processFactory.Create(connection, response.Process);
            }
            var grpcError = new GrpcSbError
            {
                Success = false,
                Error = "Rpc error while calling AttachToProcessWithId."
            };
            error = errorFactory.Create(grpcError);
            return null;
        }

        public RemoteBreakpoint BreakpointCreateByLocation(string file, uint line)
        {
            var request = new BreakpointCreateByLocationRequest()
            {
                Target = grpcSbTarget,
                File = file,
                Line = line,
            };
            BreakpointCreateByLocationResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.BreakpointCreateByLocation(request);
                }))
            {
                if (response.Breakpoint != null && response.Breakpoint.Id != 0)
                {
                    return breakpointFactory.Create(connection, response.Breakpoint);
                }
            }
            return null;
        }

        public RemoteBreakpoint BreakpointCreateByName(string symbolName)
        {
            var request = new BreakpointCreateByNameRequest()
            {
                Target = grpcSbTarget,
                SymbolName = symbolName,
            };
            BreakpointCreateByNameResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.BreakpointCreateByName(request);
                }))
            {
                if (response.Breakpoint != null && response.Breakpoint.Id != 0)
                {
                    return breakpointFactory.Create(connection, response.Breakpoint);
                }
            }
            return null;
        }

        public RemoteBreakpoint BreakpointCreateByAddress(ulong address)
        {
            var request = new BreakpointCreateByAddressRequest()
            {
                Target = grpcSbTarget,
                Address = address,
            };
            BreakpointCreateByAddressResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.BreakpointCreateByAddress(request);
            }))
            {
                if (response.Breakpoint != null && response.Breakpoint.Id != 0)
                {
                    return breakpointFactory.Create(connection, response.Breakpoint);
                }
            }
            return null;
        }

        public BreakpointErrorPair CreateFunctionOffsetBreakpoint(string symbolName, uint offset)
        {
            var request = new CreateFunctionOffsetBreakpointRequest()
            {
                Target = grpcSbTarget,
                SymbolName = symbolName,
                Offset = offset
            };
            CreateFunctionOffsetBreakpointResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.CreateFunctionOffsetBreakpoint(request);
            }))
            {
                if (response.Breakpoint != null && response.Breakpoint.Id != 0)
                {
                    return new BreakpointErrorPair(
                        breakpointFactory.Create(connection, response.Breakpoint),
                        EnumUtil.ConvertTo<DebuggerApi.BreakpointError>(response.Error));
                }
            }
            return new BreakpointErrorPair(null,
                EnumUtil.ConvertTo<DebuggerApi.BreakpointError>(response.Error));
        }

        public bool BreakpointDelete(int breakpointId)
        {
            BreakpointDeleteResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.BreakpointDelete(
                    new BreakpointDeleteRequest
                    {
                        Target = grpcSbTarget,
                        BreakpointId = breakpointId
                    });
            }))
            {
                return response.Success;
            }
            return false;
        }

        public int GetNumModules()
        {
            var request = new GetNumModulesRequest()
            {
                Target = grpcSbTarget,
            };
            GetNumModulesResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetNumModules(request);
                }))
            {
                return response.Result;
            }
            return 0;
        }

        public SbModule GetModuleAtIndex(int index)
        {
            var request = new GetModuleAtIndexRequest()
            {
                Target = grpcSbTarget,
                Index = index,
            };
            GetModuleAtIndexResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.GetModuleAtIndex(request);
                }))
            {
                if (response.Module != null && response.Module.Id != 0)
                {
                    return moduleFactory.Create(connection, response.Module);
                }
            }
            return null;
        }

        public long GetId()
        {
            return grpcSbTarget.Id;
        }

        public SbWatchpoint WatchAddress(long address, ulong size, bool read, bool write,
            out SbError error)
        {
            WatchAddressResponse response = null;
            error = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.WatchAddress(
                    new WatchAddressRequest
                    {
                        Target = grpcSbTarget,
                        Address = address,
                        Size = size,
                        Read = read,
                        Write = write
                    });
            }))
            {
                error = errorFactory.Create(response.Error);
                if (response.Watchpoint != null && response.Watchpoint.Id != 0)
                {
                    return watchpointFactory.Create(connection, response.Watchpoint);
                }
                return null;
            }
            var grpcError = new GrpcSbError
            {
                Success = false,
                Error = "Rpc error while calling WatchAddress."
            };
            error = errorFactory.Create(grpcError);
            return null;
        }

        public bool DeleteWatchpoint(int watchId)
        {
            DeleteWatchpointResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.DeleteWatchpoint(
                        new DeleteWatchpointRequest
                        {
                            Target = grpcSbTarget,
                            WatchId = watchId
                        });
                }))
            {
                return response.Success;
            }
            return false;
        }

        public RemoteBreakpoint FindBreakpointById(int id)
        {
            throw new NotImplementedException();
        }

        public SbAddress ResolveLoadAddress(ulong address)
        {
            var request = new ResolveLoadAddressRequest()
            {
                Target = grpcSbTarget,
                Address = address,
            };
            ResolveLoadAddressResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.ResolveLoadAddress(request);
            }))
            {
                if (response.Address != null && response.Address.Id != 0)
                {
                    return addressFactory.Create(connection, response.Address);
                }
            }
            return null;
        }

        // <summary>
        // |coreFile| should be a path to core file on the local machine.
        // </summary>
        public SbProcess LoadCore(string coreFile)
        {
            LoadCoreResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.LoadCore(new LoadCoreRequest
                    {
                        Target = grpcSbTarget,
                        CorePath = coreFile
                    });
                }))
            {
                if (response.Process != null)
                {
                    return processFactory.Create(connection, response.Process);
                }
            }
            return null;
        }

        public SbModule AddModule(string path, string triple, string uuid)
        {
            AddModuleResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.AddModule(new AddModuleRequest
                    {
                        Target = grpcSbTarget,
                        Path = path,
                        Triple = triple,
                        Uuid = uuid,
                    });
                }))
            {
                if (response.Module != null && response.Module.Id != 0)
                {
                    return moduleFactory.Create(connection, response.Module);
                }
            }
            return null;
        }

        public bool RemoveModule(SbModule module)
        {
            RemoveModuleResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.RemoveModule(new RemoveModuleRequest
                    {
                        Target = grpcSbTarget,
                        Module = new GrpcSbModule { Id = module.GetId() },
                    });
                }))
            {
                return response.Result;
            }
            return false;
        }

        public SbError SetModuleLoadAddress(SbModule module, long sectionsOffset)
        {
            SetModuleLoadAddressResponse response = null;
            if (connection.InvokeRpc(() =>
                {
                    response = client.SetModuleLoadAddress(
                        new SetModuleLoadAddressRequest
                        {
                            Target = grpcSbTarget,
                            Module = new GrpcSbModule { Id = module.GetId() },
                            SectionsOffset = sectionsOffset,
                        });
                }))
            {
                return errorFactory.Create(response.Error);
            }
            var grpcSbError = new GrpcSbError
            {
                Success = false,
                Error = "Rpc error while calling SetModuleLoadAddress."
            };
            return errorFactory.Create(grpcSbError);
        }

        public List<InstructionInfo> ReadInstructionInfos(SbAddress address,
            uint count, string flavor)
        {
            ReadInstructionInfosResponse response = null;
            if (connection.InvokeRpc(() =>
            {
                response = client.ReadInstructionInfos(
                    new ReadInstructionInfosRequest
                    {
                        Target = grpcSbTarget,
                        Address = new GrpcSbAddress { Id = address.GetId() },
                        Count = count,
                        Flavor = flavor,
                    });
            }))
            {
                var instructions = new List<InstructionInfo>();
                foreach (var instruction in response.Instructions)
                {
                    instructions.Add(new InstructionInfo(
                        instruction.Address,
                        instruction.Operands,
                        instruction.Comment,
                        instruction.Mnemonic,
                        instruction.SymbolName,
                        FrameInfoUtils.CreateLineEntryInfo(instruction.LineEntry)));
                }
                return instructions;
            }
            return new List<InstructionInfo>();
        }
    }
}
